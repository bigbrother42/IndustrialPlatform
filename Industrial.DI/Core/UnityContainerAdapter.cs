using Industrial.DI.Abstractions;
using System;
using Unity;
using Unity.Lifetime;

namespace Industrial.DI.Core
{
    /// <summary>
    /// 基于 Unity Container 的 <see cref="IContainer"/> 实现。
    /// 对外仍使用平台统一的 IContainer 接口，底层替换为 UnityContainer。
    /// </summary>
    public class UnityContainerAdapter : IContainer, IDisposable
    {
        private readonly IUnityContainer _unity;
        private bool _disposed;

        public UnityContainerAdapter(IUnityContainer unity)
        {
            _unity = unity ?? throw new ArgumentNullException(nameof(unity));
        }

        /// <summary>获取底层 Unity 容器（高级场景可直接使用 Unity API）。</summary>
        public IUnityContainer Unity => _unity;

        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            _unity.RegisterType<TService, TImplementation>(
                new ContainerControlledLifetimeManager());
        }

        public void RegisterSingleton<TService>(Func<IContainer, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _unity.RegisterFactory<TService>(
                u => factory(new UnityContainerAdapter(u)),
                new ContainerControlledLifetimeManager());
        }

        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            _unity.RegisterType<TService, TImplementation>();
        }

        public void RegisterTransient<TService>(Func<IContainer, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _unity.RegisterFactory<TService>(
                u => factory(new UnityContainerAdapter(u)));
        }

        public void RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            _unity.RegisterType<TService, TImplementation>(
                new HierarchicalLifetimeManager());
        }

        public void RegisterInstance<TService>(TService instance)
        {
            _unity.RegisterInstance(instance);
        }

        public T Resolve<T>() => _unity.Resolve<T>();

        public object Resolve(Type type) => _unity.Resolve(type);

        public IScope CreateScope() => new UnityScope(_unity.CreateChildContainer());

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unity.Dispose();
        }
    }
}
