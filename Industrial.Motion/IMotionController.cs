using Industrial.Contracts.Device;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Industrial.Motion
{
    /// <summary>
    /// 多轴运动控制器接口（对应一张运动控制卡或一个控制系统）。
    /// 实现 IDevice，由 DeviceManager 统一管理生命周期。
    /// 
    /// 探针台典型配置：
    ///   - "X"  : 晶圆台水平 X 方向
    ///   - "Y"  : 晶圆台水平 Y 方向
    ///   - "Z"  : 探针针座 Z 方向（上下）
    ///   - "Theta": 晶圆台旋转（可选）
    /// </summary>
    public interface IMotionController : IDevice
    {
        IReadOnlyList<string> AxisNames { get; }

        IAxis GetAxis(string axisName);
        bool TryGetAxis(string axisName, out IAxis axis);

        // 多轴协调（用于插补运动）
        Task MoveAllAsync(System.Collections.Generic.Dictionary<string, double> positions,
                          double? speed = null);
        void StopAll();
        void EStopAll();
        Task HomeAllAsync();
    }
}
