using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Event
{
    /// <summary>
    /// [TypeEventSystem] 事件标记接口
    /// 所有用于强类型事件系统的结构体必须实现此接口
    /// </summary>
    public interface ITypeEvent
    {
    }

    /// <summary>
    /// [StellarFramework] 强类型事件系统 
    /// <para>优势：</para>
    /// <para>1. 类型安全：编译期检查，防止参数传错。</para>
    /// <para>2. 极速：使用泛型静态类存储委托，无 Dictionary 查找开销。</para>
    /// <para>3. 0GC：发送事件无 GC，注册句柄使用对象池复用。</para>
    /// </summary>
    public static class GlobalTypeEvent
    {
        /// <summary>
        /// 注册事件监听
        /// </summary>
        /// <typeparam name="T">必须实现 IEvent 接口的结构体</typeparam>
        /// <param name="onEvent">回调函数</param>
        /// <returns>注销句柄 (记得绑定生命周期)</returns>
        public static IUnRegister Register<T>(Action<T> onEvent) where T : ITypeEvent
        {
            if (onEvent == null) return new CustomUnRegister(null);

            // 1. 订阅静态委托
            EventBox<T>.Subscribers += onEvent;

            // 2. 从池中分配注销句柄
            return EventBox<T>.AllocateToken(onEvent);
        }

        /// <summary>
        /// 发送事件
        /// </summary>
        /// <param name="e">事件结构体数据</param>
        public static void Broadcast<T>(T e) where T : ITypeEvent
        {
            // 直接调用静态委托，速度极快
            EventBox<T>.Subscribers?.Invoke(e);
        }

        /// <summary>
        /// 发送事件 (使用默认构造函数)
        /// </summary>
        public static void Broadcast<T>() where T : ITypeEvent, new()
        {
            EventBox<T>.Subscribers?.Invoke(new T());
        }

        /// <summary>
        /// 清空某类事件的所有监听 (慎用，通常用于重置游戏)
        /// </summary>
        public static void Clear<T>() where T : ITypeEvent
        {
            EventBox<T>.Subscribers = null;
            EventBox<T>.ClearPool();
        }

        // ==================================================================================
        // 内部核心实现 (Generic Static Class)
        // 利用泛型特性，为每种 T 生成独立的存储空间，物理隔离，访问速度 O(1)
        // ==================================================================================
        private static class EventBox<T> where T : ITypeEvent
        {
            // 静态委托链
            public static Action<T> Subscribers;

            // 句柄对象池 (每个事件类型拥有独立的池)
            private static readonly Stack<EventToken> _pool = new Stack<EventToken>();

            public static EventToken AllocateToken(Action<T> callback)
            {
                EventToken token = _pool.Count > 0 ? _pool.Pop() : new EventToken();
                token.Handler = callback;
                token.IsRecycled = false;
                return token;
            }

            public static void RecycleToken(EventToken token)
            {
                if (token.IsRecycled) return;

                token.Handler = null;
                token.IsRecycled = true;
                _pool.Push(token);
            }

            public static void ClearPool()
            {
                _pool.Clear();
            }

            // --- 内部注销句柄 ---
            public class EventToken : IUnRegister
            {
                public Action<T> Handler;
                public bool IsRecycled;

                public void UnRegister()
                {
                    if (IsRecycled) return;

                    // 从委托链移除
                    if (Handler != null)
                    {
                        Subscribers -= Handler;
                    }

                    // 回收自己
                    RecycleToken(this);
                }

                public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
                {
                    if (gameObject == null)
                    {
                        UnRegister();
                        return this;
                    }

                    // 使用框架统一的 EventUnregisterTrigger
                    if (!gameObject.TryGetComponent<EventUnregisterTrigger>(out var trigger))
                    {
                        trigger = gameObject.AddComponent<EventUnregisterTrigger>();
                        trigger.hideFlags = HideFlags.HideInInspector;
                    }

                    trigger.Add(this);
                    return this;
                }
            }
        }
    }
}