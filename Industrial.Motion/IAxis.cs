using Industrial.Contracts.Device;
using System;
using System.Threading.Tasks;

namespace Industrial.Motion
{
    /// <summary>
    /// 单轴运动控制接口。
    /// 适用于探针台的 X/Y/Z 轴以及任何线性/旋转运动轴。
    /// </summary>
    public interface IAxis : IDisposable
    {
        string AxisName { get; }
        AxisState State { get; }

        // ── 位置查询 ──────────────────────────────────────────
        double CurrentPosition { get; }   // 当前位置（mm 或 deg）
        double CommandPosition { get; }   // 指令位置
        bool IsInPosition { get; }        // 是否到位
        bool IsHomed { get; }

        // ── 运动 ──────────────────────────────────────────────
        Task HomeAsync();
        Task MoveAbsoluteAsync(double position, double? speed = null);
        Task MoveRelativeAsync(double distance, double? speed = null);
        void Stop();
        void EStop();  // 紧急停止

        // ── 使能/禁用 ─────────────────────────────────────────
        void Enable();
        void Disable();
        bool IsEnabled { get; }

        event EventHandler<AxisStateChangedEventArgs> StateChanged;
        event EventHandler<AxisPositionEventArgs> PositionChanged;
    }

    public enum AxisState { Idle, Homing, Moving, Error, Disabled }

    public class AxisStateChangedEventArgs : EventArgs
    {
        public AxisState OldState { get; }
        public AxisState NewState { get; }
        public AxisStateChangedEventArgs(AxisState o, AxisState n) { OldState = o; NewState = n; }
    }

    public class AxisPositionEventArgs : EventArgs
    {
        public double Position { get; }
        public AxisPositionEventArgs(double pos) { Position = pos; }
    }
}
