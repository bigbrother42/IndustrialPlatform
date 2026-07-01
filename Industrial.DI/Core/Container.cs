using Unity;

namespace Industrial.DI.Core
{
    /// <summary>
    /// 平台默认容器入口，内部使用 UnityContainer。
    /// 保留此类名以兼容现有 <c>new Container()</c> 写法。
    /// </summary>
    public sealed class Container : UnityContainerAdapter
    {
        public Container() : base(new UnityContainer())
        {
        }

        public Container(IUnityContainer unityContainer) : base(unityContainer)
        {
        }
    }
}
