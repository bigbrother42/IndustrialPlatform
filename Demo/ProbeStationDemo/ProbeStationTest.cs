using Industrial.Contracts.Alarm;
using Industrial.Contracts.Device;
using Industrial.Contracts.Logging;
using Industrial.Contracts.Recipe;
using Industrial.Contracts.TestFlow;
using Industrial.Motion;
using Industrial.Motion.Simulation;
using ProbeStationDemo.Steps;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ProbeStationDemo
{
    /// <summary>
    /// 探针台完整测试流程控制器。
    /// 
    /// 测试场景（COC/探针台典型流程）：
    ///   晶圆: 5×5 Die 矩阵（25个芯片）
    ///   每个 Die 执行三项测试：
    ///     1. 接触电阻  (ContactResistance)  目标 < 1.5 Ω
    ///     2. 漏电流    (LeakageCurrent)     目标 < 100 nA
    ///     3. 击穿电压  (BreakdownVoltage)   目标 > 500 V
    /// </summary>
    public sealed class ProbeStationTest
    {
        private readonly IDeviceManager _deviceManager;
        private readonly IRecipeManager _recipeManager;
        private readonly IAlarmManager _alarmManager;
        private readonly ITestFlowEngine _engine;
        private readonly ILogger _logger;

        // 晶圆布局（从配方读取）
        private int _dieRows;
        private int _dieCols;
        private double _diePitch;      // Die间距（mm）
        private double _safeHeight;    // 探针安全高度（mm）
        private double _contactHeight; // 探针接触高度（mm）

        // 测试结果统计
        private int _totalDies;
        private int _passDies;
        private int _failDies;
        private readonly List<DieTestResult> _results = new List<DieTestResult>();

        public ProbeStationTest(
            IDeviceManager deviceManager,
            IRecipeManager recipeManager,
            IAlarmManager alarmManager,
            ITestFlowEngine engine,
            ILoggerFactory loggerFactory)
        {
            _deviceManager = deviceManager;
            _recipeManager = recipeManager;
            _alarmManager = alarmManager;
            _engine = engine;
            _logger = loggerFactory.CreateLogger(typeof(ProbeStationTest));
        }

        public void Run()
        {
            // ── 1. 定义报警 ──────────────────────────────────────
            DefineAlarms();

            // ── 2. 加载配方 ──────────────────────────────────────
            LoadRecipe();

            // ── 3. 等待设备连接 ───────────────────────────────────
            WaitForDevices();

            // ── 4. 回零 ──────────────────────────────────────────
            HomeAxes();

            // ── 5. 执行晶圆测试 ───────────────────────────────────
            RunWaferTest();

            // ── 6. 打印报告 ───────────────────────────────────────
            PrintReport();
        }

        // ── 步骤实现 ─────────────────────────────────────────────

        void DefineAlarms()
        {
            _alarmManager.Define(new AlarmDefinition(
                "MOTION_ERROR", "运动控制器错误",
                AlarmSeverity.Error, AlarmCategory.Hardware, requiresAcknowledge: true));

            _alarmManager.Define(new AlarmDefinition(
                "CONTACT_RESISTANCE_HIGH", "接触电阻超标",
                AlarmSeverity.Warning, AlarmCategory.Process));

            _alarmManager.Define(new AlarmDefinition(
                "LEAKAGE_CURRENT_HIGH", "漏电流超标",
                AlarmSeverity.Warning, AlarmCategory.Process));

            _alarmManager.Define(new AlarmDefinition(
                "BREAKDOWN_VOLTAGE_LOW", "击穿电压不足",
                AlarmSeverity.Error, AlarmCategory.Process));

            _alarmManager.Define(new AlarmDefinition(
                "YIELD_LOW", "晶圆良率低于阈值",
                AlarmSeverity.Fatal, AlarmCategory.Process, requiresAcknowledge: true));
        }

        void LoadRecipe()
        {
            Program.WriteColored("\n── 加载配方 ──", ConsoleColor.Cyan);

            var recipe = new Recipe("COC_TEST_V2", "COC芯片探针台测试配方", "2.1")
                // 晶圆布局
                .Set("wafer.rows",    5.0,  null, 1, 50)
                .Set("wafer.cols",    5.0,  null, 1, 50)
                .Set("die.pitch",     4.0,  "mm", 0.1, 50.0)
                // 运动参数
                .Set("motion.speed.xy",    100.0, "mm/s", 1, 500)
                .Set("motion.speed.z",       3.0, "mm/s", 0.1, 10)
                .Set("probe.safe_height",    5.0,  "mm")
                .Set("probe.contact_height", 0.0,  "mm")
                // 测试电气参数
                .Set("test.contact_resistance.max", 1.5,   "Ω")
                .Set("test.leakage_current.max",  100.0,   "nA")
                .Set("test.breakdown_voltage.min", 500.0,  "V")
                // 测试时序
                .Set("test.settling_time_ms",  50.0,  "ms")
                .Set("test.measure_time_ms",  100.0,  "ms")
                // 质量控制
                .Set("qc.min_yield",           80.0,  "%")
                .Set("qc.retry_on_fail",       false);

            _recipeManager.Add(recipe);
            _recipeManager.Activate("COC_TEST_V2");

            var r = _recipeManager.ActiveRecipe;
            _dieRows = (int)r.GetDouble("wafer.rows");
            _dieCols = (int)r.GetDouble("wafer.cols");
            _diePitch = r.GetDouble("die.pitch");
            _safeHeight = r.GetDouble("probe.safe_height");
            _contactHeight = r.GetDouble("probe.contact_height");

            Console.WriteLine($"  配方: {r.Name} v{r.Version}");
            Console.WriteLine($"  晶圆布局: {_dieRows}行 × {_dieCols}列 = {_dieRows * _dieCols} Dies");
            Console.WriteLine($"  Die间距: {_diePitch} mm");
            Console.WriteLine($"  测试项: 接触电阻 / 漏电流 / 击穿电压");
        }

        void WaitForDevices()
        {
            Program.WriteColored("\n── 等待设备连接 ──", ConsoleColor.Cyan);
            for (int i = 0; i < 20; i++)
            {
                var state = _deviceManager.GetState("motion-probestation");
                if (state == DeviceState.Connected)
                {
                    Program.WriteColored("  ✓ 运动控制器已连接", ConsoleColor.Green);
                    return;
                }
                Thread.Sleep(100);
            }
            _alarmManager.Raise("MOTION_ERROR", "连接超时");
            throw new TimeoutException("运动控制器连接超时");
        }

        void HomeAxes()
        {
            Program.WriteColored("\n── 轴回零 ──", ConsoleColor.Cyan);
            var controller = (IMotionController)_deviceManager.Get("motion-probestation");
            controller.HomeAllAsync().Wait();
            Program.WriteColored("  ✓ 所有轴已回零", ConsoleColor.Green);
        }

        void RunWaferTest()
        {
            Program.WriteColored("\n── 开始晶圆测试 ──", ConsoleColor.Cyan);
            Console.WriteLine($"  测试矩阵: {_dieRows} × {_dieCols}");
            Console.WriteLine();

            var controller = (IMotionController)_deviceManager.Get("motion-probestation");
            var recipe = _recipeManager.ActiveRecipe;
            var rng = new Random(42); // 固定种子确保可复现

            _totalDies = _dieRows * _dieCols;

            for (int row = 0; row < _dieRows; row++)
            {
                for (int col = 0; col < _dieCols; col++)
                {
                    double targetX = col * _diePitch;
                    double targetY = row * _diePitch;

                    var dieResult = TestOneDie(
                        controller, recipe, rng,
                        row, col, targetX, targetY);

                    _results.Add(dieResult);

                    if (dieResult.IsPass) _passDies++;
                    else _failDies++;

                    // 实时显示进度
                    PrintDieProgress(row, col, dieResult);
                }
            }
        }

        DieTestResult TestOneDie(IMotionController controller, Recipe recipe,
            Random rng, int row, int col, double x, double y)
        {
            var context = new TestContext($"Die[{row},{col}]");
            context.RegisterService(controller);
            context.RegisterService(recipe);
            context.RegisterService(_alarmManager);
            context.SharedData["rng"] = rng;
            context.SharedData["targetX"] = x;
            context.SharedData["targetY"] = y;
            context.SharedData["safeHeight"] = _safeHeight;
            context.SharedData["contactHeight"] = _contactHeight;

            var steps = new List<ITestStep>
            {
                new MoveToWaferStep(x, y),
                new ProbeContactStep(),
                new ContactResistanceStep(recipe.GetDouble("test.contact_resistance.max")),
                new LeakageCurrentStep(recipe.GetDouble("test.leakage_current.max")),
                new BreakdownVoltageStep(recipe.GetDouble("test.breakdown_voltage.min")),
                new ProbeRetractStep()
            };

            var plan = new TestPlan($"Die[{row},{col}]", steps,
                abortOnFirstFail: false, continueOnError: true);

            // 必须用局部变量捕获 handler，才能正确 -= 取消订阅
            PlanResult planResult = null;
            var done = new ManualResetEventSlim(false);

            EventHandler<PlanCompletedEventArgs> handler = null;
            handler = (s, e) =>
            {
                planResult = e.Result;
                _engine.PlanCompleted -= handler;  // 立即自我取消订阅
                done.Set();
            };

            _engine.PlanCompleted += handler;
            _engine.Execute(plan);
            done.Wait(TimeSpan.FromSeconds(30));

            return new DieTestResult(row, col, planResult);
        }

        void PrintDieProgress(int row, int col, DieTestResult result)
        {
            var icon = result.IsPass ? "●" : "○";
            var color = result.IsPass ? ConsoleColor.Green : ConsoleColor.Red;

            Program.WriteColored(
                $"  [{row},{col}] {icon} {(result.IsPass ? "PASS" : "FAIL")} | " +
                $"R={result.ContactResistance:F3}Ω  " +
                $"I={result.LeakageCurrent:F1}nA  " +
                $"V={result.BreakdownVoltage:F0}V",
                color);
        }

        void PrintReport()
        {
            double yield = _totalDies > 0 ? (double)_passDies / _totalDies * 100 : 0;
            var recipe = _recipeManager.ActiveRecipe;
            double minYield = recipe.GetDouble("qc.min_yield");

            Console.WriteLine();
            Program.WriteColored("╔══════════════════════ 测试报告 ══════════════════════╗", ConsoleColor.Cyan);
            Console.WriteLine($"  配方:       {recipe.Name} v{recipe.Version}");
            Console.WriteLine($"  测试时间:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  晶圆布局:   {_dieRows} × {_dieCols} = {_totalDies} Dies");
            Console.WriteLine();
            Console.WriteLine($"  总计 Dies:  {_totalDies}");

            Program.WriteColored($"  通过 PASS:  {_passDies}", ConsoleColor.Green);
            Program.WriteColored($"  失败 FAIL:  {_failDies}", _failDies > 0 ? ConsoleColor.Red : ConsoleColor.White);

            var yieldColor = yield >= minYield ? ConsoleColor.Green : ConsoleColor.Red;
            Program.WriteColored($"  良    率:   {yield:F1}%  (阈值 {minYield}%)", yieldColor);

            // 统计各测试项数据
            if (_results.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  ── 测量统计 ──");
                PrintStats("接触电阻", _results, r => r.ContactResistance, "Ω",
                    recipe.GetDouble("test.contact_resistance.max"), isMaxLimit: true);
                PrintStats("漏 电 流", _results, r => r.LeakageCurrent, "nA",
                    recipe.GetDouble("test.leakage_current.max"), isMaxLimit: true);
                PrintStats("击穿电压", _results, r => r.BreakdownVoltage, "V",
                    recipe.GetDouble("test.breakdown_voltage.min"), isMaxLimit: false);
            }

            // 良率过低时触发报警
            if (yield < minYield)
            {
                Console.WriteLine();
                _alarmManager.Raise("YIELD_LOW", $"良率 {yield:F1}% < {minYield}%");
                Program.WriteColored($"  ⚠ 良率低于阈值 {minYield}%，已触发报警！", ConsoleColor.Red);
            }

            Program.WriteColored("╚══════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
        }

        void PrintStats(string name, List<DieTestResult> results,
            Func<DieTestResult, double> selector, string unit,
            double limit, bool isMaxLimit)
        {
            double sum = 0, min = double.MaxValue, max = double.MinValue;
            int count = 0;

            foreach (var r in results)
            {
                double v = selector(r);
                if (double.IsNaN(v)) continue;
                sum += v; min = Math.Min(min, v); max = Math.Max(max, v); count++;
            }

            if (count == 0) return;
            double avg = sum / count;
            bool withinSpec = isMaxLimit ? max <= limit : min >= limit;
            var color = withinSpec ? ConsoleColor.White : ConsoleColor.Yellow;

            string limitStr = isMaxLimit ? $"Limit≤{limit}" : $"Limit≥{limit}";
            Program.WriteColored(
                $"  {name}:  Min={min:F3} Avg={avg:F3} Max={max:F3} {unit}  ({limitStr} {unit})",
                color);
        }
    }

    public sealed class DieTestResult
    {
        public int Row { get; }
        public int Col { get; }
        public bool IsPass { get; }
        public double ContactResistance { get; }
        public double LeakageCurrent { get; }
        public double BreakdownVoltage { get; }

        public DieTestResult(int row, int col, PlanResult planResult)
        {
            Row = row; Col = col;

            if (planResult == null) { IsPass = false; return; }
            IsPass = planResult.IsPass;

            foreach (var step in planResult.StepResults)
            {
                if (step.Measurements == null) continue;
                if (step.Measurements.TryGetValue("resistance_ohm", out var r))
                    ContactResistance = Convert.ToDouble(r);
                if (step.Measurements.TryGetValue("current_nA", out var i))
                    LeakageCurrent = Convert.ToDouble(i);
                if (step.Measurements.TryGetValue("voltage_V", out var v))
                    BreakdownVoltage = Convert.ToDouble(v);
            }
        }
    }
}
