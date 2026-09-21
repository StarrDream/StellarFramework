using System;
using System.Collections;
using System.Collections.Generic;
using StellarFramework.Event;
using UnityEngine;

namespace StellarFramework.Bindable
{
    /// <summary>
    /// BindableDictionary 产生的结构变化类型。
    /// </summary>
    public enum DictEventType
    {
        Add,
        Remove,
        Clear,
        Update
    }

    /// <summary>
    /// BindableDictionary 的单次变化描述。
    /// </summary>
    /// <typeparam name="K">键类型。</typeparam>
    /// <typeparam name="V">值类型。</typeparam>
    public struct DictEvent<K, V>
    {
        /// <summary>变化类型。</summary>
        public DictEventType Type;
        /// <summary>发生变化的键；Clear / NotifyRefresh 时通常为 default。</summary>
        public K Key;
        /// <summary>新增、删除或更新后的值。</summary>
        public V Value;
        /// <summary>Update 时更新前的旧值；其他事件通常为 default。</summary>
        public V OldValue;
    }

    /// <summary>
    /// 带同步变化通知的轻量字典。
    /// </summary>
    /// <remarks>
    /// 所有通知都在修改发生的线程同步执行，本类型不做线程同步。
    /// 为避免回调重入导致 Dictionary 状态和事件顺序不一致，监听回调中禁止再次修改同一个实例。
    /// </remarks>
    /// <typeparam name="K">键类型。</typeparam>
    /// <typeparam name="V">值类型。</typeparam>
    [Serializable]
    public class BindableDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        private readonly Dictionary<K, V> _dict = new Dictionary<K, V>();
        private ObserverNode _head;
        private ObserverNode _tail;
        private int _iteratingCount;
        private bool _isNotifying;

        /// <summary>当前键值对数量。</summary>
        public int Count => _dict.Count;
        /// <summary>底层字典的键集合视图。</summary>
        public ICollection<K> Keys => _dict.Keys;
        /// <summary>底层字典的值集合视图。</summary>
        public ICollection<V> Values => _dict.Values;

        /// <summary>
        /// 读取或设置指定键。
        /// 已存在键会发送 Update；不存在键等价于 Add。
        /// </summary>
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

        /// <summary>
        /// 新增键值对并发送 Add 事件。
        /// 已存在同名键时记录错误且不覆盖原值。
        /// </summary>
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

        /// <summary>
        /// 删除指定键并发送 Remove 事件。
        /// </summary>
        /// <returns>实际删除时返回 true。</returns>
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

        /// <summary>
        /// 尝试读取指定键。
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            return _dict.TryGetValue(key, out value);
        }

        /// <summary>
        /// 判断字典是否包含指定键。
        /// </summary>
        public bool ContainsKey(K key)
        {
            return _dict.ContainsKey(key);
        }

        /// <summary>
        /// 清空字典。
        /// 空字典不会重复发送 Clear 事件。
        /// </summary>
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

        /// <summary>
        /// 订阅字典结构变化。
        /// </summary>
        /// <param name="onDictChanged">变化回调。</param>
        /// <returns>可手动注销或绑定 Unity 生命周期的句柄。</returns>
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

        /// <summary>
        /// 主动通知订阅者重新读取整体状态，不修改字典内容。
        /// </summary>
        /// <remarks>
        /// 当前实现使用 Update + default Key 表达“整体刷新”，不要将其当作真实单键更新。
        /// </remarks>
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

        /// <summary>
        /// 注销该字典上的全部订阅者。
        /// 通知进行中调用时会延迟到本轮遍历完成后清理。
        /// </summary>
        public void UnRegisterAll()
        {
            if (_iteratingCount > 0)
            {
                ObserverNode deferredNode = _head;
                while (deferredNode != null)
                {
                    deferredNode.MarkedForDeletion = true;
                    deferredNode = deferredNode.Next;
                }

                return;
            }

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

            /// <summary>
            /// 记录本节点绑定过的生命周期触发器。
            /// 回收时必须从这些触发器中移除自身，防止池化复用后旧触发器误取消新注册（use-after-free）。
            /// </summary>
            private readonly HashSet<EventUnregisterTrigger> _destroyTriggers =
                new HashSet<EventUnregisterTrigger>();

            private readonly HashSet<EventUnregisterOnDisableTrigger> _disableTriggers =
                new HashSet<EventUnregisterOnDisableTrigger>();

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
                UnbindLifecycleTriggers();
                Action = null;
                Owner = null;
                Previous = null;
                Next = null;
                MarkedForDeletion = false;
                Pool.Push(this);
            }

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
                _destroyTriggers.Add(trigger);
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
                _disableTriggers.Add(trigger);
                return this;
            }
        }

        /// <summary>
        /// 返回底层字典的枚举器。
        /// </summary>
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
