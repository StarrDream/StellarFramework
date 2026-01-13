using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace StellarFramework.Event
{
    /// <summary>
    /// 全局枚举事件总线
    /// 特性：泛型隔离、对象池、反向查找、签名校验、零GC(运行时)
    /// </summary>
    public static class GlobalEnumEvent
    {
        #region Internal Types

        // 组合 Key，用于在 LookupTable 中精确定位
        private readonly struct CallbackKey<T> : IEquatable<CallbackKey<T>> where T : Enum
        {
            public readonly T Key;
            public readonly Delegate Callback;

            public CallbackKey(T key, Delegate callback)
            {
                Key = key;
                Callback = callback;
            }

            public bool Equals(CallbackKey<T> other)
            {
                return EqualityComparer<T>.Default.Equals(Key, other.Key)
                       && EqualityComparer<Delegate>.Default.Equals(Callback, other.Callback);
            }

            public override bool Equals(object obj) => obj is CallbackKey<T> other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h1 = EqualityComparer<T>.Default.GetHashCode(Key);
                    var h2 = Callback != null ? EqualityComparer<Delegate>.Default.GetHashCode(Callback) : 0;
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static class EventBox<T> where T : Enum
        {
            // 核心委托表
            public static readonly Dictionary<T, Delegate> EventTable =
                new Dictionary<T, Delegate>(EqualityComparer<T>.Default);

            // Token 对象池
            public static readonly Stack<EnumEventToken<T>> TokenPool = new Stack<EnumEventToken<T>>();

            // 反向查找表：支持同 Key 同 Callback 的重复注册 (使用 List 存储)
            public static readonly Dictionary<CallbackKey<T>, List<EnumEventToken<T>>> LookupTable =
                new Dictionary<CallbackKey<T>, List<EnumEventToken<T>>>();

            // 签名校验表：防止 Release 模式下因签名不匹配导致的强转崩溃
            public static readonly Dictionary<T, Type> DelegateTypeByKey =
                new Dictionary<T, Type>(EqualityComparer<T>.Default);
        }

        private sealed class EnumEventToken<T> : IUnRegister where T : Enum
        {
            public T Key;
            public Delegate Callback;
            public bool IsInUse;

            public void UnRegister()
            {
                if (!IsInUse) return;

                // 移除委托并回收 Token
                RemoveFromEventTable(Key, Callback);
                Recycle(this);
            }

            public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
            {
                if (gameObject == null)
                {
                    UnRegister();
                    return this;
                }

                if (!gameObject.TryGetComponent<EventUnregisterTrigger>(out var trigger))
                {
                    trigger = gameObject.AddComponent<EventUnregisterTrigger>();
                    trigger.hideFlags = HideFlags.HideInInspector;
                }

                trigger.Add(this);
                return this;
            }
        }

        #endregion

        #region Core Logic

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EnsureDelegateTypeMatches<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null) return false;

            var box = EventBox<T>.DelegateTypeByKey;
            var cbType = callback.GetType();

            if (!box.TryGetValue(key, out var existedType))
            {
                box[key] = cbType;
                return true;
            }

            if (existedType == cbType) return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GlobalEnumEvent] 注册失败: Key '{key}' 的委托签名不匹配。期望: '{existedType}', 实际: '{cbType}'。请检查参数类型。");
#endif
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddToLookup<T>(EnumEventToken<T> token) where T : Enum
        {
            var lookup = EventBox<T>.LookupTable;
            var ck = new CallbackKey<T>(token.Key, token.Callback);

            if (!lookup.TryGetValue(ck, out var list))
            {
                list = new List<EnumEventToken<T>>(1);
                lookup.Add(ck, list);
            }

            list.Add(token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveFromLookup<T>(EnumEventToken<T> token) where T : Enum
        {
            if (token.Callback == null) return;

            var lookup = EventBox<T>.LookupTable;
            var ck = new CallbackKey<T>(token.Key, token.Callback);

            if (!lookup.TryGetValue(ck, out var list)) return;

            // 倒序遍历，按引用移除
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(list[i], token))
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            if (list.Count == 0)
                lookup.Remove(ck);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddToEventTable<T>(T key, Delegate callback) where T : Enum
        {
            var table = EventBox<T>.EventTable;
            if (!table.TryGetValue(key, out var d))
                table[key] = callback;
            else
                table[key] = Delegate.Combine(d, callback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveFromEventTable<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null) return;

            var table = EventBox<T>.EventTable;
            if (!table.TryGetValue(key, out var currentDel)) return;

            currentDel = Delegate.Remove(currentDel, callback);

            if (currentDel == null)
            {
                table.Remove(key);
                EventBox<T>.DelegateTypeByKey.Remove(key);
            }
            else
            {
                table[key] = currentDel;
            }
        }

        private static IUnRegister AllocateToken<T>(T key, Delegate callback) where T : Enum
        {
            var pool = EventBox<T>.TokenPool;
            var token = pool.Count > 0 ? pool.Pop() : new EnumEventToken<T>();

            token.Key = key;
            token.Callback = callback;
            token.IsInUse = true;

            AddToLookup(token);
            return token;
        }

        private static void Recycle<T>(EnumEventToken<T> token) where T : Enum
        {
            RemoveFromLookup(token); // 清理反向表
            token.Key = default;
            token.Callback = null;
            token.IsInUse = false;
            EventBox<T>.TokenPool.Push(token);
        }

        #endregion

        #region Public API: Register

        private static IUnRegister OnRegister<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null) return new CustomUnRegister(null);
            if (!EnsureDelegateTypeMatches(key, callback)) return new CustomUnRegister(null);

            AddToEventTable(key, callback);
            return AllocateToken(key, callback);
        }

        public static IUnRegister Register<T>(T key, Action callback) where T : Enum => OnRegister(key, callback);
        public static IUnRegister Register<T, T1>(T key, Action<T1> callback) where T : Enum => OnRegister(key, callback);
        public static IUnRegister Register<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum => OnRegister(key, callback);
        public static IUnRegister Register<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum => OnRegister(key, callback);

        #endregion

        #region Public API: UnRegister (Manual)

        private static void OnUnRegister<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null) return;

            var ck = new CallbackKey<T>(key, callback);
            var lookup = EventBox<T>.LookupTable;

            // 优先尝试通过 Token 进行完整注销（包含回收逻辑）
            if (lookup.TryGetValue(ck, out var list) && list.Count > 0)
            {
                var token = list[list.Count - 1]; // 取出最后一个注册的 Token
                token.UnRegister();
                return;
            }

            // 保底逻辑：如果找不到 Token，强制移除委托，防止逻辑残留
            RemoveFromEventTable(key, callback);
        }

        public static void UnRegister<T>(T key, Action callback) where T : Enum => OnUnRegister(key, callback);
        public static void UnRegister<T, T1>(T key, Action<T1> callback) where T : Enum => OnUnRegister(key, callback);
        public static void UnRegister<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum => OnUnRegister(key, callback);
        public static void UnRegister<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum => OnUnRegister(key, callback);

        #endregion

        #region Public API: Broadcast

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T>(T key) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                ((Action)d).Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Event] '{key}': {ex}");
            }
#else
            ((Action)d).Invoke();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1>(T key, T1 v1) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                ((Action<T1>)d).Invoke(v1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Event] '{key}': {ex}");
            }
#else
            ((Action<T1>)d).Invoke(v1);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2>(T key, T1 v1, T2 v2) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                ((Action<T1, T2>)d).Invoke(v1, v2);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Event] '{key}': {ex}");
            }
#else
            ((Action<T1, T2>)d).Invoke(v1, v2);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2, T3>(T key, T1 v1, T2 v2, T3 v3) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                ((Action<T1, T2, T3>)d).Invoke(v1, v2, v3);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Event] '{key}': {ex}");
            }
#else
            ((Action<T1, T2, T3>)d).Invoke(v1, v2, v3);
#endif
        }

        #endregion

        /// <summary>
        /// 彻底清理某类型的所有事件数据（慎用）
        /// </summary>
        public static void ClearAll<T>() where T : Enum
        {
            EventBox<T>.EventTable.Clear();
            EventBox<T>.TokenPool.Clear();
            EventBox<T>.LookupTable.Clear();
            EventBox<T>.DelegateTypeByKey.Clear();
        }
    }
}