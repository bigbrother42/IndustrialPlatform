using Industrial.DI.Abstractions;
using Industrial.DI.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Core
{
    public sealed class Container : IContainer
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services =
            new Dictionary<Type, ServiceDescriptor>();

        private readonly Stack<Type> _stack =
            new Stack<Type>();

        #region Register

        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            _services[typeof(TService)] =
                new ServiceDescriptor(
                    typeof(TService),
                    typeof(TImplementation),
                    ServiceLifetime.Singleton);
        }

        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            _services[typeof(TService)] =
                new ServiceDescriptor(
                    typeof(TService),
                    typeof(TImplementation),
                    ServiceLifetime.Transient);
        }

        public void RegisterSingleton<TService>(Func<IContainer, object> factory)
        {
            _services[typeof(TService)] =
                new ServiceDescriptor(
                    typeof(TService),
                    factory,
                    ServiceLifetime.Singleton);
        }

        public void RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            _services[typeof(TService)] =
                new ServiceDescriptor(
                    typeof(TService),
                    typeof(TImplementation),
                    ServiceLifetime.Scoped);
        }

        #endregion

        #region Resolve

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public object Resolve(Type type)
        {
            if (_stack.Contains(type))
                throw new CircularDependencyException(type);

            _stack.Push(type);

            try
            {
                if (!_services.TryGetValue(type, out var descriptor))
                {
                    if (!type.IsAbstract)
                        return CreateInstance(type);

                    throw new ServiceNotFoundException(type);
                }

                if (descriptor.Lifetime == ServiceLifetime.Singleton)
                {
                    if (descriptor.Implementation != null)
                        return descriptor.Implementation;
                }

                object instance = Create(descriptor);

                if (descriptor.Lifetime == ServiceLifetime.Singleton)
                    descriptor.Implementation = instance;

                return instance;
            }
            finally
            {
                _stack.Pop();
            }
        }

        public object Resolve(Type type, Scope scope = null)
        {
            if (_stack.Contains(type))
                throw new CircularDependencyException(type);

            _stack.Push(type);

            try
            {
                if (!_services.TryGetValue(type, out var descriptor))
                {
                    if (!type.IsAbstract)
                        return CreateInstance(type, scope); // concrete type

                    throw new ServiceNotFoundException(type);
                }

                switch (descriptor.Lifetime)
                {
                    case ServiceLifetime.Singleton:
                        if (descriptor.Implementation == null)
                            descriptor.Implementation = CreateInstance(descriptor, scope);

                        return descriptor.Implementation;

                    case ServiceLifetime.Scoped:
                        if (scope == null)
                            throw new ScopeException(type);

                        return scope.GetOrCreateScoped(type,
                            () => CreateInstance(descriptor, scope));

                    default:
                        return CreateInstance(descriptor, scope);
                }
            }
            finally
            {
                _stack.Pop();
            }
        }

        #endregion

        #region Create

        private object Create(ServiceDescriptor descriptor)
        {
            if (descriptor.IsFactory)
                return descriptor.Factory(this);

            return CreateInstance(descriptor.ImplementationType);
        }

        public IScope CreateScope()
        {
            return new Scope(this);
        }

        #endregion

        #region CreateInstance

        private object CreateInstance(Type type)
        {
            var ctor = ConstructorCache.GetBestConstructor(type);

            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
                return Activator.CreateInstance(type);

            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = Resolve(parameters[i].ParameterType);
            }

            return Activator.CreateInstance(type, args);
        }

        private object CreateInstance(Type type, Scope scope)
        {
            var ctor = ConstructorCache.GetBestConstructor(type);

            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
                return Activator.CreateInstance(type);

            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                args[i] = scope != null
                    ? scope.Resolve(paramType)
                    : Resolve(paramType);
            }

            return Activator.CreateInstance(type, args);
        }

        private object CreateInstance(ServiceDescriptor descriptor, Scope scope)
        {
            // 1. Factory模式优先
            if (descriptor.IsFactory)
                return descriptor.Factory(this);

            // 2. 普通类型创建
            return CreateInstance(descriptor.ImplementationType, scope);
        }

        #endregion
    }
}
