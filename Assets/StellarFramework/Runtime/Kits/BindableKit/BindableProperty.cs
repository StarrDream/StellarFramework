// ==================================================================================
// BindableProperty - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：响应式属性。
// 改造说明：
// 1. 移除 Notify 中的 try-catch，强制业务层处理自己的异常。
// 2. 将递归修改检测升级为致命断言 (Assert)，防止死循环导致栈溢出。
// ==================================================================================

using System;
using System.Collections.Generic;
using StellarFramework.Event;
using UnityEngine;

namespace StellarFramework.Bindable
{
    [Serializable]
    public class BindableProperty<T>
    {
        [SerializeField] private T _value;

        private ObserverNode _head;
        private ObserverNode _tail;
        private int _iteratingCount = 0;
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

        public void SetValueWithoutNotify(T value) => _value = value;

        public void SetValueForceNotify(T value)
        {
            _value = value;
            Notify();
        }

        public void Notify()
        {
            // Fail-Fast: 严格拦截在回调中再次修改 Value 导致的死循环
            LogKit.Assert(!_isNotifying, "[BindableProperty] 致命错误：检测到递归修改 Value，已拦截以防止 StackOverflow。");
            if (_isNotifying) return;

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
                        // 核心改造：移除 try-catch，让业务异常自然上抛
                        node.Action?.Invoke(_value);
                    }

                    node = next;
                }
            }
            finally
            {
                // 确保即使发生异常，状态锁和迭代计数器也能正确恢复
                _iteratingCount--;
                _isNotifying = false;
                if (_iteratingCount == 0)
                {
                    Cleanup();
                }
            }
        }

        #region 注册 API

        public IUnRegister Register(Action<T> onValueChanged)
        {
            LogKit.AssertNotNull(onValueChanged, "[BindableProperty] 注册失败：回调委托不能为空");
            return AddNode(onValueChanged);
        }

        public IUnRegister RegisterWithInitValue(Action<T> onValueChanged)
        {
            LogKit.AssertNotNull(onValueChanged, "[BindableProperty] 注册失败：回调委托不能为空");
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