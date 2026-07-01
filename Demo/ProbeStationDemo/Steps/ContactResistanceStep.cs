using Industrial.Contracts.Alarm;
using Industrial.Contracts.TestFlow;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProbeStationDemo.Steps
{
    /// <summary>
    /// 步骤3：测量接触电阻（Contact Resistance）。
    /// 
    /// 测试原理（四线法）：
    ///   - 通过两路探针注入测试电流（通常 1mA~100mA）
    ///   - 另两路探针测量压降
    ///   - R = V / I
    /// 
    /// 仿真数据：
    ///   - 正态分布，均值 0.8Ω，标准差 0.3Ω
    ///   - 约 5% 概率产生超标值（模拟真实不良率）
    /// </summary>
    public sealed class ContactResistanceStep : ITestStep
    {
        private readonly double _maxResistance;

        public string Name => "接触电阻测量";
        public bool IsEnabled => true;

        public ContactResistanceStep(double maxResistance)
        {
            _maxResistance = maxResistance;
        }

        public StepResult Execute(TestContext context)
        {
            var sw = Stopwatch.StartNew();
            var rng = (Random)context.SharedData["rng"];
            var alarmManager = context.GetService<IAlarmManager>();

            // 模拟测量延迟（ADC采样 + 稳定时间）
            System.Threading.Thread.Sleep(30);

            // 假数据：正态分布 (均值=0.8Ω, σ=0.3Ω)，5% 概率超标
            double resistance = rng.NextDouble() < 0.05
                ? 1.5 + rng.NextDouble() * 1.0      // 超标值：1.5~2.5Ω
                : NextGaussian(rng, 0.8, 0.3);       // 正常值

            resistance = Math.Max(0.01, resistance);

            var measurements = new Dictionary<string, object>
            {
                ["resistance_ohm"] = resistance,
                ["test_current_mA"] = 10.0,
                ["limit_ohm"] = _maxResistance
            };

            if (resistance > _maxResistance)
            {
                alarmManager.Raise("CONTACT_RESISTANCE_HIGH",
                    $"{resistance:F3} Ω > {_maxResistance} Ω");

                return StepResult.Fail(Name, sw.Elapsed,
                    $"接触电阻 {resistance:F3} Ω 超过限值 {_maxResistance} Ω",
                    measurements);
            }

            return StepResult.Pass(Name, sw.Elapsed, measurements,
                $"接触电阻 {resistance:F3} Ω  ✓");
        }

        // Box-Muller 正态分布生成
        private static double NextGaussian(Random rng, double mean, double stdDev)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * z;
        }
    }
}
