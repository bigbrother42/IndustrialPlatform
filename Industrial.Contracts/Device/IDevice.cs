using System;

namespace Industrial.Contracts.Device
{
    /// <summary>
    /// 所有硬件设备的统一抽象基接口。
    /// Motion / PLC / Vision / Instrument 均需实现此接口。
    /// </summary>
    public interface IDevice : IDisposable
    {
        string Id { get; }
        string Name { get; }
        DeviceState State { get; }

        void Connect();
        void Disconnect();

        event EventHandler<DeviceStateChangedEventArgs> StateChanged;
    }

    public enum DeviceState
    {
        Disconnected,
        Connecting,
        Connected,
        Error,
        Disabled
    }

    public class DeviceStateChangedEventArgs : EventArgs
    {
        public DeviceState OldState { get; }
        public DeviceState NewState { get; }
        public string Reason { get; }

        public DeviceStateChangedEventArgs(DeviceState oldState, DeviceState newState, string reason = null)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
        }
    }
}
