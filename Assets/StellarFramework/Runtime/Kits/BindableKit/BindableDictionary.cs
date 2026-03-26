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
        public V OldValue;
    }

    /// <summary>
    /// 响应式字典
    /// 我只负责字典结构和键值替换通知，不负责 Value 内部字段变化通知。
    /// 这样设计是为了保持事件边界清晰，避免业务把引用对象内部修改误认为字典更新。
    /// </summary>
    [Serializable]
    public class BindableDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        private readonly Dictionary<K, V> _dict = new Dictionary<K, V>();

        private ObserverNode _head;
        private ObserverNode _tail;
        private int _iteratingCount;
        private bool _isNotifying;

        public int Count => _dict.Count;

        public ICollection<K> Keys => _dict.Keys;
        public ICollection<V> Values => _dict.Values;

        public V this[K key]
        {
            get => _dict[key];
            set
            {
                if (_dict.TryGetValue(key, out var oldVal))
                {
                    _dict[key] = value;

                    Notify(new DictEvent<K, V>
                    {
                        Type = DictEventType.Update,
                        Key = key,
                        Value = value,
                        OldValue = oldVal
                    });

                    return;
                }

                Add(key, value);
            }
        }

        public void Add(K key, V value)
        {
            if (_dict.ContainsKey(key))
            {
                LogKit.LogError(
                    $"[BindableDictionary] Add 失败: Key 已存在, Key={key}, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}, Count={_dict.Count}");
                return;
            }

            _dict.Add(key, value);

            Notify(new DictEvent<K, V>
            {
                Type = DictEventType.Add,
                Key = key,
                Value = value
            });
        }

        public bool Remove(K key)
        {
            if (!_dict.TryGetValue(key, out var val))
            {
                return false;
            }

            _dict.Remove(key);

            Notify(new DictEvent<K, V>
            {
                Type = DictEventType.Remove,
                Key = key,
                Value = val
            });

            return true;
        }

        public bool TryGetValue(K key, out V value)
        {
            return _dict.TryGetValue(key, out value);
        }

        public bool ContainsKey(K key)
        {
            return _dict.ContainsKey(key);
        }

        public void Clear()
        {
            if (_dict.Count == 0)
            {
                return;
            }

            _dict.Clear();

            Notify(new DictEvent<K, V>
            {
                Type = DictEventType.Clear
            });
        }

        public IUnRegister Register(Action<DictEvent<K, V>> onDictChanged)
        {
            LogKit.AssertNotNull(onDictChanged,
                $"[BindableDictionary] 注册失败: 回调为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
            if (onDictChanged == null)
            {
                return new CustomUnRegister(null);
            }

            return AddNode(onDictChanged);
        }

        /// <summary>
        /// 手动广播一次刷新
        /// 我提供这个能力给批量写入后的业务层使用，避免它们通过伪修改触发通知。
        /// </summary>
        public void NotifyRefresh()
        {
            Notify(new DictEvent<K, V>
            {
                Type = DictEventType.Update
            });
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
            _isNotifying = false;
        }

        private void Notify(DictEvent<K, V> e)
        {
            LogKit.Assert(!_isNotifying,
                $"[BindableDictionary] 致命错误: 检测到递归通知, EventType={e.Type}, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
            if (_isNotifying)
            {
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
                        node.Action?.Invoke(e);
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

        private ObserverNode AddNode(Action<DictEvent<K, V>> action)
        {
            var node = ObserverNode.Allocate(action, this);

            if (_head == null)
            {
                _head = node;
                _tail = node;
                return node;
            }

            _tail.Next = node;
            node.Previous = _tail;
            _tail = node;
            return node;
        }

        private void RemoveNode(ObserverNode node)
        {
            if (node == null)
            {
                LogKit.LogError(
                    $"[BindableDictionary] RemoveNode 失败: 节点为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
                return;
            }

            if (!ReferenceEquals(node.Owner, this))
            {
                LogKit.LogError(
                    $"[BindableDictionary] RemoveNode 失败: 节点归属不匹配, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
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

        private sealed class ObserverNode : IUnRegister
        {
            public Action<DictEvent<K, V>> Action;
            public BindableDictionary<K, V> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> Pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<DictEvent<K, V>> action, BindableDictionary<K, V> owner)
            {
                var node = Pool.Count > 0 ? Pool.Pop() : new ObserverNode();
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
                    LogKit.LogError(
                        $"[BindableDictionary] 生命周期绑定失败: GameObject 为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
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

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _dict.GetEnumerator();
        }
    }
}