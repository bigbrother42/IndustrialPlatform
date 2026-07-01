using Industrial.Contracts.Logging;
using Industrial.Contracts.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Industrial.Plugin
{
    /// <summary>
    /// 插件管理器：管理平台插件的注册与生命周期。
    /// 
    /// 插件生命周期：Register → Initialize → Start → [运行中] → Stop → Shutdown
    /// 
    /// 用途（半导体测试平台）：
    ///   - 测试算法插件（不同产品的测试方案作为独立插件）
    ///   - 数据上报插件（MES、SPC 集成）
    ///   - 报表生成插件
    ///   - 设备驱动包（运行时热插拔）
    /// </summary>
    public sealed class PluginManager
    {
        private readonly ConcurrentDictionary<string, PluginEntry> _plugins =
            new ConcurrentDictionary<string, PluginEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly IServiceRegistrar _registrar;
        private readonly ILogger _logger;

        public IReadOnlyList<string> LoadedPlugins
            => _plugins.Keys.ToList().AsReadOnly();

        public PluginManager(IServiceRegistrar registrar, ILoggerFactory loggerFactory)
        {
            _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
            _logger = loggerFactory.CreateLogger(typeof(PluginManager));
        }

        /// <summary>注册并初始化插件（通常在 Bootstrap 阶段调用）</summary>
        public void Register(IPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            var key = plugin.Name;
            if (_plugins.ContainsKey(key))
                throw new InvalidOperationException($"插件 [{key}] 已注册");

            try
            {
                _logger.Info($"初始化插件: [{plugin.Name}] v{plugin.Version}");
                plugin.Initialize(_registrar);

                _plugins[key] = new PluginEntry(plugin, PluginStatus.Initialized);
                _logger.Info($"插件 [{plugin.Name}] 初始化成功");
            }
            catch (Exception ex)
            {
                _logger.Error($"插件 [{plugin.Name}] 初始化失败", ex);
                throw;
            }
        }

        /// <summary>启动所有已注册插件</summary>
        public void StartAll()
        {
            foreach (var entry in _plugins.Values.Where(e => e.Status == PluginStatus.Initialized))
            {
                try
                {
                    _logger.Info($"启动插件: [{entry.Plugin.Name}]");
                    entry.Plugin.Start();
                    entry.Status = PluginStatus.Running;
                }
                catch (Exception ex)
                {
                    _logger.Error($"插件 [{entry.Plugin.Name}] 启动失败", ex);
                    entry.Status = PluginStatus.Error;
                }
            }
        }

        /// <summary>停止所有运行中的插件</summary>
        public void StopAll()
        {
            foreach (var entry in _plugins.Values.Where(e => e.Status == PluginStatus.Running))
            {
                try
                {
                    entry.Plugin.Stop();
                    entry.Status = PluginStatus.Stopped;
                    _logger.Info($"插件 [{entry.Plugin.Name}] 已停止");
                }
                catch (Exception ex)
                {
                    _logger.Error($"插件 [{entry.Plugin.Name}] 停止异常", ex);
                }
            }
        }

        /// <summary>关闭并卸载所有插件（应用退出时调用）</summary>
        public void ShutdownAll()
        {
            StopAll();
            foreach (var entry in _plugins.Values)
            {
                try
                {
                    entry.Plugin.Shutdown();
                    _logger.Info($"插件 [{entry.Plugin.Name}] 已卸载");
                }
                catch (Exception ex)
                {
                    _logger.Error($"插件 [{entry.Plugin.Name}] 卸载异常", ex);
                }
            }
            _plugins.Clear();
        }

        public PluginStatus GetStatus(string pluginName)
        {
            if (_plugins.TryGetValue(pluginName, out var e)) return e.Status;
            return PluginStatus.NotFound;
        }

        private sealed class PluginEntry
        {
            public IPlugin Plugin { get; }
            public PluginStatus Status { get; set; }
            public PluginEntry(IPlugin plugin, PluginStatus status) { Plugin = plugin; Status = status; }
        }
    }

    public enum PluginStatus { NotFound, Initialized, Running, Stopped, Error }

    /// <summary>
    /// 将 IContainer 包装为受限视图，插件只能注册服务，不能解析或做其他操作。
    /// </summary>
    public sealed class ServiceRegistrarAdapter : IServiceRegistrar
    {
        private readonly Industrial.DI.Abstractions.IContainer _container;

        public ServiceRegistrarAdapter(Industrial.DI.Abstractions.IContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
            => _container.RegisterSingleton<TService, TImplementation>();

        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
            => _container.RegisterTransient<TService, TImplementation>();

        public void RegisterInstance<TService>(TService instance)
            => _container.RegisterInstance(instance);
    }
}
