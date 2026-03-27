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

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public IUnRegister Register(Action<ListEvent<T>> onListChanged)
        {
            if (onListChanged == null)
            {
                LogKit.LogError($"[BindableList] 注册失败: 回调为空, ValueType={typeof(T).Name}");
                return new CustomUnRegister(null);
            }

            return AddNode(onListChanged);
        }

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