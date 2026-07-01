using Industrial.Contracts.Device;
using Industrial.Motion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Industrial.Motion.Simulation
{
    /// <summary>
    /// 仿真运动控制器：包含多个 SimulatedAxis。
    /// 实现 IMotionController + IDevice，由 DeviceManager 管理生命周期。
    /// 
    /// 探针台典型配置（在 DeviceDescriptor.Properties 中配置）：
    ///   axes: X,Y,Z        → 三轴系统
    ///   speed.X: 200       → X轴默认速度 200mm/s
    ///   speed.Y: 200       → Y轴默认速度 200mm/s
    ///   speed.Z: 5         → Z轴（探针）默认速度 5mm/s（安全速度）
    /// </summary>
    public sealed class SimulatedMotionController : IMotionController
    {
        private readonly Dictionary<string, SimulatedAxis> _axes =
            new Dictionary<string, SimulatedAxis>(StringComparer.OrdinalIgnoreCase);

        private DeviceState _state = DeviceState.Disconnected;

        public string Id { get; }
        public string Name { get; }
        public DeviceState State => _state;
        public IReadOnlyList<string> AxisNames => _axes.Keys.ToList().AsReadOnly();

        public event EventHandler<DeviceStateChangedEventArgs> StateChanged;

        public SimulatedMotionController(string id, string name, IEnumerable<SimulatedAxis> axes)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;

            foreach (var axis in axes)
                _axes[axis.AxisName] = axis;
        }

        public void Connect()
        {
            var old = _state;
            _state = DeviceState.Connected;
            StateChanged?.Invoke(this, new DeviceStateChangedEventArgs(old, _state, "仿真连接"));
        }

        public void Disconnect()
        {
            var old = _state;
            _state = DeviceState.Disconnected;
            StateChanged?.Invoke(this, new DeviceStateChangedEventArgs(old, _state, "仿真断开"));
        }

        public IAxis GetAxis(string axisName)
        {
            if (_axes.TryGetValue(axisName, out var axis)) return axis;
            throw new KeyNotFoundException($"轴 [{axisName}] 未找到，可用轴: {string.Join(", ", AxisNames)}");
        }

        public bool TryGetAxis(string axisName, out IAxis axis)
        {
            if (_axes.TryGetValue(axisName, out var sim)) { axis = sim; return true; }
            axis = null; return false;
        }

        public async Task MoveAllAsync(Dictionary<string, double> positions, double? speed = null)
        {
            var tasks = new List<Task>();
            foreach (var kv in positions)
            {
                if (_axes.TryGetValue(kv.Key, out var axis))
                    tasks.Add(axis.MoveAbsoluteAsync(kv.Value, speed));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public async Task HomeAllAsync()
        {
            var tasks = _axes.Values.Select(a => a.HomeAsync());
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public void StopAll() { foreach (var a in _axes.Values) a.Stop(); }
        public void EStopAll() { foreach (var a in _axes.Values) a.EStop(); }

        public void Dispose()
        {
            foreach (var a in _axes.Values) a.Dispose();
        }
    }
}
