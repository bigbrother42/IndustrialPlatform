using Industrial.DI.Abstractions;
using System;
using Unity;

namespace Industrial.DI.Core
{
    /// <summary>
    /// Unity 子容器作用域，对应 Scoped 生命周期。
    /// </summary>
    public sealed class UnityScope : IScope
    {
        private readonly IUnityContainer _child;
        private bool _disposed;

        public UnityScope(IUnityContainer child)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public T Resolve<T>() => _child.Resolve<T>();

        public object Resolve(Type type) => _child.Resolve(type);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _child.Dispose();
        }
    }
}
