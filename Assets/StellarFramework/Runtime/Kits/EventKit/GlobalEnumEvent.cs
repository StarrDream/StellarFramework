// ==================================================================================
// GlobalEnumEvent - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：全局枚举事件总线。
// 改造说明：
// 1. 彻底移除 Broadcast 内部的 try-catch，贯彻 Fail-Fast 原则。
// 2. 增加 Register 时的空引用断言。
// ==================================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace StellarFramework.Event
{
    public static class GlobalEnumEvent
    {
        #region Internal Types

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
            public static readonly Dictionary<T, Delegate> EventTable =
                new Dictionary<T, Delegate>(EqualityComparer<T>.Default);

            public static readonly Stack<EnumEventToken<T>> TokenPool = new Stack<EnumEventToken<T>>();

            public static readonly Dictionary<CallbackKey<T>, List<EnumEventToken<T>>> LookupTable =
                new Dictionary<CallbackKey<T>, List<EnumEventToken<T>>>();

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

            LogKit.LogError($"[GlobalEnumEvent] 注册失败: Key '{key}' 的委托签名不匹配。期望: '{existedType}', 实际: '{cbType}'。");
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

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(list[i], token))
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            if (list.Count == 0) lookup.Remove(ck);
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
            RemoveFromLookup(token);
            token.Key = default;
            token.Callback = null;
            token.IsInUse = false;
            EventBox<T>.TokenPool.Push(token);
        }

        #endregion

        #region Public API: Register

        private static IUnRegister OnRegister<T>(T key, Delegate callback) where T : Enum
        {
            LogKit.AssertNotNull(callback, $"[GlobalEnumEvent] 注册失败: 传入的回调委托为空, Key: {key}");
            if (callback == null) return new CustomUnRegister(null);
            if (!EnsureDelegateTypeMatches(key, callback)) return new CustomUnRegister(null);

            AddToEventTable(key, callback);
            return AllocateToken(key, callback);
        }

        public static IUnRegister Register<T>(T key, Action callback) where T : Enum => OnRegister(key, callback);

        public static IUnRegister Register<T, T1>(T key, Action<T1> callback) where T : Enum =>
            OnRegister(key, callback);

        public static IUnRegister Register<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum =>
            OnRegister(key, callback);

        public static IUnRegister Register<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum =>
            OnRegister(key, callback);

        #endregion

        #region Public API: UnRegister (Manual)

        private static void OnUnRegister<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null) return;

            var ck = new CallbackKey<T>(key, callback);
            var lookup = EventBox<T>.LookupTable;

            if (lookup.TryGetValue(ck, out var list) && list.Count > 0)
            {
                var token = list[list.Count - 1];
                token.UnRegister();
                return;
            }

            RemoveFromEventTable(key, callback);
        }

        public static void UnRegister<T>(T key, Action callback) where T : Enum => OnUnRegister(key, callback);
        public static void UnRegister<T, T1>(T key, Action<T1> callback) where T : Enum => OnUnRegister(key, callback);

        public static void UnRegister<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum =>
            OnUnRegister(key, callback);

        public static void UnRegister<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum =>
            OnUnRegister(key, callback);

        #endregion

        #region Public API: Broadcast

        // 核心改造：移除 try-catch。如果业务层在事件回调中抛出异常，必须让其暴露，
        // 否则会导致后续的回调被静默跳过，产生难以排查的脏状态。

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T>(T key) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
            ((Action)d).Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1>(T key, T1 v1) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
            ((Action<T1>)d).Invoke(v1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2>(T key, T1 v1, T2 v2) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
            ((Action<T1, T2>)d).Invoke(v1, v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2, T3>(T key, T1 v1, T2 v2, T3 v3) where T : Enum
        {
            if (!EventBox<T>.EventTable.TryGetValue(key, out var d)) return;
            ((Action<T1, T2, T3>)d).Invoke(v1, v2, v3);
        }

        #endregion

        public static void ClearAll<T>() where T : Enum
        {
            EventBox<T>.EventTable.Clear();
            EventBox<T>.TokenPool.Clear();
            EventBox<T>.LookupTable.Clear();
            EventBox<T>.DelegateTypeByKey.Clear();
        }
    }
}