using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Exceptions
{
    public class ServiceNotFoundException : Exception
    {
        public ServiceNotFoundException(Type type) : base($"Service [{type.FullName}] is not registered.")
        {
        }
    }
}
