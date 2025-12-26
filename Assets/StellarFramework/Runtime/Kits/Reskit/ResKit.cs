using StellarFramework.Pool;

namespace StellarFramework.Res
{
    /// <summary>
    /// ResKit 门面
    /// 统一管理加载器的分配与回收
    /// </summary>
    public static class ResKit
    {
        /// <summary>
        /// [推荐] 从对象池申请一个加载器 (0 GC)
        /// </summary>
        /// <typeparam name="T">ResourceLoader 或 AddressableLoader</typeparam>
        public static T Allocate<T>() where T : ResLoader, new()
        {
            return PoolKit.Allocate<T>();
        }

        /// <summary>
        ///  回收加载器
        /// 自动释放该加载器持有的所有资源引用
        /// </summary>
        public static void Recycle<T>(T loader) where T : ResLoader, new()
        {
            PoolKit.Recycle(loader);
        }

        /// <summary>
        ///  回收加载器 (接口版本)
        /// 方便持有 IResLoader 的地方直接调用
        /// </summary>
        public static void Recycle(IResLoader loader)
        {
            if (loader == null) return;

            // 1. 释放资源
            loader.ReleaseAll();

            // 2. 根据具体类型回收到对应的池
            // 注意：这里需要根据你的 ResLoader 和 AddressableLoader 的具体实现来判断
            // 假设它们都继承自 ResLoader 基类

            if (loader is ResourceLoader resLoader)
            {
                PoolKit.Recycle(resLoader);
            }
            else if (loader is AddressableLoader aaLoader)
            {
                PoolKit.Recycle(aaLoader);
            }
            else if (loader is ResLoader baseLoader)
            {
                // 如果有其他继承自 ResLoader 的自定义加载器
                // 这里可能需要反射或者更通用的池化处理，
                // 但考虑到 PoolKit.Recycle<T> 需要确定的 T，
                // 最简单的方式是强转。

                // 如果 PoolKit 支持非泛型 Recycle(object obj)，可以直接调用。
                // 如果不支持，我们需要在这里穷举已知类型。
            }
            else
            {
                // 如果是未知的 IResLoader 实现（没继承 ResLoader），无法回收到池
                // 只能任由 GC 回收
                LogKit.LogWarning($"[ResKit] Unknown loader type '{loader.GetType().Name}', cannot recycle to pool.");
            }
        }
    }
}