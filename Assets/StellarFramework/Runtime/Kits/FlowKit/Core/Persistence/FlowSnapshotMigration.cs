using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public enum FlowSnapshotMigrationCode
    {
        Succeeded,
        AlreadyCurrent,
        InvalidSnapshot,
        MissingMigrator,
        MigrationFailed,
        PlanHashMismatch
    }

    public readonly struct FlowSnapshotMigrationResult
    {
        public FlowSnapshotMigrationCode Code { get; }
        public FlowSnapshot Snapshot { get; }
        public string Message { get; }
        public bool Succeeded => Code == FlowSnapshotMigrationCode.Succeeded ||
            Code == FlowSnapshotMigrationCode.AlreadyCurrent;

        public FlowSnapshotMigrationResult(FlowSnapshotMigrationCode code, FlowSnapshot snapshot, string message = null)
        {
            Code = code;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public static FlowSnapshotMigrationResult Success(FlowSnapshot snapshot) =>
            new FlowSnapshotMigrationResult(FlowSnapshotMigrationCode.Succeeded, snapshot);
    }

    /// <summary>
    /// Snapshot 迁移与 Graph Migration 独立。迁移器必须声明精确的 FlowId、源 PlanHash 和目标 PlanHash。
    /// </summary>
    public interface IFlowSnapshotMigrator
    {
        string FlowId { get; }
        ulong FromPlanHash { get; }
        ulong ToPlanHash { get; }
        FlowSnapshotMigrationResult Migrate(FlowSnapshot snapshot, FlowCompiledPlan targetPlan);
    }

    /// <summary>按 PlanHash 显式串联 Snapshot 迁移；不会隐式接受“最新”快照。</summary>
    public sealed class FlowSnapshotMigrationPipeline
    {
        private readonly List<IFlowSnapshotMigrator> _migrators = new List<IFlowSnapshotMigrator>();

        public FlowSnapshotMigrationPipeline Register(IFlowSnapshotMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));
            if (string.IsNullOrEmpty(migrator.FlowId)) throw new ArgumentException("Snapshot migrator 的 FlowId 不能为空。", nameof(migrator));
            if (migrator.FromPlanHash == migrator.ToPlanHash)
                throw new ArgumentException("Snapshot migrator 的源和目标 PlanHash 必须不同。", nameof(migrator));

            for (int i = 0; i < _migrators.Count; i++)
            {
                IFlowSnapshotMigrator existing = _migrators[i];
                if (string.Equals(existing.FlowId, migrator.FlowId, StringComparison.Ordinal) &&
                    existing.FromPlanHash == migrator.FromPlanHash)
                {
                    throw new InvalidOperationException(
                        $"Snapshot migrator 已存在: {migrator.FlowId}/{migrator.FromPlanHash}");
                }
            }

            _migrators.Add(migrator);
            return this;
        }

        public FlowSnapshotMigrationResult Migrate(FlowSnapshot snapshot, FlowCompiledPlan targetPlan)
        {
            if (snapshot == null || targetPlan == null)
                return new FlowSnapshotMigrationResult(
                    FlowSnapshotMigrationCode.InvalidSnapshot, snapshot, "Snapshot 和目标 Plan 不能为空。");
            if (!string.Equals(snapshot.FlowId, targetPlan.FlowId, StringComparison.Ordinal))
                return new FlowSnapshotMigrationResult(
                    FlowSnapshotMigrationCode.PlanHashMismatch, snapshot, "Snapshot FlowId 与目标 Plan 不匹配。");
            if (snapshot.PlanHash == targetPlan.PlanHash)
                return new FlowSnapshotMigrationResult(FlowSnapshotMigrationCode.AlreadyCurrent, snapshot);

            FlowSnapshot current = snapshot;
            var visited = new HashSet<ulong>();
            int guard = 0;
            while (current.PlanHash != targetPlan.PlanHash)
            {
                if (++guard > _migrators.Count + 1 || !visited.Add(current.PlanHash))
                    return new FlowSnapshotMigrationResult(
                        FlowSnapshotMigrationCode.MigrationFailed, current, "Snapshot 迁移链存在循环。");

                IFlowSnapshotMigrator migrator = Find(current.FlowId, current.PlanHash);
                if (migrator == null)
                    return new FlowSnapshotMigrationResult(
                        FlowSnapshotMigrationCode.MissingMigrator, current,
                        $"缺少 {current.FlowId}/{current.PlanHash} 到目标 PlanHash 的 Snapshot 迁移器。");

                FlowSnapshotMigrationResult result = migrator.Migrate(current, targetPlan);
                if (!result.Succeeded || result.Snapshot == null)
                    return new FlowSnapshotMigrationResult(
                        FlowSnapshotMigrationCode.MigrationFailed, current,
                        string.IsNullOrEmpty(result.Message) ? "Snapshot 迁移器未返回可用 Snapshot。" : result.Message);
                if (!string.Equals(result.Snapshot.FlowId, current.FlowId, StringComparison.Ordinal) ||
                    result.Snapshot.PlanHash != migrator.ToPlanHash)
                {
                    return new FlowSnapshotMigrationResult(
                        FlowSnapshotMigrationCode.MigrationFailed, current,
                        "Snapshot 迁移器返回的 FlowId/PlanHash 与声明不一致。");
                }

                current = result.Snapshot;
            }

            FlowSnapshotResult validation = FlowSnapshotService.Validate(current, targetPlan);
            return validation.Succeeded
                ? new FlowSnapshotMigrationResult(FlowSnapshotMigrationCode.Succeeded, current)
                : new FlowSnapshotMigrationResult(
                    FlowSnapshotMigrationCode.PlanHashMismatch, current, validation.Message);
        }

        private IFlowSnapshotMigrator Find(string flowId, ulong fromPlanHash)
        {
            for (int i = 0; i < _migrators.Count; i++)
            {
                IFlowSnapshotMigrator migrator = _migrators[i];
                if (migrator.FromPlanHash == fromPlanHash &&
                    string.Equals(migrator.FlowId, flowId, StringComparison.Ordinal)) return migrator;
            }

            return null;
        }
    }
}
