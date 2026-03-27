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

            public override bool Equals(object obj)
            {
                return obj is CallbackKey<T> other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h1 = EqualityComparer<T>.Default.GetHashCode(Key);
                    int h2 = Callback != null ? EqualityComparer<Delegate>.Default.GetHashCode(Callback) : 0;
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
            public bool IsRegistered;

            public void UnRegister()
            {
                if (!IsInUse)
                {
                    return;
                }

                if (IsRegistered)
                {
                    RemoveFromEventTable(Key, Callback);
                }

                Recycle(this);
            }

            public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
            {
                if (gameObject == null)
                {
                    LogKit.LogError($"[GlobalEnumEvent] 生命周期绑定失败: gameObject 为空, EventKey={Key}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
                {
                    LogKit.LogError(
                        $"[GlobalEnumEvent] 生命周期绑定失败: 无法挂载销毁触发器, EventKey={Key}, TriggerObject={gameObject.name}");
                    UnRegister();
                    return this;
                }

                trigger.Add(this);
                return this;
            }

            public IUnRegister UnRegisterWhenGameObjectDestroyed(MonoBehaviour mono)
            {
                if (mono == null)
                {
                    LogKit.LogError($"[GlobalEnumEvent] 生命周期绑定失败: mono 为空, EventKey={Key}");
                    UnRegister();
                    return this;
                }

                return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
            }

            public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
            {
                if (mono == null || mono.gameObject == null)
                {
                    LogKit.LogError($"[GlobalEnumEvent] 生命周期绑定失败: mono 或 gameObject 为空, EventKey={Key}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDisableTrigger(mono.gameObject,
                        out EventUnregisterOnDisableTrigger trigger))
                {
                    LogKit.LogError(
                        $"[GlobalEnumEvent] 生命周期绑定失败: 无法挂载失活触发器, EventKey={Key}, TriggerObject={mono.gameObject.name}");
                    UnRegister();
                    return this;
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
            if (callback == null)
            {
                return false;
            }

            Dictionary<T, Type> box = EventBox<T>.DelegateTypeByKey;
            Type cbType = callback.GetType();

            if (!box.TryGetValue(key, out Type existedType))
            {
                box[key] = cbType;
                return true;
            }

            if (existedType == cbType)
            {
                return true;
            }

            LogKit.LogError($"[GlobalEnumEvent] 注册失败: Key '{key}' 的委托签名不匹配, Expected={existedType}, Actual={cbType}");
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsRegistration<T>(T key, Delegate callback) where T : Enum
        {
            CallbackKey<T> ck = new CallbackKey<T>(key, callback);
            Dictionary<CallbackKey<T>, List<EnumEventToken<T>>> lookup = EventBox<T>.LookupTable;
            return lookup.TryGetValue(ck, out List<EnumEventToken<T>> list) && list.Count > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddToLookup<T>(EnumEventToken<T> token) where T : Enum
        {
            Dictionary<CallbackKey<T>, List<EnumEventToken<T>>> lookup = EventBox<T>.LookupTable;
            CallbackKey<T> ck = new CallbackKey<T>(token.Key, token.Callback);

            if (!lookup.TryGetValue(ck, out List<EnumEventToken<T>> list))
            {
                list = new List<EnumEventToken<T>>(1);
                lookup.Add(ck, list);
            }

            list.Add(token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveFromLookup<T>(EnumEventToken<T> token) where T : Enum
        {
            if (token.Callback == null)
            {
                return;
            }

            Dictionary<CallbackKey<T>, List<EnumEventToken<T>>> lookup = EventBox<T>.LookupTable;
            CallbackKey<T> ck = new CallbackKey<T>(token.Key, token.Callback);

            if (!lookup.TryGetValue(ck, out List<EnumEventToken<T>> list))
            {
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(list[i], token))
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            if (list.Count == 0)
            {
                lookup.Remove(ck);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddToEventTable<T>(T key, Delegate callback) where T : Enum
        {
            Dictionary<T, Delegate> table = EventBox<T>.EventTable;
            if (!table.TryGetValue(key, out Delegate d))
            {
                table[key] = callback;
                return;
            }

            table[key] = Delegate.Combine(d, callback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveFromEventTable<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null)
            {
                return;
            }

            Dictionary<T, Delegate> table = EventBox<T>.EventTable;
            if (!table.TryGetValue(key, out Delegate currentDel))
            {
                return;
            }

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
            Stack<EnumEventToken<T>> pool = EventBox<T>.TokenPool;
            EnumEventToken<T> token = pool.Count > 0 ? pool.Pop() : new EnumEventToken<T>();
            token.Key = key;
            token.Callback = callback;
            token.IsInUse = true;
            token.IsRegistered = true;
            AddToLookup(token);
            return token;
        }

        private static void Recycle<T>(EnumEventToken<T> token) where T : Enum
        {
            RemoveFromLookup(token);
            token.Key = default;
            token.Callback = null;
            token.IsRegistered = false;
            token.IsInUse = false;
            EventBox<T>.TokenPool.Push(token);
        }

        private static bool TryGetDelegate<T, TDelegate>(T key, out TDelegate typedDelegate)
            where T : Enum
            where TDelegate : class
        {
            typedDelegate = null;

            if (!EventBox<T>.EventTable.TryGetValue(key, out Delegate d))
            {
                return false;
            }

            typedDelegate = d as TDelegate;
            if (typedDelegate != null)
            {
                return true;
            }

            LogKit.LogError(
                $"[GlobalEnumEvent] 广播失败: Key '{key}' 的委托签名与当前 Broadcast 调用不匹配, ActualDelegateType={d.GetType().Name}, ExpectedDelegateType={typeof(TDelegate).Name}");
            return false;
        }

        #endregion

        #region Public API: Register

        private static IUnRegister OnRegister<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null)
            {
                LogKit.LogError($"[GlobalEnumEvent] 注册失败: 回调为空, Key={key}");
                return new CustomUnRegister(null);
            }

            if (!EnsureDelegateTypeMatches(key, callback))
            {
                return new CustomUnRegister(null);
            }

            if (ContainsRegistration(key, callback))
            {
                LogKit.LogWarning($"[GlobalEnumEvent] 检测到重复注册，已拦截, Key={key}, Method={callback.Method.Name}");
                return new CustomUnRegister(() => OnUnRegister(key, callback));
            }

            AddToEventTable(key, callback);
            return AllocateToken(key, callback);
        }

        public static IUnRegister Register<T>(T key, Action callback) where T : Enum
        {
            return OnRegister(key, callback);
        }

        public static IUnRegister Register<T, T1>(T key, Action<T1> callback) where T : Enum
        {
            return OnRegister(key, callback);
        }

        public static IUnRegister Register<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum
        {
            return OnRegister(key, callback);
        }

        public static IUnRegister Register<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum
        {
            return OnRegister(key, callback);
        }

        #endregion

        #region Public API: UnRegister

        private static void OnUnRegister<T>(T key, Delegate callback) where T : Enum
        {
            if (callback == null)
            {
                return;
            }

            CallbackKey<T> ck = new CallbackKey<T>(key, callback);
            Dictionary<CallbackKey<T>, List<EnumEventToken<T>>> lookup = EventBox<T>.LookupTable;
            if (lookup.TryGetValue(ck, out List<EnumEventToken<T>> list) && list.Count > 0)
            {
                EnumEventToken<T> token = list[list.Count - 1];
                token.UnRegister();
                return;
            }

            RemoveFromEventTable(key, callback);
        }

        public static void UnRegister<T>(T key, Action callback) where T : Enum
        {
            OnUnRegister(key, callback);
        }

        public static void UnRegister<T, T1>(T key, Action<T1> callback) where T : Enum
        {
            OnUnRegister(key, callback);
        }

        public static void UnRegister<T, T1, T2>(T key, Action<T1, T2> callback) where T : Enum
        {
            OnUnRegister(key, callback);
        }

        public static void UnRegister<T, T1, T2, T3>(T key, Action<T1, T2, T3> callback) where T : Enum
        {
            OnUnRegister(key, callback);
        }

        #endregion

        #region Public API: Broadcast

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T>(T key) where T : Enum
        {
            if (!TryGetDelegate<T, Action>(key, out Action action))
            {
                return;
            }

            action.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1>(T key, T1 v1) where T : Enum
        {
            if (!TryGetDelegate<T, Action<T1>>(key, out Action<T1> action))
            {
                return;
            }

            action.Invoke(v1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2>(T key, T1 v1, T2 v2) where T : Enum
        {
            if (!TryGetDelegate<T, Action<T1, T2>>(key, out Action<T1, T2> action))
            {
                return;
            }

            action.Invoke(v1, v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Broadcast<T, T1, T2, T3>(T key, T1 v1, T2 v2, T3 v3) where T : Enum
        {
            if (!TryGetDelegate<T, Action<T1, T2, T3>>(key, out Action<T1, T2, T3> action))
            {
                return;
            }

            action.Invoke(v1, v2, v3);
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