using Industrial.Contracts.TestFlow;
using Industrial.Motion;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProbeStationDemo.Steps
{
    /// <summary>
    /// 步骤6：Z轴上升，探针离开芯片（Probe Retract）。
    /// 每个 Die 测试完毕后必须执行，防止探针划伤晶圆。
    /// </summary>
    public sealed class ProbeRetractStep : ITestStep
    {
        public string Name => "探针上升（离开）";
        public bool IsEnabled => true;

        public StepResult Execute(TestContext context)
        {
            var sw = Stopwatch.StartNew();
            var controller = context.GetService<IMotionController>();
            var safeHeight = (double)context.SharedData["safeHeight"];

            try
            {
                var z = controller.GetAxis("Z");
                z.MoveAbsoluteAsync(safeHeight, speed: 10.0).Wait(); // 回程可快些

                return StepResult.Pass(Name, sw.Elapsed,
                    new Dictionary<string, object> { ["z_pos"] = z.CurrentPosition },
                    $"探针已回到安全高度 Z={safeHeight:F1} mm");
            }
            catch (Exception ex)
            {
                return StepResult.Error(Name, sw.Elapsed, ex.Message);
            }
        }
    }
}
