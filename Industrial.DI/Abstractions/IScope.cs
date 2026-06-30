using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Abstractions
{
    public interface IScope : IDisposable
    {
        T Resolve<T>();

        object Resolve(Type type);
    }
}
