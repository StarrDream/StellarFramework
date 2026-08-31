using System;

namespace StellarFramework
{
    /// <summary>
    /// 基于 Slot Pool 与 Indexed MinHeap 的未来时刻调度器。
    /// 不变量：Scheduled 节点恰好在 Heap 中一次；Executing 节点已从 Heap 移除；
    /// Free 节点不持有任何业务引用，且旧 Handle 的 Version 不再匹配。
    /// </summary>
    internal sealed class TimeScheduler
    {
        private readonly TimerSlotPool _slots;
        private readonly IndexedMinHeap _heap;
        private readonly TimeKitDiagnostics _diagnostics;
        private ulong _nextSequence;
        private int _executingNodeId;
        private bool _isProcessing;

        internal TimeScheduler(int initialCapacity, TimeKitDiagnostics diagnostics)
        {
            _slots = new TimerSlotPool(initialCapacity);
            _heap = new IndexedMinHeap(_slots, initialCapacity);
            _diagnostics = diagnostics;
        }

        internal bool IsProcessing => _isProcessing;
        internal int ActiveTimerCount => _diagnostics.ActiveTimerCount;

        internal TimerHandle Schedule(long triggerTick, long intervalTicks, int remainingExecutions,
            TimerCatchUpPolicy catchUpPolicy, Action callback)
        {
            if (callback == null)
            {
                return TimerHandle.Invalid;
            }

            return ScheduleInternal(triggerTick, intervalTicks, remainingExecutions, catchUpPolicy,
                TimerCallbackKind.Action, callback, null, null, 0);
        }

        internal TimerHandle Schedule(long triggerTick, long intervalTicks, int remainingExecutions,
            TimerCatchUpPolicy catchUpPolicy, Action<TimeTriggerContext> callback)
        {
            if (callback == null)
            {
                return TimerHandle.Invalid;
            }

            return ScheduleInternal(triggerTick, intervalTicks, remainingExecutions, catchUpPolicy,
                TimerCallbackKind.ContextAction, null, callback, null, 0);
        }

        internal TimerHandle Schedule(long triggerTick, long intervalTicks, int remainingExecutions,
            TimerCatchUpPolicy catchUpPolicy, ITimeEventReceiver receiver, int eventId)
        {
            if (receiver == null)
            {
                return TimerHandle.Invalid;
            }

            return ScheduleInternal(triggerTick, intervalTicks, remainingExecutions, catchUpPolicy,
                TimerCallbackKind.Receiver, null, null, receiver, eventId);
        }

        internal bool Cancel(TimerHandle handle)
        {
            if (!_slots.IsActive(handle.Id, handle.Version))
            {
                return false;
            }

            ref TimerNode node = ref _slots.GetNode(handle.Id);
            if (node.State == TimerState.Scheduled)
            {
                if (!_heap.Remove(handle.Id))
                {
                    LogKit.LogError("[TimeKit] Cancel 发现 Scheduled 节点不在 Heap 中。");
                    return false;
                }

                ReleaseNode(handle.Id, true);
                return true;
            }

            if (node.State == TimerState.Executing)
            {
                node.State = TimerState.CancelRequested;
                return true;
            }

            return false;
        }

        internal void ClearAll()
        {
            while (_heap.Count > 0)
            {
                int nodeId = _heap.PopMin();
                ReleaseNode(nodeId, true);
            }

            if (_executingNodeId != 0 && _slots.GetNode(_executingNodeId).State == TimerState.Executing)
            {
                _slots.GetNode(_executingNodeId).State = TimerState.CancelRequested;
            }
        }

        internal void Reserve(int timerCapacity)
        {
            _slots.Reserve(timerCapacity);
        }

        internal int ProcessDue(long nowTick, int callbackBudget)
        {
            if (_isProcessing)
            {
                LogKit.LogError("[TimeKit] 禁止在 Timer callback 内嵌套 ProcessDueNow。");
                return 0;
            }

            _isProcessing = true;
            _diagnostics.CallbacksExecutedLastUpdate = 0;
            _diagnostics.DueBacklogCount = 0;
            try
            {
                while (_heap.Count > 0)
                {
                    int nodeId = _heap.PeekNodeId();
                    if (_slots.GetNode(nodeId).TriggerTick > nowTick)
                    {
                        break;
                    }

                    if (_diagnostics.CallbacksExecutedLastUpdate >= callbackBudget)
                    {
                        // 为避免在 budget 用尽时扫描整个 Heap，这里表示至少仍有一个 due 节点。
                        _diagnostics.DueBacklogCount = 1;
                        break;
                    }

                    _heap.PopMin();
                    int callbackCount = ExecuteDueNode(nodeId, nowTick);
                    _diagnostics.CallbacksExecutedLastUpdate += callbackCount;
                    _diagnostics.TotalExecutedCount += callbackCount;
                }
            }
            finally
            {
                _executingNodeId = 0;
                _isProcessing = false;
            }

            return _diagnostics.CallbacksExecutedLastUpdate;
        }

        internal bool IsHandleValid(TimerHandle handle) => _slots.IsActive(handle.Id, handle.Version);

        internal TimeKitDiagnosticsSnapshot GetDiagnostics() =>
            new TimeKitDiagnosticsSnapshot(_diagnostics, _heap, _slots);

        internal bool ValidateInvariants()
        {
            if (!_heap.ValidateInvariants())
            {
                return false;
            }

            for (int nodeId = 1; nodeId <= _slots.AllocatedSlotCount; nodeId++)
            {
                ref TimerNode node = ref _slots.GetNode(nodeId);
                if (node.State == TimerState.Free && !_slots.ValidateFreeNode(nodeId))
                {
                    return false;
                }

                if (node.State == TimerState.Scheduled && node.HeapIndex < 1)
                {
                    return false;
                }

                if ((node.State == TimerState.Executing || node.State == TimerState.CancelRequested) &&
                    node.HeapIndex != -1)
                {
                    return false;
                }
            }

            return true;
        }

        internal void Reset()
        {
            ClearAll();
            _nextSequence = 0UL;
            _executingNodeId = 0;
            _isProcessing = false;
            _heap.ResetDiagnostics();
            _diagnostics.Reset();
        }

        private TimerHandle ScheduleInternal(long triggerTick, long intervalTicks, int remainingExecutions,
            TimerCatchUpPolicy catchUpPolicy, TimerCallbackKind callbackKind, Action callback,
            Action<TimeTriggerContext> contextCallback, ITimeEventReceiver receiver, int eventId)
        {
            int nodeId = _slots.Allocate();
            ref TimerNode node = ref _slots.GetNode(nodeId);
            node.TriggerTick = triggerTick;
            node.IntervalTicks = intervalTicks;
            node.Sequence = NextSequence();
            node.RemainingExecutions = remainingExecutions;
            node.CatchUpPolicy = catchUpPolicy;
            node.CallbackKind = callbackKind;
            node.Callback = callback;
            node.ContextCallback = contextCallback;
            node.Receiver = receiver;
            node.EventId = eventId;
            node.State = TimerState.Scheduled;
            _heap.Push(nodeId);
            _diagnostics.ActiveTimerCount++;
            if (_diagnostics.ActiveTimerCount > _diagnostics.PeakActiveTimerCount)
            {
                _diagnostics.PeakActiveTimerCount = _diagnostics.ActiveTimerCount;
            }

            _diagnostics.TotalScheduledCount++;
            return new TimerHandle(nodeId, node.Version);
        }

        private int ExecuteDueNode(int nodeId, long nowTick)
        {
            ref TimerNode scheduledNode = ref _slots.GetNode(nodeId);
            if (scheduledNode.State != TimerState.Scheduled)
            {
                LogKit.LogError("[TimeKit] Heap 中出现非 Scheduled Timer 节点。");
                return 0;
            }

            scheduledNode.State = TimerState.Executing;
            _executingNodeId = nodeId;
            if (scheduledNode.IntervalTicks == 0L)
            {
                TimerNode snapshot = scheduledNode;
                Invoke(snapshot, CreateContext(snapshot.TriggerTick, nowTick, 1, false));
                ReleaseNode(nodeId, false);
                _executingNodeId = 0;
                return 1;
            }

            TimerNode node = scheduledNode;
            long elapsedPeriods = CalculateElapsedPeriods(node.TriggerTick, nowTick, node.IntervalTicks);
            if (node.CatchUpPolicy == TimerCatchUpPolicy.Skip)
            {
                CompleteSkippedRecurring(nodeId, node, elapsedPeriods);
                _executingNodeId = 0;
                return 0;
            }

            int representedPeriods = node.CatchUpPolicy == TimerCatchUpPolicy.All ? 1 :
                ToContextCount(elapsedPeriods, node.RemainingExecutions);
            bool isCatchUp = elapsedPeriods > 1L;
            Invoke(node, CreateContext(node.TriggerTick, nowTick, representedPeriods, isCatchUp));
            CompleteRecurring(nodeId, node, nowTick, elapsedPeriods);
            _executingNodeId = 0;
            return 1;
        }

        private void CompleteRecurring(int nodeId, TimerNode snapshot, long nowTick, long elapsedPeriods)
        {
            ref TimerNode node = ref _slots.GetNode(nodeId);
            if (node.State == TimerState.CancelRequested)
            {
                ReleaseNode(nodeId, false);
                return;
            }

            int consumedPeriods = snapshot.CatchUpPolicy == TimerCatchUpPolicy.All ||
                                  snapshot.CatchUpPolicy == TimerCatchUpPolicy.Once ? 1 :
                ToConsumedCount(elapsedPeriods, snapshot.RemainingExecutions);
            if (node.RemainingExecutions > 0)
            {
                node.RemainingExecutions -= consumedPeriods;
                if (node.RemainingExecutions <= 0)
                {
                    ReleaseNode(nodeId, false);
                    return;
                }
            }

            long nextTrigger;
            if (snapshot.CatchUpPolicy == TimerCatchUpPolicy.Once)
            {
                if (!TickMath.TryAdd(nowTick, snapshot.IntervalTicks, out nextTrigger))
                {
                    LogKit.LogError("[TimeKit] Once 周期 Timer 的下次触发 Tick 溢出，已释放任务。");
                    ReleaseNode(nodeId, false);
                    return;
                }
            }
            else if (!TryAdvanceTrigger(snapshot.TriggerTick, snapshot.IntervalTicks,
                         snapshot.CatchUpPolicy == TimerCatchUpPolicy.All ? 1L : elapsedPeriods, out nextTrigger))
            {
                LogKit.LogError("[TimeKit] 周期 Timer 的下次触发 Tick 溢出，已释放任务。");
                ReleaseNode(nodeId, false);
                return;
            }

            node.TriggerTick = nextTrigger;
            node.State = TimerState.Scheduled;
            _heap.Push(nodeId);
        }

        private void CompleteSkippedRecurring(int nodeId, TimerNode snapshot, long elapsedPeriods)
        {
            ref TimerNode node = ref _slots.GetNode(nodeId);
            if (node.RemainingExecutions > 0)
            {
                node.RemainingExecutions -= ToConsumedCount(elapsedPeriods, node.RemainingExecutions);
                if (node.RemainingExecutions <= 0)
                {
                    ReleaseNode(nodeId, false);
                    return;
                }
            }

            if (!TryAdvanceTrigger(snapshot.TriggerTick, snapshot.IntervalTicks, elapsedPeriods, out long nextTrigger))
            {
                LogKit.LogError("[TimeKit] Skip 周期 Timer 的下次触发 Tick 溢出，已释放任务。");
                ReleaseNode(nodeId, false);
                return;
            }

            node.TriggerTick = nextTrigger;
            node.State = TimerState.Scheduled;
            _heap.Push(nodeId);
        }

        private void Invoke(TimerNode node, TimeTriggerContext context)
        {
            try
            {
                switch (node.CallbackKind)
                {
                    case TimerCallbackKind.Action:
                        node.Callback?.Invoke();
                        break;
                    case TimerCallbackKind.ContextAction:
                        node.ContextCallback?.Invoke(context);
                        break;
                    case TimerCallbackKind.Receiver:
                        node.Receiver?.OnTimeEvent(node.EventId, in context);
                        break;
                }
            }
            catch (Exception exception)
            {
                _diagnostics.TotalErrorCount++;
                LogKit.LogException(exception);
            }
        }

        private void ReleaseNode(int nodeId, bool cancelled)
        {
            if (_slots.GetNode(nodeId).State == TimerState.Free)
            {
                return;
            }

            _slots.Release(nodeId);
            _diagnostics.ActiveTimerCount--;
            if (cancelled)
            {
                _diagnostics.TotalCancelledCount++;
            }
        }

        private ulong NextSequence()
        {
            if (_nextSequence == ulong.MaxValue)
            {
                // 此值在正常游戏生命周期中不可达；保留显式错误而不是破坏同 Tick 确定性。
                LogKit.LogError("[TimeKit] Timer Sequence 已达到上限，请 Reset TimeKit。");
                return _nextSequence;
            }

            return ++_nextSequence;
        }

        private static long CalculateElapsedPeriods(long triggerTick, long nowTick, long intervalTicks)
        {
            return (nowTick - triggerTick) / intervalTicks + 1L;
        }

        private static int ToContextCount(long elapsedPeriods, int remainingExecutions)
        {
            long represented = remainingExecutions > 0 ? Math.Min(elapsedPeriods, remainingExecutions) : elapsedPeriods;
            return represented > int.MaxValue ? int.MaxValue : (int)represented;
        }

        private static int ToConsumedCount(long elapsedPeriods, int remainingExecutions)
        {
            if (remainingExecutions < 0)
            {
                return 0;
            }

            return elapsedPeriods >= remainingExecutions ? remainingExecutions : (int)elapsedPeriods;
        }

        private static bool TryAdvanceTrigger(long triggerTick, long intervalTicks, long elapsedPeriods,
            out long nextTrigger)
        {
            if (!TickMath.TryMultiply(intervalTicks, elapsedPeriods, out long delta))
            {
                nextTrigger = 0L;
                return false;
            }

            return TickMath.TryAdd(triggerTick, delta, out nextTrigger);
        }

        private static TimeTriggerContext CreateContext(long scheduledTick, long nowTick, int elapsedCount,
            bool isCatchUp)
        {
            return new TimeTriggerContext(scheduledTick, nowTick, elapsedCount, isCatchUp);
        }
    }
}
