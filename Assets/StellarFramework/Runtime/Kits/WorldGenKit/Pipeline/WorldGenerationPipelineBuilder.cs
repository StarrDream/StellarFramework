using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldGenerationCompileResult
    {
        public bool Success { get; }
        public WorldGenerationPlan Plan { get; }
        public WorldGenerationDiagnostic[] Diagnostics { get; }

        internal WorldGenerationCompileResult(
            bool success,
            WorldGenerationPlan plan,
            WorldGenerationDiagnostic[] diagnostics)
        {
            Success = success;
            Plan = plan;
            Diagnostics = diagnostics ?? Array.Empty<WorldGenerationDiagnostic>();
        }
    }

    public sealed class WorldGenerationPipelineBuilder
    {
        private readonly List<IWorldGenerationStage> _stages = new List<IWorldGenerationStage>();
        private WorldChannelRegistry _registry;
        private bool _compiledSuccessfully;

        public WorldChannelRegistryBuilder Channels { get; } = new WorldChannelRegistryBuilder();
        public int StageCount => _stages.Count;

        public void AddStage(IWorldGenerationStage stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (_compiledSuccessfully)
                throw new InvalidOperationException("Cannot add stages after the pipeline compiled successfully.");
            _stages.Add(stage);
        }

        public WorldGenerationCompileResult Compile()
        {
            if (_compiledSuccessfully)
                throw new InvalidOperationException("Pipeline builder has already produced a successful immutable plan.");

            if (_registry == null) _registry = Channels.Build();

            List<WorldGenerationDiagnostic> diagnostics = new List<WorldGenerationDiagnostic>();
            WorldGenerationStageDescriptor[] descriptors = new WorldGenerationStageDescriptor[_stages.Count];
            HashSet<WorldGenerationStageId> stageIds = new HashSet<WorldGenerationStageId>();

            for (int i = 0; i < _stages.Count; i++)
            {
                IWorldGenerationStage stage = _stages[i];
                WorldGenerationStageId stageId = stage.Id;

                if (!stageId.IsValid)
                {
                    diagnostics.Add(Error(
                        WorldGenerationDiagnosticIds.InvalidStageId,
                        stageId,
                        default(WorldDataChannelId),
                        "Stage ID is invalid."));
                }
                else if (!stageIds.Add(stageId))
                {
                    diagnostics.Add(Error(
                        WorldGenerationDiagnosticIds.DuplicateStageId,
                        stageId,
                        default(WorldDataChannelId),
                        "Duplicate Stage ID."));
                }

                WorldGenerationStageDescriptorBuilder descriptorBuilder =
                    new WorldGenerationStageDescriptorBuilder(_registry, stageId);
                stage.Describe(descriptorBuilder);
                descriptors[i] = descriptorBuilder.Build(out WorldGenerationDiagnostic[] stageDiagnostics);
                for (int d = 0; d < stageDiagnostics.Length; d++) diagnostics.Add(stageDiagnostics[d]);
            }

            int channelCount = _registry.Count;
            int[] producers = CreateFilled(channelCount, -1);
            int[] mutators = CreateFilled(channelCount, -1);

            DiscoverWriters(descriptors, producers, mutators, diagnostics);

            List<int>[] adjacency = new List<int>[_stages.Count];
            int[] indegree = new int[_stages.Count];
            HashSet<ulong> edges = new HashSet<ulong>();
            bool[] usedChannels = new bool[channelCount];

            for (int stageIndex = 0; stageIndex < descriptors.Length; stageIndex++)
            {
                WorldGenerationStageDescriptor descriptor = descriptors[stageIndex];

                AddInputDependencies(
                    descriptor.Required,
                    false,
                    stageIndex,
                    descriptor.StageId,
                    producers,
                    mutators,
                    adjacency,
                    indegree,
                    edges,
                    usedChannels,
                    diagnostics);

                AddInputDependencies(
                    descriptor.Optional,
                    true,
                    stageIndex,
                    descriptor.StageId,
                    producers,
                    mutators,
                    adjacency,
                    indegree,
                    edges,
                    usedChannels,
                    diagnostics);

                AddMutationDependencies(
                    descriptor,
                    stageIndex,
                    producers,
                    adjacency,
                    indegree,
                    edges,
                    usedChannels,
                    diagnostics);

                ReadOnlySpan<WorldStageChannelReference> produced = descriptor.Produced;
                for (int r = 0; r < produced.Length; r++) usedChannels[produced[r].ChannelIndex] = true;
            }

            int[] order = TopologicalSort(indegree, adjacency);
            if (order.Length != _stages.Count)
            {
                diagnostics.Add(Error(
                    WorldGenerationDiagnosticIds.DependencyCycle,
                    default(WorldGenerationStageId),
                    default(WorldDataChannelId),
                    "Stage dependency graph contains a cycle."));
            }

            if (HasErrors(diagnostics))
                return new WorldGenerationCompileResult(false, null, diagnostics.ToArray());

            WorldGenerationPlan.CompiledStage[] compiled = new WorldGenerationPlan.CompiledStage[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                int original = order[i];
                compiled[i] = new WorldGenerationPlan.CompiledStage(_stages[original], descriptors[original]);
            }

            int[] usedIndices = BuildUsedChannelIndexArray(usedChannels);
            ulong planHash = ComputePlanHash(_registry, compiled);
            WorldGenerationPlan plan = new WorldGenerationPlan(_registry, compiled, usedIndices, planHash);
            _compiledSuccessfully = true;
            return new WorldGenerationCompileResult(true, plan, diagnostics.ToArray());
        }

        private void DiscoverWriters(
            WorldGenerationStageDescriptor[] descriptors,
            int[] producers,
            int[] mutators,
            List<WorldGenerationDiagnostic> diagnostics)
        {
            for (int stageIndex = 0; stageIndex < descriptors.Length; stageIndex++)
            {
                WorldGenerationStageDescriptor descriptor = descriptors[stageIndex];
                ReadOnlySpan<WorldStageChannelReference> produced = descriptor.Produced;
                for (int r = 0; r < produced.Length; r++)
                {
                    int channelIndex = produced[r].ChannelIndex;
                    _registry.TryGetDescriptor(channelIndex, out WorldChannelDescriptor channel);

                    if (channel.SourceMode == WorldChannelSourceMode.ProvidedInput)
                    {
                        diagnostics.Add(Error(
                            WorldGenerationDiagnosticIds.ProvidedChannelProduced,
                            descriptor.StageId,
                            channel.Id,
                            "A ProvidedInput Channel cannot also be produced by a stage. Use Mutate for intentional modification."));
                    }

                    if (producers[channelIndex] >= 0)
                    {
                        diagnostics.Add(Error(
                            WorldGenerationDiagnosticIds.DuplicateProducer,
                            descriptor.StageId,
                            channel.Id,
                            "A Channel may have only one producer."));
                    }
                    else
                    {
                        producers[channelIndex] = stageIndex;
                    }
                }

                ReadOnlySpan<WorldStageChannelReference> mutated = descriptor.Mutated;
                for (int r = 0; r < mutated.Length; r++)
                {
                    int channelIndex = mutated[r].ChannelIndex;
                    _registry.TryGetDescriptor(channelIndex, out WorldChannelDescriptor channel);
                    if (mutators[channelIndex] >= 0)
                    {
                        diagnostics.Add(Error(
                            WorldGenerationDiagnosticIds.DuplicateMutator,
                            descriptor.StageId,
                            channel.Id,
                            "Core allows at most one mutator per Channel so execution order stays explicit."));
                    }
                    else
                    {
                        mutators[channelIndex] = stageIndex;
                    }
                }
            }
        }

        private void AddInputDependencies(
            ReadOnlySpan<WorldStageChannelReference> inputs,
            bool optional,
            int consumer,
            WorldGenerationStageId consumerId,
            int[] producers,
            int[] mutators,
            List<int>[] adjacency,
            int[] indegree,
            HashSet<ulong> edges,
            bool[] usedChannels,
            List<WorldGenerationDiagnostic> diagnostics)
        {
            for (int r = 0; r < inputs.Length; r++)
            {
                int channelIndex = inputs[r].ChannelIndex;
                if (!optional) usedChannels[channelIndex] = true;
                _registry.TryGetDescriptor(channelIndex, out WorldChannelDescriptor channel);

                int writer = mutators[channelIndex] >= 0 && mutators[channelIndex] != consumer
                    ? mutators[channelIndex]
                    : producers[channelIndex];

                if (writer >= 0)
                {
                    AddEdge(writer, consumer, consumerId, channel.Id, adjacency, indegree, edges, diagnostics);
                }
                else if (!optional && channel.SourceMode != WorldChannelSourceMode.ProvidedInput)
                {
                    diagnostics.Add(Error(
                        WorldGenerationDiagnosticIds.MissingProducer,
                        consumerId,
                        channel.Id,
                        "Required Channel has neither a producer nor ProvidedInput source."));
                }
            }
        }

        private void AddMutationDependencies(
            WorldGenerationStageDescriptor descriptor,
            int stageIndex,
            int[] producers,
            List<int>[] adjacency,
            int[] indegree,
            HashSet<ulong> edges,
            bool[] usedChannels,
            List<WorldGenerationDiagnostic> diagnostics)
        {
            ReadOnlySpan<WorldStageChannelReference> mutated = descriptor.Mutated;
            for (int r = 0; r < mutated.Length; r++)
            {
                int channelIndex = mutated[r].ChannelIndex;
                usedChannels[channelIndex] = true;
                _registry.TryGetDescriptor(channelIndex, out WorldChannelDescriptor channel);

                int producer = producers[channelIndex];
                if (producer >= 0)
                {
                    AddEdge(
                        producer,
                        stageIndex,
                        descriptor.StageId,
                        channel.Id,
                        adjacency,
                        indegree,
                        edges,
                        diagnostics);
                }
                else if (channel.SourceMode != WorldChannelSourceMode.ProvidedInput)
                {
                    diagnostics.Add(Error(
                        WorldGenerationDiagnosticIds.MissingProducer,
                        descriptor.StageId,
                        channel.Id,
                        "Mutated Channel has neither a producer nor ProvidedInput source."));
                }
            }
        }

        private static void AddEdge(
            int from,
            int to,
            WorldGenerationStageId consumerId,
            WorldDataChannelId channelId,
            List<int>[] adjacency,
            int[] indegree,
            HashSet<ulong> edges,
            List<WorldGenerationDiagnostic> diagnostics)
        {
            if (from == to)
            {
                diagnostics.Add(Error(
                    WorldGenerationDiagnosticIds.SelfDependency,
                    consumerId,
                    channelId,
                    "Stage depends on a Channel whose effective writer is itself."));
                return;
            }

            ulong edgeKey = ((ulong)(uint)from << 32) | (uint)to;
            if (!edges.Add(edgeKey)) return;

            if (adjacency[from] == null) adjacency[from] = new List<int>();
            adjacency[from].Add(to);
            indegree[to]++;
        }

        private static int[] TopologicalSort(int[] sourceIndegree, List<int>[] adjacency)
        {
            int count = sourceIndegree.Length;
            int[] indegree = new int[count];
            Array.Copy(sourceIndegree, indegree, count);
            int[] order = new int[count];
            bool[] emitted = new bool[count];
            int written = 0;

            while (written < count)
            {
                int next = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!emitted[i] && indegree[i] == 0)
                    {
                        next = i;
                        break;
                    }
                }

                if (next < 0) break;
                emitted[next] = true;
                order[written++] = next;

                List<int> outgoing = adjacency[next];
                if (outgoing == null) continue;
                for (int i = 0; i < outgoing.Count; i++) indegree[outgoing[i]]--;
            }

            if (written == count) return order;
            int[] partial = new int[written];
            Array.Copy(order, partial, written);
            return partial;
        }

        private static int[] BuildUsedChannelIndexArray(bool[] usedChannels)
        {
            int count = 0;
            for (int i = 0; i < usedChannels.Length; i++) if (usedChannels[i]) count++;

            int[] indices = new int[count];
            int write = 0;
            for (int i = 0; i < usedChannels.Length; i++)
            {
                if (usedChannels[i]) indices[write++] = i;
            }
            return indices;
        }

        private static int[] CreateFilled(int count, int value)
        {
            int[] result = new int[count];
            if (value != 0)
            {
                for (int i = 0; i < result.Length; i++) result[i] = value;
            }
            return result;
        }

        private static bool HasErrors(List<WorldGenerationDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == WorldGenerationDiagnosticSeverity.Error) return true;
            }
            return false;
        }

        private static WorldGenerationDiagnostic Error(
            WorldGenerationDiagnosticId code,
            WorldGenerationStageId stageId,
            WorldDataChannelId channelId,
            string message)
        {
            return new WorldGenerationDiagnostic(
                WorldGenerationDiagnosticSeverity.Error,
                code,
                stageId,
                channelId,
                message);
        }

        private static ulong ComputePlanHash(
            WorldChannelRegistry registry,
            WorldGenerationPlan.CompiledStage[] stages)
        {
            ulong hash = 0x5354454C4C415247UL;
            for (int i = 0; i < stages.Length; i++)
            {
                WorldGenerationStageDescriptor descriptor = stages[i].Descriptor;
                hash = WorldGenStableHash64.Combine(hash, WorldGenStableHash64.HashString(descriptor.StageId.Value));
                hash = WorldGenStableHash64.Combine(hash, (ulong)descriptor.SeedScope);
                HashRefs(ref hash, registry, descriptor.Required, 1UL);
                HashRefs(ref hash, registry, descriptor.Optional, 2UL);
                HashRefs(ref hash, registry, descriptor.Produced, 3UL);
                HashRefs(ref hash, registry, descriptor.Mutated, 4UL);
            }
            return hash;
        }

        private static void HashRefs(
            ref ulong hash,
            WorldChannelRegistry registry,
            ReadOnlySpan<WorldStageChannelReference> refs,
            ulong category)
        {
            hash = WorldGenStableHash64.Combine(hash, category);
            hash = WorldGenStableHash64.Combine(hash, (ulong)refs.Length);
            for (int i = 0; i < refs.Length; i++)
            {
                registry.TryGetDescriptor(refs[i].ChannelIndex, out WorldChannelDescriptor channel);
                hash = WorldGenStableHash64.Combine(hash, WorldGenStableHash64.HashString(channel.Id.Value));
            }
        }
    }
}
