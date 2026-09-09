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
        {
            ActiveRuns = activeRuns;
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
}
