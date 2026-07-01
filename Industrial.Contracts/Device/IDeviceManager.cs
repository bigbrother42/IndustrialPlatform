using System;
using System.Collections.Generic;

namespace Industrial.Contracts.Device
{
    /// <summary>
    /// 设备管理器：平台所有硬件设备的统一入口。
    /// 负责设备的注册、连接生命周期、自动重连和健康监控。
    /// </summary>
    public interface IDeviceManager : IDisposable
    {
        // ── 注册 ──────────────────────────────────────────────
        void Register(IDeviceDescriptor descriptor);
        void Unregister(string deviceId);
        bool IsRegistered(string deviceId);

        // ── 查询 ──────────────────────────────────────────────
        IDevice Get(string deviceId);
        T Get<T>(string deviceId) where T : class, IDevice;
        bool TryGet(string deviceId, out IDevice device);
        bool TryGet<T>(string deviceId, out T device) where T : class, IDevice;
        IReadOnlyList<IDevice> GetAll();
        IReadOnlyList<T> GetAll<T>() where T : class, IDevice;

        // ── 连接控制 ──────────────────────────────────────────
        void Connect(string deviceId);
        void Disconnect(string deviceId);
        void ConnectAll();
        void DisconnectAll();

        // ── 状态 ──────────────────────────────────────────────
        DeviceState GetState(string deviceId);
        IReadOnlyDictionary<string, DeviceState> GetAllStates();
        IDeviceStatistics GetStatistics(string deviceId);

        // ── 事件 ──────────────────────────────────────────────
        event EventHandler<DeviceStateChangedEventArgs> AnyDeviceStateChanged;
    }

    /// <summary>
    /// 设备注册描述符，作为 <see cref="IDeviceManager.Register"/> 的参数。
    /// 使用 DeviceDescriptor.Builder 构建。
    /// </summary>
    public interface IDeviceDescriptor
    {
        string Id { get; }
        string Name { get; }
        string DeviceType { get; }
        bool AutoConnect { get; }
        IReconnectPolicy ReconnectPolicy { get; }
        IReadOnlyDictionary<string, string> Properties { get; }
    }

    /// <summary>
    /// 重连策略描述。
    /// </summary>
    public interface IReconnectPolicy
    {
        bool Enabled { get; }
        int MaxAttempts { get; }         // -1 = 无限重试
        TimeSpan InitialDelay { get; }
        TimeSpan MaxDelay { get; }
        double BackoffMultiplier { get; }
    }

    /// <summary>
    /// 设备运行统计信息（只读快照）。
    /// </summary>
    public interface IDeviceStatistics
    {
        string DeviceId { get; }
        int TotalConnectAttempts { get; }
        int SuccessfulConnections { get; }
        int FailedConnections { get; }
        int ReconnectAttempts { get; }
        DateTime? LastConnectedAt { get; }
        DateTime? LastDisconnectedAt { get; }
        DateTime? LastErrorAt { get; }
        string LastErrorMessage { get; }
        TimeSpan TotalConnectedTime { get; }
    }
}
