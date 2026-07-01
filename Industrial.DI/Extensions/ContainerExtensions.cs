using Industrial.DI.Abstractions;
using Industrial.DI.Core;
using System;
using Unity;

namespace Industrial.DI.Extensions
{
    /// <summary>
    /// IContainer / Unity 便捷扩展。
    /// </summary>
    public static class ContainerExtensions
    {
        /// <summary>创建基于 UnityContainer 的平台容器。</summary>
        public static IContainer CreatePlatformContainer()
            => new Container();

        /// <summary>将现有 UnityContainer 包装为平台 IContainer。</summary>
        public static IContainer AsPlatformContainer(this IUnityContainer unity)
            => new UnityContainerAdapter(unity);

        /// <summary>以自身类型注册单例。</summary>
        public static void RegisterSingleton<TService>(this IContainer container)
            where TService : class
        {
            container.RegisterSingleton<TService, TService>();
        }

        /// <summary>以自身类型注册瞬态。</summary>
        public static void RegisterTransient<TService>(this IContainer container)
            where TService : class
        {
            container.RegisterTransient<TService, TService>();
        }

        /// <summary>批量注册模块。</summary>
        public static IContainer AddModule(this IContainer container, IServiceModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            module.Register(container);
            return container;
        }
    }
}
