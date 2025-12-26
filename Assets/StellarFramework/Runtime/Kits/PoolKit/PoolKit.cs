using System;
using System.Collections.Generic;

namespace StellarFramework.Pool
{
    /// <summary>
    /// 全局对象池管理器 (Facade)
    /// 提供静态方法方便全局访问，内部管理不同类型的对象池
    /// </summary>
    public static class PoolKit
    {
        // 字典缓存：Key=对象类型, Value=对应的对象池实例
        private static readonly Dictionary<Type, object> _pools = new Dictionary<Type, object>();

        /// <summary>
        /// 从对象池中获取一个对象
        /// </summary>
        public static T Allocate<T>() where T : new()
        {
            var type = typeof(T);

            // 如果还没有这个类型的池子，创建一个
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new SimpleObjectPool<T>();
                _pools.Add(type, pool);
            }

            return ((SimpleObjectPool<T>)pool).Allocate();
        }

        /// <summary>
        /// 将对象回收到对象池
        /// </summary>
        public static void Recycle<T>(T obj) where T : new()
        {
            var type = typeof(T);
            if (_pools.TryGetValue(type, out var pool))
            {
                ((SimpleObjectPool<T>)pool).Recycle(obj);
            }
        }
    }
}