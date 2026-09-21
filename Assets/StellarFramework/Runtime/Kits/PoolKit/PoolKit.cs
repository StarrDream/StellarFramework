using System;
using StellarFramework.Pool;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 零配置的强类型静态对象池入口。
    /// 每个闭合泛型类型 T 拥有独立池；若 T 实现 <see cref="IPoolable"/>，
    /// 取出和回收时会分别调用 OnAllocated / OnRecycled。
    /// </summary>
    /// <remarks>
    /// 适合纯 C# 对象。它不负责 GameObject Instantiate/Destroy、线程同步或跨类型回收。
    /// 为避免父类型与真实类型进入不同池，回收必须显式使用对象的真实泛型类型。
    /// </remarks>
    public static class PoolKit
    {
        #region 静态泛型池

        private static class StaticPool<T> where T : new()
        {
            public static readonly FactoryObjectPool<T> Pool = new FactoryObjectPool<T>(
                factoryMethod: () => new T(),
                allocateMethod: item =>
                {
                    if (item is IPoolable poolable)
                    {
                        poolable.OnAllocated();
                    }
                },
                recycleMethod: item =>
                {
                    if (item is IPoolable poolable)
                    {
                        poolable.OnRecycled();
                    }
                },
                destroyMethod: null,
                maxCount: 500
            );
        }

        #endregion

        /// <summary>
        /// 从 T 对应的静态池取出实例；池为空时通过 new T() 创建。
        /// </summary>
        /// <typeparam name="T">具有无参构造函数的对象类型。</typeparam>
        public static T Allocate<T>() where T : new()
        {
            return StaticPool<T>.Pool.Allocate();
        }

        /// <summary>
        /// 将对象归还其真实类型对应的静态池。
        /// </summary>
        /// <typeparam name="T">必须与 obj.GetType() 完全一致。</typeparam>
        /// <param name="obj">待回收对象。</param>
        public static void Recycle<T>(T obj) where T : new()
        {
            if (obj == null)
            {
                PoolKitDiagnostics.LogError($"[PoolKit] Recycle<T> 失败: 试图回收空对象, DeclaredType={typeof(T).Name}");
                return;
            }

            Type realType = obj.GetType();
            if (realType != typeof(T))
            {
                PoolKitDiagnostics.LogError(
                    $"[PoolKit] Recycle<T> 失败: 禁止以父类型或错误声明类型回收对象, DeclaredType={typeof(T).Name}, RealType={realType.Name}\n" +
                    "请显式以真实类型调用 PoolKit.Recycle<真实类型>(obj)，避免运行时弱类型回收。");
                return;
            }

            StaticPool<T>.Pool.Recycle(obj);
        }

        /// <summary>
        /// 弱类型回收入口仅用于给出明确错误。
        /// PoolKit 不通过反射猜测真实泛型池，调用方必须使用强类型 Recycle&lt;T&gt;。
        /// </summary>
        /// <param name="obj">待回收对象；该重载不会真正回收。</param>
        public static void Recycle(object obj)
        {
            if (obj == null)
            {
                PoolKitDiagnostics.LogError("[PoolKit] Recycle(object) 失败: obj 为空");
                return;
            }

            PoolKitDiagnostics.LogError($"[PoolKit] Recycle(object) 已禁用: 禁止弱类型回收, RealType={obj.GetType().Name}\n" +
                            "请改为显式调用强类型接口 PoolKit.Recycle<真实类型>(obj)。");
        }
    }
}
