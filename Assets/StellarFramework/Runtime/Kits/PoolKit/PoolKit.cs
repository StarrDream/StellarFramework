using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 全局对象池门面管理器
    /// 向上层提供极简 API，内部利用 FactoryObjectPool 统一管理纯 C# 对象的生命周期。
    /// </summary>
    public static class PoolKit
    {
        #region 核心优化：静态泛型池 (O(1) Fast Path)

        /// <summary>
        /// 利用 C# 泛型静态类特性，为每个具体的 T 在内存中生成独立的 FactoryObjectPool。
        /// 彻底消除泛型调用时的 Dictionary 查找、装箱/拆箱与反射开销。
        /// </summary>
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
                destroyMethod: null, // 纯 C# 对象交由 GC 处理，无需显式销毁
                maxCount: 500 // 纯 C# 对象占用极小，默认容量可适当调大
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
                    Debug.LogError(
                        $"[PoolKit] RecycleObject 失败: 对象类型不匹配，池类型={typeof(T).Name}，传入对象类型={obj?.GetType().Name ?? "null"}");
                    return false;
                }

                return StaticPool<T>.Pool.Recycle(typedObj);
            }
        }

        /// <summary>
        /// 字典缓存：仅用于非泛型入口或多态回收时的降级路由
        /// </summary>
        private static readonly Dictionary<Type, IPoolWrapper> _dynamicPools = new Dictionary<Type, IPoolWrapper>();

        #endregion

        #region 公开 API

        /// <summary>
        /// 申请泛型对象。
        /// 直接命中静态泛型池，零字典查询，零装箱，零反射。
        /// </summary>
        public static T Allocate<T>() where T : new()
        {
            return StaticPool<T>.Pool.Allocate();
        }

        /// <summary>
        /// 回收泛型对象。
        /// 优先走 O(1) 静态池，遇到多态基类引用时自动降级到动态路由。
        /// </summary>
        public static void Recycle<T>(T obj) where T : new()
        {
            if (obj == null)
            {
                Debug.LogError($"[PoolKit] Recycle<T> 失败: 试图回收空对象，声明类型={typeof(T).Name}");
                return;
            }

            Type realType = obj.GetType();

            // 性能优化：如果真实类型与声明类型完全一致（99%的情况），直接走 O(1) 静态池
            if (realType == typeof(T))
            {
                StaticPool<T>.Pool.Recycle(obj);
                return;
            }

            // 多态降级：声明类型是基类/接口，真实类型是子类，必须通过动态分发回收到子类池
            RecycleDynamic(obj, realType);
        }

        /// <summary>
        /// 非泛型回收入口。
        /// 专门解决接口引用或基类引用丢失具体泛型类型的问题，确保对象按真实类型回收到正确池。
        /// </summary>
        public static void Recycle(object obj)
        {
            if (obj == null)
            {
                Debug.LogError("[PoolKit] Recycle(object) 失败: 试图回收空对象");
                return;
            }

            RecycleDynamic(obj, obj.GetType());
        }

        #endregion

        #region 内部动态路由逻辑

        /// <summary>
        /// 处理真实类型与泛型声明不一致时的动态回收路由
        /// </summary>
        private static void RecycleDynamic(object obj, Type realType)
        {
            if (!_dynamicPools.TryGetValue(realType, out IPoolWrapper wrapper))
            {
                if (!TryCreateWrapper(realType, out wrapper))
                {
                    Debug.LogError($"[PoolKit] RecycleDynamic 失败: 无法为对象创建池包装器，真实类型={realType.Name}");
                    return;
                }
            }

            if (!wrapper.RecycleObject(obj))
            {
                Debug.LogError($"[PoolKit] RecycleDynamic 失败: 回收被拒绝，真实类型={realType.Name}");
            }
        }

        /// <summary>
        /// 通过运行时真实类型动态创建对应的泛型池包装器。
        /// </summary>
        private static bool TryCreateWrapper(Type type, out IPoolWrapper wrapper)
        {
            wrapper = null;

            if (type.IsAbstract || type.IsInterface)
            {
                Debug.LogError($"[PoolKit] TryCreateWrapper 失败: 抽象类或接口不能创建对象池，Type={type.Name}");
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogError($"[PoolKit] TryCreateWrapper 失败: 类型缺少公共无参构造，Type={type.Name}");
                return false;
            }

            Type wrapperType = typeof(PoolWrapper<>).MakeGenericType(type);
            object wrapperObj = Activator.CreateInstance(wrapperType);

            if (!(wrapperObj is IPoolWrapper createdWrapper))
            {
                Debug.LogError(
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