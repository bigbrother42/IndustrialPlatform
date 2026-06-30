using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.Plugin
{
    public interface IPlugin
    {
        string Name { get; }

        string Version { get; }

        void Initialize();

        void Shutdown();
    }
}
