using System;

namespace StellarFramework
{
    /// <summary>
    /// 存储 Timer Slot Id 的索引最小堆。核心不变量：heap[i] 对应节点的 HeapIndex 必须等于 i，
    /// 且父节点按 TriggerTick + Sequence 的排序键不晚于子节点。
    /// </summary>
    internal sealed class IndexedMinHeap
    {
        private readonly TimerSlotPool _slots;
        private int[] _nodeIds;
        private int _count;
        private long _comparisonCount;
        private long _swapCount;

        internal IndexedMinHeap(TimerSlotPool slots, int initialCapacity)
        {
            _slots = slots;
            _nodeIds = new int[Math.Max(2, initialCapacity + 1)];
        }

        internal int Count => _count;
        internal long ComparisonCount => _comparisonCount;
        internal long SwapCount => _swapCount;

        internal void Push(int nodeId)
        {
            if (_count + 1 >= _nodeIds.Length)
            {
                Array.Resize(ref _nodeIds, _nodeIds.Length * 2);
            }

            int index = ++_count;
            _nodeIds[index] = nodeId;
            _slots.GetNode(nodeId).HeapIndex = index;
            SiftUp(index);
        }

        internal int PeekNodeId() => _count > 0 ? _nodeIds[1] : 0;

        internal int PopMin()
        {
            return _count == 0 ? 0 : RemoveAt(1);
        }

        internal bool Remove(int nodeId)
        {
            ref TimerNode node = ref _slots.GetNode(nodeId);
            int index = node.HeapIndex;
            if (index < 1 || index > _count || _nodeIds[index] != nodeId)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        internal void Clear()
        {
            for (int i = 1; i <= _count; i++)
            {
                _slots.GetNode(_nodeIds[i]).HeapIndex = -1;
            }

            _count = 0;
        }

        internal void ResetDiagnostics()
        {
            _comparisonCount = 0L;
            _swapCount = 0L;
        }

        internal bool ValidateInvariants()
        {
            for (int index = 1; index <= _count; index++)
            {
                int nodeId = _nodeIds[index];
                if (_slots.GetNode(nodeId).HeapIndex != index)
                {
                    return false;
                }

                int left = index * 2;
                int right = left + 1;
                if (left <= _count && Compare(_nodeIds[left], nodeId) < 0 ||
                    right <= _count && Compare(_nodeIds[right], nodeId) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private int RemoveAt(int index)
        {
            int removedId = _nodeIds[index];
            int lastId = _nodeIds[_count--];
            _slots.GetNode(removedId).HeapIndex = -1;
            if (index <= _count)
            {
                _nodeIds[index] = lastId;
                _slots.GetNode(lastId).HeapIndex = index;
                int parent = index / 2;
                if (parent > 0 && Compare(lastId, _nodeIds[parent]) < 0)
                {
                    SiftUp(index);
                }
                else
                {
                    SiftDown(index);
                }
            }

            return removedId;
        }

        private void SiftUp(int index)
        {
            while (index > 1)
            {
                int parent = index / 2;
                if (Compare(_nodeIds[index], _nodeIds[parent]) >= 0)
                {
                    break;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = index * 2;
                if (left > _count)
                {
                    return;
                }

                int smallest = left;
                int right = left + 1;
                if (right <= _count && Compare(_nodeIds[right], _nodeIds[left]) < 0)
                {
                    smallest = right;
                }

                if (Compare(_nodeIds[smallest], _nodeIds[index]) >= 0)
                {
                    return;
                }

                Swap(index, smallest);
                index = smallest;
            }
        }

        private int Compare(int leftId, int rightId)
        {
            _comparisonCount++;
            ref TimerNode left = ref _slots.GetNode(leftId);
            ref TimerNode right = ref _slots.GetNode(rightId);
            if (left.TriggerTick != right.TriggerTick)
            {
                return left.TriggerTick < right.TriggerTick ? -1 : 1;
            }

            if (left.Sequence == right.Sequence)
            {
                return 0;
            }

            return left.Sequence < right.Sequence ? -1 : 1;
        }

        private void Swap(int leftIndex, int rightIndex)
        {
            int leftId = _nodeIds[leftIndex];
            int rightId = _nodeIds[rightIndex];
            _nodeIds[leftIndex] = rightId;
            _nodeIds[rightIndex] = leftId;
            _slots.GetNode(leftId).HeapIndex = rightIndex;
            _slots.GetNode(rightId).HeapIndex = leftIndex;
            _swapCount++;
        }
    }
}
