using Industrial.Contracts.Events;
using System;

namespace Industrial.Contracts.Device
{
    /// <summary>
    /// 设备状态变更事件（通过 IEventBus 全局广播）。
    /// 报警、UI、日志等模块均可订阅，无需直接依赖 DeviceManager。
    /// </summary>
    public sealed class DeviceStateChangedBusEvent : PlatformEvent
    {
        public string DeviceId { get; }
        public string DeviceName { get; }
        public DeviceState OldState { get; }
        public DeviceState NewState { get; }
        public string Reason { get; }

        public DeviceStateChangedBusEvent(
            string deviceId,
            string deviceName,
            DeviceState oldState,
            DeviceState newState,
            string reason = null)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            OldState = oldState;
            NewState = newState;
            Reason = reason;
            Source = deviceId;
        }

        public override string ToString()
            => $"[{DeviceId}] {OldState} → {NewState}" +
               (Reason != null ? $" ({Reason})" : string.Empty);
    }

    /// <summary>
    /// 设备发生错误事件。
    /// </summary>
    public sealed class DeviceErrorBusEvent : PlatformEvent
    {
        public string DeviceId { get; }
        public string DeviceName { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        public DeviceErrorBusEvent(
            string deviceId,
            string deviceName,
            string errorMessage,
            Exception exception = null)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            ErrorMessage = errorMessage;
            Exception = exception;
            Source = deviceId;
        }
    }

    /// <summary>
    /// 设备自动重连尝试事件。
    /// </summary>
    public sealed class DeviceReconnectingBusEvent : PlatformEvent
    {
        public string DeviceId { get; }
        public string DeviceName { get; }
        public int AttemptNumber { get; }
        public TimeSpan Delay { get; }

        public DeviceReconnectingBusEvent(
            string deviceId,
            string deviceName,
            int attemptNumber,
            TimeSpan delay)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            AttemptNumber = attemptNumber;
            Delay = delay;
            Source = deviceId;
        }
    }
}
