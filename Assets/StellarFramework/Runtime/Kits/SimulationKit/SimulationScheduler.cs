using System;
using System.Collections.Generic;

namespace StellarFramework
{
    /// <summary>
    /// 基于索引最小堆的批量模拟调度器。
    /// </summary>
    /// <remarks>
    /// 调度器只管理 ID、间隔和下一次到期 tick。它不拥有业务对象，也不执行回调。
    /// 同一个实例上的时间必须单调不减；不同实例可以使用不同时间线。
    /// </remarks>
    public sealed class SimulationScheduler
    {
        private struct SimulationEntry
        {
            public SimulationId Id;
            public long IntervalTicks;
            public long NextDueTick;

            public SimulationEntry(SimulationId id, long intervalTicks, long nextDueTick)
            {
                Id = id;
                IntervalTicks = intervalTicks;
                NextDueTick = nextDueTick;
            }
        }

        private SimulationEntry[] _heap;
        private int _heapCount;
        private readonly Dictionary<SimulationId, int> _indices;
        private long _lastObservedTick;
        private bool _hasObservedTick;

        public SimulationScheduler(int initialCapacity = 0)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            int capacity = initialCapacity == 0 ? 4 : initialCapacity;
            _heap = new SimulationEntry[capacity];
            _indices = new Dictionary<SimulationId, int>(capacity);
        }

        /// <summary>当前注册项数量。</summary>
        public int Count => _heapCount;

        public bool Contains(SimulationId id) => id.IsValid && _indices.ContainsKey(id);

        public bool TryGetInterval(SimulationId id, out long intervalTicks)
        {
            if (id.IsValid && _indices.TryGetValue(id, out int index))
            {
                intervalTicks = _heap[index].IntervalTicks;
                return true;
            }

            intervalTicks = default(long);
            return false;
        }

        public bool TryGetNextDueTick(SimulationId id, out long nextDueTick)
        {
            if (id.IsValid && _indices.TryGetValue(id, out int index))
            {
                nextDueTick = _heap[index].NextDueTick;
                return true;
            }

            nextDueTick = default(long);
            return false;
        }

        public SimulationMutationResult TryRegister(SimulationId id, long nowTick, long intervalTicks)
        {
            return TryRegisterInternal(id, nowTick, intervalTicks, intervalTicks, false);
        }

        public SimulationMutationResult TryRegister(SimulationId id, long nowTick, long intervalTicks,
            long firstDelayTicks)
        {
            return TryRegisterInternal(id, nowTick, intervalTicks, firstDelayTicks, true);
        }

        public SimulationMutationResult TryUnregister(SimulationId id)
        {
            if (!id.IsValid)
            {
                return SimulationMutationResult.Failed(SimulationMutationError.InvalidId);
            }

            if (!_indices.TryGetValue(id, out int index))
            {
                return SimulationMutationResult.Failed(SimulationMutationError.NotFound);
            }

            int lastIndex = _heapCount - 1;
            _indices.Remove(id);
            _heapCount = lastIndex;
            if (index < lastIndex)
            {
                _heap[index] = _heap[lastIndex];
                _indices[_heap[index].Id] = index;
                if (index > 0 && IsLess(_heap[index], _heap[(index - 1) / 2]))
                {
                    HeapifyUp(index);
                }
                else
                {
                    HeapifyDown(index);
                }
            }

            return SimulationMutationResult.Succeeded();
        }

        public SimulationMutationResult TrySetInterval(SimulationId id, long nowTick, long newIntervalTicks)
        {
            ObserveTick(nowTick);

            if (!id.IsValid)
            {
                return SimulationMutationResult.Failed(SimulationMutationError.InvalidId);
            }

            if (newIntervalTicks <= 0)
            {
                return SimulationMutationResult.Failed(SimulationMutationError.InvalidInterval);
            }

            if (!_indices.TryGetValue(id, out int index))
            {
                return SimulationMutationResult.Failed(SimulationMutationError.NotFound);
            }

            if (!TryAdd(nowTick, newIntervalTicks, out long nextDueTick))
            {
                return SimulationMutationResult.Failed(SimulationMutationError.TickOverflow);
            }

            SimulationEntry entry = _heap[index];
            entry.IntervalTicks = newIntervalTicks;
            entry.NextDueTick = nextDueTick;
            _heap[index] = entry;
            if (index > 0 && IsLess(_heap[index], _heap[(index - 1) / 2]))
            {
                HeapifyUp(index);
            }
            else
            {
                HeapifyDown(index);
            }

            return SimulationMutationResult.Succeeded();
        }

        /// <summary>
        /// 将当前 tick 已到期的 ID 写入 destination，最多写入 destination.Length 个。
        /// 过期项按当前 dispatch tick 合并为一次，并从当前 tick 重新计算下一次到期时间。
        /// </summary>
        public SimulationCollectResult CollectDue(long nowTick, Span<SimulationId> destination)
        {
            ObserveTick(nowTick);
            int written = 0;
            while (written < destination.Length && _heapCount > 0 && _heap[0].NextDueTick <= nowTick)
            {
                SimulationEntry entry = _heap[0];
                if (!TryAdd(nowTick, entry.IntervalTicks, out long nextDueTick))
                {
                    // 先检查，再写出和修改 root，保证当前溢出项是原子失败。
                    throw new OverflowException("SimulationScheduler 的下一次到期 tick 溢出。");
                }

                destination[written++] = entry.Id;
                entry.NextDueTick = nextDueTick;
                _heap[0] = entry;
                HeapifyDown(0);
            }

            return new SimulationCollectResult(written, _heapCount > 0 && _heap[0].NextDueTick <= nowTick);
        }

        /// <summary>移除全部注册项并重置时间线，保留已经分配的容量。</summary>
        public void Clear()
        {
            _heapCount = 0;
            _indices.Clear();
            _lastObservedTick = default(long);
            _hasObservedTick = false;
        }

        private SimulationMutationResult TryRegisterInternal(SimulationId id, long nowTick, long intervalTicks,
            long firstDelayTicks, bool hasExplicitDelay)
        {
            ObserveTick(nowTick);

            if (!id.IsValid)
            {
                return SimulationMutationResult.Failed(SimulationMutationError.InvalidId);
            }

            if (_indices.ContainsKey(id))
            {
                return SimulationMutationResult.Failed(SimulationMutationError.DuplicateId);
            }

            if (intervalTicks <= 0)
            {
                return SimulationMutationResult.Failed(SimulationMutationError.InvalidInterval);
            }

            if (hasExplicitDelay && firstDelayTicks < 0)
            {
                return SimulationMutationResult.Failed(SimulationMutationError.InvalidDelay);
            }

            if (!TryAdd(nowTick, hasExplicitDelay ? firstDelayTicks : intervalTicks, out long nextDueTick))
            {
                return SimulationMutationResult.Failed(SimulationMutationError.TickOverflow);
            }

            EnsureCapacity(_heapCount + 1);
            int index = _heapCount++;
            _heap[index] = new SimulationEntry(id, intervalTicks, nextDueTick);
            _indices.Add(id, index);
            HeapifyUp(index);
            return SimulationMutationResult.Succeeded();
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _heap.Length)
            {
                return;
            }

            int nextCapacity = _heap.Length == 0 ? 4 : _heap.Length * 2;
            if (nextCapacity < required)
            {
                nextCapacity = required;
            }

            Array.Resize(ref _heap, nextCapacity);
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsLess(_heap[index], _heap[parent]))
                {
                    break;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private void HeapifyDown(int index)
        {
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= _heapCount)
                {
                    return;
                }

                int right = left + 1;
                int smallest = left;
                if (right < _heapCount && IsLess(_heap[right], _heap[left]))
                {
                    smallest = right;
                }

                if (!IsLess(_heap[smallest], _heap[index]))
                {
                    return;
                }

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int first, int second)
        {
            SimulationEntry value = _heap[first];
            _heap[first] = _heap[second];
            _heap[second] = value;
            _indices[_heap[first].Id] = first;
            _indices[_heap[second].Id] = second;
        }

        private static bool IsLess(SimulationEntry left, SimulationEntry right)
        {
            if (left.NextDueTick != right.NextDueTick)
            {
                return left.NextDueTick < right.NextDueTick;
            }

            return left.Id.Value < right.Id.Value;
        }

        private void ObserveTick(long nowTick)
        {
            if (_hasObservedTick && nowTick < _lastObservedTick)
            {
                throw new InvalidOperationException("SimulationScheduler 的时间线不能回退。");
            }

            _lastObservedTick = nowTick;
            _hasObservedTick = true;
        }

        private static bool TryAdd(long left, long right, out long result)
        {
            if (right < 0 || left > long.MaxValue - right)
            {
                result = default(long);
                return false;
            }

            result = left + right;
            return true;
        }
    }
}
