using Industrial.Contracts.Device;
using Industrial.Contracts.Events;
using Industrial.Contracts.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Industrial.Device
{
    /// <summary>
    /// 平台核心设备管理器。
    /// 
    /// 职责：
    ///   1. 设备注册/注销（配置驱动，运行时可热插拔）
    ///   2. 连接生命周期管理（Connect/Disconnect/ConnectAll/DisconnectAll）
    ///   3. 自动重连（指数退避，每设备独立 CancellationToken）
    ///   4. 健康监控（定时心跳，检测意外断连）
    ///   5. 事件广播（通过 IEventBus 解耦通知 UI / 报警 / 日志）
    /// </summary>
    public sealed class DeviceManager : IDeviceManager
    {
        private readonly DeviceRegistry _registry;
        private readonly DeviceFactory _factory;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;

        // 健康监控
        private readonly Timer _healthTimer;
        private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(5);

        private bool _disposed;

        public event EventHandler<DeviceStateChangedEventArgs> AnyDeviceStateChanged;

        public DeviceManager(
            DeviceFactory factory,
            IEventBus eventBus,
            ILoggerFactory loggerFactory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = loggerFactory?.CreateLogger(typeof(DeviceManager))
                      ?? throw new ArgumentNullException(nameof(loggerFactory));

            _registry = new DeviceRegistry();
            _healthTimer = new Timer(OnHealthCheck, null, HealthCheckInterval, HealthCheckInterval);
        }

        // ════════════════════════════════════════════════════════
        // 注册
        // ════════════════════════════════════════════════════════

        public void Register(IDeviceDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            if (_registry.Contains(descriptor.Id))
                throw new InvalidOperationException($"设备 [{descriptor.Id}] 已注册，请先 Unregister。");

            var concreteDescriptor = descriptor as DeviceDescriptor
                ?? throw new ArgumentException(
                    $"descriptor 必须是 DeviceDescriptor 类型，当前类型：{descriptor.GetType().Name}");

            _logger.Info($"注册设备: {concreteDescriptor}");

            IDevice device;
            try
            {
                device = _factory.Create(descriptor);
            }
            catch (Exception ex)
            {
                _logger.Error($"创建设备 [{descriptor.Id}] 失败", ex);
                throw;
            }

            var context = new DeviceContext(concreteDescriptor, device);
            device.StateChanged += (s, e) => OnDeviceStateChanged(context, e);

            _registry.TryAdd(context);

            _logger.Info($"设备 [{descriptor.Id}] 注册成功。AutoConnect={descriptor.AutoConnect}");

            if (descriptor.AutoConnect)
                ConnectAsync(context);
        }

        public void Unregister(string deviceId)
        {
            if (!_registry.TryRemove(deviceId, out var context))
            {
                _logger.Warn($"Unregister: 未找到设备 [{deviceId}]");
                return;
            }

            _logger.Info($"注销设备: [{deviceId}]");

            context.CancelReconnect();

            try
            {
                if (context.Device.State == DeviceState.Connected)
                    context.Device.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.Error($"注销设备 [{deviceId}] 时断开连接异常", ex);
            }

            context.Dispose();
        }

        public bool IsRegistered(string deviceId)
            => _registry.Contains(deviceId);

        // ════════════════════════════════════════════════════════
        // 查询
        // ════════════════════════════════════════════════════════

        public IDevice Get(string deviceId)
        {
            if (_registry.TryGet(deviceId, out var ctx))
                return ctx.Device;

            throw new DeviceNotFoundException(deviceId);
        }

        public T Get<T>(string deviceId) where T : class, IDevice
        {
            var device = Get(deviceId);
            if (device is T typed)
                return typed;

            throw new InvalidCastException(
                $"设备 [{deviceId}] 类型为 {device.GetType().Name}，无法转换为 {typeof(T).Name}");
        }

        public bool TryGet(string deviceId, out IDevice device)
        {
            if (_registry.TryGet(deviceId, out var ctx))
            {
                device = ctx.Device;
                return true;
            }
            device = null;
            return false;
        }

        public bool TryGet<T>(string deviceId, out T device) where T : class, IDevice
        {
            if (TryGet(deviceId, out var raw) && raw is T typed)
            {
                device = typed;
                return true;
            }
            device = null;
            return false;
        }

        public IReadOnlyList<IDevice> GetAll()
            => _registry.GetAll().Select(ctx => ctx.Device).ToList().AsReadOnly();

        public IReadOnlyList<T> GetAll<T>() where T : class, IDevice
            => _registry.GetAll<T>().Select(ctx => (T)ctx.Device).ToList().AsReadOnly();

        public DeviceState GetState(string deviceId)
        {
            if (_registry.TryGet(deviceId, out var ctx))
                return ctx.Device.State;
            throw new DeviceNotFoundException(deviceId);
        }

        public IReadOnlyDictionary<string, DeviceState> GetAllStates()
        {
            var result = new Dictionary<string, DeviceState>();
            foreach (var ctx in _registry.GetAll())
                result[ctx.Id] = ctx.Device.State;
            return result;
        }

        public IDeviceStatistics GetStatistics(string deviceId)
        {
            if (_registry.TryGet(deviceId, out var ctx))
                return ctx.Statistics;
            throw new DeviceNotFoundException(deviceId);
        }

        // ════════════════════════════════════════════════════════
        // 连接控制
        // ════════════════════════════════════════════════════════

        public void Connect(string deviceId)
        {
            if (!_registry.TryGet(deviceId, out var ctx))
                throw new DeviceNotFoundException(deviceId);

            ConnectAsync(ctx);
        }

        public void Disconnect(string deviceId)
        {
            if (!_registry.TryGet(deviceId, out var ctx))
                throw new DeviceNotFoundException(deviceId);

            DisconnectCore(ctx, "手动断开");
        }

        public void ConnectAll()
        {
            foreach (var ctx in _registry.GetAll())
                ConnectAsync(ctx);
        }

        public void DisconnectAll()
        {
            foreach (var ctx in _registry.GetAll())
                DisconnectCore(ctx, "批量断开");
        }

        // ════════════════════════════════════════════════════════
        // 内部：异步连接（不阻塞调用方）
        // ════════════════════════════════════════════════════════

        private void ConnectAsync(DeviceContext ctx)
        {
            Task.Run(() => ConnectCore(ctx, CancellationToken.None));
        }

        private void ConnectCore(DeviceContext ctx, CancellationToken ct)
        {
            if (ctx.Device.State == DeviceState.Connected ||
                ctx.Device.State == DeviceState.Connecting)
                return;

            ctx.Statistics.OnConnectAttempt();
            _logger.Info($"连接设备 [{ctx.Id}] ...");

            try
            {
                ct.ThrowIfCancellationRequested();
                ctx.Device.Connect();
                // Statistics.OnConnected() 由 OnDeviceStateChanged 事件统一处理，此处不重复调用
                _logger.Info($"设备 [{ctx.Id}] 连接成功");
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"设备 [{ctx.Id}] 连接已取消");
            }
            catch (Exception ex)
            {
                ctx.Statistics.OnConnectFailed(ex.Message);
                _logger.Error($"设备 [{ctx.Id}] 连接失败: {ex.Message}", ex);
                PublishError(ctx, ex.Message, ex);
                TryScheduleReconnect(ctx);
            }
        }

        private void DisconnectCore(DeviceContext ctx, string reason)
        {
            ctx.CancelReconnect();

            if (ctx.Device.State == DeviceState.Disconnected ||
                ctx.Device.State == DeviceState.Disabled)
                return;

            _logger.Info($"断开设备 [{ctx.Id}]（{reason}）");

            try
            {
                ctx.Device.Disconnect();
                ctx.Statistics.OnDisconnected();
            }
            catch (Exception ex)
            {
                _logger.Error($"断开设备 [{ctx.Id}] 时异常", ex);
            }
        }

        // ════════════════════════════════════════════════════════
        // 内部：自动重连（指数退避）
        // ════════════════════════════════════════════════════════

        private void TryScheduleReconnect(DeviceContext ctx)
        {
            var policy = ctx.Descriptor.ReconnectPolicy as ReconnectPolicy
                         ?? ReconnectPolicy.Default;

            if (!policy.Enabled)
                return;

            ctx.TryStartReconnect(ct => ReconnectLoop(ctx, policy, ct));
        }

        private async Task ReconnectLoop(DeviceContext ctx, ReconnectPolicy policy, CancellationToken ct)
        {
            int attempt = 0;

            while (!ct.IsCancellationRequested && policy.ShouldRetry(attempt))
            {
                attempt++;
                ctx.Statistics.OnReconnectAttempt();

                var delay = policy.GetDelay(attempt);
                _logger.Info($"设备 [{ctx.Id}] 第 {attempt} 次重连，等待 {delay.TotalSeconds:F1}s ...");

                PublishReconnecting(ctx, attempt, delay);

                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.Info($"设备 [{ctx.Id}] 重连已取消");
                    return;
                }

                if (ct.IsCancellationRequested) return;

                if (ctx.Device.State == DeviceState.Connected)
                {
                    _logger.Info($"设备 [{ctx.Id}] 已连接，退出重连循环");
                    return;
                }

                try
                {
                    ctx.Statistics.OnConnectAttempt();
                    ctx.Device.Connect();
                    // Statistics.OnConnected() 由 StateChanged 事件触发，此处不重复调用
                    _logger.Info($"设备 [{ctx.Id}] 重连成功（第 {attempt} 次）");
                    return;
                }
                catch (Exception ex)
                {
                    ctx.Statistics.OnConnectFailed(ex.Message);
                    _logger.Warn($"设备 [{ctx.Id}] 重连失败（第 {attempt} 次）: {ex.Message}");
                }
            }

            if (!policy.ShouldRetry(attempt))
            {
                var msg = $"设备 [{ctx.Id}] 重连已达最大次数 {policy.MaxAttempts}，停止重连";
                _logger.Error(msg);
                PublishError(ctx, msg, null);
            }
        }

        // ════════════════════════════════════════════════════════
        // 内部：健康监控
        // ════════════════════════════════════════════════════════

        private void OnHealthCheck(object state)
        {
            if (_disposed) return;

            foreach (var ctx in _registry.GetAll())
            {
                try
                {
                    // 发现意外断连（非 Disabled，且非正在重连中）
                    if (ctx.Device.State == DeviceState.Error ||
                        ctx.Device.State == DeviceState.Disconnected)
                    {
                        if (!ctx.IsReconnecting)
                        {
                            _logger.Warn($"健康检查：设备 [{ctx.Id}] 处于 {ctx.Device.State}，触发重连");
                            TryScheduleReconnect(ctx);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"健康检查异常 [{ctx.Id}]", ex);
                }
            }
        }

        // ════════════════════════════════════════════════════════
        // 内部：设备状态变更处理 + 事件广播
        // ════════════════════════════════════════════════════════

        private void OnDeviceStateChanged(DeviceContext ctx, DeviceStateChangedEventArgs e)
        {
            _logger.Info($"设备 [{ctx.Id}] 状态: {e.OldState} → {e.NewState}" +
                         (e.Reason != null ? $" ({e.Reason})" : string.Empty));

            // 触发管理器级别的聚合事件（UI 直接订阅）
            AnyDeviceStateChanged?.Invoke(this, e);

            // 通过 EventBus 广播（解耦：报警模块、日志模块无需引用 DeviceManager）
            _eventBus.Publish(new DeviceStateChangedBusEvent(
                ctx.Id, ctx.Name, e.OldState, e.NewState, e.Reason));

            // 统计
            switch (e.NewState)
            {
                case DeviceState.Connected:
                    ctx.Statistics.OnConnected();
                    // 连接成功时清除重连任务标记（已在 ConnectCore 中处理，此处作为兜底）
                    break;

                case DeviceState.Disconnected:
                    ctx.Statistics.OnDisconnected();
                    TryScheduleReconnect(ctx);
                    break;

                case DeviceState.Error:
                    ctx.Statistics.OnError(e.Reason ?? "未知错误");
                    PublishError(ctx, e.Reason ?? "未知错误", null);
                    TryScheduleReconnect(ctx);
                    break;
            }
        }

        private void PublishError(DeviceContext ctx, string message, Exception ex)
        {
            _eventBus.Publish(new DeviceErrorBusEvent(ctx.Id, ctx.Name, message, ex));
        }

        private void PublishReconnecting(DeviceContext ctx, int attempt, TimeSpan delay)
        {
            _eventBus.Publish(new DeviceReconnectingBusEvent(ctx.Id, ctx.Name, attempt, delay));
        }

        // ════════════════════════════════════════════════════════
        // Dispose
        // ════════════════════════════════════════════════════════

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _logger.Info("DeviceManager 正在关闭，断开所有设备 ...");
            _healthTimer?.Dispose();
            DisconnectAll();
            _registry.Clear();
            _logger.Info("DeviceManager 已关闭");
        }
    }

    public sealed class DeviceNotFoundException : Exception
    {
        public string DeviceId { get; }

        public DeviceNotFoundException(string deviceId)
            : base($"未找到设备 [{deviceId}]，请先调用 DeviceManager.Register。")
        {
            DeviceId = deviceId;
        }
    }
}
