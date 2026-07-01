using System;

namespace Industrial.Contracts.Events
{
    /// <summary>
    /// 平台事件总线：支持发布/订阅解耦模块通信。
    /// 适用于：设备状态变更、报警触发、配方切换等跨模块通知。
    /// </summary>
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent @event) where TEvent : class;

        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
    }

    /// <summary>
    /// 平台事件基类，提供公共元数据。
    /// </summary>
    public abstract class PlatformEvent
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string Source { get; protected set; }
    }
}
