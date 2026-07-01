using Industrial.Contracts.Logging;
using System;

namespace Industrial.Logging
{
    /// <summary>
    /// log4net 日志工厂，实现平台 <see cref="ILoggerFactory"/> 契约。
    /// </summary>
    public sealed class Log4NetLoggerFactory : ILoggerFactory
    {
        public Log4NetLoggerFactory(string logDirectory = null, string configFilePath = null)
        {
            Log4NetConfigurator.Configure(logDirectory, configFilePath);
        }

        public ILogger CreateLogger(string name)
            => new Log4NetLogger(name);

        public ILogger CreateLogger(Type type)
            => new Log4NetLogger(type?.FullName ?? "App");
    }
}
