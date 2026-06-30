using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Core
{
    public class ConstructorCache
    {
        private static readonly ConcurrentDictionary<Type, ConstructorInfo> _cache
            = new ConcurrentDictionary<Type, ConstructorInfo>();

        public static ConstructorInfo GetBestConstructor(Type type)
        {
            return _cache.GetOrAdd(type, t =>
            {
                return t.GetConstructors()
                        .OrderByDescending(c => c.GetParameters().Length)
                        .First();
            });
        }
    }
}
