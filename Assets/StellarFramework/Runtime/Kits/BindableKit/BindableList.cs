using System;
using System.Collections;
using System.Collections.Generic;
using StellarFramework.Event;
using UnityEngine;

namespace StellarFramework.Bindable
{
    public enum ListEventType
    {
        Add,
        Remove,
        Clear,
        Replace
    }

    public struct ListEvent<T>
    {
        public ListEventType Type;
        public T Item;
        public T OldItem; // 仅 Replace 用
        public int Index;
    }

    /// <summary>
    /// [StellarFramework] 响应式列表 
    /// </summary>
    [Serializable]
    public class BindableList<T> : IEnumerable<T>
    {
        [SerializeField] private List<T> _list = new List<T>();

        // 观察者链表
        private ObserverNode _head;
        private ObserverNode _tail;

        //  遍历计数器
        private int _iteratingCount = 0;

        public int Count => _list.Count;

        public T this[int index]
        {
            get => _list[index];
            set
            {
                T old = _list[index];
                _list[index] = value;
                Notify(new ListEvent<T> { Type = ListEventType.Replace, Item = value, OldItem = old, Index = index });
            }
        }

        public void Add(T item)
        {
            _list.Add(item);
            Notify(new ListEvent<T> { Type = ListEventType.Add, Item = item, Index = _list.Count - 1 });
        }

        public void Remove(T item)
        {
            int index = _list.IndexOf(item);
            if (index >= 0)
            {
                _list.RemoveAt(index);
                Notify(new ListEvent<T> { Type = ListEventType.Remove, Item = item, Index = index });
            }
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _list.Count)
            {
                T item = _list[index];
                _list.RemoveAt(index);
                Notify(new ListEvent<T> { Type = ListEventType.Remove, Item = item, Index = index });
            }
        }

        public void Clear()
        {
            _list.Clear();
            Notify(new ListEvent<T> { Type = ListEventType.Clear });
        }

        // --- 核心通知逻辑 ---
        private void Notify(ListEvent<T> e)
        {
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
                            node.Action?.Invoke(e);
                        }
                        catch (Exception ex)
                        {
                            LogKit.LogError($"[BindableList/Dict] 回调异常: {ex}");
                        }
                    }

                    node = next;
                }
            }
            finally
            {
                _iteratingCount--;
                if (_iteratingCount == 0) Cleanup();
            }
        }

        public IUnRegister Register(Action<ListEvent<T>> onListChanged)
        {
            return AddNode(onListChanged);
        }

        // --- 链表与池化 ---
        private ObserverNode AddNode(Action<ListEvent<T>> action)
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
                if (node.MarkedForDeletion) UnlinkAndRecycle(node);
                node = next;
            }
        }

        private class ObserverNode : IUnRegister
        {
            public Action<ListEvent<T>> Action;
            public BindableList<T> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> _pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<ListEvent<T>> action, BindableList<T> owner)
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

        // IEnumerable 实现
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }
}