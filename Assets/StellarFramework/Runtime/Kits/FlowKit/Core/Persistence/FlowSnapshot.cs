using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    [Serializable]
    public sealed class FlowSnapshot
    {
        public string FlowId;
        public ulong PlanHash;
        public long RunId;
        public string PersistentRunId;
        public int PlanVersion;
        public string CheckpointId;
        public FlowRunStatus Status;
        public List<FlowBlackboardEntry> Blackboard = new List<FlowBlackboardEntry>();
        public List<FlowPersistentStateEntry> PersistentStates = new List<FlowPersistentStateEntry>();
    }

    [Serializable]
    public struct FlowPersistentStateEntry
    {
        public string StateId;
        public string SourceKey;
        public FlowValue Value;
        public long Revision;
        public FlowStateLifetime Lifetime;

        public FlowPersistentStateEntry(
            string stateId,
            string sourceKey,
            FlowValue value,
            long revision,
            FlowStateLifetime lifetime)
        {
            StateId = stateId;
            SourceKey = sourceKey ?? string.Empty;
            Value = value;
            Revision = revision;
            Lifetime = lifetime;
        }
    }

    public enum FlowSnapshotResultCode
    {
        Succeeded,
        NotQuiescent,
        PlanHashMismatch,
        InvalidSnapshot
    }

    public readonly struct FlowSnapshotResult
    {
        public FlowSnapshotResultCode Code { get; }
        public FlowSnapshot Snapshot { get; }
        public string Message { get; }
        public bool Succeeded => Code == FlowSnapshotResultCode.Succeeded;

        public FlowSnapshotResult(FlowSnapshotResultCode code, FlowSnapshot snapshot, string message)
        {
            Code = code;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>仅在无活动执行、无待处理激活/完成时捕获；PlanHash 不匹配禁止恢复。</summary>
    public static class FlowSnapshotService
    {
        public static FlowSnapshotResult Capture(FlowRun run) => Capture(run, null, null);

        public static FlowSnapshotResult Capture(FlowRun run, string checkpointId, string persistentRunId)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.ActiveExecutionCount != 0 || run.PendingActivationCount != 0 || run.PendingCompletionCount != 0 ||
                (run.Status != FlowRunStatus.Completed && run.Status != FlowRunStatus.Failed &&
                 run.Status != FlowRunStatus.Cancelled && run.Status != FlowRunStatus.Rejected))
            {
                return new FlowSnapshotResult(FlowSnapshotResultCode.NotQuiescent, null,
                    "Flow 仍在运行或有活动执行/待处理队列，不能创建 V1 终态快照。");
            }

            var snapshot = new FlowSnapshot
            {
                FlowId = run.Plan.FlowId,
                PlanHash = run.Plan.PlanHash,
                RunId = run.RunId.Value,
                PersistentRunId = string.IsNullOrEmpty(persistentRunId)
                    ? run.PersistentRunId
                    : persistentRunId,
                PlanVersion = run.Plan.SchemaVersion,
                CheckpointId = checkpointId ?? string.Empty,
                Status = run.Status
            };
            FlowBlackboardEntry[] entries = run.Blackboard.CapturePersistentEntries();
            for (int i = 0; i < entries.Length; i++) snapshot.Blackboard.Add(entries[i]);
            FlowPersistentStateEntry[] states = run.Context.States.CapturePersistentEntries();
            for (int i = 0; i < states.Length; i++) snapshot.PersistentStates.Add(states[i]);
            return new FlowSnapshotResult(FlowSnapshotResultCode.Succeeded, snapshot, string.Empty);
        }

        public static FlowSnapshotResult Validate(FlowSnapshot snapshot, FlowCompiledPlan plan)
        {
            if (snapshot == null || plan == null)
                return new FlowSnapshotResult(FlowSnapshotResultCode.InvalidSnapshot, null, "Snapshot 和 Plan 不能为空。");
            if (snapshot.RunId <= 0 || string.IsNullOrEmpty(snapshot.PersistentRunId))
                return new FlowSnapshotResult(FlowSnapshotResultCode.InvalidSnapshot, snapshot,
                    "Snapshot 必须包含有效 RunId 和 PersistentRunId。");
            if (!string.Equals(snapshot.FlowId, plan.FlowId, StringComparison.Ordinal) || snapshot.PlanHash != plan.PlanHash ||
                (snapshot.PlanVersion > 0 && snapshot.PlanVersion != plan.SchemaVersion))
                return new FlowSnapshotResult(FlowSnapshotResultCode.PlanHashMismatch, snapshot, "Snapshot 的 FlowId/PlanHash/PlanVersion 与当前 Plan 不匹配。");
            if (snapshot.Status != FlowRunStatus.Completed && snapshot.Status != FlowRunStatus.Failed &&
                snapshot.Status != FlowRunStatus.Cancelled && snapshot.Status != FlowRunStatus.Rejected)
                return new FlowSnapshotResult(FlowSnapshotResultCode.InvalidSnapshot, snapshot,
                    "V1 Snapshot 只能表示已结束的 Run；活动流程需要实现显式 Stateful Resume。");
            if (!HasValidPayload(snapshot, out string payloadError))
                return new FlowSnapshotResult(FlowSnapshotResultCode.InvalidSnapshot, snapshot, payloadError);
            return new FlowSnapshotResult(FlowSnapshotResultCode.Succeeded, snapshot, string.Empty);
        }

        public static void RestoreBlackboard(FlowSnapshot snapshot, FlowBlackboard blackboard, FlowCompiledPlan plan)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));
            FlowSnapshotResult validation = Validate(snapshot, plan);
            if (!validation.Succeeded) throw new InvalidOperationException(validation.Message);
            using (FlowBlackboardBatch batch = blackboard.BeginBatch())
            {
                if (snapshot.Blackboard != null)
                {
                    for (int i = 0; i < snapshot.Blackboard.Count; i++)
                    {
                        FlowBlackboardEntry entry = snapshot.Blackboard[i];
                        batch.Set(entry.Key, entry.Value, entry.Persistence);
                    }
                }

                batch.Commit();
            }
        }

        public static void RestorePersistentStates(
            FlowSnapshot snapshot,
            FlowStateStore states,
            FlowCompiledPlan plan)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (states == null) throw new ArgumentNullException(nameof(states));
            FlowSnapshotResult validation = Validate(snapshot, plan);
            if (!validation.Succeeded) throw new InvalidOperationException(validation.Message);
            if (snapshot.PersistentStates == null) return;
            for (int i = 0; i < snapshot.PersistentStates.Count; i++)
                states.RestorePersistent(snapshot.PersistentStates[i]);
        }

        private static bool HasValidPayload(FlowSnapshot snapshot, out string error)
        {
            var blackboardKeys = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot.Blackboard != null)
            {
                for (int i = 0; i < snapshot.Blackboard.Count; i++)
                {
                    FlowBlackboardEntry entry = snapshot.Blackboard[i];
                    if (string.IsNullOrEmpty(entry.Key) || entry.Persistence != FlowBlackboardPersistence.Persistent)
                    {
                        error = "Snapshot Blackboard 只能包含有效的 Persistent 条目。";
                        return false;
                    }

                    if (!blackboardKeys.Add(entry.Key))
                    {
                        error = "Snapshot Blackboard 不能包含重复 key。";
                        return false;
                    }
                }
            }

            var stateKeys = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot.PersistentStates != null)
            {
                for (int i = 0; i < snapshot.PersistentStates.Count; i++)
                {
                    FlowPersistentStateEntry entry = snapshot.PersistentStates[i];
                    if (string.IsNullOrEmpty(entry.StateId) || entry.Lifetime != FlowStateLifetime.Persistent || entry.Revision < 0)
                    {
                        error = "Snapshot Persistent State 必须包含有效 StateId、Persistent Lifetime 和非负 Revision。";
                        return false;
                    }

                    string key = entry.StateId + "\u001f" + (entry.SourceKey ?? string.Empty);
                    if (!stateKeys.Add(key))
                    {
                        error = "Snapshot Persistent State 不能包含重复 key。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
