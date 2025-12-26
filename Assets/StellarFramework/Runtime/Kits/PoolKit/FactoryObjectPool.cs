using System;
using System.Collections.Generic;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 工厂对象池 
    /// 池满时自动销毁对象，防止内存泄漏
    /// 先判断容量，再执行重置，避免无效计算
    /// </summary>
    public class FactoryObjectPool<T>
    {
        private readonly Stack<T> _pool = new Stack<T>();
        private readonly Func<T> _factoryMethod;
        private readonly Action<T> _resetMethod;
        private readonly Action<T> _destroyMethod;
        private int _maxCount;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="factoryMethod">创建新对象的逻辑</param>
        /// <param name="resetMethod">回收对象的重置逻辑（仅在成功回收时调用）</param>
        /// <param name="destroyMethod">销毁对象的逻辑（池满时调用，防止内存泄漏）</param>
        /// <param name="maxCount">最大缓存数量</param>
        public FactoryObjectPool(
            Func<T> factoryMethod,
            Action<T> resetMethod = null,
            Action<T> destroyMethod = null,
            int maxCount = 50)
        {
            _factoryMethod = factoryMethod;
            _resetMethod = resetMethod;
            _destroyMethod = destroyMethod;
            _maxCount = maxCount;
        }

        public T Allocate()
        {
            return _pool.Count > 0 ? _pool.Pop() : _factoryMethod.Invoke();
        }

        /// <summary>
        /// 回收对象
        /// 性能优化：先判断容量，避免无效的重置操作
        /// </summary>
        public bool Recycle(T item)
        {
            if (item == null) return false;

            // 性能优化：先判断池子是否已满
            if (_pool.Count >= _maxCount)
            {
                // 池满：直接销毁，不执行 Reset（节省 CPU）
                _destroyMethod?.Invoke(item);
                return false;
            }

            // 池未满：执行重置后入池
            _resetMethod?.Invoke(item);
            _pool.Push(item);
            return true;
        }

        /// <summary>
        /// 清空对象池（可选：在切场景时调用）
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Pop();
                _destroyMethod?.Invoke(item);
            }
        }
    }
}