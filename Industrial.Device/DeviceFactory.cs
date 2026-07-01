using Industrial.Contracts.Device;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Industrial.Device
{
    /// <summary>
    /// 设备工厂：根据 DeviceDescriptor.DeviceType 找到匹配的 IDeviceProvider 并创建设备。
    /// 
    /// 设计：Provider 注册表 + 策略模式
    ///   各硬件驱动通过 RegisterProvider 注册自己，
    ///   DeviceManager 调用 Create 时由工厂路由到正确的 Provider。
    /// </summary>
    public sealed class DeviceFactory
    {
        private readonly ConcurrentDictionary<string, IDeviceProvider> _providers =
            new ConcurrentDictionary<string, IDeviceProvider>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册硬件驱动 Provider（允许覆盖，便于测试时替换为仿真 Provider）。
        /// </summary>
        public void RegisterProvider(IDeviceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(provider.SupportedDeviceType))
                throw new ArgumentException("Provider.SupportedDeviceType 不能为空");

            _providers[provider.SupportedDeviceType] = provider;
        }

        /// <summary>
        /// 批量注册 Provider（启动时调用）。
        /// </summary>
        public void RegisterProviders(IEnumerable<IDeviceProvider> providers)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            foreach (var p in providers)
                RegisterProvider(p);
        }

        /// <summary>
        /// 根据描述符创建对应的设备实例。
        /// </summary>
        /// <exception cref="DeviceProviderNotFoundException">未找到对应 Provider 时抛出</exception>
        public IDevice Create(IDeviceDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            if (!_providers.TryGetValue(descriptor.DeviceType, out var provider))
                throw new DeviceProviderNotFoundException(descriptor.DeviceType);

            var device = provider.Create(descriptor);
            if (device == null)
                throw new InvalidOperationException(
                    $"Provider [{descriptor.DeviceType}] 返回了 null 设备实例");

            return device;
        }

        public bool HasProvider(string deviceType)
            => _providers.ContainsKey(deviceType);

        public IReadOnlyCollection<string> RegisteredDeviceTypes
            => _providers.Keys as IReadOnlyCollection<string>
               ?? new List<string>(_providers.Keys).AsReadOnly();
    }

    public sealed class DeviceProviderNotFoundException : Exception
    {
        public string DeviceType { get; }

        public DeviceProviderNotFoundException(string deviceType)
            : base($"未找到 DeviceType='{deviceType}' 的 IDeviceProvider。" +
                   $"请确认对应驱动已注册到 DeviceFactory。")
        {
            DeviceType = deviceType;
        }
    }
}
