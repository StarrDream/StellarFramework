namespace StellarFramework.FlowKit
{
    /// <summary>宿主可按帧采样的运行态摘要；不产生 Trace 日志，也不遍历未激活 Graph 节点。</summary>
    public readonly struct FlowRuntimeDiagnostics
    {
        public int ActiveRuns { get; }
        public int PendingActivations { get; }
        public int PendingCompletions { get; }
        public int PendingSignals { get; }
        public double OldestNotificationAge { get; }
        public int ActiveTimers { get; }
        public int ActivePollers { get; }
        public int ActiveOperations { get; }
        public int StateCount { get; }
        public int ActiveBindings { get; }

        public FlowRuntimeDiagnostics(
            int activeRuns,
            int pendingActivations,
            int pendingCompletions,
            int pendingSignals,
            double oldestNotificationAge,
            int activeTimers,
            int activePollers,
            int activeOperations,
            int stateCount,
            int activeBindings)
        {            ActiveRuns = activeRuns;
            PendingActivations = pendingActivations;
            PendingCompletions = pendingCompletions;
            PendingSignals = pendingSignals;
            OldestNotificationAge = oldestNotificationAge;
            ActiveTimers = activeTimers;
            ActivePollers = activePollers;
            ActiveOperations = activeOperations;
            StateCount = stateCount;
            ActiveBindings = activeBindings;
        }
    }

    /// <summary>单个活动节点执行的只读诊断快照；仅用于调试器，不参与流程语义。</summary>
    public readonly struct FlowExecutionDiagnostics
    {
        public FlowRunId RunId { get; }
        public FlowExecutionId ExecutionId { get; }
        public string NodeId { get; }
        public FlowFrameId FrameId { get; }
        public FlowTokenLineage Lineage { get; }

        public FlowExecutionDiagnostics(
            FlowRunId runId,
            FlowExecutionId executionId,
            string nodeId,
            FlowFrameId frameId,
            FlowTokenLineage lineage)
        {            RunId = runId;
            ExecutionId = executionId;
            NodeId = nodeId ?? string.Empty;
            FrameId = frameId;
            Lineage = lineage;
        }
    }
}
