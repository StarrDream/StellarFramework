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
        public T OldItem;
        public int Index;
    }

    /// <summary>
    /// 响应式列表
    /// 我只负责列表结构变化通知，不负责列表元素内部字段变化通知。
    /// 这样设计是为了明确边界，避免业务层误以为修改引用对象内部字段也能自动广播。
    /// </summary>
    [Serializable]
    public class BindableList<T> : IEnumerable<T>
    {
        [SerializeField] private List<T> _list = new List<T>();

        private ObserverNode _head;
        private ObserverNode _tail;
        private int _iteratingCount;
        private bool _isNotifying;

        public int Count => _list.Count;

        public T this[int index]
        {
            get => _list[index];
            set
            {
                if (index < 0 || index >= _list.Count)
                {
                    LogKit.LogError(
                        $"[BindableList] 设置元素失败: 索引越界, Index={index}, Count={_list.Count}, 元素类型={typeof(T).Name}");
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

        public void Add(T item)
        {
            _list.Add(item);

            Notify(new ListEvent<T>
            {
                Type = ListEventType.Add,
                Item = item,
                Index = _list.Count - 1
            });
        }

        public bool Remove(T item)
        {
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

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _list.Count)
            {
                LogKit.LogError(
                    $"[BindableList] RemoveAt 失败: 索引越界, Index={index}, Count={_list.Count}, 元素类型={typeof(T).Name}");
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

        public void Clear()
        {
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

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        /// <summary>
        /// 注册列表变化监听
        /// 我要求回调不能为空，否则后续链表节点会成为无意义脏节点。
        /// </summary>
        public IUnRegister Register(Action<ListEvent<T>> onListChanged)
        {
            LogKit.AssertNotNull(onListChanged, $"[BindableList] 注册失败: 回调为空, 元素类型={typeof(T).Name}");
            if (onListChanged == null)
            {
                return new CustomUnRegister(null);
            }

            return AddNode(onListChanged);
        }

        /// <summary>
        /// 主动广播当前列表被刷新
        /// 我提供这个入口是为了支持某些业务在批量修改后手动触发一次刷新，而不暴露内部 Notify 细节。
        /// </summary>
        public void NotifyRefresh()
        {
            Notify(new ListEvent<T>
            {
                Type = ListEventType.Replace,
                Index = -1
            });
        }

        /// <summary>
        /// 清理所有监听
        /// 我在业务域销毁或测试环境重置时需要一个强制清空入口，避免历史监听残留。
        /// </summary>
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

        private void Notify(ListEvent<T> e)
        {
            LogKit.Assert(!_isNotifying,
                $"[BindableList] 致命错误: 检测到递归通知, EventType={e.Type}, Index={e.Index}, 元素类型={typeof(T).Name}");
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

        private ObserverNode AddNode(Action<ListEvent<T>> action)
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
                LogKit.LogError($"[BindableList] RemoveNode 失败: 节点为空, 元素类型={typeof(T).Name}");
                return;
            }

            if (!ReferenceEquals(node.Owner, this))
            {
                LogKit.LogError($"[BindableList] RemoveNode 失败: 节点归属不匹配, 元素类型={typeof(T).Name}");
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
            public Action<ListEvent<T>> Action;
            public BindableList<T> Owner;
            public ObserverNode Previous;
            public ObserverNode Next;
            public bool MarkedForDeletion;

            private static readonly Stack<ObserverNode> Pool = new Stack<ObserverNode>();

            public static ObserverNode Allocate(Action<ListEvent<T>> action, BindableList<T> owner)
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
                    LogKit.LogError($"[BindableList] 生命周期绑定失败: GameObject 为空, 元素类型={typeof(T).Name}");
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