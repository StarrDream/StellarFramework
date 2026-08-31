using System;

namespace StellarFramework
{
    /// <summary>
    /// TimerNode 的连续数组 Slot Pool。Free Slot 只通过 NextFree 链接，
    /// 不使用 class 对象池，以避免高频注册/取消中的对象分配与引用追踪。
    /// </summary>
    internal sealed class TimerSlotPool
    {
        private TimerNode[] _nodes;
        private int _allocatedSlotCount;
        private int _freeHead;
        private int _freeCount;

        internal TimerSlotPool(int initialCapacity)
        {
            int capacity = Math.Max(1, initialCapacity);
            _nodes = new TimerNode[capacity + 1]; // Slot 0 永远无效，便于 Handle 校验。
        }

        internal int Capacity => _nodes.Length - 1;
        internal int FreeCount => _freeCount;
        internal int AllocatedSlotCount => _allocatedSlotCount;

        internal int Allocate()
        {
            int id;
            if (_freeHead != 0)
            {
                id = _freeHead;
                ref TimerNode reused = ref _nodes[id];
                _freeHead = reused.NextFree;
                _freeCount--;
                reused.NextFree = 0;
                reused.HeapIndex = -1;
                reused.State = TimerState.Scheduled;
                return id;
            }

            if (_allocatedSlotCount >= Capacity)
            {
                Grow(Capacity * 2);
            }

            id = ++_allocatedSlotCount;
            ref TimerNode node = ref _nodes[id];
            node.Version = node.Version == 0U ? 1U : node.Version;
            node.HeapIndex = -1;
            node.State = TimerState.Scheduled;
            return id;
        }

        internal void Reserve(int capacity)
        {
            if (capacity > Capacity)
            {
                Grow(capacity);
            }
        }

        internal ref TimerNode GetNode(int id) => ref _nodes[id];

        internal bool IsActive(int id, uint version)
        {
            return id > 0 && id <= _allocatedSlotCount && _nodes[id].Version == version &&
                   _nodes[id].State != TimerState.Free;
        }

        internal void Release(int id)
        {
            ref TimerNode node = ref _nodes[id];
            node.ClearManagedReferences();
            node.TriggerTick = 0L;
            node.IntervalTicks = 0L;
            node.Sequence = 0UL;
            node.HeapIndex = -1;
            node.RemainingExecutions = 0;
            node.CatchUpPolicy = TimerCatchUpPolicy.Latest;
            node.CallbackKind = TimerCallbackKind.Action;
            node.EventId = 0;
            node.State = TimerState.Free;
            node.Version = NextVersion(node.Version);
            node.NextFree = _freeHead;
            _freeHead = id;
            _freeCount++;
        }

        internal bool ValidateFreeNode(int id)
        {
            ref TimerNode node = ref _nodes[id];
            return node.State == TimerState.Free && node.HeapIndex == -1 && node.Callback == null &&
                   node.ContextCallback == null && node.Receiver == null;
        }

        private void Grow(int requestedCapacity)
        {
            int newCapacity = Math.Max(requestedCapacity, Capacity + 1);
            Array.Resize(ref _nodes, newCapacity + 1);
        }

        private static uint NextVersion(uint version)
        {
            return version == uint.MaxValue ? 1U : version + 1U;
        }
    }
}
