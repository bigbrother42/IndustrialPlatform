using Industrial.Contracts.Alarm;
using Industrial.Contracts.Events;
using Industrial.Contracts.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Industrial.Alarm
{
    /// <summary>
    /// 报警管理器：定义、触发、确认、清除报警。
    /// 
    /// 报警生命周期：
    ///   Raise → Active → Acknowledge → Cleared
    ///                  ↘ (无需确认时直接) → Cleared
    /// 
    /// 同一报警码重复触发时：若已有 Active 报警则忽略（防止报警风暴）。
    /// </summary>
    public sealed class AlarmManager : IAlarmManager
    {
        private readonly ConcurrentDictionary<string, AlarmDefinition> _definitions =
            new ConcurrentDictionary<string, AlarmDefinition>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<Guid, AlarmEntry> _activeAlarms =
            new ConcurrentDictionary<Guid, AlarmEntry>();

        // 历史记录（滚动队列，最多保留 500 条）
        private readonly Queue<AlarmEntry> _history = new Queue<AlarmEntry>();
        private const int MaxHistorySize = 500;
        private readonly object _historyLock = new object();

        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;

        public event EventHandler<AlarmEntry> AlarmRaised;
        public event EventHandler<AlarmEntry> AlarmAcknowledged;
        public event EventHandler<AlarmEntry> AlarmCleared;

        public bool HasActiveAlarms => !_activeAlarms.IsEmpty;
        public bool HasFatalAlarms => _activeAlarms.Values
            .Any(a => a.Severity == AlarmSeverity.Fatal);

        public AlarmManager(IEventBus eventBus, ILoggerFactory loggerFactory)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = loggerFactory.CreateLogger(typeof(AlarmManager));
        }

        public void Define(AlarmDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _definitions[definition.Code] = definition;
        }

        public void Raise(string alarmCode, string detail = null)
        {
            if (!_definitions.TryGetValue(alarmCode, out var def))
            {
                _logger.Warn($"触发未定义报警: {alarmCode}，已自动创建 Error 级别定义");
                def = new AlarmDefinition(alarmCode, alarmCode, AlarmSeverity.Error);
                _definitions[alarmCode] = def;
            }

            // 防止同码重复触发（报警风暴保护）
            if (_activeAlarms.Values.Any(a => a.Code == alarmCode))
            {
                _logger.Debug($"报警 [{alarmCode}] 已处于 Active 状态，跳过重复触发");
                return;
            }

            var entry = new AlarmEntry(def, detail);
            _activeAlarms[entry.Id] = entry;

            _logger.Warn($"报警触发: {entry}");
            AlarmRaised?.Invoke(this, entry);
            _eventBus.Publish(new AlarmRaisedBusEvent(entry));
        }

        public void Acknowledge(Guid alarmEntryId)
        {
            if (!_activeAlarms.TryGetValue(alarmEntryId, out var entry))
                return;

            entry.AcknowledgedAt = DateTime.Now;
            entry.State = AlarmState.Acknowledged;

            _logger.Info($"报警已确认: [{entry.Code}]");
            AlarmAcknowledged?.Invoke(this, entry);

            if (!entry.Definition.RequiresAcknowledge)
                Clear(alarmEntryId);
        }

        public void Clear(Guid alarmEntryId)
        {
            if (!_activeAlarms.TryRemove(alarmEntryId, out var entry))
                return;

            entry.ClearedAt = DateTime.Now;
            entry.State = AlarmState.Cleared;

            AddToHistory(entry);
            _logger.Info($"报警已清除: [{entry.Code}]");
            AlarmCleared?.Invoke(this, entry);
            _eventBus.Publish(new AlarmClearedBusEvent(entry));
        }

        public void ClearAll()
        {
            foreach (var id in _activeAlarms.Keys.ToList())
                Clear(id);
        }

        public IReadOnlyList<AlarmEntry> GetActive()
            => _activeAlarms.Values
               .OrderByDescending(a => a.Severity)
               .ThenBy(a => a.RaisedAt)
               .ToList()
               .AsReadOnly();

        public IReadOnlyList<AlarmEntry> GetHistory(int maxCount = 100)
        {
            lock (_historyLock)
            {
                // TakeLast 在 .NET 4.8 不可用，用 Skip 实现
                int skip = Math.Max(0, _history.Count - maxCount);
                return _history.Skip(skip).ToList().AsReadOnly();
            }
        }

        private void AddToHistory(AlarmEntry entry)
        {
            lock (_historyLock)
            {
                _history.Enqueue(entry);
                while (_history.Count > MaxHistorySize)
                    _history.Dequeue();
            }
        }
    }
}
