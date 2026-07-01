using Industrial.Contracts.Device;
using System;

namespace Industrial.Device
{
    /// <summary>
    /// 设备状态快照（比 <see cref="DeviceState"/> 枚举携带更多上下文）。
    /// 适用于 UI 层展示和日志记录。
    /// </summary>
    public sealed class DeviceStatus
    {
        public string DeviceId { get; }
        public string DeviceName { get; }
        public DeviceState State { get; }
        public bool IsConnected => State == DeviceState.Connected;
        public bool HasError => State == DeviceState.Error;
        public string Message { get; }
        public DateTime Timestamp { get; }

        public DeviceStatus(string deviceId, string deviceName, DeviceState state, string message = null)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            State = state;
            Message = message;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
            => $"[{DeviceId}] {DeviceName}: {State}" +
               (Message != null ? $" - {Message}" : string.Empty);
    }
}
