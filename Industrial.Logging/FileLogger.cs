using Industrial.Contracts.Logging;
using System;
using System.IO;
using System.Text;

namespace Industrial.Logging
{
    /// <summary>
    /// 线程安全的滚动文件日志。
    /// 每天生成一个日志文件，格式：yyyy-MM-dd.log
    /// </summary>
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly string _contextName;
        private readonly string _logDirectory;
        private readonly LogLevel _minimumLevel;
        private readonly object _writeLock = new object();

        private string _currentFilePath;
        private DateTime _currentDate;

        public FileLogger(string contextName, string logDirectory, LogLevel minimumLevel = LogLevel.Debug)
        {
            _contextName = contextName ?? "App";
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _minimumLevel = minimumLevel;

            Directory.CreateDirectory(_logDirectory);
            _currentDate = DateTime.Today;
            _currentFilePath = BuildFilePath(_currentDate);
        }

        public void Debug(string message) => Write(LogLevel.Debug, message, null);
        public void Info(string message) => Write(LogLevel.Info, message, null);
        public void Warn(string message) => Write(LogLevel.Warn, message, null);
        public void Error(string message, Exception exception = null) => Write(LogLevel.Error, message, exception);
        public void Fatal(string message, Exception exception = null) => Write(LogLevel.Fatal, message, exception);

        public ILogger ForContext(string contextName)
            => new FileLogger(contextName, _logDirectory, _minimumLevel);

        private void Write(LogLevel level, string message, Exception exception)
        {
            if (level < _minimumLevel) return;

            var now = DateTime.Now;
            var entry = BuildEntry(now, level, message, exception);

            lock (_writeLock)
            {
                RollFileIfNeeded(now);
                File.AppendAllText(_currentFilePath, entry, Encoding.UTF8);
            }
        }

        private string BuildEntry(DateTime now, LogLevel level, string message, Exception exception)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(now.ToString("HH:mm:ss.fff")).Append(']');
            sb.Append('[').Append(level.ToString().ToUpper().PadRight(5)).Append(']');
            sb.Append('[').Append(_contextName).Append("] ");
            sb.AppendLine(message);

            if (exception != null)
            {
                sb.AppendLine("  Exception: " + exception.GetType().Name + ": " + exception.Message);
                sb.AppendLine("  StackTrace: " + exception.StackTrace);

                var inner = exception.InnerException;
                while (inner != null)
                {
                    sb.AppendLine("  Inner: " + inner.GetType().Name + ": " + inner.Message);
                    inner = inner.InnerException;
                }
            }

            return sb.ToString();
        }

        private void RollFileIfNeeded(DateTime now)
        {
            if (now.Date == _currentDate) return;

            _currentDate = now.Date;
            _currentFilePath = BuildFilePath(_currentDate);
        }

        private string BuildFilePath(DateTime date)
            => Path.Combine(_logDirectory, date.ToString("yyyy-MM-dd") + ".log");

        public void Dispose() { }
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }
}
