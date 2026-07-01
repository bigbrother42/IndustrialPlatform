using Industrial.Contracts.Logging;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using System;
using System.IO;
using System.Reflection;

namespace Industrial.Logging
{
    /// <summary>
    /// log4net 一次性初始化：优先读取 log4net.config，否则使用内置 RollingFile 配置。
    /// </summary>
    public static class Log4NetConfigurator
    {
        private static bool _configured;
        private static readonly object Lock = new object();

        public static void Configure(string logDirectory = null, string configFilePath = null)
        {
            lock (Lock)
            {
                if (_configured) return;

                var repository = GetRepository();
                if (repository.Configured)
                {
                    _configured = true;
                    return;
                }

                var configPath = configFilePath
                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");

                if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
                {
                    log4net.Config.XmlConfigurator.Configure(repository, new FileInfo(configPath));
                }
                else
                {
                    ConfigureProgrammatically(repository, logDirectory);
                }

                _configured = true;
            }
        }

        private static ILoggerRepository GetRepository()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            return entryAssembly != null
                ? LogManager.GetRepository(entryAssembly)
                : LogManager.GetRepository(typeof(Log4NetConfigurator).Assembly);
        }

        private static void ConfigureProgrammatically(ILoggerRepository repository, string logDirectory)
        {
            var hierarchy = (Hierarchy)repository;
            hierarchy.Root.RemoveAllAppenders();

            var logDir = logDirectory
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDir);

            var layout = new PatternLayout
            {
                ConversionPattern = "%date{yyyy-MM-dd HH:mm:ss.fff} [%level] [%logger] %message%newline"
            };
            layout.ActivateOptions();

            var fileAppender = new RollingFileAppender
            {
                Name = "RollingFile",
                File = Path.Combine(logDir, "platform"),
                AppendToFile = true,
                RollingStyle = RollingFileAppender.RollingMode.Date,
                DatePattern = "yyyyMMdd'.log'",
                StaticLogFileName = false,
                Layout = layout,
                Encoding = System.Text.Encoding.UTF8
            };
            fileAppender.ActivateOptions();

            hierarchy.Root.AddAppender(fileAppender);
            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
        }
    }
}
