// ==================================================================================
// PoolKit - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：全局对象池门面管理器。
// 改造说明：
// 1. 将类型不匹配和空对象回收的错误升级为断言拦截。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Pool
{
    public static class PoolKit
    {
        #region 核心优化：静态泛型池 (O(1) Fast Path)

        private static class StaticPool<T> where T : new()
        {
            public static readonly FactoryObjectPool<T> Pool = new FactoryObjectPool<T>(
                factoryMethod: () => new T(),
                allocateMethod: item =>
                {
                    if (item is IPoolable poolable) poolable.OnAllocated();
                },
                recycleMethod: item =>
                {
                    if (item is IPoolable poolable) poolable.OnRecycled();
                },
                destroyMethod: null,
                maxCount: 500
            );
        }

        #endregion

        #region 降级路由：动态类型池包装器 (Fallback Path)

        private interface IPoolWrapper
        {
            Type ItemType { get; }
            bool RecycleObject(object obj);
        }

        private sealed class PoolWrapper<T> : IPoolWrapper where T : new()
        {
            public Type ItemType => typeof(T);

            public bool RecycleObject(object obj)
            {
                if (!(obj is T typedObj))
                {
                    LogKit.LogError(
                        $"[PoolKit] RecycleObject 失败: 对象类型不匹配，池类型={typeof(T).Name}，传入对象类型={obj?.GetType().Name ?? "null"}");
                    return false;
                }

                return StaticPool<T>.Pool.Recycle(typedObj);
            }
        }

        private static readonly Dictionary<Type, IPoolWrapper> _dynamicPools = new Dictionary<Type, IPoolWrapper>();

        #endregion

        #region 公开 API

        public static T Allocate<T>() where T : new()
        {
            return StaticPool<T>.Pool.Allocate();
        }

        public static void Recycle<T>(T obj) where T : new()
        {
            LogKit.AssertNotNull(obj, $"[PoolKit] Recycle<T> 失败: 试图回收空对象，声明类型={typeof(T).Name}");
            if (obj == null) return;

            Type realType = obj.GetType();

            if (realType == typeof(T))
            {
                StaticPool<T>.Pool.Recycle(obj);
                return;
            }

            RecycleDynamic(obj, realType);
        }

        public static void Recycle(object obj)
        {
            LogKit.AssertNotNull(obj, "[PoolKit] Recycle(object) 失败: 试图回收空对象");
            if (obj == null) return;

            RecycleDynamic(obj, obj.GetType());
        }

        #endregion

        #region 内部动态路由逻辑

        private static void RecycleDynamic(object obj, Type realType)
        {
            if (!_dynamicPools.TryGetValue(realType, out IPoolWrapper wrapper))
            {
                if (!TryCreateWrapper(realType, out wrapper))
                {
                    LogKit.LogError($"[PoolKit] RecycleDynamic 失败: 无法为对象创建池包装器，真实类型={realType.Name}");
                    return;
                }
            }

            LogKit.Assert(wrapper.RecycleObject(obj), $"[PoolKit] RecycleDynamic 失败: 回收被拒绝，真实类型={realType.Name}");
        }

        private static bool TryCreateWrapper(Type type, out IPoolWrapper wrapper)
        {
            wrapper = null;

            if (type.IsAbstract || type.IsInterface)
            {
                LogKit.LogError($"[PoolKit] TryCreateWrapper 失败: 抽象类或接口不能创建对象池，Type={type.Name}");
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                LogKit.LogError($"[PoolKit] TryCreateWrapper 失败: 类型缺少公共无参构造，Type={type.Name}");
                return false;
            }

            Type wrapperType = typeof(PoolWrapper<>).MakeGenericType(type);
            object wrapperObj = Activator.CreateInstance(wrapperType);

            if (!(wrapperObj is IPoolWrapper createdWrapper))
            {
                LogKit.LogError(
                    $"[PoolKit] TryCreateWrapper 失败: 包装器实例化异常，Type={type.Name}，WrapperType={wrapperType.Name}");
                return false;
            }

            _dynamicPools[type] = createdWrapper;
            wrapper = createdWrapper;
            return true;
        }

        #endregion
    }
}