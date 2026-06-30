using Industrial.DI.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Core
{
    public class Scope : IScope
    {
        private readonly Container _container;

        private readonly Dictionary<Type, object> _scopedObjects =
            new Dictionary<Type, object>();

        public Scope(Container container)
        {
            _container = container;
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public object Resolve(Type type)
        {
            return _container.Resolve(type, this);
        }

        internal object GetOrCreateScoped(Type type, Func<object> factory)
        {
            if (_scopedObjects.TryGetValue(type, out var instance))
                return instance;

            instance = factory();
            _scopedObjects[type] = instance;

            return instance;
        }

        public void Dispose()
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
