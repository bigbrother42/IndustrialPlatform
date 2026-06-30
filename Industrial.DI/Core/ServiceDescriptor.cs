using Industrial.DI.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Core
{
    /// <summary>
    /// 服务描述
    /// </summary>
    public sealed class ServiceDescriptor
    {
        public Type ServiceType { get; }

        public Type ImplementationType { get; }

        public Func<IContainer, object> Factory { get; }

        public ServiceLifetime Lifetime { get; }

        public object Implementation { get; set; }

        public bool IsFactory => Factory != null;

        public ServiceDescriptor(
            Type serviceType,
            Type implementationType,
            ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        public ServiceDescriptor(
            Type serviceType,
            Func<IContainer, object> factory,
            ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            Factory = factory;
            Lifetime = lifetime;
        }
    }
}
