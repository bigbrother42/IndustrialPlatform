using Industrial.Contracts.TestFlow;
using Industrial.Motion;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProbeStationDemo.Steps
{
    /// <summary>
    /// 步骤1：移动 XY Stage 到目标 Die 位置。
    /// 
    /// 真实场景：
    ///   - 根据配方中的 Die 坐标，驱动 XY 运动台到对准位置
    ///   - 包含视觉对准（此处仿真跳过）
    /// </summary>
    public sealed class MoveToWaferStep : ITestStep
    {
        private readonly double _targetX;
        private readonly double _targetY;

        public string Name => "移动至Die位置";
        public bool IsEnabled => true;

        public MoveToWaferStep(double targetX, double targetY)
        {
            _targetX = targetX;
            _targetY = targetY;
        }

        public StepResult Execute(TestContext context)
        {
            var sw = Stopwatch.StartNew();
            var controller = context.GetService<IMotionController>();

            try
            {
                var x = controller.GetAxis("X");
                var y = controller.GetAxis("Y");

                // 并行移动 X 和 Y（探针台常见插补方式：X/Y 同时到位）
                var taskX = x.MoveAbsoluteAsync(_targetX);
                var taskY = y.MoveAbsoluteAsync(_targetY);
                System.Threading.Tasks.Task.WhenAll(taskX, taskY).Wait();

                return StepResult.Pass(Name, sw.Elapsed,
                    new Dictionary<string, object>
                    {
                        ["pos_x"] = x.CurrentPosition,
                        ["pos_y"] = y.CurrentPosition
                    },
                    $"已到达 ({_targetX:F2}, {_targetY:F2}) mm");
            }
            catch (Exception ex)
            {
                return StepResult.Error(Name, sw.Elapsed, ex.Message);
            }
        }
    }
}
