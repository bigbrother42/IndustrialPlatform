using Industrial.Contracts.Events;
using System;
using System.Collections.Generic;

namespace Industrial.EventBus
{
    /// <summary>
    /// 进程内同步事件总线实现。
    /// 适用于模块间解耦通信（如设备 → 报警 → UI），无需跨进程/网络。
    /// 线程安全：订阅列表读写加锁，但事件处理在调用线程执行。
    /// </summary>
    public sealed class InProcessEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers =
            new Dictionary<Type, List<Delegate>>();

        private readonly object _lock = new object();

        public void Publish<TEvent>(TEvent @event) where TEvent : class
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));

            List<Delegate> snapshot;

            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                    return;

                // 快照副本：避免在迭代中被修改
                snapshot = new List<Delegate>(list);
            }

            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<TEvent>)handler)(@event);
                }
                catch (Exception ex)
                {
                    // 单个订阅者出错不应阻断其他订阅者
                    OnHandlerException(typeof(TEvent), ex);
                }
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                {
                    list = new List<Delegate>();
                    _handlers[typeof(TEvent)] = list;
                }
                list.Add(handler);
            }

            return new Subscription<TEvent>(this, handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null) return;

            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var list))
                    list.Remove(handler);
            }
        }

        private void OnHandlerException(Type eventType, Exception ex)
        {
            // 生产环境可注入 ILogger 记录此处异常
            // 避免在此抛出，防止干扰其他订阅者
        }

        private sealed class Subscription<TEvent> : IDisposable where TEvent : class
        {
            private readonly InProcessEventBus _bus;
            private readonly Action<TEvent> _handler;
            private bool _disposed;

            public Subscription(InProcessEventBus bus, Action<TEvent> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _bus.Unsubscribe(_handler);
            }
        }
    }
}
