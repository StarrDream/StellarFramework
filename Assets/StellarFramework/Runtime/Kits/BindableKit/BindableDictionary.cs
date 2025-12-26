using System;
using System.Collections;
using System.Collections.Generic;
using StellarFramework.Event;
using UnityEngine;

namespace StellarFramework.Bindable
{
    public enum DictEventType
    {
        Add,
        Remove,
        Clear,
        Update
    }

    public struct DictEvent<K, V>
    {
        public DictEventType Type;
        public K Key;
        public V Value;
        public V OldValue; // Update 用
    }

    /// <summary>
    /// [StellarFramework] 响应式字典 
    /// </summary>
    public class BindableDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        private Dictionary<K, V> _dict = new Dictionary<K, V>();

        private ObserverNode _head;
        private ObserverNode _tail;

        //  遍历计数器
        private int _iteratingCount = 0;

        public int Count => _dict.Count;

        public V this[K key]
        {
            get => _dict[key];
            set
            {
                if (_dict.TryGetValue(key, out var oldVal))
                {
                    _dict[key] = value;
                    Notify(new DictEvent<K, V> { Type = DictEventType.Update, Key = key, Value = value, OldValue = oldVal });
                }
                else
                {
                    Add(key, value);
                }
            }
        }

        public void Add(K key, V value)
        {
            _dict.Add(key, value);
            Notify(new DictEvent<K, V> { Type = DictEventType.Add, Key = key, Value = value });
        }

        public bool Remove(K key)
        {
            if (_dict.TryGetValue(key, out var val))
            {
                _dict.Remove(key);
                Notify(new DictEvent<K, V> { Type = DictEventType.Remove, Key = key, Value = val });
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _dict.Clear();
            Notify(new DictEvent<K, V> { Type = DictEventType.Clear });
        }

        public bool TryGetValue(K key, out V value) => _dict.TryGetValue(key, out value);
        public bool ContainsKey(K key) => _dict.ContainsKey(key);

        private void Notify(DictEvent<K, V> e)
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

        public IUnRegister Register(Action<DictEvent<K, V>> onDictChanged)
        {
            return AddNode(onDictChanged);
        }

        // --- 链表与池化 ---
        private ObserverNode AddNode(Action<DictEvent<K, V>> action)
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
            public Action<DictEvent<K, V>> Action;
            public BindableDictionary<K, V> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> _pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<DictEvent<K, V>> action, BindableDictionary<K, V> owner)
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

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _dict.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _dict.GetEnumerator();
    }
}