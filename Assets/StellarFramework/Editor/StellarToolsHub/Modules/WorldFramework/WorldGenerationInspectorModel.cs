using System;
using StellarFramework.WorldGenKit;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public readonly struct WorldGenerationChannelInspection
    {
        public int Index { get; }
        public WorldDataChannelId Id { get; }
        public WorldChannelStorageDescriptor Storage { get; }
        public WorldChannelSourceMode SourceMode { get; }

        internal WorldGenerationChannelInspection(WorldChannelDescriptor descriptor)
        {
            Index = descriptor.Index;
            Id = descriptor.Id;
            Storage = descriptor.Storage;
            SourceMode = descriptor.SourceMode;
        }
    }

    public readonly struct WorldGenerationStageInspection
    {
        public int Index { get; }
        public WorldGenerationStageId StageId { get; }
        public WorldGenerationSeedScope SeedScope { get; }
        public int RequiredCount { get; }
        public int OptionalCount { get; }
        public int ProducedCount { get; }
        public int MutatedCount { get; }

        internal WorldGenerationStageInspection(int index, WorldGenerationStageDescriptor descriptor)
        {
            Index = index;
            StageId = descriptor.StageId;
            SeedScope = descriptor.SeedScope;
            RequiredCount = descriptor.Required.Length;
            OptionalCount = descriptor.Optional.Length;
            ProducedCount = descriptor.Produced.Length;
            MutatedCount = descriptor.Mutated.Length;
        }
    }

    public static class WorldGenerationInspectorModel
    {
        public static int WriteChannels(
            WorldGenerationPlan plan,
            Span<WorldGenerationChannelInspection> destination)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            int count = plan.Channels.Count;
            if (destination.Length < count)
                throw new ArgumentException("Destination is smaller than the channel count.", nameof(destination));

            for (int i = 0; i < count; i++)
            {
                if (!plan.Channels.TryGetDescriptor(i, out WorldChannelDescriptor descriptor))
                    throw new InvalidOperationException("Compiled channel registry failed to resolve a valid index.");
                destination[i] = new WorldGenerationChannelInspection(descriptor);
            }
            return count;
        }

        public static int WriteStages(
            WorldGenerationPlan plan,
            Span<WorldGenerationStageInspection> destination)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (destination.Length < plan.StageCount)
                throw new ArgumentException("Destination is smaller than the stage count.", nameof(destination));

            for (int i = 0; i < plan.StageCount; i++)
                destination[i] = new WorldGenerationStageInspection(i, plan.GetStageDescriptor(i));
            return plan.StageCount;
        }

        public static int WriteReportStages(
            WorldGenerationReport report,
            Span<WorldGenerationStageExecutionRecord> destination)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return report.WriteStages(destination);
        }
    }
}
