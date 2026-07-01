using Industrial.Contracts.Events;
using System;
using System.Collections.Generic;

namespace Industrial.Contracts.Alarm
{
    public interface IAlarmManager
    {
        void Define(AlarmDefinition definition);
        void Raise(string alarmCode, string detail = null);
        void Acknowledge(Guid alarmEntryId);
        void Clear(Guid alarmEntryId);
        void ClearAll();

        IReadOnlyList<AlarmEntry> GetActive();
        IReadOnlyList<AlarmEntry> GetHistory(int maxCount = 100);
        bool HasActiveAlarms { get; }
        bool HasFatalAlarms { get; }

        event EventHandler<AlarmEntry> AlarmRaised;
        event EventHandler<AlarmEntry> AlarmAcknowledged;
        event EventHandler<AlarmEntry> AlarmCleared;
    }

    public enum AlarmSeverity { Info = 0, Warning = 1, Error = 2, Fatal = 3 }
    public enum AlarmCategory { Hardware, Software, Process, Safety, Communication }
    public enum AlarmState { Active, Acknowledged, Cleared }

    public sealed class AlarmDefinition
    {
        public string Code { get; }
        public string MessageTemplate { get; }
        public AlarmSeverity Severity { get; }
        public AlarmCategory Category { get; }
        public bool RequiresAcknowledge { get; }

        public AlarmDefinition(
            string code,
            string messageTemplate,
            AlarmSeverity severity = AlarmSeverity.Error,
            AlarmCategory category = AlarmCategory.Software,
            bool requiresAcknowledge = false)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            MessageTemplate = messageTemplate ?? throw new ArgumentNullException(nameof(messageTemplate));
            Severity = severity;
            Category = category;
            RequiresAcknowledge = requiresAcknowledge;
        }
    }

    public sealed class AlarmEntry
    {
        public Guid Id { get; } = Guid.NewGuid();
        public AlarmDefinition Definition { get; }
        public string Detail { get; }
        public DateTime RaisedAt { get; } = DateTime.Now;
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? ClearedAt { get; set; }
        public AlarmState State { get; set; } = AlarmState.Active;

        public string Code => Definition.Code;
        public AlarmSeverity Severity => Definition.Severity;
        public string Message => string.IsNullOrEmpty(Detail)
            ? Definition.MessageTemplate
            : $"{Definition.MessageTemplate}: {Detail}";

        public AlarmEntry(AlarmDefinition definition, string detail = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Detail = detail;
        }

        public override string ToString()
            => $"[{Severity}][{Code}] {Message} @ {RaisedAt:HH:mm:ss}";
    }

    // EventBus 广播事件
    public sealed class AlarmRaisedBusEvent : PlatformEvent
    {
        public AlarmEntry Entry { get; }
        public AlarmRaisedBusEvent(AlarmEntry entry) { Entry = entry; Source = entry.Code; }
    }

    public sealed class AlarmClearedBusEvent : PlatformEvent
    {
        public AlarmEntry Entry { get; }
        public AlarmClearedBusEvent(AlarmEntry entry) { Entry = entry; Source = entry.Code; }
    }
}
