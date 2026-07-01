using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Exceptions
{
    public class CircularDependencyException : Exception
    {
        public CircularDependencyException(Type type) 
            : base($"Circular dependency detected: {type.FullName}")
        {
        }
    }
}
