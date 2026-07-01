using Industrial.Contracts.Device;
using Industrial.Contracts.Events;
using Industrial.Contracts.Logging;
using Industrial.Device;
using Industrial.DI.Abstractions;
using Industrial.DI.Extensions;
using Industrial.EventBus;
using Industrial.Logging;
using System;
using System.IO;

namespace Industrial.Bootstrap
{
    /// <summary>
    /// 应用程序组合根（Composition Root）。
    /// 服务注册通过 IContainer 完成，底层由 UnityContainer 驱动。
    ///
    /// 原则：
    ///   1. 唯一允许 new 具体实现的地方
    ///   2. 按依赖顺序注册：Core → Infrastructure → Hardware → Business → UI
    ///   3. 各层通过 IServiceModule 封装自己的注册逻辑，保持此文件整洁
    /// </summary>
    public static class Bootstrapper
    {
        public static void Initialize(IContainer container)
        {
            RegisterInfrastructure(container);
            RegisterHardware(container);
            RegisterBusiness(container);
            RegisterUI(container);
        }

        private static void RegisterInfrastructure(IContainer container)
        {
            // ── 日志 ──────────────────────────────────────────
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            var loggerFactory = new FileLoggerFactory(logDir, LogLevel.Debug);

            container.RegisterInstance<ILoggerFactory>(loggerFactory);
            container.RegisterSingleton<ILogger>(c =>
                c.Resolve<ILoggerFactory>().CreateLogger("Platform"));

            // ── 事件总线 ──────────────────────────────────────
            container.RegisterSingleton<IEventBus, InProcessEventBus>();

            // TODO: 配置
            // container.RegisterSingleton<IConfigurationProvider, AppConfigProvider>();
        }

        private static void RegisterHardware(IContainer container)
        {
            // DeviceFactory 是单例，各驱动 Provider 向它注册自己
            container.RegisterSingleton<DeviceFactory>();

            // DeviceManager 依赖：DeviceFactory + IEventBus + ILoggerFactory（均已注册）
            container.RegisterSingleton<IDeviceManager, DeviceManager>();

            // ── 注册硬件驱动 Provider（示例：仿真模式）────────
            // 真实项目中，每个驱动模块有自己的 IServiceModule
            // 在此注册后调用 factory.RegisterProvider(...)
            //
            // container.AddModule(new AcsMotionServiceModule());
            // container.AddModule(new ModbusPlcServiceModule());
            // container.AddModule(new HikVisionServiceModule());

            // ── 在 DeviceManager 中注册具体设备 ─────────────
            // 真实项目从配置文件读取设备列表，此处仅示意
            //
            // var dm = container.Resolve<IDeviceManager>();
            // dm.Register(
            //     DeviceDescriptor.Create("axis-x", "Motion.ACS")
            //         .WithName("X轴")
            //         .WithProperty("Port", "COM3")
            //         .WithReconnectPolicy(ReconnectPolicy.Default)
            //         .Build());
        }

        private static void RegisterBusiness(IContainer container)
        {
            // container.AddModule(new AlarmServiceModule());
            // container.AddModule(new RecipeServiceModule());
        }

        private static void RegisterUI(IContainer container)
        {
            // container.RegisterTransient<MainForm>();
        }
    }
}
