using System;
using StellarFramework.Pool;

namespace StellarFramework.Pool
{
    public static class PoolKit
    {
        #region 核心优化：静态泛型池

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

        #region 公开 API

        public static T Allocate<T>() where T : new()
        {
            return StaticPool<T>.Pool.Allocate();
        }

        public static void Recycle<T>(T obj) where T : new()
        {
            if (obj == null)
            {
                LogKit.LogError($"[PoolKit] Recycle<T> 失败: 试图回收空对象, DeclaredType={typeof(T).Name}");
                return;
            }

            Type realType = obj.GetType();
            if (realType != typeof(T))
            {
                LogKit.LogError(
                    $"[PoolKit] Recycle<T> 失败: 禁止以父类型或错误声明类型回收对象, DeclaredType={typeof(T).Name}, RealType={realType.Name}\n" +
                    "请显式以真实类型调用 PoolKit.Recycle<真实类型>(obj)，避免运行时弱类型回收。");
                return;
            }

            StaticPool<T>.Pool.Recycle(obj);
        }

        /// <summary>
        /// 我主动封死弱类型 object 回收入口。
        /// 运行时对象池主链路禁止再依赖反射式动态包装器。
        /// </summary>
        public static void Recycle(object obj)
        {
            if (obj == null)
            {
                LogKit.LogError("[PoolKit] Recycle(object) 失败: obj 为空");
                return;
            }

            LogKit.LogError($"[PoolKit] Recycle(object) 已禁用: 禁止弱类型回收, RealType={obj.GetType().Name}\n" +
                            "请改为显式调用强类型接口 PoolKit.Recycle<真实类型>(obj)。");
        }

        #endregion
    }
}