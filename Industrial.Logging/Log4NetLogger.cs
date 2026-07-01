using Industrial.Contracts.Logging;
using log4net;
using System;

namespace Industrial.Logging
{
    /// <summary>
    /// 基于 log4net 的 <see cref="ILogger"/> 实现。
    /// </summary>
    public sealed class Log4NetLogger : ILogger
    {
        private readonly ILog _log;

        public Log4NetLogger(string name)
        {
            _log = LogManager.GetLogger(name ?? "App");
        }

        public void Debug(string message)
        {
            if (_log.IsDebugEnabled) _log.Debug(message);
        }

        public void Info(string message)
        {
            if (_log.IsInfoEnabled) _log.Info(message);
        }

        public void Warn(string message)
        {
            if (_log.IsWarnEnabled) _log.Warn(message);
        }

        public void Error(string message, Exception exception = null)
        {
            if (exception != null)
            {
                if (_log.IsErrorEnabled) _log.Error(message, exception);
            }
            else if (_log.IsErrorEnabled)
            {
                _log.Error(message);
            }
        }

        public void Fatal(string message, Exception exception = null)
        {
            if (exception != null)
            {
                if (_log.IsFatalEnabled) _log.Fatal(message, exception);
            }
            else if (_log.IsFatalEnabled)
            {
                _log.Fatal(message);
            }
        }

        public ILogger ForContext(string contextName)
            => new Log4NetLogger(contextName);
    }
}
