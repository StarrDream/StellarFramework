using StellarFramework.Pool;

namespace StellarFramework.Res
{
    /// <summary>
    /// ResKit 门面
    /// 我统一管理加载器的分配与回收，避免业务层直接操作池与资源生命周期细节。
    /// </summary>
    public static class ResKit
    {
        /// <summary>
        /// 我从对象池申请一个加载器，保持 0 GC 的常驻使用体验。
        /// </summary>
        public static T Allocate<T>() where T : ResLoader, new()
        {
            return PoolKit.Allocate<T>();
        }

        /// <summary>
        /// 我回收强类型加载器。
        /// 资源释放逻辑交给对象池回收流程中的 OnRecycled 统一处理，避免重复 ReleaseAll。
        /// </summary>
        public static void Recycle<T>(T loader) where T : ResLoader, new()
        {
            if (loader == null)
            {
                LogKit.LogError("[ResKit] Recycle 失败: loader 为空");
                return;
            }

            PoolKit.Recycle(loader);
        }

        /// <summary>
        /// 我回收接口类型加载器。
        /// 这里不穷举具体子类，而是交给对象池系统按运行时真实类型回收，保持对扩展开放、对修改关闭。
        /// </summary>
        public static void Recycle(IResLoader loader)
        {
            if (loader == null)
            {
                LogKit.LogError("[ResKit] Recycle(IResLoader) 失败: loader 为空");
                return;
            }

            PoolKit.Recycle(loader);
        }
    }
}