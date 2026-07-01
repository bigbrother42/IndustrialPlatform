using Industrial.Contracts.TestFlow;
using Industrial.Motion;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProbeStationDemo.Steps
{
    /// <summary>
    /// 步骤2：Z轴下降，探针接触芯片焊垫（Probe Contact）。
    /// 
    /// 真实场景：
    ///   - Z轴以低速（安全速度）下降到接触高度
    ///   - 部分系统使用力传感器检测接触，此处用位置模拟
    ///   - overdrive（过压量）通常 5~50µm，模拟时略过
    /// </summary>
    public sealed class ProbeContactStep : ITestStep
    {
        public string Name => "探针下降（接触）";
        public bool IsEnabled => true;

        public StepResult Execute(TestContext context)
        {
            var sw = Stopwatch.StartNew();
            var controller = context.GetService<IMotionController>();
            var contactHeight = (double)context.SharedData["contactHeight"];

            try
            {
                var z = controller.GetAxis("Z");
                z.MoveAbsoluteAsync(contactHeight, speed: 3.0).Wait(); // 低速下降

                System.Threading.Thread.Sleep(20); // 接触稳定等待

                return StepResult.Pass(Name, sw.Elapsed,
                    new Dictionary<string, object> { ["z_pos"] = z.CurrentPosition },
                    $"探针接触位置 Z={z.CurrentPosition:F3} mm");
            }
            catch (Exception ex)
            {
                return StepResult.Error(Name, sw.Elapsed, ex.Message);
            }
        }
    }
}
