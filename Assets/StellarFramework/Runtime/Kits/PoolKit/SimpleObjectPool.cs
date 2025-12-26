using System.Collections.Generic;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 简易泛型对象池
    /// 使用 Stack 实现，遵循 LIFO (后进先出) 原则，对 CPU 缓存友好
    /// </summary>
    /// <typeparam name="T">对象类型，必须有无参构造函数</typeparam>
    public class SimpleObjectPool<T> where T : new()
    {
        // 核心存储容器
        private readonly Stack<T> _pool = new Stack<T>();

        // 最大容量限制，防止无限增长导致内存浪费
        private int _maxCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //  仅在开发环境启用 HashSet 检查，防止 Release 包性能损耗
        // 用于检测双重回收 (Double Free)
        private readonly HashSet<T> _checkSet = new HashSet<T>();
#endif

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxCount">池子最大容量，默认 50</param>
        public SimpleObjectPool(int maxCount = 50)
        {
            _maxCount = maxCount;
        }

        /// <summary>
        /// 申请对象
        /// </summary>
        public T Allocate()
        {
            // 如果池里有，弹出一个；如果池里空了，new 一个新的
            T item = _pool.Count > 0 ? _pool.Pop() : new T();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _checkSet.Remove(item);
#endif

            // 如果对象实现了 IPoolable，调用初始化方法
            if (item is IPoolable poolable)
            {
                poolable.OnAllocated();
            }

            return item;
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        public bool Recycle(T item)
        {
            // 空对象或池子已满，拒绝回收
            if (item == null || _pool.Count >= _maxCount)
            {
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //  双重回收检测
            if (_checkSet.Contains(item))
            {
                LogKit.LogError($"[PoolKit] 严重错误：试图回收已经在池中的对象！Type: {typeof(T).Name}");
                return false;
            }

            _checkSet.Add(item);
#endif

            // 如果对象实现了 IPoolable，调用重置方法
            if (item is IPoolable poolable)
            {
                poolable.OnRecycled();
            }

            _pool.Push(item);
            return true;
        }
    }
}