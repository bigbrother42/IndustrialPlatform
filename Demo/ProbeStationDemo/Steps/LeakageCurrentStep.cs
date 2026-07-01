using Industrial.Contracts.Alarm;
using Industrial.Contracts.TestFlow;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProbeStationDemo.Steps
{
    /// <summary>
    /// 步骤4：测量漏电流（Leakage Current / Iddq）。
    /// 
    /// 测试原理：
    ///   - 施加额定工作电压（如 1.8V 或 3.3V）
    ///   - 测量静态电流消耗
    ///   - 过大的漏电流表示存在短路或栅极氧化层缺陷
    /// 
    /// 仿真数据：
    ///   - 对数正态分布（因为漏电流通常跨多个数量级）
    ///   - 均值 30 nA，3% 概率超标
    /// </summary>
    public sealed class LeakageCurrentStep : ITestStep
    {
        private readonly double _maxCurrentNa;

        public string Name => "漏电流测量";
        public bool IsEnabled => true;

        public LeakageCurrentStep(double maxCurrentNa)
        {
            _maxCurrentNa = maxCurrentNa;
        }

        public StepResult Execute(TestContext context)
        {
            var sw = Stopwatch.StartNew();
            var rng = (Random)context.SharedData["rng"];
            var alarmManager = context.GetService<IAlarmManager>();

            System.Threading.Thread.Sleep(50); // 稳定等待

            // 对数正态分布，3% 超标
            double currentNa = rng.NextDouble() < 0.03
                ? 100 + rng.NextDouble() * 500     // 超标值
                : Math.Exp(NextGaussian(rng, 3.4, 0.5)); // 正常值（ln均值≈30nA）

            currentNa = Math.Max(0.1, currentNa);

            var measurements = new Dictionary<string, object>
            {
                ["current_nA"] = currentNa,
                ["test_voltage_V"] = 1.8,
                ["limit_nA"] = _maxCurrentNa
            };

            if (currentNa > _maxCurrentNa)
            {
                alarmManager.Raise("LEAKAGE_CURRENT_HIGH",
                    $"{currentNa:F1} nA > {_maxCurrentNa} nA");

                return StepResult.Fail(Name, sw.Elapsed,
                    $"漏电流 {currentNa:F1} nA 超过限值 {_maxCurrentNa} nA",
                    measurements);
            }

            return StepResult.Pass(Name, sw.Elapsed, measurements,
                $"漏电流 {currentNa:F1} nA  ✓");
        }

        private static double NextGaussian(Random rng, double mean, double stdDev)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return mean + stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
