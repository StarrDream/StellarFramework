using System;

namespace StellarFramework.WorldGenKit
{
    public enum WorldGenerationRunStatus
    {
        Succeeded = 0,
        Failed = 1,
        Cancelled = 2,
        BufferTooSmall = 3,
        RegistryMismatch = 4,
        UnboundChannel = 5
    }

    public readonly struct WorldGenerationStageExecutionRecord
    {
        public WorldGenerationStageId StageId { get; }
        public WorldGenerationSeed StageSeed { get; }
        public WorldGenerationStageResult Result { get; }

        internal WorldGenerationStageExecutionRecord(
            WorldGenerationStageId stageId,
            WorldGenerationSeed stageSeed,
            WorldGenerationStageResult result)
        {
            StageId = stageId;
            StageSeed = stageSeed;
            Result = result;
        }
    }

    public readonly struct WorldGenerationRunResult
    {
        public WorldGenerationRunStatus Status { get; }
        public int StagesWritten { get; }
        public int TerminalStageIndex { get; }
        public WorldGenerationDiagnosticId Code { get; }
        public bool Success => Status == WorldGenerationRunStatus.Succeeded;

        internal WorldGenerationRunResult(
            WorldGenerationRunStatus status,
            int stagesWritten,
            int terminalStageIndex,
            WorldGenerationDiagnosticId code)
        {
            Status = status;
            StagesWritten = stagesWritten;
            TerminalStageIndex = terminalStageIndex;
            Code = code;
        }
    }

    public sealed class WorldGenerationPlan
    {
        private readonly CompiledStage[] _stages;
        private readonly int[] _usedChannelIndices;

        public WorldChannelRegistry Channels { get; }
        public int StageCount => _stages.Length;
        public ulong PlanHash { get; }

        internal WorldGenerationPlan(
            WorldChannelRegistry channels,
            CompiledStage[] stages,
            int[] usedChannelIndices,
            ulong planHash)
        {
            Channels = channels;
            _stages = stages;
            _usedChannelIndices = usedChannelIndices;
            PlanHash = planHash;
        }

        public WorldGenerationStageDescriptor GetStageDescriptor(int index)
        {
            if ((uint)index >= (uint)_stages.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _stages[index].Descriptor;
        }

        public WorldGenerationRunResult Execute(
            WorldGenerationDataSet data,
            WorldGenerationSeed worldSeed,
            WorldGenerationRunKey runKey,
            Span<WorldGenerationStageExecutionRecord> destination)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (destination.Length < _stages.Length)
            {
                return new WorldGenerationRunResult(
                    WorldGenerationRunStatus.BufferTooSmall,
                    0,
                    -1,
                    WorldGenerationDiagnosticIds.BufferTooSmall);
            }

            if (data.Registry.Generation != Channels.Generation)
            {
                return new WorldGenerationRunResult(
                    WorldGenerationRunStatus.RegistryMismatch,
                    0,
                    -1,
                    WorldGenerationDiagnosticIds.RegistryMismatch);
            }

            for (int i = 0; i < _usedChannelIndices.Length; i++)
            {
                if (!data.IsBoundIndex(_usedChannelIndices[i]))
                {
                    return new WorldGenerationRunResult(
                        WorldGenerationRunStatus.UnboundChannel,
                        0,
                        -1,
                        WorldGenerationDiagnosticIds.UnboundChannel);
                }
            }

            for (int i = 0; i < _stages.Length; i++)
            {
                CompiledStage compiled = _stages[i];
                WorldGenerationSeed stageSeed = DeriveStageSeed(
                    worldSeed,
                    runKey,
                    compiled.Descriptor.StageId,
                    compiled.Descriptor.SeedScope);

                WorldGenerationContext context = new WorldGenerationContext(
                    data,
                    stageSeed,
                    runKey,
                    compiled.Descriptor.StageId);

                WorldGenerationStageResult result = compiled.Stage.Execute(in context);
                destination[i] = new WorldGenerationStageExecutionRecord(
                    compiled.Descriptor.StageId,
                    stageSeed,
                    result);

                if (result.Status == WorldGenerationStageStatus.Failed)
                {
                    return new WorldGenerationRunResult(
                        WorldGenerationRunStatus.Failed,
                        i + 1,
                        i,
                        result.Code);
                }

                if (result.Status == WorldGenerationStageStatus.Cancelled)
                {
                    return new WorldGenerationRunResult(
                        WorldGenerationRunStatus.Cancelled,
                        i + 1,
                        i,
                        result.Code);
                }
            }

            return new WorldGenerationRunResult(
                WorldGenerationRunStatus.Succeeded,
                _stages.Length,
                -1,
                default(WorldGenerationDiagnosticId));
        }

        private static WorldGenerationSeed DeriveStageSeed(
            WorldGenerationSeed worldSeed,
            WorldGenerationRunKey runKey,
            WorldGenerationStageId stageId,
            WorldGenerationSeedScope seedScope)
        {
            switch (seedScope)
            {
                case WorldGenerationSeedScope.World:
                    return worldSeed.Derive(0L, 0L, stageId, 0x574F524C44UL);
                case WorldGenerationSeedScope.Region:
                    return worldSeed.Derive(runKey.X, runKey.Y, stageId, 0x524547494F4EUL);
                case WorldGenerationSeedScope.Chunk:
                    return worldSeed.Derive(runKey.X, runKey.Y, stageId, 0x4348554E4BUL);
                case WorldGenerationSeedScope.RunKey:
                    return worldSeed.Derive(runKey.X, runKey.Y, stageId, runKey.LocalKey ^ 0x52554E4B4559UL);
                default:
                    throw new InvalidOperationException("Compiled stage has no valid seed scope.");
            }
        }

        internal readonly struct CompiledStage
        {
            internal IWorldGenerationStage Stage { get; }
            internal WorldGenerationStageDescriptor Descriptor { get; }

            internal CompiledStage(IWorldGenerationStage stage, WorldGenerationStageDescriptor descriptor)
            {
                Stage = stage;
                Descriptor = descriptor;
            }
        }
    }
}
