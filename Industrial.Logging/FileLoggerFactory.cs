using Industrial.Contracts.Logging;
using System;

namespace Industrial.Logging
{
    public sealed class FileLoggerFactory : ILoggerFactory
    {
        private readonly string _logDirectory;
        private readonly LogLevel _minimumLevel;

        public FileLoggerFactory(string logDirectory, LogLevel minimumLevel = LogLevel.Debug)
        {
            _logDirectory = logDirectory;
            _minimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string name)
            => new FileLogger(name, _logDirectory, _minimumLevel);

        public ILogger CreateLogger(Type type)
            => CreateLogger(type.Name);
    }
}
