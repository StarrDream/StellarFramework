using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace StellarFramework.Event
{
    /// <summary>
    /// 全局枚举事件总线
    /// </summary>
    public static class GlobalEnumEvent
    {
        // 泛型类隔离存储：Table 和 Pool 都放在这里
        // 优势1：不同 Enum 类型拥有独立的存储空间，访问速度极快
        // 优势2：TokenPool 也是独立的，解决了之前单栈混存导致的类型不匹配问题
        private static class EventBox<T> where T : Enum
        {
            // 使用 Default 比较器避免 Enum 装箱
            public static readonly Dictionary<T, Delegate> EventTable = new Dictionary<T, Delegate>(EqualityComparer<T>.Default);

            // 独立的 Token 池
            public static readonly Stack<EnumEventToken<T>> TokenPool = new Stack<EnumEventToken<T>>();
        }

        /// <summary>
        /// 专用 Token，持有状态以避免闭包捕获
        /// </summary>
        private class EnumEventToken<T> : IUnRegister where T : Enum
        {
            public T Key;
            public Delegate Callback;
            public bool IsInUse;

            public void UnRegister()
            {
                if (!IsInUse) return;

                // 核心注销逻辑
                var table = EventBox<T>.EventTable;
                if (table.TryGetValue(Key, out var currentDel))
                {
                    currentDel = Delegate.Remove(currentDel, Callback);

                    if (currentDel == null)
                        table.Remove(Key);
                    else
                        table[Key] = currentDel;
                }

                // 回收自己
                Recycle(this);
            }

            public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
            {
                if (gameObject == null)
                {
                    UnRegister();
                    return this;
                }

                // 获取或添加辅助组件
                if (!gameObject.TryGetComponent<EventUnregisterTrigger>(out var trigger))
                {
                    trigger = gameObject.AddComponent<EventUnregisterTrigger>();
                    trigger.hideFlags = HideFlags.HideInInspector;
                }

                trigger.Add(this);
                return this;
            }
        }

        #region Pool Logic

        private static IUnRegister AllocateToken<T>(T key, Delegate callback) where T : Enum
        {
            EnumEventToken<T> token;
            var pool = EventBox<T>.TokenPool;

            // 因为 pool 是 EventBox<T> 的静态成员，所以取出来的绝对是 EnumEventToken<T>
            if (pool.Count > 0)
            {
                token = pool.Pop();
            }
            else
            {
                token = new EnumEventToken<T>();
            }

            token.Key = key;
            token.Callback = callback;
            token.IsInUse = true;
            return token;
        }

        private static void Recycle<T>(EnumEventToken<T> token) where T : Enum
        {
            token.Key = default;
            token.Callback = null;
            token.IsInUse = false;
            EventBox<T>.TokenPool.Push(token);
        }

        #endregion

        #region Register Logic

        private static IUnRegister OnRegister<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null) return new CustomUnRegister(null);

            var table = EventBox<T>.EventTable;
            if (!table.TryGetValue(key, out var d))
            {
                table[key] = callback;
            }
            else
            {
                table[key] = Delegate.Combine(d, callback);
            }

            return AllocateToken(key, callback);
        }

        #endregion

        #region Broadcast API (AggressiveInlining)

        // --- 0 Args ---
        public static IUnRegister Register<T>(T key, Action callback) where T : Enum => OnRegister(key, callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T>(T key) where T : Enum
        {
            if (EventBox<T>.EventTable.TryGetValue(key, out var d))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug 模式下类型检查
                try
                {
                    ((Action)d).Invoke();
                }
                catch (InvalidCastException ex)
                {
                    LogKit.LogError($"[EventKit] 键的类型不匹配 '{key}': {ex.Message}");
                }
#else
        // Release 模式下直接调用（性能优先）
        ((Action)d).Invoke();
#endif
            }
        }

        // --- 1 Arg ---
        public static IUnRegister Register<T, T1>(T key, Action<T1> callback) where T : Enum => OnRegister(key, callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1>(T key, T1 v1) where T : Enum
        {
            if (EventBox<T>.EventTable.TryGetValue(key, out var d))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug 模式下类型检查
                try
                {
                    ((Action<T1>)d).Invoke(v1);
                }
                catch (InvalidCastException ex)
                {
                    LogKit.LogError($"[EventKit] 键的类型不匹配 '{key}': {ex.Message}");
                }
#else
        // Release 模式下直接调用（性能优先）
        ((Action<T1>)d).Invoke(v1);
#endif
            }
        }

        // --- 2 Args ---
        public static IUnRegister Register<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum => OnRegister(key, callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2>(T key, T1 v1, T2 v2) where T : Enum
        {
            if (EventBox<T>.EventTable.TryGetValue(key, out var d))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug 模式下类型检查
                try
                {
                    ((Action<T1, T2>)d).Invoke(v1, v2);
                }
                catch (InvalidCastException ex)
                {
                    LogKit.LogError($"[EventKit] 键的类型不匹配 '{key}': {ex.Message}");
                }
#else
        // Release 模式下直接调用（性能优先）
       ((Action<T1, T2>)d).Invoke(v1, v2);
#endif
            }
        }

        // --- 3 Args ---
        public static IUnRegister Register<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum => OnRegister(key, callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2, T3>(T key, T1 v1, T2 v2, T3 v3) where T : Enum
        {
            if (EventBox<T>.EventTable.TryGetValue(key, out var d))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug 模式下类型检查
                try
                {
                    ((Action<T1, T2, T3>)d).Invoke(v1, v2, v3);
                }
                catch (InvalidCastException ex)
                {
                    LogKit.LogError($"[EventKit] 键的类型不匹配 '{key}': {ex.Message}");
                }
#else
        // Release 模式下直接调用（性能优先）
        ((Action<T1, T2, T3>)d).Invoke(v1, v2, v3);
#endif
            }
        }

        #endregion

        public static void Clear<T>() where T : Enum
        {
            EventBox<T>.EventTable.Clear();
            EventBox<T>.TokenPool.Clear();
        }
    }
}