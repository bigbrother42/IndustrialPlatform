using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Exceptions
{
    public class ScopeException : Exception
    {
        public ScopeException(Type type) : base($"Cannot resolve scoped service outside scope: {type.FullName}")
        {
        }
    }
}
