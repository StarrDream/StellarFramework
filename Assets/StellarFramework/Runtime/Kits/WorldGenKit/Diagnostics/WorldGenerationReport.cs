using System;

namespace StellarFramework.WorldGenKit
{
    /// <summary>
    /// Optional immutable diagnostic snapshot. Capture only when a caller needs a persistent report.
    /// </summary>
    public sealed class WorldGenerationReport
    {
        private readonly WorldGenerationStageExecutionRecord[] _stages;

        public ulong PlanHash { get; }
        public WorldGenerationSeed WorldSeed { get; }
        public WorldGenerationRunKey RunKey { get; }
        public WorldGenerationRunStatus Status { get; }
        public WorldGenerationDiagnosticId TerminalCode { get; }
        public int TerminalStageIndex { get; }
        public int StageCount => _stages.Length;

        private WorldGenerationReport(
            ulong planHash,
            WorldGenerationSeed worldSeed,
            WorldGenerationRunKey runKey,
            WorldGenerationRunStatus status,
            WorldGenerationDiagnosticId terminalCode,
            int terminalStageIndex,
            WorldGenerationStageExecutionRecord[] stages)
        {
            PlanHash = planHash;
            WorldSeed = worldSeed;
            RunKey = runKey;
            Status = status;
            TerminalCode = terminalCode;
            TerminalStageIndex = terminalStageIndex;
            _stages = stages;
        }

        public static WorldGenerationReport Capture(
            WorldGenerationPlan plan,
            WorldGenerationSeed worldSeed,
            WorldGenerationRunKey runKey,
            WorldGenerationRunResult runResult,
            ReadOnlySpan<WorldGenerationStageExecutionRecord> records)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (runResult.StagesWritten < 0 || runResult.StagesWritten > records.Length)
                throw new ArgumentException("Run result stage count exceeds supplied execution records.", nameof(records));
            if (runResult.StagesWritten > plan.StageCount)
                throw new ArgumentException("Run result stage count exceeds the supplied plan stage count.", nameof(runResult));

            WorldGenerationStageExecutionRecord[] snapshot = new WorldGenerationStageExecutionRecord[runResult.StagesWritten];
            records.Slice(0, runResult.StagesWritten).CopyTo(snapshot);

            return new WorldGenerationReport(
                plan.PlanHash,
                worldSeed,
                runKey,
                runResult.Status,
                runResult.Code,
                runResult.TerminalStageIndex,
                snapshot);
        }

        public WorldGenerationStageExecutionRecord GetStage(int index)
        {
            if ((uint)index >= (uint)_stages.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _stages[index];
        }

        public int WriteStages(Span<WorldGenerationStageExecutionRecord> destination)
        {
            if (destination.Length < _stages.Length)
                throw new ArgumentException("Destination buffer is smaller than the report stage count.", nameof(destination));
            _stages.AsSpan().CopyTo(destination);
            return _stages.Length;
        }
    }
}
