using Industrial.DI.Abstractions;
using System;
using System.Collections.Generic;

namespace Industrial.DI.Core
{
    public sealed class Scope : IScope
    {
        private readonly Container _container;
        private readonly object _lock = new object();

        private readonly Dictionary<Type, object> _scopedObjects = new Dictionary<Type, object>();

        private bool _disposed;

        public Scope(Container container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public object Resolve(Type type)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Scope));
            return _container.Resolve(type, this);
        }

        internal object GetOrCreateScoped(Type type, Func<object> factory)
        {
            lock (_lock)
            {
                if (_scopedObjects.TryGetValue(type, out var cached))
                    return cached;

                var instance = factory();
                _scopedObjects[type] = instance;
                return instance;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                foreach (var obj in _scopedObjects.Values)
                {
                    if (obj is IDisposable disposable)
                        disposable.Dispose();
                }
                _scopedObjects.Clear();
            }
        }
    }
}
