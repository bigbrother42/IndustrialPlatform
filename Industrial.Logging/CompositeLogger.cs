using Industrial.Contracts.Logging;
using System;
using System.Collections.Generic;

namespace Industrial.Logging
{
    /// <summary>
    /// 组合日志：将消息同时写入多个 ILogger（如文件 + UI 控制台）。
    /// </summary>
    public sealed class CompositeLogger : ILogger
    {
        private readonly IReadOnlyList<ILogger> _loggers;
        private readonly string _contextName;

        public CompositeLogger(IReadOnlyList<ILogger> loggers, string contextName = null)
        {
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
            _contextName = contextName;
        }

        public void Debug(string message) => Forward(l => l.Debug(message));
        public void Info(string message) => Forward(l => l.Info(message));
        public void Warn(string message) => Forward(l => l.Warn(message));
        public void Error(string message, Exception exception = null) => Forward(l => l.Error(message, exception));
        public void Fatal(string message, Exception exception = null) => Forward(l => l.Fatal(message, exception));

        public ILogger ForContext(string contextName)
        {
            var contexted = new List<ILogger>(_loggers.Count);
            foreach (var logger in _loggers)
                contexted.Add(logger.ForContext(contextName));
            return new CompositeLogger(contexted, contextName);
        }

        private void Forward(Action<ILogger> action)
        {
            foreach (var logger in _loggers)
            {
                try { action(logger); }
                catch { /* 单个日志目标故障不应影响整体 */ }
            }
        }
    }
}
