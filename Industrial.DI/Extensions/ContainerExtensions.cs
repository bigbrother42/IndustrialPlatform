using Industrial.DI.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Extensions
{
    public static class ContainerExtensions
    {
        public static void RegisterInstance<T>(
            this IContainer container,
            T instance)
        {
            container.RegisterSingleton<T>(c => instance);
        }
    }
}
