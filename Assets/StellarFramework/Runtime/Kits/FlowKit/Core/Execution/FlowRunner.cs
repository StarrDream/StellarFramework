using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public sealed class FlowRunnerOptions
    {
        public int MaxActivationsPerTick { get; set; } = 1024;
        public int MaxTotalActivationsPerRun { get; set; } = 100000;
        public int MaxCompletionsPerTick { get; set; } = 1024;
        public int MaxTimerCallbacksPerTick { get; set; } = 1024;
        public int MaxSignalNotificationsPerTick { get; set; } = 1024;
        public int MaxPollingCallbacksPerTick { get; set; } = 1024;

        internal void Validate()
        {
            if (MaxActivationsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(MaxActivationsPerTick));
            if (MaxTotalActivationsPerRun <= 0) throw new ArgumentOutOfRangeException(nameof(MaxTotalActivationsPerRun));
            if (MaxCompletionsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(MaxCompletionsPerTick));
            if (MaxTimerCallbacksPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(MaxTimerCallbacksPerTick));
            if (MaxSignalNotificationsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(MaxSignalNotificationsPerTick));
            if (MaxPollingCallbacksPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(MaxPollingCallbacksPerTick));
        }
    }

    /// <summary>一个 Host 共享的 Core 服务容器，具体 Unity/SDK 实现通过适配器注入。</summary>
    public sealed class FlowRuntimeServices
    {
        public FlowTimerScheduler Timers { get; }
        public FlowPollingScheduler Polling { get; }
        public FlowSignalRouter Signals { get; }
        public FlowStateStore States { get; }
        public FlowRuntimeEpoch RuntimeEpoch { get; }
        public FlowBindingRegistry Bindings { get; }
        public FlowOperationRegistry Operations { get; }
        public IFlowAssetResolver Assets { get; }
        public FlowCapabilitySet Capabilities { get; }
        public IFlowTraceSink Trace { get; }
        public FlowTimeSnapshot Time { get; internal set; }

        public FlowRuntimeServices(
            FlowTimerScheduler timers = null,
            FlowPollingScheduler polling = null,
            FlowSignalRouter signals = null,
            FlowStateStore states = null,
            FlowBindingRegistry bindings = null,
            FlowOperationRegistry operations = null,
            FlowCapabilitySet capabilities = null,
            IFlowTraceSink trace = null,
            FlowRuntimeEpoch runtimeEpoch = null,
            IFlowAssetResolver assets = null)
        {
            Timers = timers ?? new FlowTimerScheduler();
            Polling = polling ?? new FlowPollingScheduler();
            Signals = signals ?? new FlowSignalRouter();
            States = states ?? new FlowStateStore();
            RuntimeEpoch = runtimeEpoch ?? new FlowRuntimeEpoch();
            Bindings = bindings ?? new FlowBindingRegistry();
            Operations = operations ?? new FlowOperationRegistry();
            Assets = assets;
            Capabilities = capabilities ?? new FlowCapabilitySet();
            Trace = trace;
        }
    }

    public sealed class FlowRunContext
    {
        private readonly FlowRun _owner;

        internal FlowRunContext(FlowRun owner, FlowRuntimeServices services, FlowBlackboard blackboard)
        {
            _owner = owner;
            Services = services;
            Blackboard = blackboard;
        }

        internal FlowRuntimeServices Services { get; }
        public FlowRunId RunId => _owner.RunId;
        public string PersistentRunId => _owner.PersistentRunId;
        public FlowCompiledPlan Plan => _owner.Plan;
        public FlowBlackboard Blackboard { get; }
        public FlowTimerScheduler Timers => Services.Timers;
        public FlowPollingScheduler Polling => Services.Polling;
        public FlowSignalRouter Signals => Services.Signals;
        public FlowStateStore States => Services.States;
        public FlowRuntimeEpoch RuntimeEpoch => Services.RuntimeEpoch;
        public FlowBindingRegistry Bindings => Services.Bindings;
        public FlowOperationRegistry Operations => Services.Operations;
        public IFlowAssetResolver Assets => Services.Assets;
        public FlowCapabilitySet Capabilities => Services.Capabilities;
        public FlowCancellationToken Cancellation => _owner.Cancellation;
        public FlowTimeSnapshot Time => Services.Time;

        public bool TryGetBinding(FlowBindingHandle handle, out object value) => Bindings.TryResolve(handle, out value);

        internal void RegisterJoin(FlowNodeHandle handle) => _owner.RegisterJoin(handle);
    }

    public readonly struct FlowNodeExecutionContext
    {
        private readonly FlowCancellationToken _cancellation;

        public FlowRunContext Run { get; }
        public FlowExecutionIdentity Identity { get; }
        public string InputPort { get; }
        public FlowValue InputPayload { get; }
        public FlowTimeSnapshot Time => Run.Time;
        public string PersistentRunId => Run.PersistentRunId;
        public FlowBlackboard Blackboard => Run.Blackboard;
        public FlowSignalRouter Signals => Run.Signals;
        public FlowStateStore States => Run.States;
        public FlowTimerScheduler Timers => Run.Timers;
        public FlowPollingScheduler Polling => Run.Polling;
        public FlowRuntimeEpoch RuntimeEpoch => Run.RuntimeEpoch;
        public FlowBindingRegistry Bindings => Run.Bindings;
        public FlowOperationRegistry Operations => Run.Operations;
        public IFlowAssetResolver Assets => Run.Assets;
        public FlowCancellationToken Cancellation => _cancellation ?? Run.Cancellation;

        internal FlowNodeExecutionContext(
            FlowRunContext run,
            FlowExecutionIdentity identity,
            string inputPort = null,
            FlowValue inputPayload = default(FlowValue),
            FlowCancellationToken cancellation = null)
        {
            Run = run;
            Identity = identity;
            InputPort = inputPort ?? string.Empty;
            InputPayload = inputPayload;
            _cancellation = cancellation;
        }

        public bool TryGetParameter(in FlowCompiledNode node, string key, out FlowValue value) =>
            node.Parameters.TryGet(key, out value);

        public void Complete(FlowNodeHandle handle, string outputPort, FlowValue payload = default(FlowValue)) =>
            handle.TryComplete(outputPort, payload);

        public void Complete(FlowNodeHandle handle, string outputPort, FlowValue payload, int priority) =>
            handle.TryComplete(outputPort, payload, priority);

        public void Fail(FlowNodeHandle handle, FlowRuntimeErrorCode code, string message, Exception exception = null) =>
            handle.TryFail(new FlowStructuredError(code, message, handle.NodeId, Identity.FrameId, Identity.ExecutionId, exception), int.MaxValue);
    }

    /// <summary>节点回调持有的单次完成句柄；完成后所有旧回调均被判定为 stale。</summary>
    public sealed class FlowNodeHandle
    {
        private readonly FlowRun _owner;
        private readonly List<IDisposable> _owned = new List<IDisposable>();
        private readonly FlowCancellationToken _cancellation;
        private readonly int _runtimeEpoch;
        private bool _closed;

        internal FlowNodeHandle(FlowRun owner, FlowExecutionIdentity identity, int nodeIndex, string nodeId)
        {
            _owner = owner;
            Identity = identity;
            NodeIndex = nodeIndex;
            NodeId = nodeId;
            _cancellation = owner.Cancellation.CreateChild();
            _runtimeEpoch = owner.RuntimeEpoch.Value;
        }

        public FlowExecutionIdentity Identity { get; }
        public FlowExecutionId ExecutionId => Identity.ExecutionId;
        public int Generation => Identity.Generation;
        public int NodeIndex { get; }
        public string NodeId { get; }
        public FlowCancellationToken Cancellation => _cancellation;
        public int RuntimeEpoch => _runtimeEpoch;
        public bool IsCompleted => _closed;

        public bool TryComplete(string outputPort, FlowValue payload = default(FlowValue), int priority = 0)
        {
            if (_closed) return false;
            return _owner.EnqueueCompletion(this, outputPort, payload, null, priority, null);
        }

        public bool TrySubmit(FlowCompletionCandidate candidate)
        {
            if (_closed) return false;
            return _owner.EnqueueCompletion(this, candidate.OutputPort, candidate.Payload, candidate.Error,
                candidate.Priority, candidate.Reason, candidate.SchedulerSequence);
        }

        /// <summary>只取消当前 Execution，不影响同一 Run 中的其它分支。</summary>
        public bool TryCancel()
        {
            if (_closed) return false;
            return _owner.CancelExecution(this);
        }

        public bool TryFail(FlowStructuredError error, int priority = 0)
        {
            if (_closed) return false;
            return _owner.EnqueueCompletion(this, null, FlowValue.None, error, priority, null);
        }

        public void Track(IDisposable owned)
        {
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            if (_closed)
            {
                owned.Dispose();
                return;
            }

            _owned.Add(owned);
        }

        internal void DisposeOwned()
        {
            List<Exception> exceptions = null;
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                try
                {
                    _owned[i].Dispose();
                }
                catch (Exception exception)
                {
                    if (exceptions == null) exceptions = new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            _owned.Clear();
            if (exceptions != null)
                throw new AggregateException("一个或多个节点资源清理失败。", exceptions);
        }

        internal void Close()
        {
            if (_closed) return;
            _closed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }

    public sealed class FlowRun
    {
        private readonly struct Activation
        {
            internal readonly int NodeIndex;
            internal readonly string InputPort;
            internal readonly FlowValue Payload;
            internal readonly FlowTokenLineage Lineage;

            internal Activation(int nodeIndex, string inputPort, FlowValue payload, FlowTokenLineage lineage)
            {
                NodeIndex = nodeIndex;
                InputPort = inputPort;
                Payload = payload;
                Lineage = lineage;
            }
        }

        private readonly struct Completion
        {
            internal readonly FlowNodeHandle Handle;
            internal readonly string OutputPort;
            internal readonly FlowValue Payload;
            internal readonly FlowStructuredError Error;
            internal readonly int Priority;
            internal readonly string Reason;
            internal readonly long Sequence;

            internal Completion(
                FlowNodeHandle handle,
                string outputPort,
                FlowValue payload,
                FlowStructuredError error,
                int priority,
                string reason,
                long sequence)
            {
                Handle = handle;
                OutputPort = outputPort;
                Payload = payload;
                Error = error;
                Priority = priority;
                Reason = reason ?? string.Empty;
                Sequence = sequence;
            }
        }

        private readonly FlowRuntimeServices _services;
        private readonly FlowRunnerOptions _options;
        private readonly int _runtimeEpoch;
        private readonly Queue<Activation> _activations = new Queue<Activation>();
        private readonly Queue<Completion> _completions = new Queue<Completion>();
        private readonly Dictionary<long, Completion> _pendingCompletions = new Dictionary<long, Completion>();
        private readonly Dictionary<long, FlowNodeHandle> _active = new Dictionary<long, FlowNodeHandle>();
        private readonly Dictionary<long, FlowExecutionGroup> _groups = new Dictionary<long, FlowExecutionGroup>();
        private long _nextExecution;
        private long _nextFork;
        private long _completionSequence;
        private long _traceSequence;
        private int _totalActivations;
        private bool _started;

        internal FlowRun(
            FlowCompiledPlan plan,
            FlowRuntimeServices services,
            FlowBlackboard blackboard,
            FlowRunnerOptions options,
            string persistentRunId = null)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _runtimeEpoch = _services.RuntimeEpoch.Value;
            Blackboard = blackboard ?? new FlowBlackboard();
            RunId = FlowRunId.Create();
            PersistentRunId = string.IsNullOrEmpty(persistentRunId)
                ? RunId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : persistentRunId;
            Cancellation = new FlowCancellationToken();
            Context = new FlowRunContext(this, services, Blackboard);
            Status = FlowRunStatus.Created;
            _activations.Enqueue(new Activation(
                plan.EntryNodeIndex,
                string.Empty,
                FlowValue.None,
                new FlowTokenLineage(RunId, 0, 0, 0)));
        }

        public FlowRunId RunId { get; }
        public string PersistentRunId { get; }
        public FlowCompiledPlan Plan { get; }
        public FlowBlackboard Blackboard { get; }
        public FlowRunContext Context { get; }
        public FlowRuntimeEpoch RuntimeEpoch => _services.RuntimeEpoch;
        public IFlowAssetResolver Assets => _services.Assets;
        public FlowCancellationToken Cancellation { get; }
        public FlowRunStatus Status { get; private set; }
        public FlowStructuredError LastError { get; private set; }
        public int ActiveExecutionCount => _active.Count;
        public int PendingActivationCount => _activations.Count;
        public int PendingCompletionCount => _completions.Count;
        public int TotalActivationCount => _totalActivations;
        public bool BudgetLimitedLastTick { get; private set; }
        public bool IsTerminal => Status == FlowRunStatus.Completed || Status == FlowRunStatus.Failed ||
            Status == FlowRunStatus.Cancelled || Status == FlowRunStatus.Rejected;

        internal void Reject(FlowStructuredError error)
        {
            Status = FlowRunStatus.Rejected;
            LastError = error;
        }

        internal int Tick()
        {
            if (IsTerminal) return 0;
            if (!_services.RuntimeEpoch.IsCurrent(_runtimeEpoch))
            {
                FailRun(new FlowStructuredError(
                    FlowRuntimeErrorCode.RuntimeEpochChanged,
                    "Flow RuntimeEpoch 已变化，当前 Run 被终止以隔离旧回调。"));
                return 0;
            }

            if (!_started)
            {
                _started = true;
                Status = FlowRunStatus.Running;
                Trace(FlowTraceEventKind.RunStarted, default(FlowExecutionIdentity), string.Empty, null);
            }

            int processed = 0;
            int activations = 0;
            int completions = 0;
            BudgetLimitedLastTick = false;
            while (!IsTerminal)
            {
                if (_completions.Count > 0)
                {
                    if (completions >= _options.MaxCompletionsPerTick)
                    {
                        BudgetLimitedLastTick = true;
                        break;
                    }

                    ProcessCompletion(_completions.Dequeue());
                    completions++;
                    processed++;
                    continue;
                }

                if (_activations.Count == 0) break;
                if (activations >= _options.MaxActivationsPerTick)
                {
                    BudgetLimitedLastTick = true;
                    break;
                }

                if (_totalActivations >= _options.MaxTotalActivationsPerRun)
                {
                    FailRun(new FlowStructuredError(
                        FlowRuntimeErrorCode.ActivationBudgetExceeded,
                        "Flow Run 超过 MaxTotalActivationsPerRun，已终止以避免无界循环。"));
                    break;
                }

                ProcessActivation(_activations.Dequeue());
                _totalActivations++;
                activations++;
                processed++;
            }

            return processed;
        }

        internal bool EnqueueCompletion(
            FlowNodeHandle handle,
            string outputPort,
            FlowValue payload,
            FlowStructuredError error,
            int priority,
            string reason,
            long schedulerSequence = 0)
        {
            if (IsTerminal || handle == null || handle.RuntimeEpoch != _runtimeEpoch ||
                !_services.RuntimeEpoch.IsCurrent(_runtimeEpoch) ||
                !_active.TryGetValue(handle.ExecutionId.Value, out FlowNodeHandle current) ||
                !ReferenceEquals(current, handle) || handle.IsCompleted)
            {
                Trace(FlowTraceEventKind.NodeFailed, handle == null ? default(FlowExecutionIdentity) : handle.Identity,
                    handle == null ? string.Empty : handle.NodeId, "stale callback");
                return false;
            }

            long sequence = schedulerSequence > 0 ? schedulerSequence : ++_completionSequence;
            if (sequence > _completionSequence) _completionSequence = sequence;
            var candidate = new Completion(handle, outputPort, payload, error, priority, reason, sequence);
            if (_pendingCompletions.TryGetValue(handle.ExecutionId.Value, out Completion previous) &&
                !FlowCompletionArbiter.IsBetter(candidate.Priority, candidate.Sequence, previous.Priority, previous.Sequence)) return false;
            _pendingCompletions[handle.ExecutionId.Value] = candidate;
            _completions.Enqueue(candidate);
            return true;
        }

        public void Cancel()
        {
            if (IsTerminal) return;
            Cancellation.Cancel();
            Status = FlowRunStatus.Cancelled;
            FlowStructuredError cleanupError = null;
            foreach (KeyValuePair<long, FlowNodeHandle> pair in _active)
            {
                FlowStructuredError currentError = CancelHandle(pair.Value);
                cleanupError = cleanupError ?? currentError;
            }

            _active.Clear();
            _activations.Clear();
            _completions.Clear();
            _pendingCompletions.Clear();
            if (cleanupError != null) LastError = cleanupError;
            Trace(FlowTraceEventKind.RunCancelled, default(FlowExecutionIdentity), string.Empty, null);
        }

        private void ProcessActivation(Activation activation)
        {
            if (activation.Lineage.ForkInstanceId != 0 && IsGroupClosed(activation.Lineage.ForkInstanceId)) return;
            if (activation.NodeIndex < 0 || activation.NodeIndex >= Plan.NodeCount)
            {
                FailRun(new FlowStructuredError(FlowRuntimeErrorCode.HandlerFailure, "激活引用了无效节点索引。"));
                return;
            }

            FlowCompiledNode node = Plan.GetNode(activation.NodeIndex);
            var executionId = new FlowExecutionId(++_nextExecution);
            var identity = new FlowExecutionIdentity(
                RunId,
                executionId,
                1,
                new FlowFrameId(1),
                new FlowScopeId(RunId.Value),
                activation.Lineage,
                _runtimeEpoch);
            var handle = new FlowNodeHandle(this, identity, node.Index, node.NodeId);
            _active.Add(executionId.Value, handle);
            Trace(FlowTraceEventKind.NodeActivated, identity, node.NodeId, activation.InputPort);
            var context = new FlowNodeExecutionContext(Context, identity, activation.InputPort, activation.Payload, handle.Cancellation);
            try
            {
                node.Handler.Start(in context, in node, handle);
            }
            catch (Exception exception)
            {
                handle.TryFail(new FlowStructuredError(
                    FlowRuntimeErrorCode.HandlerFailure,
                    $"节点 Handler 执行失败: {exception.Message}",
                    node.NodeId,
                    identity.FrameId,
                    identity.ExecutionId,
                    exception), int.MaxValue);
            }
        }

        private void ProcessCompletion(Completion completion)
        {
            FlowNodeHandle handle = completion.Handle;
            if (!_pendingCompletions.TryGetValue(handle.ExecutionId.Value, out Completion winner) ||
                winner.Sequence != completion.Sequence) return;
            _pendingCompletions.Remove(handle.ExecutionId.Value);
            if (!_active.Remove(handle.ExecutionId.Value)) return;
            handle.Close();
            try
            {
                handle.DisposeOwned();
            }
            catch (Exception exception)
            {
                FailRun(new FlowStructuredError(
                    FlowRuntimeErrorCode.CleanupFailed,
                    $"节点资源清理失败: {exception.Message}",
                    handle.NodeId,
                    handle.Identity.FrameId,
                    handle.Identity.ExecutionId,
                    exception));
                return;
            }

            FlowCompiledNode node = Plan.GetNode(handle.NodeIndex);
            if (completion.Error != null)
            {
                Trace(FlowTraceEventKind.NodeFailed, handle.Identity, handle.NodeId, completion.Error.Message);
                FailRun(completion.Error);
                return;
            }

            Trace(FlowTraceEventKind.NodeCompleted, handle.Identity, handle.NodeId,
                string.IsNullOrEmpty(completion.Reason) ? completion.OutputPort : completion.Reason);
            if (string.Equals(node.TypeId.Value, FlowBuiltInNodes.JoinTypeId, StringComparison.Ordinal) &&
                !ClaimJoinOutput(handle.Identity.Lineage.ForkInstanceId))
            {
                return;
            }
            if (string.IsNullOrEmpty(completion.OutputPort))
            {
                FailRun(new FlowStructuredError(
                    FlowRuntimeErrorCode.UnknownOutputPort,
                    "节点完成时没有指定输出端口。",
                    handle.NodeId,
                    handle.Identity.FrameId,
                    handle.Identity.ExecutionId));
                return;
            }

            if (!node.Descriptor.TryGetPort(completion.OutputPort, FlowPortDirection.Output, out _))
            {
                FailRun(new FlowStructuredError(
                    FlowRuntimeErrorCode.UnknownOutputPort,
                    $"节点输出端口未声明: {completion.OutputPort}",
                    handle.NodeId,
                    handle.Identity.FrameId,
                    handle.Identity.ExecutionId));
                return;
            }

            IReadOnlyList<FlowOutputRoute> routes = node.GetOutputRoutes(completion.OutputPort);
            bool isParallel = string.Equals(node.TypeId.Value, FlowBuiltInNodes.ParallelTypeId, StringComparison.Ordinal) ||
                string.Equals(node.TypeId.Value, FlowBuiltInNodes.RaceTypeId, StringComparison.Ordinal);
            long forkId = isParallel && routes.Count > 0 ? ++_nextFork : handle.Identity.Lineage.ForkInstanceId;
            FlowJoinPolicy joinPolicy = string.Equals(node.TypeId.Value, FlowBuiltInNodes.RaceTypeId, StringComparison.Ordinal)
                ? FlowJoinPolicy.Any
                : FlowJoinPolicy.All;
            if (isParallel && routes.Count > 0)
                _groups[forkId] = new FlowExecutionGroup(forkId, routes.Count, joinPolicy);

            for (int i = 0; i < routes.Count; i++)
            {
                FlowTokenLineage lineage = isParallel
                    ? new FlowTokenLineage(RunId, forkId, i, 0)
                    : handle.Identity.Lineage;
                _activations.Enqueue(new Activation(routes[i].NodeIndex, routes[i].InputPort, completion.Payload, lineage));
            }

            if (routes.Count == 0 && node.Descriptor.CompletesFlow)
            {
                CompleteRun();
            }
            else if (routes.Count == 0)
            {
                FailRun(new FlowStructuredError(
                    FlowRuntimeErrorCode.UnroutedCompletion,
                    "节点完成后没有可达输出，也没有声明 CompletesFlow。请连接输出端口或使用 flow.complete。",
                    handle.NodeId,
                    handle.Identity.FrameId,
                    handle.Identity.ExecutionId));
            }
        }

        private void CompleteRun()
        {
            if (IsTerminal) return;
            Status = FlowRunStatus.Completed;
            Cancellation.Cancel();
            var active = new List<FlowNodeHandle>(_active.Values);
            _active.Clear();
            _activations.Clear();
            _completions.Clear();
            _pendingCompletions.Clear();
            FlowStructuredError cleanupError = null;
            for (int i = 0; i < active.Count; i++)
            {
                FlowStructuredError currentError = CancelHandle(active[i]);
                cleanupError = cleanupError ?? currentError;
            }

            if (cleanupError != null)
            {
                Status = FlowRunStatus.Failed;
                LastError = cleanupError;
                Trace(FlowTraceEventKind.RunFailed, default(FlowExecutionIdentity), string.Empty, cleanupError.Message);
                return;
            }

            Trace(FlowTraceEventKind.RunCompleted, default(FlowExecutionIdentity), string.Empty, null);
        }

        internal void RegisterJoin(FlowNodeHandle handle)
        {
            long forkId = handle.Identity.Lineage.ForkInstanceId;
            if (forkId == 0 || !_groups.TryGetValue(forkId, out FlowExecutionGroup group))
            {
                handle.TryFail(new FlowStructuredError(
                    FlowRuntimeErrorCode.HandlerFailure,
                    "Join 不在有效的 Parallel/Race 执行组内。",
                    handle.NodeId,
                    handle.Identity.FrameId,
                    handle.Identity.ExecutionId));
                return;
            }

            if (group.IsClosed)
            {
                handle.TryComplete("joined");
                return;
            }

            if (!group.TryRegisterBranch(handle.Identity.Lineage.BranchId))
            {
                handle.TryComplete("joined");
                return;
            }

            group.AddWaiter(handle);
            if (group.IsReady)
            {
                group.IsClosed = true;
                for (int i = 0; i < group.Waiters.Count; i++) group.Waiters[i].TryComplete("joined");
                if (group.Policy == FlowJoinPolicy.Any || group.Policy == FlowJoinPolicy.NOfM) CancelGroupBranches(group.Id);
            }
        }

        private bool ClaimJoinOutput(long forkId)
        {
            if (forkId == 0 || !_groups.TryGetValue(forkId, out FlowExecutionGroup group)) return true;
            if (group.JoinOutputClaimed) return false;
            group.JoinOutputClaimed = true;
            group.ClearWaiters();
            return true;
        }

        private bool IsGroupClosed(long forkId) => _groups.TryGetValue(forkId, out FlowExecutionGroup group) && group.IsClosed;

        internal bool CancelExecution(FlowNodeHandle handle)
        {
            if (IsTerminal || handle == null ||
                !_active.TryGetValue(handle.ExecutionId.Value, out FlowNodeHandle current) ||
                !ReferenceEquals(current, handle)) return false;

            _active.Remove(handle.ExecutionId.Value);
            FlowStructuredError cleanupError = CancelHandle(handle);
            if (cleanupError != null)
            {
                FailRun(cleanupError);
            }
            // A cancelled execution may already have queued a completion
            // candidate. CancelHandle removes it from the arbiter's pending
            // map, while the FIFO keeps the stale entry for lazy discard.
            // Use the pending map (not the raw queue length) to determine
            // whether the run still has meaningful work; otherwise a run
            // with only a stale completion could remain Running forever.
            else if (_active.Count == 0 && _activations.Count == 0 && _pendingCompletions.Count == 0)
            {
                Cancellation.Cancel();
                Status = FlowRunStatus.Cancelled;
                Trace(FlowTraceEventKind.RunCancelled, default(FlowExecutionIdentity), string.Empty, null);
            }

            return true;
        }

        private void CancelGroupBranches(long forkId)
        {
            var cancel = new List<FlowNodeHandle>();
            foreach (KeyValuePair<long, FlowNodeHandle> pair in _active)
            {
                if (pair.Value.Identity.Lineage.ForkInstanceId != forkId) continue;
                bool isWaiter = false;
                if (_groups.TryGetValue(forkId, out FlowExecutionGroup group))
                {
                    for (int i = 0; i < group.Waiters.Count; i++)
                    {
                        if (ReferenceEquals(group.Waiters[i], pair.Value)) { isWaiter = true; break; }
                    }
                }

                if (!isWaiter) cancel.Add(pair.Value);
            }

            FlowStructuredError cleanupError = null;
            for (int i = 0; i < cancel.Count; i++)
            {
                _active.Remove(cancel[i].ExecutionId.Value);
                FlowStructuredError currentError = CancelHandle(cancel[i]);
                cleanupError = cleanupError ?? currentError;
            }

            if (cleanupError != null) FailRun(cleanupError);
        }

        private void FailRun(FlowStructuredError error)
        {
            if (IsTerminal) return;
            Status = FlowRunStatus.Failed;
            Cancellation.Cancel();
            LastError = error;
            FlowStructuredError cleanupError = null;
            foreach (KeyValuePair<long, FlowNodeHandle> pair in _active)
            {
                FlowStructuredError currentError = CancelHandle(pair.Value);
                cleanupError = cleanupError ?? currentError;
            }

            _active.Clear();
            _activations.Clear();
            _completions.Clear();
            _pendingCompletions.Clear();
            if (LastError == null) LastError = cleanupError;
            Trace(FlowTraceEventKind.RunFailed, default(FlowExecutionIdentity), string.Empty,
                LastError == null ? null : LastError.Message);
        }

        private FlowStructuredError CancelHandle(FlowNodeHandle handle)
        {
            _pendingCompletions.Remove(handle.ExecutionId.Value);
            handle.Close();
            FlowCompiledNode node = Plan.GetNode(handle.NodeIndex);
            var context = new FlowNodeExecutionContext(Context, handle.Identity, cancellation: handle.Cancellation);
            List<Exception> cleanupExceptions = null;
            try
            {
                node.Handler.Cancel(in context, in node);
            }
            catch (Exception exception)
            {
                cleanupExceptions = new List<Exception> { exception };
            }
            finally
            {
                try
                {
                    handle.DisposeOwned();
                }
                catch (Exception exception)
                {
                    if (cleanupExceptions == null) cleanupExceptions = new List<Exception>();
                    cleanupExceptions.Add(exception);
                }
            }

            if (cleanupExceptions == null) return null;
            Exception aggregate = cleanupExceptions.Count == 1
                ? cleanupExceptions[0]
                : new AggregateException("节点取消/清理产生多个错误。", cleanupExceptions);
            return CreateCleanupError(handle, aggregate);
        }

        private static FlowStructuredError CreateCleanupError(FlowNodeHandle handle, Exception exception)
        {
            return new FlowStructuredError(
                FlowRuntimeErrorCode.CleanupFailed,
                $"节点取消/清理失败: {exception.Message}",
                handle.NodeId,
                handle.Identity.FrameId,
                handle.Identity.ExecutionId,
                exception);
        }

        private void Trace(FlowTraceEventKind kind, FlowExecutionIdentity identity, string nodeId, string message)
        {
            if (_services.Trace == null) return;
            var trace = new FlowTraceEvent(kind, RunId, identity, nodeId, message, ++_traceSequence);
            _services.Trace.Write(in trace);
        }
    }

    /// <summary>宿主可依赖的最小 Runner 边界；具体实现保持可替换。</summary>
    public interface IFlowRunner
    {
        FlowRun Start(FlowCompiledPlan plan, FlowBlackboard blackboard = null, string persistentRunId = null);
        int Tick(FlowTimeSnapshot time);
        void CancelAll();
    }

    public sealed class FlowRunner : IFlowRunner
    {
        private readonly FlowRuntimeServices _services;
        private readonly FlowRunnerOptions _options;
        private readonly List<FlowRun> _runs = new List<FlowRun>();

        public FlowRunner(FlowRuntimeServices services = null, FlowRunnerOptions options = null)
        {
            _services = services ?? new FlowRuntimeServices();
            _options = options ?? new FlowRunnerOptions();
            _options.Validate();
        }

        public FlowRuntimeServices Services => _services;
        public int ActiveRunCount => _runs.Count;

        public FlowRuntimeDiagnostics CaptureDiagnostics()
        {
            int pendingActivations = 0;
            int pendingCompletions = 0;
            for (int i = 0; i < _runs.Count; i++)
            {
                pendingActivations += _runs[i].PendingActivationCount;
                pendingCompletions += _runs[i].PendingCompletionCount;
            }

            return new FlowRuntimeDiagnostics(
                _runs.Count,
                pendingActivations,
                pendingCompletions,
                _services.Signals.PendingNotificationCount,
                _services.Signals.OldestNotificationAge,
                _services.Timers.ActiveCount,
                _services.Polling.ActiveCount,
                _services.Operations.ActiveCount,
                _services.States.StateCount,
                _services.Bindings.Count);
        }

        public FlowRun Start(
            FlowCompiledPlan plan,
            FlowBlackboard blackboard = null,
            string persistentRunId = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var run = new FlowRun(plan, _services, blackboard, _options, persistentRunId);
            if (!_services.Capabilities.ContainsAll(plan.RequiredCapabilities))
            {
                run.Reject(new FlowStructuredError(
                    FlowRuntimeErrorCode.FlowStartRejected,
                    "Flow 所需 Capability 未全部注册。"));
                return run;
            }

            _runs.Add(run);
            return run;
        }

        public int Tick(FlowTimeSnapshot time)
        {
            _services.Time = time;
            _services.Signals.ObserveTime(in time);
            // Keep the scheduler phases explicit: queued external notifications
            // and pollers are collected before due timers, then Runs arbitrate
            // completions and emit bounded activations. Signals published by a
            // timer stay queued for the next Tick instead of re-entering the
            // current notification phase.
            int processed = _services.Signals.Drain(_options.MaxSignalNotificationsPerTick);
            processed += _services.Polling.Poll(in time, _options.MaxPollingCallbacksPerTick);
            processed += _services.Timers.Advance(time, _options.MaxTimerCallbacksPerTick);
            for (int i = _runs.Count - 1; i >= 0; i--)
            {
                processed += _runs[i].Tick();
                if (_runs[i].IsTerminal) _runs.RemoveAt(i);
            }

            return processed;
        }

        public void CancelAll()
        {
            for (int i = 0; i < _runs.Count; i++) _runs[i].Cancel();
            _runs.Clear();
        }
    }
}
