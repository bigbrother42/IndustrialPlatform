using Industrial.Alarm;
using Industrial.Contracts.Alarm;
using Industrial.Contracts.Device;
using Industrial.Contracts.Events;
using Industrial.Contracts.Logging;
using Industrial.Contracts.Recipe;
using Industrial.Contracts.TestFlow;
using Industrial.Device;
using Industrial.DI.Abstractions;
using Industrial.DI.Extensions;
using Industrial.EventBus;
using Industrial.Logging;
using Industrial.Motion.Simulation;
using Industrial.Recipe;
using Industrial.TestFlow;
using System;
using System.IO;

namespace ProbeStationDemo
{
    public static class DemoBootstrapper
    {
        public static void Initialize(IContainer container)
        {
            RegisterInfrastructure(container);
            RegisterHardware(container);
            RegisterBusiness(container);
            RegisterDemo(container);
        }

        static void RegisterInfrastructure(IContainer container)
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            var loggerFactory = new FileLoggerFactory(logDir, LogLevel.Debug);
            container.RegisterInstance<ILoggerFactory>(loggerFactory);

            container.RegisterSingleton<ILogger>(c => c.Resolve<ILoggerFactory>().CreateLogger("Demo"));

            container.RegisterSingleton<IEventBus, InProcessEventBus>();
        }

        static void RegisterHardware(IContainer container)
        {
            // DeviceFactory 和 Provider 先注册为单例
            container.RegisterSingleton<DeviceFactory>();
            container.RegisterSingleton<SimulationMotionProvider>();

            // IDeviceManager：工厂 lambda 延迟初始化，确保 Factory/Provider 已就绪
            container.RegisterSingleton<IDeviceManager>(c =>
            {
                var factory = c.Resolve<DeviceFactory>();
                factory.RegisterProvider(c.Resolve<SimulationMotionProvider>());

                var dm = new DeviceManager(
                    factory,
                    c.Resolve<IEventBus>(),
                    c.Resolve<ILoggerFactory>());

                // 注册探针台三轴仿真控制器
                dm.Register(
                    DeviceDescriptor.Create("motion-probestation", "Motion.Simulation")
                        .WithName("探针台运动控制器")
                        .WithAutoConnect(true)
                        .WithProperty("axes", "X,Y,Z")
                        .WithProperty("speed.X", "200")
                        .WithProperty("speed.Y", "200")
                        .WithProperty("speed.Z", "5")
                        .WithProperty("limitMax.X", "300")
                        .WithProperty("limitMax.Y", "300")
                        .WithProperty("limitMin.Z", "-50")
                        .WithProperty("limitMax.Z", "50")
                        .Build());

                return dm;
            });
        }

        static void RegisterBusiness(IContainer container)
        {
            container.RegisterSingleton<IAlarmManager, AlarmManager>();
            container.RegisterSingleton<IRecipeManager, RecipeManager>();
            container.RegisterSingleton<ITestFlowEngine, TestFlowEngine>();
        }

        static void RegisterDemo(IContainer container)
        {
            container.RegisterTransient<ProbeStationTest>();
        }
    }
}
