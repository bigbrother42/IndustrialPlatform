using System;

namespace Industrial.DI.Abstractions
{
    public interface IContainer
    {
        void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterSingleton<TService>(Func<IContainer, TService> factory);

        void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterTransient<TService>(Func<IContainer, TService> factory);

        void RegisterScoped<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterInstance<TService>(TService instance);

        T Resolve<T>();

        object Resolve(Type type);

        IScope CreateScope();
    }
}
