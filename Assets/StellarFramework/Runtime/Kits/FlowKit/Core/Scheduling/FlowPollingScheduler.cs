using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public enum FlowPollingFrequency
    {
        EveryFrame,
        Hertz30,
        Hertz10,
        Hertz5,
        Hertz1
    }

    public readonly struct FlowPollingHandle : IEquatable<FlowPollingHandle>
    {
        public int Slot { get; }
        public int Generation { get; }
        public bool IsValid => Slot >= 0 && Generation > 0;

        public FlowPollingHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public bool Equals(FlowPollingHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is FlowPollingHandle other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397) ^ Generation);
    }

    public interface IFlowPollingSource
    {
        bool TryPoll(in FlowTimeSnapshot time, out FlowValue value);
    }

    /// <summary>仅轮询已显式注册的外部状态源，按预算推进，不扫描 Flow Graph。</summary>
    public sealed class FlowPollingScheduler
    {
        private sealed class Entry
        {
            internal FlowPollingHandle Handle;
            internal IFlowPollingSource Source;
            internal Action<FlowValue> Callback;
            internal double NextPollUnscaledSeconds;
            internal double IntervalSeconds;
            internal bool Active;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();

        public int ActiveCount { get; private set; }

        public FlowPollingSubscription Subscribe(IFlowPollingSource source, Action<FlowValue> callback) =>
            Subscribe(source, callback, FlowPollingFrequency.EveryFrame);

        /// <summary>
        /// 注册一个明确频率的外部状态源。调度频率使用 Unscaled 时间，不为每个节点创建 Update；
        /// EveryFrame 仅表示每次 Poll 调用尝试一次，实际调用仍受预算限制。
        /// </summary>
        public FlowPollingSubscription Subscribe(
            IFlowPollingSource source,
            Action<FlowValue> callback,
            FlowPollingFrequency frequency)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            double interval = GetIntervalSeconds(frequency);
            int slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : _entries.Count;
            int generation = _generations.TryGetValue(slot, out int previous) ? previous + 1 : 1;
            _generations[slot] = generation;
            var entry = new Entry
            {
                Handle = new FlowPollingHandle(slot, generation),
                Source = source,
                Callback = callback,
                NextPollUnscaledSeconds = double.NegativeInfinity,
                IntervalSeconds = interval,
                Active = true
            };
            if (slot == _entries.Count) _entries.Add(entry);
            else _entries[slot] = entry;
            ActiveCount++;
            return new FlowPollingSubscription(this, entry.Handle);
        }

        public int Poll(in FlowTimeSnapshot time, int maxCallbacks)
        {
            if (maxCallbacks <= 0) return 0;
            int callbacks = 0;
            for (int i = 0; i < _entries.Count && callbacks < maxCallbacks; i++)
            {
                Entry entry = _entries[i];
                if (!entry.Active) continue;
                if (time.UnscaledSeconds < entry.NextPollUnscaledSeconds) continue;
                entry.NextPollUnscaledSeconds = time.UnscaledSeconds + entry.IntervalSeconds;
                if (entry.Source.TryPoll(in time, out FlowValue value))
                {
                    entry.Callback(value);
                    callbacks++;
                }
            }

            return callbacks;
        }

        internal void Unsubscribe(FlowPollingHandle handle)
        {
            if (!handle.IsValid || handle.Slot >= _entries.Count) return;
            Entry entry = _entries[handle.Slot];
            if (!entry.Active || entry.Handle.Generation != handle.Generation) return;
            entry.Active = false;
            entry.Source = null;
            entry.Callback = null;
            _freeSlots.Push(handle.Slot);
            ActiveCount--;
        }

        private static double GetIntervalSeconds(FlowPollingFrequency frequency)
        {
            switch (frequency)
            {
                case FlowPollingFrequency.EveryFrame: return 0d;
                case FlowPollingFrequency.Hertz30: return 1d / 30d;
                case FlowPollingFrequency.Hertz10: return 0.1d;
                case FlowPollingFrequency.Hertz5: return 0.2d;
                case FlowPollingFrequency.Hertz1: return 1d;
                default: throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "未知的 Polling 频率。");
            }
        }
    }

    public sealed class FlowPollingSubscription : IDisposable
    {
        private FlowPollingScheduler _scheduler;
        private readonly FlowPollingHandle _handle;

        internal FlowPollingSubscription(FlowPollingScheduler scheduler, FlowPollingHandle handle)
        {
            _scheduler = scheduler;
            _handle = handle;
        }

        public void Dispose()
        {
            if (_scheduler == null) return;
            _scheduler.Unsubscribe(_handle);
            _scheduler = null;
        }
    }
}
