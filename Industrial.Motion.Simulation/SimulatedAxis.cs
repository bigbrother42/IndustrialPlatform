using Industrial.Motion;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Industrial.Motion.Simulation
{
    /// <summary>
    /// 仿真轴实现：模拟真实轴的运动行为（延迟、位置更新、状态机）。
    /// 
    /// 特性：
    ///   - 运动速度和加速度可配置
    ///   - 软件限位检查
    ///   - 位置精度：0.001mm
    ///   - 使用 Task.Delay 模拟运动时间
    /// </summary>
    public sealed class SimulatedAxis : IAxis
    {
        private double _currentPosition;
        private double _commandPosition;
        private AxisState _state = AxisState.Idle;
        private bool _isEnabled = true;
        private bool _isHomed;
        private readonly object _lock = new object();

        private CancellationTokenSource _moveCts;

        public string AxisName { get; }
        public double DefaultSpeed { get; }     // mm/s
        public double SoftLimitMin { get; }
        public double SoftLimitMax { get; }

        public double CurrentPosition { get { lock (_lock) return _currentPosition; } }
        public double CommandPosition { get { lock (_lock) return _commandPosition; } }
        public bool IsInPosition { get { lock (_lock) return Math.Abs(_currentPosition - _commandPosition) < 0.001; } }
        public bool IsHomed => _isHomed;
        public bool IsEnabled => _isEnabled;
        public AxisState State { get { lock (_lock) return _state; } }

        public event EventHandler<AxisStateChangedEventArgs> StateChanged;
        public event EventHandler<AxisPositionEventArgs> PositionChanged;

        public SimulatedAxis(string axisName, double defaultSpeed = 100.0,
            double softLimitMin = -500, double softLimitMax = 500)
        {
            AxisName = axisName;
            DefaultSpeed = defaultSpeed;
            SoftLimitMin = softLimitMin;
            SoftLimitMax = softLimitMax;
        }

        public void Enable() { _isEnabled = true; }
        public void Disable() { _isEnabled = false; }

        public async Task HomeAsync()
        {
            SetState(AxisState.Homing);
            await Task.Delay(500); // 模拟回零时间
            lock (_lock) { _currentPosition = 0; _commandPosition = 0; }
            _isHomed = true;
            SetState(AxisState.Idle);
        }

        public async Task MoveAbsoluteAsync(double position, double? speed = null)
        {
            CheckEnabled();
            CheckSoftLimit(position);

            _moveCts?.Cancel();
            _moveCts = new CancellationTokenSource();
            var ct = _moveCts.Token;

            double from;
            lock (_lock) { from = _currentPosition; _commandPosition = position; }

            double distance = Math.Abs(position - from);
            if (distance < 0.001) return;

            double spd = speed ?? DefaultSpeed;
            int delayMs = (int)(distance / spd * 1000);
            delayMs = Math.Max(delayMs, 10);

            SetState(AxisState.Moving);

            // 模拟中间位置更新（每50ms报告一次位置）
            var steps = Math.Max(1, delayMs / 50);
            for (int i = 1; i <= steps; i++)
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(Math.Min(50, delayMs / steps), ct).ConfigureAwait(false);
                double fraction = (double)i / steps;
                lock (_lock) { _currentPosition = from + (position - from) * fraction; }
                PositionChanged?.Invoke(this, new AxisPositionEventArgs(_currentPosition));
            }

            if (!ct.IsCancellationRequested)
            {
                lock (_lock) { _currentPosition = position; }
                SetState(AxisState.Idle);
            }
        }

        public async Task MoveRelativeAsync(double distance, double? speed = null)
        {
            double target;
            lock (_lock) { target = _currentPosition + distance; }
            await MoveAbsoluteAsync(target, speed);
        }

        public void Stop()
        {
            _moveCts?.Cancel();
            SetState(AxisState.Idle);
        }

        public void EStop()
        {
            _moveCts?.Cancel();
            SetState(AxisState.Idle);
        }

        private void SetState(AxisState newState)
        {
            AxisState old;
            lock (_lock) { old = _state; _state = newState; }
            if (old != newState)
                StateChanged?.Invoke(this, new AxisStateChangedEventArgs(old, newState));
        }

        private void CheckEnabled()
        {
            if (!_isEnabled) throw new InvalidOperationException($"轴 [{AxisName}] 未使能");
        }

        private void CheckSoftLimit(double position)
        {
            if (position < SoftLimitMin || position > SoftLimitMax)
                throw new ArgumentOutOfRangeException(
                    $"目标位置 {position:F3} 超出软限位 [{SoftLimitMin}, {SoftLimitMax}]");
        }

        public void Dispose() { _moveCts?.Dispose(); }
    }
}
