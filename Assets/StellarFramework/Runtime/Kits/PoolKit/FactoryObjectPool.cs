// ==================================================================================
// FactoryObjectPool
// ----------------------------------------------------------------------------------
// 核心工厂对象池。公共契约见类型与方法 XML 注释。
// 运行时保持轻量；开发构建额外执行双重回收与空引用诊断。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 由调用方提供创建、取出、回收和销毁策略的通用对象池。
    /// </summary>
    /// <remarks>
    /// 池内部使用 LIFO 栈，适合频繁 Allocate / Recycle 的短生命周期对象。
    /// 本类型不做线程同步，调用方必须保证池操作位于同一线程或自行串行化。
    /// Editor / Development Build 会额外维护集合用于检测双重回收；Release 不承担该检查开销。
    /// </remarks>
    /// <typeparam name="T">池中对象类型。</typeparam>
    public class FactoryObjectPool<T>
    {
        private readonly Stack<T> _pool = new Stack<T>();
        private readonly Func<T> _factoryMethod;
        private readonly Action<T> _allocateMethod;
        private readonly Action<T> _recycleMethod;
        private readonly Action<T> _destroyMethod;
        private readonly int _maxCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<T> _checkSet = new HashSet<T>();
#endif

        /// <summary>
        /// 创建对象池。
        /// </summary>
        /// <param name="factoryMethod">池为空时创建新对象的工厂；不能为空。</param>
        /// <param name="allocateMethod">对象被取出后执行，可用于恢复激活状态或重置临时字段。</param>
        /// <param name="recycleMethod">对象进入池前执行，可用于解除外部引用或恢复默认状态。</param>
        /// <param name="destroyMethod">池已满或 Clear 时执行，用于真正释放对象持有的资源。</param>
        /// <param name="maxCount">池最多保留的空闲对象数量。</param>
        public FactoryObjectPool(
            Func<T> factoryMethod,
            Action<T> allocateMethod = null,
            Action<T> recycleMethod = null,
            Action<T> destroyMethod = null,
            int maxCount = 50)
        {
            PoolKitDiagnostics.AssertNotNull(factoryMethod,
                $"[FactoryObjectPool] 初始化失败: factoryMethod 不能为空，泛型类型: {typeof(T).Name}");

            _factoryMethod = factoryMethod;
            _allocateMethod = allocateMethod;
            _recycleMethod = recycleMethod;
            _destroyMethod = destroyMethod;
            _maxCount = maxCount;
        }

        /// <summary>
        /// 从池中取出一个对象；池为空时调用工厂创建。
        /// </summary>
        /// <returns>可用对象。只有工厂自身返回 null/default 时才可能得到空值。</returns>
        public T Allocate()
        {
            if (_factoryMethod == null) return default;

            T item = _pool.Count > 0 ? _pool.Pop() : _factoryMethod.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _checkSet.Remove(item);
#endif
            _allocateMethod?.Invoke(item);
            return item;
        }

        /// <summary>
        /// 将对象归还池。
        /// </summary>
        /// <param name="item">待回收对象，不能为 null。</param>
        /// <returns>
        /// true 表示对象已进入池；false 表示对象未缓存，例如池已达到上限并执行了 destroyMethod。
        /// </returns>
        /// <remarks>
        /// 同一对象只能回收一次；重复回收在 Editor / Development Build 中会触发 Fail-Fast 诊断。
        /// </remarks>
        public bool Recycle(T item)
        {
            PoolKitDiagnostics.AssertNotNull(item, $"[FactoryObjectPool] Recycle 失败: 试图回收空对象，泛型类型: {typeof(T).Name}");
            if (item == null) return false;

            if (_pool.Count >= _maxCount)
            {
                _destroyMethod?.Invoke(item);
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Fail-Fast: 严格拦截双重回收。双重回收会导致同一个对象在池中存在两份，
            // 下次 Allocate 时会被分配给两个不同的系统，引发极其严重的逻辑串线。
            PoolKitDiagnostics.Assert(_checkSet.Add(item),
                $"[FactoryObjectPool] 致命错误: 试图回收已经在池中的对象 (双重回收)，触发对象类型: {typeof(T).Name}");
#endif

            _recycleMethod?.Invoke(item);
            _pool.Push(item);
            return true;
        }

        /// <summary>
        /// 清空当前缓存，并对每个池内对象调用 destroyMethod。
        /// 已经 Allocate 到外部的对象不受影响。
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Pop();
                _destroyMethod?.Invoke(item);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _checkSet.Clear();
#endif
        }
    }
}
