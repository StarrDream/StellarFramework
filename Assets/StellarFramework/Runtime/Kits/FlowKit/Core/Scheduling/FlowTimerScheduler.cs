using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public interface IFlowTimeScheduler
    {
        FlowTimerHandle Schedule(FlowDuration delay, FlowTimeDomain domain, Action callback);
        bool Cancel(FlowTimerHandle handle);
    }

    /// <summary>
    /// 按时间堆调度一次性回调。时间推进由宿主显式提供，避免 Runner 扫描整个节点图。
    /// </summary>
    public sealed class FlowTimerScheduler : IFlowTimeScheduler
    {
        private sealed class TimerEntry
        {
            internal FlowTimerHandle Handle;
            internal FlowTimeDomain Domain;
            internal double DueTime;
            internal long Sequence;
            internal Action<FlowTimerHandle> Callback;
            internal bool Active;
        }

        // Each time domain has its own heap. Scaled/Unscaled/FlowTime are
        // independent clocks; a single heap ordered by raw DueTime could let
        // an unscaled timer be hidden behind an unrelated scaled timer.
        private readonly List<TimerEntry>[] _heaps =
        {
            new List<TimerEntry>(),
            new List<TimerEntry>(),
            new List<TimerEntry>()
        };
        private readonly Dictionary<int, TimerEntry> _active = new Dictionary<int, TimerEntry>();
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();
        private readonly double[] _currentTimes = new double[3];
        private long _sequence;
        private int _nextSlot;

        public int ActiveCount => _active.Count;

        /// <summary>按最近一次 Advance 观察到的时间，使用相对时长创建定时器。</summary>
        public FlowTimerHandle Schedule(FlowDuration delay, FlowTimeDomain domain, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (domain < FlowTimeDomain.Scaled || domain > FlowTimeDomain.FlowTime)
                throw new ArgumentOutOfRangeException(nameof(domain), domain, "未知的 FlowTimeDomain。");
            return Schedule(domain, _currentTimes[(int)domain] + delay.Seconds, _ => callback());
        }

        public FlowTimerHandle Schedule(
            FlowTimeDomain domain,
            double dueTime,
            Action<FlowTimerHandle> callback)
        {
            if (domain < FlowTimeDomain.Scaled || domain > FlowTimeDomain.FlowTime)
                throw new ArgumentOutOfRangeException(nameof(domain), domain, "未知的 FlowTimeDomain。");
            if (double.IsNaN(dueTime) || double.IsInfinity(dueTime))
                throw new ArgumentOutOfRangeException(nameof(dueTime), "定时器时间必须是有限数值。");
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            int slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : _nextSlot++;
            int generation = _generations.TryGetValue(slot, out int previous) ? previous + 1 : 1;
            _generations[slot] = generation;
            var entry = new TimerEntry
            {
                Handle = new FlowTimerHandle(slot, generation),
                Domain = domain,
                DueTime = dueTime,
                Sequence = ++_sequence,
                Callback = callback,
                Active = true
            };
            _active.Add(slot, entry);
            Push(_heaps[(int)domain], entry);
            return entry.Handle;
        }

        public bool Cancel(FlowTimerHandle handle)
        {
            if (!handle.IsValid || !_active.TryGetValue(handle.Slot, out TimerEntry entry) ||
                entry.Handle.Generation != handle.Generation)
            {
                return false;
            }

            entry.Active = false;
            _active.Remove(handle.Slot);
            _freeSlots.Push(handle.Slot);
            return true;
        }

        /// <summary>推进指定时间域，最多执行 maxCallbacks 个到期回调。</summary>
        public int Advance(FlowTimeSnapshot time, int maxCallbacks)
        {
            if (maxCallbacks <= 0) return 0;
            int fired = 0;
            for (int domainIndex = 0; domainIndex < _heaps.Length && fired < maxCallbacks; domainIndex++)
            {
                List<TimerEntry> heap = _heaps[domainIndex];
                FlowTimeDomain domain = (FlowTimeDomain)domainIndex;
                _currentTimes[domainIndex] = time.Get(domain);
                while (fired < maxCallbacks && heap.Count > 0)
                {
                    TimerEntry entry = heap[0];
                    if (!entry.Active)
                    {
                        Pop(heap);
                        continue;
                    }

                    double now = time.Get(domain);
                    if (entry.DueTime > now) break;
                    Pop(heap);
                    if (!_active.Remove(entry.Handle.Slot)) continue;
                    entry.Active = false;
                    _freeSlots.Push(entry.Handle.Slot);
                    entry.Callback(entry.Handle);
                    fired++;
                }
            }

            return fired;
        }

        private static void Push(List<TimerEntry> heap, TimerEntry entry)
        {
            heap.Add(entry);
            int index = heap.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(heap[parent], entry) <= 0) break;
                heap[index] = heap[parent];
                index = parent;
            }

            heap[index] = entry;
        }

        private static TimerEntry Pop(List<TimerEntry> heap)
        {
            TimerEntry result = heap[0];
            int last = heap.Count - 1;
            TimerEntry tail = heap[last];
            heap.RemoveAt(last);
            if (heap.Count == 0) return result;

            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= heap.Count) break;
                int right = left + 1;
                int child = right < heap.Count && Compare(heap[right], heap[left]) < 0 ? right : left;
                if (Compare(heap[child], tail) >= 0) break;
                heap[index] = heap[child];
                index = child;
            }

            heap[index] = tail;
            return result;
        }

        private static int Compare(TimerEntry left, TimerEntry right)
        {
            int time = left.DueTime.CompareTo(right.DueTime);
            return time != 0 ? time : left.Sequence.CompareTo(right.Sequence);
        }
    }
}
