namespace StellarFramework
{
    /// <summary>TimeKit 的低成本运行时诊断快照。</summary>
    public readonly struct TimeKitDiagnosticsSnapshot
    {
        /// <summary>活动 Timer 数。</summary>
        public int ActiveTimerCount { get; }
        /// <summary>Heap 中的 Timer 数。</summary>
        public int HeapCount { get; }
        /// <summary>Slot 容量。</summary>
        public int Capacity { get; }
        /// <summary>可复用 Slot 数。</summary>
        public int FreeSlotCount { get; }
        /// <summary>历史峰值活动 Timer 数。</summary>
        public int PeakActiveTimerCount { get; }
        /// <summary>累计注册数。</summary>
        public long TotalScheduledCount { get; }
        /// <summary>累计取消数。</summary>
        public long TotalCancelledCount { get; }
        /// <summary>累计实际回调执行数。</summary>
        public long TotalExecutedCount { get; }
        /// <summary>累计回调异常数。</summary>
        public long TotalErrorCount { get; }
        /// <summary>当前帧已知至少仍有一个到期任务等待处理时为 1，否则为 0。</summary>
        public int DueBacklogCount { get; }
        /// <summary>最近一次处理阶段实际执行的回调数。</summary>
        public int CallbacksExecutedLastUpdate { get; }
        /// <summary>开发诊断中的 Heap 比较次数。</summary>
        public long HeapComparisons { get; }
        /// <summary>开发诊断中的 Heap 交换次数。</summary>
        public long HeapSwaps { get; }

        internal TimeKitDiagnosticsSnapshot(TimeKitDiagnostics diagnostics, IndexedMinHeap heap, TimerSlotPool slots)
        {
            ActiveTimerCount = diagnostics.ActiveTimerCount;
            HeapCount = heap.Count;
            Capacity = slots.Capacity;
            FreeSlotCount = slots.FreeCount;
            PeakActiveTimerCount = diagnostics.PeakActiveTimerCount;
            TotalScheduledCount = diagnostics.TotalScheduledCount;
            TotalCancelledCount = diagnostics.TotalCancelledCount;
            TotalExecutedCount = diagnostics.TotalExecutedCount;
            TotalErrorCount = diagnostics.TotalErrorCount;
            DueBacklogCount = diagnostics.DueBacklogCount;
            CallbacksExecutedLastUpdate = diagnostics.CallbacksExecutedLastUpdate;
            HeapComparisons = heap.ComparisonCount;
            HeapSwaps = heap.SwapCount;
        }
    }

    internal sealed class TimeKitDiagnostics
    {
        internal int ActiveTimerCount;
        internal int PeakActiveTimerCount;
        internal long TotalScheduledCount;
        internal long TotalCancelledCount;
        internal long TotalExecutedCount;
        internal long TotalErrorCount;
        internal int DueBacklogCount;
        internal int CallbacksExecutedLastUpdate;

        internal void Reset()
        {
            ActiveTimerCount = 0;
            PeakActiveTimerCount = 0;
            TotalScheduledCount = 0L;
            TotalCancelledCount = 0L;
            TotalExecutedCount = 0L;
            TotalErrorCount = 0L;
            DueBacklogCount = 0;
            CallbacksExecutedLastUpdate = 0;
        }
    }
}
