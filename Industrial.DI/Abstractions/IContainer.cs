using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Abstractions
{
    public interface IContainer
    {
        void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService;

        void RegisterScoped<TService, TImplementation>()
            where TImplementation : TService;

        T Resolve<T>();

        object Resolve(Type type);

        IScope CreateScope();
    }
}
