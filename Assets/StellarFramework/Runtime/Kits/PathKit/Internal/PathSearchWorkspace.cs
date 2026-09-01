using System;
using System.Collections.Generic;

namespace StellarFramework
{
    /// <summary>Reusable search storage owned by one non-reentrant pathfinder.</summary>
    internal sealed class PathSearchWorkspace
    {
        private readonly Dictionary<PathNodeId, int> _nodeToRecordIndex;
        private PathRecord[] _records;
        private int[] _openHeap;
        private int _recordCount;
        private int _openCount;

        internal PathSearchWorkspace(int initialCapacity)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity,
                    "Initial capacity cannot be negative.");
            }

            _nodeToRecordIndex = initialCapacity > 0
                ? new Dictionary<PathNodeId, int>(initialCapacity)
                : new Dictionary<PathNodeId, int>();
            if (initialCapacity > 0)
            {
                _records = new PathRecord[initialCapacity];
                _openHeap = new int[initialCapacity];
            }
            else
            {
                _records = Array.Empty<PathRecord>();
                _openHeap = Array.Empty<int>();
            }
        }

        internal int RecordCount => _recordCount;

        internal ref PathRecord GetRecord(int index) => ref _records[index];

        internal void Begin()
        {
            _nodeToRecordIndex.Clear();
            _recordCount = 0;
            _openCount = 0;
        }

        internal bool TryGetRecordIndex(PathNodeId node, out int index)
        {
            return _nodeToRecordIndex.TryGetValue(node, out index);
        }

        internal int AddRecord(PathRecord record)
        {
            EnsureRecordCapacity(_recordCount + 1);
            int index = _recordCount++;
            _records[index] = record;
            _nodeToRecordIndex.Add(record.Node, index);
            return index;
        }

        internal void PushOpen(int recordIndex, bool useHeuristic)
        {
            EnsureHeapCapacity(_openCount + 1);
            int heapIndex = _openCount++;
            _openHeap[heapIndex] = recordIndex;
            _records[recordIndex].State = PathRecordState.Open;
            _records[recordIndex].OpenHeapIndex = heapIndex;
            SiftUp(heapIndex, useHeuristic);
        }

        internal int PopOpen(bool useHeuristic)
        {
            if (_openCount == 0) throw new InvalidOperationException("The open heap is empty.");

            int result = _openHeap[0];
            _openCount--;
            _records[result].OpenHeapIndex = -1;
            if (_openCount > 0)
            {
                int replacement = _openHeap[_openCount];
                _openHeap[0] = replacement;
                _records[replacement].OpenHeapIndex = 0;
                SiftDown(0, useHeuristic);
            }

            return result;
        }

        internal int OpenCount => _openCount;

        internal void DecreaseOpenKey(int recordIndex, bool useHeuristic)
        {
            int heapIndex = _records[recordIndex].OpenHeapIndex;
            if (heapIndex < 0 || heapIndex >= _openCount)
            {
                throw new InvalidOperationException("Cannot decrease the key of a record that is not open.");
            }

            SiftUp(heapIndex, useHeuristic);
        }

        private void SiftUp(int index, bool useHeuristic)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsHigherPriority(_openHeap[index], _openHeap[parent], useHeuristic)) break;
                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index, bool useHeuristic)
        {
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= _openCount) return;
                int right = left + 1;
                int best = left;
                if (right < _openCount && IsHigherPriority(_openHeap[right], _openHeap[left], useHeuristic))
                {
                    best = right;
                }

                if (!IsHigherPriority(_openHeap[best], _openHeap[index], useHeuristic)) return;
                Swap(index, best);
                index = best;
            }
        }

        private bool IsHigherPriority(int leftIndex, int rightIndex, bool useHeuristic)
        {
            PathRecord left = _records[leftIndex];
            PathRecord right = _records[rightIndex];
            if (useHeuristic)
            {
                if (left.F != right.F) return left.F < right.F;
                if (left.H != right.H) return left.H < right.H;
            }
            else if (left.G != right.G)
            {
                return left.G < right.G;
            }

            return left.Node.Value < right.Node.Value;
        }

        private void Swap(int leftHeapIndex, int rightHeapIndex)
        {
            int leftRecord = _openHeap[leftHeapIndex];
            int rightRecord = _openHeap[rightHeapIndex];
            _openHeap[leftHeapIndex] = rightRecord;
            _openHeap[rightHeapIndex] = leftRecord;
            _records[leftRecord].OpenHeapIndex = rightHeapIndex;
            _records[rightRecord].OpenHeapIndex = leftHeapIndex;
        }

        private void EnsureRecordCapacity(int required)
        {
            if (required <= _records.Length) return;
            int next = NextCapacity(_records.Length, required);
            Array.Resize(ref _records, next);
        }

        private void EnsureHeapCapacity(int required)
        {
            if (required <= _openHeap.Length) return;
            int next = NextCapacity(_openHeap.Length, required);
            Array.Resize(ref _openHeap, next);
        }

        private static int NextCapacity(int current, int required)
        {
            int next = current == 0 ? 16 : current * 2;
            if (next < 0 || next < required) next = required;
            return next;
        }
    }
}
