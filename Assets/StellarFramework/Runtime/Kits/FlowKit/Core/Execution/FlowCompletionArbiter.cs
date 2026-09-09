using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    /// <summary>
    /// 完成来源提交给 Runner 的候选结果。SchedulerSequence 为正数时由来源/宿主显式提供，
    /// 设为 0 则由 Runner 在入队时分配单调序号。
    /// </summary>
    public readonly struct FlowCompletionCandidate
    {
        public string Reason { get; }
        public int Priority { get; }
        public long SchedulerSequence { get; }
        public string OutputPort { get; }
        public FlowValue Payload { get; }
        public FlowStructuredError Error { get; }

        public FlowCompletionCandidate(
            string reason,
            int priority,
            string outputPort,
            FlowValue payload = default(FlowValue),
            FlowStructuredError error = null,
            long schedulerSequence = 0)
        {
            Reason = reason ?? string.Empty;
            Priority = priority;
            SchedulerSequence = schedulerSequence;
            OutputPort = outputPort ?? string.Empty;
            Payload = payload;
            Error = error;
        }

        public bool IsFailure => Error != null;
    }

    /// <summary>同一执行句柄的完成仲裁：优先级高者胜出，同优先级按调度序号先到先得。</summary>
    public static class FlowCompletionArbiter
    {
        public static bool IsBetter(FlowCompletionCandidate candidate, FlowCompletionCandidate previous) =>
            IsBetter(candidate.Priority, candidate.SchedulerSequence, previous.Priority, previous.SchedulerSequence);

        public static bool IsBetter(int candidatePriority, long candidateSequence, int previousPriority, long previousSequence)
        {
            if (candidatePriority != previousPriority) return candidatePriority > previousPriority;
            return candidateSequence < previousSequence;
        }

        public static bool TrySelect(
            IReadOnlyList<FlowCompletionCandidate> candidates,
            out FlowCompletionCandidate winner)
        {
            if (candidates == null || candidates.Count == 0)
            {
                winner = default(FlowCompletionCandidate);
                return false;
            }

            winner = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (IsBetter(candidates[i], winner)) winner = candidates[i];
            }

            return true;
        }
    }
}
