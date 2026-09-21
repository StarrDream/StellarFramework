using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Event
{
    /// <summary>
    /// 类型事件的标记接口。事件数据类型实现该接口后即可作为 GlobalTypeEvent 的强类型事件键。
    /// </summary>
    public interface ITypeEvent
    {
    }

    /// <summary>
    /// 以事件数据类型本身作为 Key 的全局事件总线。
    /// </summary>
    /// <remarks>
    /// 注册和广播均为同步调用，不做线程同步。
    /// 同一个回调重复注册会被拦截；推荐始终保存 Register 返回的 IUnRegister 做精确注销。
    /// </remarks>
    public static class GlobalTypeEvent
    {
        /// <summary>
        /// 注册 T 类型事件。
        /// </summary>
        /// <returns>精确注销该次注册的句柄。</returns>
        public static IUnRegister Register<T>(Action<T> onEvent) where T : ITypeEvent
        {
            if (onEvent == null)
            {
                Debug.LogError($"[GlobalTypeEvent] 注册失败: 回调为空, EventType={typeof(T).Name}");
                return new CustomUnRegister(null);
            }

            if (EventBox<T>.Contains(onEvent))
            {
                Debug.LogWarning(
                    $"[GlobalTypeEvent] 检测到重复注册，已拦截, EventType={typeof(T).Name}, Method={onEvent.Method.Name}");
                return new CustomUnRegister(null);
            }

            EventBox<T>.Subscribe(onEvent);
            return EventBox<T>.AllocateToken(onEvent);
        }

        /// <summary>
        /// 同步广播一个已有事件实例。
        /// </summary>
        public static void Broadcast<T>(T e) where T : ITypeEvent
        {
            EventBox<T>.Invoke(e);
        }

        /// <summary>
        /// 创建 T 的默认实例并同步广播。
        /// </summary>
        public static void Broadcast<T>() where T : ITypeEvent, new()
        {
            EventBox<T>.Invoke(new T());
        }

        /// <summary>
        /// 危险接口已封死。
        /// 我不再允许业务一键清空某个事件类型下的所有监听者，这会破坏全局隔离边界。
        /// </summary>
        [Obsolete("危险接口已禁用，请改用 Register 返回的 IUnRegister 实例进行精确注销。", true)]
        public static void UnRegister<T>() where T : ITypeEvent
        {
        }

        private static class EventBox<T> where T : ITypeEvent
        {
            public static Action<T> Subscribers;

            private static readonly Stack<EventToken> TokenPool = new Stack<EventToken>();
            private static readonly HashSet<Delegate> CallbackSet = new HashSet<Delegate>();

            public static bool Contains(Action<T> callback)
            {
                return CallbackSet.Contains(callback);
            }

            public static void Subscribe(Action<T> callback)
            {
                if (callback == null)
                {
                    return;
                }

                Subscribers += callback;
                CallbackSet.Add(callback);
            }

            public static void Unsubscribe(Action<T> callback)
            {
                if (callback == null)
                {
                    return;
                }

                Subscribers -= callback;
                CallbackSet.Remove(callback);
            }

            public static void Invoke(T e)
            {
                Subscribers?.Invoke(e);
            }

            public static EventToken AllocateToken(Action<T> callback)
            {
                EventToken token = TokenPool.Count > 0 ? TokenPool.Pop() : new EventToken();
                token.Handler = callback;
                token.IsRecycled = false;
                token.IsRegistered = true;
                return token;
            }

            public static void RecycleToken(EventToken token)
            {
                if (token == null || token.IsRecycled)
                {
                    return;
                }

                token.UnbindLifecycleTriggers();
                token.Handler = null;
                token.IsRegistered = false;
                token.IsRecycled = true;
                TokenPool.Push(token);
            }

            public sealed class EventToken : IUnRegister
            {
                public Action<T> Handler;
                public bool IsRecycled;
                public bool IsRegistered;

                /// <summary>
                /// 记录本 Token 绑定过的生命周期触发器。
                /// 主动回收时必须从这些触发器中移除自身，否则池化复用的 Token 会在旧宿主销毁时被误取消注册（use-after-free）。
                /// </summary>
                private readonly HashSet<EventUnregisterTrigger> _destroyTriggers =
                    new HashSet<EventUnregisterTrigger>();

                private readonly HashSet<EventUnregisterOnDisableTrigger> _disableTriggers =
                    new HashSet<EventUnregisterOnDisableTrigger>();

                public void UnRegister()
                {
                    if (IsRecycled)
                    {
                        return;
                    }

                    if (IsRegistered && Handler != null)
                    {
                        Unsubscribe(Handler);
                    }

                    RecycleToken(this);
                }

                public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
                {
                    if (gameObject == null)
                    {
                        Debug.LogError($"[GlobalTypeEvent] 生命周期绑定失败: gameObject 为空, EventType={typeof(T).Name}");
                        UnRegister();
                        return this;
                    }

                    if (!CustomUnRegister.TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
                    {
                        Debug.LogError(
                            $"[GlobalTypeEvent] 生命周期绑定失败: 无法挂载销毁触发器, EventType={typeof(T).Name}, TriggerObject={gameObject.name}");
                        UnRegister();
                        return this;
                    }

                    trigger.Add(this);
                    _destroyTriggers.Add(trigger);
                    return this;
                }

                public IUnRegister UnRegisterWhenGameObjectDestroyed(MonoBehaviour mono)
                {
                    if (mono == null)
                    {
                        Debug.LogError($"[GlobalTypeEvent] 生命周期绑定失败: mono 为空, EventType={typeof(T).Name}");
                        UnRegister();
                        return this;
                    }

                    return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
                }

                public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
                {
                    if (mono == null || mono.gameObject == null)
                    {
                        Debug.LogError(
                            $"[GlobalTypeEvent] 生命周期绑定失败: mono 或 gameObject 为空, EventType={typeof(T).Name}");
                        UnRegister();
                        return this;
                    }

                    if (!CustomUnRegister.TryAttachDisableTrigger(mono.gameObject,
                            out EventUnregisterOnDisableTrigger trigger))
                    {
                        Debug.LogError(
                            $"[GlobalTypeEvent] 生命周期绑定失败: 无法挂载失活触发器, EventType={typeof(T).Name}, TriggerObject={mono.gameObject.name}");
                        UnRegister();
                        return this;
                    }

                    trigger.Add(this);
                    _disableTriggers.Add(trigger);
                    return this;
                }

                /// <summary>
                /// 回收前解除全部生命周期绑定，防止池化复用后旧触发器误操作新回调。
                /// </summary>
                internal void UnbindLifecycleTriggers()
                {
                    foreach (EventUnregisterTrigger trigger in _destroyTriggers)
                    {
                        trigger?.Remove(this);
                    }

                    _destroyTriggers.Clear();

                    foreach (EventUnregisterOnDisableTrigger trigger in _disableTriggers)
                    {
                        trigger?.Remove(this);
                    }

                    _disableTriggers.Clear();
                }
            }
        }
    }
}
