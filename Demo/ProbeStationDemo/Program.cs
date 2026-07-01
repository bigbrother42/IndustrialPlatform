using Industrial.Contracts.Alarm;
using Industrial.Contracts.Events;
using Industrial.Contracts.Logging;
using Industrial.DI.Core;
using System;
using System.Threading;

namespace ProbeStationDemo
{
    /// <summary>
    /// 探针台测试系统仿真入口。
    /// 
    /// 演示内容：
    ///   1. 平台初始化（IOC + 日志 + 事件总线 + 设备管理器）
    ///   2. 加载半导体测试配方（晶圆布局 + 电气参数 + 判定阈值）
    ///   3. 注册仿真运动控制器（XY Stage + Z 探针轴）
    ///   4. 执行完整晶圆测试流程（5×5 Die 矩阵）
    ///   5. 展示测试报告（良率、统计数据、报警记录）
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "IndustrialPlatform — 探针台仿真演示";

            PrintBanner();

            // ── 1. 构建 IOC 容器并初始化平台 ─────────────────
            // Industrial.DI.Container 内部封装 UnityContainer
            var container = new Container();
            DemoBootstrapper.Initialize(container);

            var logger = container.Resolve<ILogger>();
            var eventBus = container.Resolve<IEventBus>();
            var alarmManager = container.Resolve<IAlarmManager>();

            // ── 2. 订阅全局事件（展示 EventBus 解耦） ────────
            var alarmSub = eventBus.Subscribe<AlarmRaisedBusEvent>(e =>
            {
                var color = e.Entry.Severity >= AlarmSeverity.Error
                    ? ConsoleColor.Red : ConsoleColor.Yellow;
                WriteColored($"  ⚠ 报警: {e.Entry}", color);
            });

            try
            {
                // ── 3. 运行探针台测试 ──────────────────────────────
                var probeTest = container.Resolve<ProbeStationTest>();
                probeTest.Run();

                // ── 4. 展示报警历史 ────────────────────────────────
                PrintAlarmSummary(alarmManager);
            }
            finally
            {
                alarmSub.Dispose();
            }

            Console.WriteLine();
            WriteColored("按任意键退出...", ConsoleColor.DarkGray);
            Console.ReadKey(true);
        }

        static void PrintBanner()
        {
            WriteColored(@"
╔══════════════════════════════════════════════════════════════╗
║       IndustrialPlatform — 半导体探针台测试仿真系统           ║
║       COC / 探针台测试基石 Demo                               ║
╚══════════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            Console.WriteLine();
        }

        static void PrintAlarmSummary(IAlarmManager alarmManager)
        {
            var history = alarmManager.GetHistory(50);
            Console.WriteLine();
            WriteColored($"── 报警历史（共 {history.Count} 条）──", ConsoleColor.Yellow);

            if (history.Count == 0)
            {
                Console.WriteLine("  无报警记录");
                return;
            }

            foreach (var entry in history)
            {
                var color = entry.Severity >= AlarmSeverity.Error
                    ? ConsoleColor.Red : ConsoleColor.Yellow;
                WriteColored($"  [{entry.RaisedAt:HH:mm:ss}] {entry}", color);
            }
        }

        internal static void WriteColored(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }
    }
}
