using System;

namespace Industrial.Contracts.Plugin
{
    /// <summary>
    /// 插件生命周期接口。
    /// 每个业务模块（Recipe、MES、SPC 等）均可作为插件动态加载。
    /// </summary>
    public interface IPlugin
    {
        string Name { get; }
        Version Version { get; }
        string Description { get; }

        /// <summary>
        /// 插件初始化：此处可注册自己的服务到容器。
        /// </summary>
        void Initialize(IServiceRegistrar registrar);

        void Start();
        void Stop();
        void Shutdown();
    }

    /// <summary>
    /// 插件内部用于向平台容器注册服务的接口（受限视图）。
    /// 插件不应获得完整 IContainer，仅能注册服务。
    /// </summary>
    public interface IServiceRegistrar
    {
        void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterInstance<TService>(TService instance);
    }
}
