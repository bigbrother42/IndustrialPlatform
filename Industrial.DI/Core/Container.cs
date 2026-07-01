using Industrial.DI.Abstractions;
using Industrial.DI.Exceptions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Industrial.DI.Core
{
    public sealed class Container : IContainer
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services =
            new Dictionary<Type, ServiceDescriptor>();

        private readonly object _lock = new object();

        // 用 ThreadStatic 替代实例级 Stack，天然线程安全且无需传递
        [ThreadStatic]
        private static HashSet<Type> _resolutionChain;

        #region Register

        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            Register(typeof(TService),
                new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
        }

        public void RegisterSingleton<TService>(Func<IContainer, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            Register(typeof(TService),
                new ServiceDescriptor(typeof(TService), c => factory(c), ServiceLifetime.Singleton));
        }

        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            Register(typeof(TService),
                new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Transient));
        }

        public void RegisterTransient<TService>(Func<IContainer, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            Register(typeof(TService),
                new ServiceDescriptor(typeof(TService), c => factory(c), ServiceLifetime.Transient));
        }

        public void RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            Register(typeof(TService),
                new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Scoped));
        }

        public void RegisterInstance<TService>(TService instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var descriptor = new ServiceDescriptor(typeof(TService), typeof(TService), ServiceLifetime.Singleton);
            descriptor.Implementation = instance;

            Register(typeof(TService), descriptor);
        }

        private void Register(Type serviceType, ServiceDescriptor descriptor)
        {
            lock (_lock)
            {
                _services[serviceType] = descriptor;
            }
        }

        #endregion

        #region Resolve

        public T Resolve<T>() => (T)Resolve(typeof(T), null);

        public object Resolve(Type type) => Resolve(type, null);

        public IScope CreateScope() => new Scope(this);

        // 统一解析入口，供 Scope 内部调用
        internal object Resolve(Type type, Scope scope)
        {
            if (_resolutionChain == null)
                _resolutionChain = new HashSet<Type>();

            if (!_resolutionChain.Add(type))
                throw new CircularDependencyException(type);

            try
            {
                ServiceDescriptor descriptor;

                lock (_lock)
                {
                    _services.TryGetValue(type, out descriptor);
                }

                if (descriptor == null)
                {
                    if (!type.IsAbstract && !type.IsInterface)
                        return CreateInstance(type, scope);

                    throw new ServiceNotFoundException(type);
                }

                switch (descriptor.Lifetime)
                {
                    case ServiceLifetime.Singleton:
                        return GetOrCreateSingleton(descriptor, scope);

                    case ServiceLifetime.Scoped:
                        if (scope == null)
                            throw new ScopeException(type);
                        return scope.GetOrCreateScoped(type, () => CreateInstance(descriptor, scope));

                    default: // Transient
                        return CreateInstance(descriptor, scope);
                }
            }
            finally
            {
                _resolutionChain.Remove(type);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object GetOrCreateSingleton(ServiceDescriptor descriptor, Scope scope)
        {
            if (descriptor.Implementation != null)
                return descriptor.Implementation;

            lock (descriptor)
            {
                // double-checked locking
                if (descriptor.Implementation != null)
                    return descriptor.Implementation;

                descriptor.Implementation = CreateInstance(descriptor, scope);
                return descriptor.Implementation;
            }
        }

        #endregion

        #region CreateInstance

        private object CreateInstance(ServiceDescriptor descriptor, Scope scope)
        {
            if (descriptor.IsFactory)
                return descriptor.Factory(this);

            return CreateInstance(descriptor.ImplementationType, scope);
        }

        private object CreateInstance(Type type, Scope scope)
        {
            var ctor = ConstructorCache.GetBestConstructor(type);
            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
                return Activator.CreateInstance(type);

            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = Resolve(parameters[i].ParameterType, scope);
            }

            return Activator.CreateInstance(type, args);
        }

        #endregion
    }
}
