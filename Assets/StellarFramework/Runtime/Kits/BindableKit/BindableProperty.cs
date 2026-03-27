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
        private int _iteratingCount;
        private bool _isNotifying;

        public BindableProperty(T initValue = default)
        {
            _value = initValue;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                {
                    return;
                }

                _value = value;
                Notify();
            }
        }

        public void SetValueWithoutNotify(T value)
        {
            _value = value;
        }

        public void SetValueForceNotify(T value)
        {
            _value = value;
            Notify();
        }

        public void Notify()
        {
            if (_isNotifying)
            {
                LogKit.LogError(
                    $"[BindableProperty] Notify 失败: 检测到递归通知，已阻断以防止状态错乱, ValueType={typeof(T).Name}, CurrentValue={_value}");
                return;
            }

            _isNotifying = true;
            _iteratingCount++;

            try
            {
                ObserverNode node = _head;
                while (node != null)
                {
                    ObserverNode next = node.Next;
                    if (!node.MarkedForDeletion)
                    {
                        node.Action?.Invoke(_value);
                    }

                    node = next;
                }
            }
            finally
            {
                _iteratingCount--;
                _isNotifying = false;
                if (_iteratingCount == 0)
                {
                    Cleanup();
                }
            }
        }

        public IUnRegister Register(Action<T> onValueChanged)
        {
            if (onValueChanged == null)
            {
                LogKit.LogError($"[BindableProperty] 注册失败: 回调委托为空, ValueType={typeof(T).Name}");
                return new CustomUnRegister(null);
            }

            return AddNode(onValueChanged);
        }

        public IUnRegister RegisterWithInitValue(Action<T> onValueChanged)
        {
            if (onValueChanged == null)
            {
                LogKit.LogError($"[BindableProperty] 注册失败: 回调委托为空, ValueType={typeof(T).Name}");
                return new CustomUnRegister(null);
            }

            // 关键修复：
            // 先注册，再回调。
            // 这样才能保证“注册并立即收到一次当前值”的语义闭环。
            IUnRegister unregister = AddNode(onValueChanged);
            onValueChanged.Invoke(_value);
            return unregister;
        }

        public void UnRegisterAll()
        {
            ObserverNode node = _head;
            while (node != null)
            {
                ObserverNode next = node.Next;
                node.Recycle();
                node = next;
            }

            _head = null;
            _tail = null;
            _iteratingCount = 0;
            _isNotifying = false;
        }

        private ObserverNode AddNode(Action<T> action)
        {
            ObserverNode node = ObserverNode.Allocate(action, this);

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
            if (node == null || node.Owner != this)
            {
                return;
            }

            if (_iteratingCount > 0)
            {
                node.MarkedForDeletion = true;
                return;
            }

            UnlinkAndRecycle(node);
        }

        private void UnlinkAndRecycle(ObserverNode node)
        {
            if (node == _head)
            {
                _head = node.Next;
            }

            if (node == _tail)
            {
                _tail = node.Previous;
            }

            if (node.Previous != null)
            {
                node.Previous.Next = node.Next;
            }

            if (node.Next != null)
            {
                node.Next.Previous = node.Previous;
            }

            node.Recycle();
        }

        private void Cleanup()
        {
            ObserverNode node = _head;
            while (node != null)
            {
                ObserverNode next = node.Next;
                if (node.MarkedForDeletion)
                {
                    UnlinkAndRecycle(node);
                }

                node = next;
            }
        }

        private sealed class ObserverNode : IUnRegister
        {
            public Action<T> Action;
            public BindableProperty<T> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> Pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<T> action, BindableProperty<T> owner)
            {
                ObserverNode node = Pool.Count > 0 ? Pool.Pop() : new ObserverNode();
                node.Action = action;
                node.Owner = owner;
                node.Previous = null;
                node.Next = null;
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
                Pool.Push(this);
            }

            public void UnRegister()
            {
                Owner?.RemoveNode(this);
            }

            public IUnRegister UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
            {
                if (gameObject == null)
                {
                    LogKit.LogError($"[BindableProperty] 生命周期绑定失败: gameObject 为空, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
                {
                    LogKit.LogError(
                        $"[BindableProperty] 生命周期绑定失败: 无法挂载销毁触发器, TriggerObject={gameObject.name}, ValueType={typeof(T).Name}");
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
                    LogKit.LogError($"[BindableProperty] 生命周期绑定失败: mono 为空, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
            }

            public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
            {
                if (mono == null || mono.gameObject == null)
                {
                    LogKit.LogError($"[BindableProperty] 生命周期绑定失败: mono 或 gameObject 为空, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDisableTrigger(mono.gameObject,
                        out EventUnregisterOnDisableTrigger trigger))
                {
                    LogKit.LogError(
                        $"[BindableProperty] 生命周期绑定失败: 无法挂载失活触发器, TriggerObject={mono.gameObject.name}, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                trigger.Add(this);
                return this;
            }
        }

        public static implicit operator T(BindableProperty<T> p)
        {
            return p.Value;
        }

        public override string ToString()
        {
            return _value?.ToString();
        }
    }
}