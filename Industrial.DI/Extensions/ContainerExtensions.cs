using Industrial.DI.Abstractions;
using System;

namespace Industrial.DI.Extensions
{
    /// <summary>
    /// 为 IContainer 提供便捷的注册扩展方法。
    /// </summary>
    public static class ContainerExtensions
    {
        /// <summary>
        /// 以自身类型注册单例（无接口映射，直接注册具体类型）。
        /// </summary>
        public static void RegisterSingleton<TService>(this IContainer container)
            where TService : class
        {
            container.RegisterSingleton<TService, TService>();
        }

        /// <summary>
        /// 以自身类型注册瞬态（无接口映射，直接注册具体类型）。
        /// </summary>
        public static void RegisterTransient<TService>(this IContainer container)
            where TService : class
        {
            container.RegisterTransient<TService, TService>();
        }

        /// <summary>
        /// 批量注册模块：将注册逻辑封装在 IServiceModule 中，保持 Bootstrap 整洁。
        /// IServiceModule 定义在 Industrial.DI.Abstractions 命名空间。
        /// </summary>
        public static IContainer AddModule(this IContainer container, IServiceModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            module.Register(container);
            return container;
        }
    }
}
