using Industrial.Contracts.Device;
using Industrial.Device;
using System;
using System.Collections.Generic;

namespace Industrial.Motion.Simulation
{
    /// <summary>
    /// 仿真运动控制器的 DeviceProvider。
    /// 在 Bootstrap 中注册到 DeviceFactory，之后任何 DeviceType="Motion.Simulation" 
    /// 的设备描述符都将创建一个 SimulatedMotionController。
    /// 
    /// 轴配置通过 DeviceDescriptor.Properties 传入：
    ///   "axes" → "X,Y,Z"
    ///   "speed.X" → "200"
    ///   "speed.Y" → "200"
    ///   "speed.Z" → "5"
    /// </summary>
    public sealed class SimulationMotionProvider : IDeviceProvider
    {
        public string SupportedDeviceType => "Motion.Simulation";

        public IDevice Create(IDeviceDescriptor descriptor)
        {
            var axisNames = descriptor.Properties.TryGetValue("axes", out var axesStr)
                ? axesStr.Split(',')
                : new[] { "X", "Y", "Z" };

            var axes = new List<SimulatedAxis>();

            foreach (var name in axisNames)
            {
                var trimmed = name.Trim();
                double speed = 100.0;

                if (descriptor.Properties.TryGetValue($"speed.{trimmed}", out var speedStr))
                    double.TryParse(speedStr, out speed);

                double limitMin = -500;
                double limitMax = 500;

                if (descriptor.Properties.TryGetValue($"limitMin.{trimmed}", out var minStr))
                    double.TryParse(minStr, out limitMin);
                if (descriptor.Properties.TryGetValue($"limitMax.{trimmed}", out var maxStr))
                    double.TryParse(maxStr, out limitMax);

                axes.Add(new SimulatedAxis(trimmed, speed, limitMin, limitMax));
            }

            return new SimulatedMotionController(descriptor.Id, descriptor.Name, axes);
        }
    }
}
