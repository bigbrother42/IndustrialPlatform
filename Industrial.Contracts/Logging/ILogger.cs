using System;

namespace Industrial.Contracts.Logging
{
    public interface ILogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception = null);
        void Fatal(string message, Exception exception = null);

        ILogger ForContext(string contextName);
    }

    public interface ILoggerFactory
    {
        ILogger CreateLogger(string name);
        ILogger CreateLogger(Type type);
    }
}
