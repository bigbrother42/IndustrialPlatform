using Industrial.Contracts.Alarm;
using Industrial.Contracts.TestFlow;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProbeStationDemo.Steps
{
    /// <summary>
    /// 步骤5：测量击穿电压（Breakdown Voltage / BVdss）。
    /// 
    /// 测试原理：
    ///   - 对器件漏源极施加递增电压
    ///   - 检测电流急剧增大时的电压（击穿点）
    ///   - BVdss 是功率器件的关键参数
    /// 
    /// 仿真数据：
    ///   - 正态分布，均值 580V，σ=30V
    ///   - 4% 概率低于最小值 500V
    /// </summary>
    public sealed class BreakdownVoltageStep : ITestStep
    {
        private readonly double _minVoltage;

        public string Name => "击穿电压测量";
        public bool IsEnabled => true;

        public BreakdownVoltageStep(double minVoltage)
        {
            _minVoltage = minVoltage;
        }

        public StepResult Execute(TestContext context)
        {
            var sw = Stopwatch.StartNew();
            var rng = (Random)context.SharedData["rng"];
            var alarmManager = context.GetService<IAlarmManager>();

            System.Threading.Thread.Sleep(80); // 高压建立时间

            // 正态分布，4% 不良
            double voltage = rng.NextDouble() < 0.04
                ? 350 + rng.NextDouble() * 140     // 低于 500V
                : NextGaussian(rng, 580, 30);      // 正常：580±30V

            voltage = Math.Max(10, voltage);

            var measurements = new Dictionary<string, object>
            {
                ["voltage_V"] = voltage,
                ["limit_V"] = _minVoltage
            };

            if (voltage < _minVoltage)
            {
                alarmManager.Raise("BREAKDOWN_VOLTAGE_LOW",
                    $"{voltage:F0} V < {_minVoltage} V");

                return StepResult.Fail(Name, sw.Elapsed,
                    $"击穿电压 {voltage:F0} V 低于限值 {_minVoltage} V",
                    measurements);
            }

            return StepResult.Pass(Name, sw.Elapsed, measurements,
                $"击穿电压 {voltage:F0} V  ✓");
        }

        private static double NextGaussian(Random rng, double mean, double stdDev)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return mean + stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
