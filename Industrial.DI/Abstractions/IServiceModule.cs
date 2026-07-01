using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial.DI.Abstractions
{
    /// <summary>
    /// 服务注册模块接口，用于分层组织 DI 注册逻辑。
    /// 各层（Hardware、Business 等）实现此接口，Bootstrap 统一调用。
    /// </summary>
    public interface IServiceModule
    {
        void Register(IContainer container);
    }
}
