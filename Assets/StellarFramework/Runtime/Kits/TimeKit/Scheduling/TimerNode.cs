using System;

namespace StellarFramework
{
    /// <summary>Timer Slot 的执行状态。</summary>
    internal enum TimerState : byte
    {
        Free = 0,
        Scheduled = 1,
        Executing = 2,
        CancelRequested = 3
    }

    /// <summary>Timer 回调存储形式。</summary>
    internal enum TimerCallbackKind : byte
    {
        Action = 0,
        ContextAction = 1,
        Receiver = 2
    }

    /// <summary>
    /// 连续 Slot 数组中的 Timer 数据。Heap 只保存本结构所在的 Slot Id，
    /// 使取消与重排不会复制业务委托或接收者引用。
    /// </summary>
    internal struct TimerNode
    {
        internal long TriggerTick;
        internal long IntervalTicks;
        internal ulong Sequence;
        internal int HeapIndex;
        internal int NextFree;
        internal int RemainingExecutions;
        internal uint Version;
        internal TimerState State;
        internal TimerCatchUpPolicy CatchUpPolicy;
        internal TimerCallbackKind CallbackKind;
        internal Action Callback;
        internal Action<TimeTriggerContext> ContextCallback;
        internal ITimeEventReceiver Receiver;
        internal int EventId;

        internal void ClearManagedReferences()
        {
            Callback = null;
            ContextCallback = null;
            Receiver = null;
        }
    }
}
