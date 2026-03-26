// ==================================================================================
// FactoryObjectPool - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：核心工厂对象池。
// 改造说明：
// 1. 将双重回收（Double Recycle）的检测升级为 LogKit.Assert，在开发期强制阻断。
// 2. 增加 Allocate 和 Recycle 时的空引用断言。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Pool
{
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

        public FactoryObjectPool(
            Func<T> factoryMethod,
            Action<T> allocateMethod = null,
            Action<T> recycleMethod = null,
            Action<T> destroyMethod = null,
            int maxCount = 50)
        {
            LogKit.AssertNotNull(factoryMethod,
                $"[FactoryObjectPool] 初始化失败: factoryMethod 不能为空，泛型类型: {typeof(T).Name}");

            _factoryMethod = factoryMethod;
            _allocateMethod = allocateMethod;
            _recycleMethod = recycleMethod;
            _destroyMethod = destroyMethod;
            _maxCount = maxCount;
        }

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

        public bool Recycle(T item)
        {
            LogKit.AssertNotNull(item, $"[FactoryObjectPool] Recycle 失败: 试图回收空对象，泛型类型: {typeof(T).Name}");
            if (item == null) return false;

            if (_pool.Count >= _maxCount)
            {
                _destroyMethod?.Invoke(item);
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Fail-Fast: 严格拦截双重回收。双重回收会导致同一个对象在池中存在两份，
            // 下次 Allocate 时会被分配给两个不同的系统，引发极其严重的逻辑串线。
            LogKit.Assert(_checkSet.Add(item),
                $"[FactoryObjectPool] 致命错误: 试图回收已经在池中的对象 (双重回收)，触发对象类型: {typeof(T).Name}");
#endif

            _recycleMethod?.Invoke(item);
            _pool.Push(item);
            return true;
        }

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