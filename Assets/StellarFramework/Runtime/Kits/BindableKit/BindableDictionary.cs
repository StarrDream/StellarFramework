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
                if (!EnsureMutationAllowed("Indexer.Set"))
                {
                    return;
                }

                if (_dict.TryGetValue(key, out V oldVal))
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
            if (!EnsureMutationAllowed("Add"))
            {
                return;
            }

            if (_dict.ContainsKey(key))
            {
                LogKit.LogError($"[BindableDictionary] Add 失败: Key 已存在, Key={key}");
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
            if (!EnsureMutationAllowed("Remove"))
            {
                return false;
            }

            if (!_dict.TryGetValue(key, out V val))
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
            if (!EnsureMutationAllowed("Clear"))
            {
                return;
            }

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
            if (onDictChanged == null)
            {
                LogKit.LogError(
                    $"[BindableDictionary] 注册失败: 回调为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
                return new CustomUnRegister(null);
            }

            return AddNode(onDictChanged);
        }

        public void NotifyRefresh()
        {
            if (_isNotifying)
            {
                LogKit.LogError(
                    $"[BindableDictionary] NotifyRefresh 失败: 当前正在通知中，禁止嵌套刷新, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}, Count={_dict.Count}");
                return;
            }

            Notify(new DictEvent<K, V>
            {
                Type = DictEventType.Update
            });
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

        private bool EnsureMutationAllowed(string apiName)
        {
            if (_isNotifying)
            {
                LogKit.LogError(
                    $"[BindableDictionary] {apiName} 失败: 禁止在通知回调中修改字典, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}, Count={_dict.Count}");
                return false;
            }

            return true;
        }

        private void Notify(DictEvent<K, V> e)
        {
            if (_isNotifying)
            {
                LogKit.LogError(
                    $"[BindableDictionary] Notify 失败: 检测到递归通知，已阻断, EventType={e.Type}, Key={e.Key}, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
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
            if (node == null || !ReferenceEquals(node.Owner, this))
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
            public Action<DictEvent<K, V>> Action;
            public BindableDictionary<K, V> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> Pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<DictEvent<K, V>> action, BindableDictionary<K, V> owner)
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
                    LogKit.LogError(
                        $"[BindableDictionary] 生命周期绑定失败: gameObject 为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
                {
                    LogKit.LogError(
                        $"[BindableDictionary] 生命周期绑定失败: 无法挂载销毁触发器, TriggerObject={gameObject.name}, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
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
                    LogKit.LogError(
                        $"[BindableDictionary] 生命周期绑定失败: mono 为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
                    UnRegister();
                    return this;
                }

                return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
            }

            public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
            {
                if (mono == null || mono.gameObject == null)
                {
                    LogKit.LogError(
                        $"[BindableDictionary] 生命周期绑定失败: mono 或 gameObject 为空, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDisableTrigger(mono.gameObject,
                        out EventUnregisterOnDisableTrigger trigger))
                {
                    LogKit.LogError(
                        $"[BindableDictionary] 生命周期绑定失败: 无法挂载失活触发器, TriggerObject={mono.gameObject.name}, KeyType={typeof(K).Name}, ValueType={typeof(V).Name}");
                    UnRegister();
                    return this;
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