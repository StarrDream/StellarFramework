using System;
using System.Collections.Generic;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public enum WorldAuthoringValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct WorldAuthoringValidationIssue
    {
        public WorldAuthoringValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }

        public WorldAuthoringValidationIssue(
            WorldAuthoringValidationSeverity severity,
            string code,
            string message)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Validation issue code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Validation issue message is required.", nameof(message));
            Severity = severity;
            Code = code;
            Message = message;
        }
    }

    public readonly struct WorldAuthoringMemoryReport
    {
        public long SampleCount { get; }
        public long KnownFixedChannelBytes { get; }
        public int DenseChannelCount { get; }
        public int ConstantChannelCount { get; }
        public int VariableStorageChannelCount { get; }

        public WorldAuthoringMemoryReport(
            long sampleCount,
            long knownFixedChannelBytes,
            int denseChannelCount,
            int constantChannelCount,
            int variableStorageChannelCount)
        {
            SampleCount = sampleCount;
            KnownFixedChannelBytes = knownFixedChannelBytes;
            DenseChannelCount = denseChannelCount;
            ConstantChannelCount = constantChannelCount;
            VariableStorageChannelCount = variableStorageChannelCount;
        }
    }

    public sealed class WorldAuthoringValidationReport
    {
        private readonly WorldAuthoringValidationIssue[] _issues;

        public bool Success { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public IReadOnlyList<WorldAuthoringValidationIssue> Issues => _issues;
        public WorldGenerationAuthoringCompileResult Generation { get; }
        public WorldSemanticAuthoringCompileResult Semantics { get; }
        public WorldAuthoringMemoryReport Memory { get; }

        internal WorldAuthoringValidationReport(
            bool success,
            int errorCount,
            int warningCount,
            WorldAuthoringValidationIssue[] issues,
            WorldGenerationAuthoringCompileResult generation,
            WorldSemanticAuthoringCompileResult semantics,
            WorldAuthoringMemoryReport memory)
        {
            Success = success;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            _issues = issues ?? Array.Empty<WorldAuthoringValidationIssue>();
            Generation = generation;
            Semantics = semantics;
            Memory = memory;
        }
    }

    public sealed class WorldCandidateHeatmapSnapshot
    {
        private readonly int[] _candidateCounts;
        private readonly int[] _acceptedCounts;

        public int Width { get; }
        public int Height { get; }
        public int GeneratedCandidates { get; }
        public int AcceptedCandidates { get; }
        public int RejectedOccupancy { get; }
        public int RejectedBudget { get; }
        public int RejectedSpacing { get; }
        public int MaxCandidateCount { get; }
        public int MaxAcceptedCount { get; }

        internal WorldCandidateHeatmapSnapshot(
            int width,
            int height,
            int generatedCandidates,
            int acceptedCandidates,
            int rejectedOccupancy,
            int rejectedBudget,
            int rejectedSpacing,
            int maxCandidateCount,
            int maxAcceptedCount,
            int[] candidateCounts,
            int[] acceptedCounts)
        {
            Width = width;
            Height = height;
            GeneratedCandidates = generatedCandidates;
            AcceptedCandidates = acceptedCandidates;
            RejectedOccupancy = rejectedOccupancy;
            RejectedBudget = rejectedBudget;
            RejectedSpacing = rejectedSpacing;
            MaxCandidateCount = maxCandidateCount;
            MaxAcceptedCount = maxAcceptedCount;
            _candidateCounts = candidateCounts;
            _acceptedCounts = acceptedCounts;
        }

        public int GetCandidateCount(int x, int y) =>
            _candidateCounts[GetIndex(x, y)];

        public int GetAcceptedCount(int x, int y) =>
            _acceptedCounts[GetIndex(x, y)];

        private int GetIndex(int x, int y)
        {
            if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
            if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
            return (y * Width) + x;
        }
    }

    public static class WorldAuthoringDiagnosticsModel
    {
        private const int MaxPreviewCandidates = 1_000_000;
        private const long LargeDenseMemoryWarningBytes = 256L * 1024L * 1024L;

        public static WorldAuthoringValidationReport Validate(
            WorldGenerationAuthoringProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            WorldGenerationAuthoringCompileResult generation =
                WorldGenerationAuthoringCompiler.Compile(profile);
            WorldSemanticAuthoringCompileResult semantics =
                WorldSemanticAuthoringCompiler.Compile(profile);
            List<WorldAuthoringValidationIssue> issues =
                new List<WorldAuthoringValidationIssue>();

            for (int i = 0; i < generation.Messages.Count; i++)
            {
                issues.Add(new WorldAuthoringValidationIssue(
                    WorldAuthoringValidationSeverity.Error,
                    "generation.compile",
                    generation.Messages[i]));
            }
            for (int i = 0; i < semantics.Messages.Count; i++)
            {
                issues.Add(new WorldAuthoringValidationIssue(
                    WorldAuthoringValidationSeverity.Error,
                    "semantic.compile",
                    semantics.Messages[i]));
            }

            if (generation.Success)
                AddUnusedChannelWarnings(generation.Plan, issues);

            try
            {
                WorldSemanticAuthoringCompiler.EvaluatePlacement(profile.PlacementProbe);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is OverflowException)
            {
                issues.Add(new WorldAuthoringValidationIssue(
                    WorldAuthoringValidationSeverity.Error,
                    "placement.probe_configuration",
                    exception.Message));
            }

            WorldAuthoringMemoryReport memory = EstimateMemory(profile);
            if (memory.KnownFixedChannelBytes >= LargeDenseMemoryWarningBytes)
            {
                issues.Add(new WorldAuthoringValidationIssue(
                    WorldAuthoringValidationSeverity.Warning,
                    "memory.large_fixed_channels",
                    $"Known Dense/Constant channel storage lower bound is {memory.KnownFixedChannelBytes:N0} bytes."));
            }
            if (memory.VariableStorageChannelCount > 0)
            {
                issues.Add(new WorldAuthoringValidationIssue(
                    WorldAuthoringValidationSeverity.Info,
                    "memory.variable_storage",
                    $"{memory.VariableStorageChannelCount} Sparse/Chunked/Computed/External channels are intentionally excluded from the fixed-byte estimate."));
            }

            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == WorldAuthoringValidationSeverity.Error) errors++;
                else if (issues[i].Severity == WorldAuthoringValidationSeverity.Warning) warnings++;
            }

            return new WorldAuthoringValidationReport(
                errors == 0,
                errors,
                warnings,
                issues.ToArray(),
                generation,
                semantics,
                memory);
        }

        public static WorldAuthoringMemoryReport EstimateMemory(
            WorldGenerationAuthoringProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            long sampleCount = checked((long)profile.Width * profile.Height);
            long fixedBytes = 0L;
            int dense = 0;
            int constant = 0;
            int variable = 0;

            for (int i = 0; i < profile.Channels.Count; i++)
            {
                WorldAuthoringChannelDefinition channel = profile.Channels[i];
                if (channel == null) continue;
                int elementBytes = GetElementSize(channel.ValueType);
                switch (channel.StorageKind)
                {
                    case WorldChannelStorageKind.Dense:
                        fixedBytes = checked(fixedBytes + checked(sampleCount * elementBytes));
                        dense++;
                        break;
                    case WorldChannelStorageKind.Constant:
                        fixedBytes = checked(fixedBytes + elementBytes);
                        constant++;
                        break;
                    default:
                        variable++;
                        break;
                }
            }

            return new WorldAuthoringMemoryReport(
                sampleCount,
                fixedBytes,
                dense,
                constant,
                variable);
        }

        public static WorldCandidateHeatmapSnapshot BuildResourceHeatmap(
            WorldGenerationAuthoringProfile profile,
            WorldSemanticAuthoringCompileResult semantics,
            int resourceIndex,
            int width,
            int height,
            ulong seed)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (semantics == null || !semantics.Success)
                throw new ArgumentException("Semantic authoring must compile successfully before preview.", nameof(semantics));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Preview dimensions must be positive.");
            if ((uint)resourceIndex >= (uint)semantics.ResourceCatalog.Count)
                throw new ArgumentOutOfRangeException(nameof(resourceIndex));

            WorldResourcePlanarDomain domain =
                new WorldResourcePlanarDomain(
                    width,
                    height,
                    profile.SampleStep,
                    0L,
                    0L);
            WorldResourceDefinition definition =
                semantics.ResourceCatalog.GetDefinition(resourceIndex);
            WorldResourceGenerationSettings generationSettings =
                new WorldResourceGenerationSettings(
                    WorldResourceGenerationMultiplier.Identity);
            WorldResolvedResourceGenerationSettings resolved =
                generationSettings.Resolve(definition);

            long capacityLong = resolved.Mode == WorldResourceDistributionMode.Coverage
                ? domain.Count
                : checked((long)domain.Count * resolved.ClusterSize);
            if (capacityLong > MaxPreviewCandidates)
                throw new InvalidOperationException(
                    $"Preview would require capacity for {capacityLong:N0} candidates; the Editor preview safety limit is {MaxPreviewCandidates:N0}.");

            int capacity = (int)capacityLong;
            WorldSpawnCandidate[] candidates =
                new WorldSpawnCandidate[capacity];
            WorldCoverageSampleRank[] coverageScratch =
                new WorldCoverageSampleRank[domain.Count];
            WorldResourceCandidateGenerationResult generated =
                WorldResourceCandidateGenerator.Generate(
                    resourceIndex,
                    semantics.ResourceCatalog,
                    in domain,
                    new WorldGenerationSeed(seed),
                    in resolved,
                    ReadOnlySpan<byte>.Empty,
                    ReadOnlySpan<float>.Empty,
                    candidates,
                    coverageScratch);

            ReadOnlySpan<WorldSpawnCandidate> generatedCandidates =
                candidates.AsSpan(0, generated.GeneratedCount);
            WorldOccupancyCellState[] occupancy =
                new WorldOccupancyCellState[domain.Count];
            int[] order = new int[generated.GeneratedCount];
            int[] resourceAccepted = new int[semantics.ResourceCatalog.Count];
            int[] categoryAccepted = new int[semantics.ResourceCatalog.CategoryCount];
            int[] spacingHeads = new int[domain.Count];
            WorldScatterSpacingNode[] spacingNodes =
                new WorldScatterSpacingNode[generated.GeneratedCount];
            WorldSpawnRecord[] accepted =
                new WorldSpawnRecord[generated.GeneratedCount];
            WorldCompiledResourceBudget budget =
                new WorldResourceBudget().Compile(semantics.ResourceCatalog);

            WorldScatterResolveResult resolvedResult =
                WorldResourceScatterResolver.Resolve(
                    generatedCandidates,
                    semantics.ResourceCatalog,
                    in domain,
                    budget,
                    occupancy,
                    order,
                    resourceAccepted,
                    categoryAccepted,
                    spacingHeads,
                    spacingNodes,
                    accepted);

            int[] candidateCounts = new int[domain.Count];
            int[] acceptedCounts = new int[domain.Count];
            int maxCandidate = 0;
            int maxAccepted = 0;
            for (int i = 0; i < generated.GeneratedCount; i++)
            {
                int count = ++candidateCounts[candidates[i].SampleIndex];
                if (count > maxCandidate) maxCandidate = count;
            }
            for (int i = 0; i < resolvedResult.AcceptedCount; i++)
            {
                int count = ++acceptedCounts[accepted[i].SampleIndex];
                if (count > maxAccepted) maxAccepted = count;
            }

            return new WorldCandidateHeatmapSnapshot(
                width,
                height,
                generated.GeneratedCount,
                resolvedResult.AcceptedCount,
                resolvedResult.RejectedOccupancyCount,
                resolvedResult.RejectedBudgetCount,
                resolvedResult.RejectedSpacingCount,
                maxCandidate,
                maxAccepted,
                candidateCounts,
                acceptedCounts);
        }

        private static void AddUnusedChannelWarnings(
            WorldGenerationPlan plan,
            List<WorldAuthoringValidationIssue> issues)
        {
            bool[] used = new bool[plan.Channels.Count];
            for (int i = 0; i < plan.StageCount; i++)
            {
                WorldGenerationStageDescriptor stage = plan.GetStageDescriptor(i);
                Mark(stage.Required, used);
                Mark(stage.Optional, used);
                Mark(stage.Produced, used);
                Mark(stage.Mutated, used);
            }

            for (int i = 0; i < used.Length; i++)
            {
                if (used[i]) continue;
                if (!plan.Channels.TryGetDescriptor(i, out WorldChannelDescriptor channel))
                    continue;
                issues.Add(new WorldAuthoringValidationIssue(
                    WorldAuthoringValidationSeverity.Warning,
                    "channel.unused",
                    $"Channel '{channel.Id.Value}' is registered but unused by the compiled pipeline."));
            }
        }

        private static void Mark(
            ReadOnlySpan<WorldStageChannelReference> references,
            bool[] used)
        {
            for (int i = 0; i < references.Length; i++)
                used[references[i].ChannelIndex] = true;
        }

        private static int GetElementSize(WorldAuthoringChannelValueType type)
        {
            switch (type)
            {
                case WorldAuthoringChannelValueType.Float:
                case WorldAuthoringChannelValueType.Int:
                    return 4;
                case WorldAuthoringChannelValueType.Byte:
                    return 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
