using Industrial.Contracts.Device;
using System;
using System.Collections.Generic;

namespace Industrial.Device
{
    /// <summary>
    /// 设备配置描述符（不可变值对象）。
    /// 通过 <see cref="Builder"/> 链式构建，避免大量构造函数参数。
    /// </summary>
    public sealed class DeviceDescriptor : IDeviceDescriptor
    {
        public string Id { get; }
        public string Name { get; }
        public string DeviceType { get; }
        public bool AutoConnect { get; }
        public IReconnectPolicy ReconnectPolicy { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }

        private DeviceDescriptor(Builder b)
        {
            if (string.IsNullOrWhiteSpace(b.Id))
                throw new ArgumentException("DeviceDescriptor.Id 不能为空");
            if (string.IsNullOrWhiteSpace(b.DeviceType))
                throw new ArgumentException("DeviceDescriptor.DeviceType 不能为空");

            Id = b.Id;
            Name = string.IsNullOrWhiteSpace(b.Name) ? b.Id : b.Name;
            DeviceType = b.DeviceType;
            AutoConnect = b.AutoConnect;
            ReconnectPolicy = b.Policy ?? Industrial.Device.ReconnectPolicy.Default;
            Properties = new Dictionary<string, string>(b.Properties);
        }

        public override string ToString() => $"[{DeviceType}] {Id} ({Name})";

        // ── Builder ──────────────────────────────────────────────

        public static Builder Create(string id, string deviceType)
            => new Builder(id, deviceType);

        public sealed class Builder
        {
            internal string Id { get; }
            internal string DeviceType { get; }
            internal string Name { get; private set; }
            internal bool AutoConnect { get; private set; } = true;
            internal ReconnectPolicy Policy { get; private set; }
            internal Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

            internal Builder(string id, string deviceType)
            {
                Id = id;
                DeviceType = deviceType;
            }

            public Builder WithName(string name) { Name = name; return this; }
            public Builder WithAutoConnect(bool auto) { AutoConnect = auto; return this; }
            public Builder WithReconnectPolicy(ReconnectPolicy policy) { Policy = policy; return this; }
            public Builder WithProperty(string key, string value) { Properties[key] = value; return this; }

            public DeviceDescriptor Build() => new DeviceDescriptor(this);
        }
    }
}
