using System;
using System.Collections;
using System.Collections.Generic;
using StellarFramework.Event;
using UnityEngine;

namespace StellarFramework.Bindable
{
    /// <summary>
    /// BindableList 产生的结构变化类型。
    /// </summary>
    public enum ListEventType
    {
        Add,
        Remove,
        Clear,
        Replace
    }

    /// <summary>
    /// BindableList 的单次变化描述。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public struct ListEvent<T>
    {
        /// <summary>变化类型。</summary>
        public ListEventType Type;
        /// <summary>新增、删除或替换后的元素。</summary>
        public T Item;
        /// <summary>Replace 时被替换掉的旧元素；其他事件通常为 default。</summary>
        public T OldItem;
        /// <summary>变化发生的索引；Clear / NotifyRefresh 使用 -1。</summary>
        public int Index;
    }

    /// <summary>
    /// 带同步变化通知的轻量列表。
    /// </summary>
    /// <remarks>
    /// 所有通知都在触发修改的线程同步执行。本类型不做线程同步。
    /// 为保持通知顺序与底层 List 状态一致，监听回调中禁止再次修改同一个 BindableList；
    /// 需要连锁修改时应把操作延后到当前通知完成之后。
    /// </remarks>
    /// <typeparam name="T">元素类型。</typeparam>
    [Serializable]
    public class BindableList<T> : IEnumerable<T>
    {
        [SerializeField] private List<T> _list = new List<T>();

        private ObserverNode _head;
        private ObserverNode _tail;
        private int _iteratingCount;
        private bool _isNotifying;

        /// <summary>
        /// 当前元素数量。
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// 读取或替换指定索引的元素。
        /// 设置成功后发送 <see cref="ListEventType.Replace"/>。
        /// </summary>
        public T this[int index]
        {
            get => _list[index];
            set
            {
                if (!EnsureMutationAllowed("Indexer.Set"))
                {
                    return;
                }

                if (index < 0 || index >= _list.Count)
                {
                    LogKit.LogError(
                        $"[BindableList] 设置元素失败: 索引越界, Index={index}, Count={_list.Count}, ValueType={typeof(T).Name}");
                    return;
                }

                T old = _list[index];
                _list[index] = value;
                Notify(new ListEvent<T>
                {
                    Type = ListEventType.Replace,
                    Item = value,
                    OldItem = old,
                    Index = index
                });
            }
        }

        /// <summary>
        /// 在列表末尾新增元素并发送 Add 事件。
        /// </summary>
        public void Add(T item)
        {
            if (!EnsureMutationAllowed("Add"))
            {
                return;
            }

            _list.Add(item);
            Notify(new ListEvent<T>
            {
                Type = ListEventType.Add,
                Item = item,
                Index = _list.Count - 1
            });
        }

        /// <summary>
        /// 删除第一个与 item 相等的元素。
        /// </summary>
        /// <returns>找到并删除时返回 true；未找到或当前禁止修改时返回 false。</returns>
        public bool Remove(T item)
        {
            if (!EnsureMutationAllowed("Remove"))
            {
                return false;
            }

            int index = _list.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            T removedItem = _list[index];
            _list.RemoveAt(index);
            Notify(new ListEvent<T>
            {
                Type = ListEventType.Remove,
                Item = removedItem,
                Index = index
            });
            return true;
        }

        /// <summary>
        /// 删除指定索引元素。
        /// </summary>
        /// <returns>删除成功时返回 true；索引非法或当前禁止修改时返回 false。</returns>
        public bool RemoveAt(int index)
        {
            if (!EnsureMutationAllowed("RemoveAt"))
            {
                return false;
            }

            if (index < 0 || index >= _list.Count)
            {
                LogKit.LogError(
                    $"[BindableList] RemoveAt 失败: 索引越界, Index={index}, Count={_list.Count}, ValueType={typeof(T).Name}");
                return false;
            }

            T removedItem = _list[index];
            _list.RemoveAt(index);
            Notify(new ListEvent<T>
            {
                Type = ListEventType.Remove,
                Item = removedItem,
                Index = index
            });
            return true;
        }

        /// <summary>
        /// 清空列表。
        /// 空列表不会重复发送 Clear 事件。
        /// </summary>
        public void Clear()
        {
            if (!EnsureMutationAllowed("Clear"))
            {
                return;
            }

            if (_list.Count == 0)
            {
                return;
            }

            _list.Clear();
            Notify(new ListEvent<T>
            {
                Type = ListEventType.Clear,
                Index = -1
            });
        }

        /// <summary>
        /// 判断列表是否包含指定元素。
        /// </summary>
        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        /// <summary>
        /// 返回指定元素第一次出现的位置；不存在时返回 -1。
        /// </summary>
        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        /// <summary>
        /// 订阅列表结构变化。
        /// </summary>
        /// <param name="onListChanged">变化回调。</param>
        /// <returns>可手动注销或绑定 Unity 生命周期的句柄。</returns>
        public IUnRegister Register(Action<ListEvent<T>> onListChanged)
        {
            if (onListChanged == null)
            {
                LogKit.LogError($"[BindableList] 注册失败: 回调为空, ValueType={typeof(T).Name}");
                return new CustomUnRegister(null);
            }

            return AddNode(onListChanged);
        }

        /// <summary>
        /// 在列表内容没有通过本类型 API 改变、但 View 需要重新读取全部状态时主动发出刷新通知。
        /// </summary>
        /// <remarks>
        /// 当前实现使用 Replace + Index=-1 表示“整体刷新”，调用方不应把它解释成真实单项 Replace。
        /// </remarks>
        public void NotifyRefresh()
        {
            if (_isNotifying)
            {
                LogKit.LogError(
                    $"[BindableList] NotifyRefresh 失败: 当前正在通知中，禁止嵌套刷新, ValueType={typeof(T).Name}, Count={_list.Count}");
                return;
            }

            Notify(new ListEvent<T>
            {
                Type = ListEventType.Replace,
                Index = -1
            });
        }

        /// <summary>
        /// 注销该列表上的全部订阅者。
        /// 通知进行中调用时会延迟到当前遍历结束后清理。
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
                    $"[BindableList] {apiName} 失败: 禁止在通知回调中修改集合, ValueType={typeof(T).Name}, Count={_list.Count}");
                return false;
            }

            return true;
        }

        private void Notify(ListEvent<T> e)
        {
            if (_isNotifying)
            {
                LogKit.LogError(
                    $"[BindableList] Notify 失败: 检测到递归通知，已阻断, EventType={e.Type}, Index={e.Index}, ValueType={typeof(T).Name}");
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

        private ObserverNode AddNode(Action<ListEvent<T>> action)
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
            public Action<ListEvent<T>> Action;
            public BindableList<T> Owner;
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

            public static ObserverNode Allocate(Action<ListEvent<T>> action, BindableList<T> owner)
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
                    LogKit.LogError($"[BindableList] 生命周期绑定失败: gameObject 为空, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDestroyTrigger(gameObject, out EventUnregisterTrigger trigger))
                {
                    LogKit.LogError(
                        $"[BindableList] 生命周期绑定失败: 无法挂载销毁触发器, TriggerObject={gameObject.name}, ValueType={typeof(T).Name}");
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
                    LogKit.LogError($"[BindableList] 生命周期绑定失败: mono 为空, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                return UnRegisterWhenGameObjectDestroyed(mono.gameObject);
            }

            public IUnRegister UnRegisterWhenDisabled(MonoBehaviour mono)
            {
                if (mono == null || mono.gameObject == null)
                {
                    LogKit.LogError($"[BindableList] 生命周期绑定失败: mono 或 gameObject 为空, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                if (!CustomUnRegister.TryAttachDisableTrigger(mono.gameObject,
                        out EventUnregisterOnDisableTrigger trigger))
                {
                    LogKit.LogError(
                        $"[BindableList] 生命周期绑定失败: 无法挂载失活触发器, TriggerObject={mono.gameObject.name}, ValueType={typeof(T).Name}");
                    UnRegister();
                    return this;
                }

                trigger.Add(this);
                _disableTriggers.Add(trigger);
                return this;
            }
        }

        /// <summary>
        /// 返回底层列表的枚举器。
        /// 枚举期间仍应遵守 List 的常规规则，不要并发修改集合。
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }
}
