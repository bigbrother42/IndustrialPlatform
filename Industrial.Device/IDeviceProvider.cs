using Industrial.Contracts.Device;

namespace Industrial.Device
{
    /// <summary>
    /// 硬件驱动提供者接口。
    /// 每种硬件驱动（Motion.ACS、PLC.Modbus、Vision.HIK 等）实现此接口，
    /// 告诉 DeviceFactory 自己能创建哪种 DeviceType 的设备实例。
    /// 
    /// 注册示例（在各硬件驱动的 IServiceModule 中）：
    ///   container.RegisterSingleton&lt;IDeviceProvider, AcsDeviceProvider&gt;()
    ///   → 实际上需要支持多实现，见 DeviceProviderCollection
    /// </summary>
    public interface IDeviceProvider
    {
        /// <summary>
        /// 该 Provider 支持的设备类型标识，与 DeviceDescriptor.DeviceType 匹配。
        /// 例如："Motion.ACS"、"PLC.Modbus"、"Vision.HIK"
        /// </summary>
        string SupportedDeviceType { get; }

        /// <summary>
        /// 根据描述符创建设备实例（不负责连接，只创建对象）。
        /// </summary>
        IDevice Create(IDeviceDescriptor descriptor);
    }
}
