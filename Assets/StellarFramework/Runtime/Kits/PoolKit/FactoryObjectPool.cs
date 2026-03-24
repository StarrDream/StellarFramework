using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 核心工厂对象池
    /// 作为框架底层唯一池结构，支持任意类型的对象缓存与生命周期委托。
    /// </summary>
    public class FactoryObjectPool<T>
    {
        private readonly Stack<T> _pool = new Stack<T>();
        private readonly Func<T> _factoryMethod;
        private readonly Action<T> _allocateMethod;
        private readonly Action<T> _recycleMethod;
        private readonly Action<T> _destroyMethod;
        private readonly int _maxCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 仅在开发环境记录池内对象，拦截双重回收，防止脏状态扩散。
        /// </summary>
        private readonly HashSet<T> _checkSet = new HashSet<T>();
#endif

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="factoryMethod">创建新对象的逻辑</param>
        /// <param name="allocateMethod">出池时的激活逻辑</param>
        /// <param name="recycleMethod">入池时的重置逻辑</param>
        /// <param name="destroyMethod">销毁对象的逻辑（池满或清空时调用）</param>
        /// <param name="maxCount">最大缓存数量</param>
        public FactoryObjectPool(
            Func<T> factoryMethod,
            Action<T> allocateMethod = null,
            Action<T> recycleMethod = null,
            Action<T> destroyMethod = null,
            int maxCount = 50)
        {
            if (factoryMethod == null)
            {
                Debug.LogError($"[FactoryObjectPool] 初始化失败: factoryMethod 委托不能为空，当前泛型类型: {typeof(T).Name}");
                return;
            }

            _factoryMethod = factoryMethod;
            _allocateMethod = allocateMethod;
            _recycleMethod = recycleMethod;
            _destroyMethod = destroyMethod;
            _maxCount = maxCount;
        }

        public T Allocate()
        {
            if (_factoryMethod == null)
            {
                Debug.LogError($"[FactoryObjectPool] Allocate 失败: factoryMethod 为空，无法创建对象，当前泛型类型: {typeof(T).Name}");
                return default;
            }

            T item = _pool.Count > 0 ? _pool.Pop() : _factoryMethod.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _checkSet.Remove(item);
#endif
            _allocateMethod?.Invoke(item);

            return item;
        }

        public bool Recycle(T item)
        {
            if (item == null)
            {
                Debug.LogError($"[FactoryObjectPool] Recycle 失败: 试图回收空对象，当前泛型类型: {typeof(T).Name}");
                return false;
            }

            if (_pool.Count >= _maxCount)
            {
                _destroyMethod?.Invoke(item);
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_checkSet.Add(item))
            {
                Debug.LogError($"[FactoryObjectPool] 严重错误: 试图回收已经在池中的对象，触发对象类型: {typeof(T).Name}");
                return false;
            }
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