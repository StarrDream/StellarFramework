using System;
using System.Collections.Generic;
using StellarFramework.Event;
using UnityEngine;

namespace StellarFramework.Bindable
{
    /// <summary>
    /// [StellarFramework] 响应式属性 
    /// </summary>
    [Serializable]
    public class BindableProperty<T>
    {
        [SerializeField] private T _value;

        // 观察者链表
        private ObserverNode _head;
        private ObserverNode _tail;

        // 遍历计数器，用于处理遍历过程中发生的注销操作
        private int _iteratingCount = 0;

        //  重入锁：防止回调中修改 Value 导致死循环
        private bool _isNotifying = false;

        public BindableProperty(T initValue = default) => _value = initValue;

        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _value = value;
                    Notify();
                }
            }
        }

        /// <summary>
        /// 设置值但不触发通知
        /// </summary>
        public void SetValueWithoutNotify(T value) => _value = value;

        // 强制设置值并通知（即使值相同）
        public void SetValueForceNotify(T value)
        {
            _value = value;
            Notify();
        }

        /// <summary>
        /// 强制通知
        /// </summary>
        public void Notify()
        {
            //  防止死循环
            if (_isNotifying)
            {
                LogKit.LogWarning("[BindableProperty] 检测到递归修改 Value，已拦截以防止 StackOverflow。");
                return;
            }

            _isNotifying = true;
            _iteratingCount++;

            try
            {
                var node = _head;
                while (node != null)
                {
                    var next = node.Next;

                    if (!node.MarkedForDeletion)
                    {
                        try
                        {
                            node.Action?.Invoke(_value);
                        }
                        catch (Exception ex)
                        {
                            LogKit.LogError($"[BindableProperty] Callback exception: {ex.Message}\n{ex.StackTrace}");
                        }
                    }

                    node = next;
                }
            }
            finally
            {
                _iteratingCount--;
                _isNotifying = false; // 解锁
                if (_iteratingCount == 0)
                {
                    Cleanup();
                }
            }
        }

        #region 注册 API

        public IUnRegister Register(Action<T> onValueChanged)
        {
            return AddNode(onValueChanged);
        }

        public IUnRegister RegisterWithInitValue(Action<T> onValueChanged)
        {
            onValueChanged?.Invoke(_value);
            return AddNode(onValueChanged);
        }

        public void UnRegisterAll()
        {
            var node = _head;
            while (node != null)
            {
                var next = node.Next;
                node.Recycle();
                node = next;
            }

            _head = null;
            _tail = null;
            _iteratingCount = 0;
        }

        #endregion

        #region 链表与池化 (Private)

        private ObserverNode AddNode(Action<T> action)
        {
            var node = ObserverNode.Allocate(action, this);
            if (_head == null)
            {
                _head = node;
                _tail = node;
            }
            else
            {
                _tail.Next = node;
                node.Previous = _tail;
                _tail = node;
            }

            return node;
        }

        private void RemoveNode(ObserverNode node)
        {
            if (node.Owner != this) return;

            if (_iteratingCount > 0)
            {
                node.MarkedForDeletion = true;
            }
            else
            {
                UnlinkAndRecycle(node);
            }
        }

        private void UnlinkAndRecycle(ObserverNode node)
        {
            if (node == _head) _head = node.Next;
            if (node == _tail) _tail = node.Previous;
            if (node.Previous != null) node.Previous.Next = node.Next;
            if (node.Next != null) node.Next.Previous = node.Previous;

            node.Recycle();
        }

        private void Cleanup()
        {
            var node = _head;
            while (node != null)
            {
                var next = node.Next;
                if (node.MarkedForDeletion)
                {
                    UnlinkAndRecycle(node);
                }

                node = next;
            }
        }

        private class ObserverNode : IUnRegister
        {
            public Action<T> Action;
            public BindableProperty<T> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> _pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<T> action, BindableProperty<T> owner)
            {
                var node = _pool.Count > 0 ? _pool.Pop() : new ObserverNode();
                node.Action = action;
                node.Owner = owner;
                node.MarkedForDeletion = false;
                return node;
            }

            public void Recycle()
            {
                Action = null;
                Owner = null;
                Previous = null;
                Next = null;
                MarkedForDeletion = false;
                _pool.Push(this);
            }

            public void UnRegister() => Owner?.RemoveNode(this);

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

        public static implicit operator T(BindableProperty<T> p) => p.Value;
        public override string ToString() => _value?.ToString();
    }
}