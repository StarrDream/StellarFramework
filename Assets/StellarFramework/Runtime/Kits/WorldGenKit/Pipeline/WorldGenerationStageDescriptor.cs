using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldStageChannelReference
    {
        public int ChannelIndex { get; }

        internal WorldStageChannelReference(int channelIndex) => ChannelIndex = channelIndex;
    }

    public sealed class WorldGenerationStageDescriptor
    {
        private readonly WorldStageChannelReference[] _required;
        private readonly WorldStageChannelReference[] _optional;
        private readonly WorldStageChannelReference[] _produced;
        private readonly WorldStageChannelReference[] _mutated;

        public WorldGenerationStageId StageId { get; }
        public WorldGenerationSeedScope SeedScope { get; }
        public ReadOnlySpan<WorldStageChannelReference> Required => _required;
        public ReadOnlySpan<WorldStageChannelReference> Optional => _optional;
        public ReadOnlySpan<WorldStageChannelReference> Produced => _produced;
        public ReadOnlySpan<WorldStageChannelReference> Mutated => _mutated;

        internal WorldGenerationStageDescriptor(
            WorldGenerationStageId stageId,
            WorldGenerationSeedScope seedScope,
            WorldStageChannelReference[] required,
            WorldStageChannelReference[] optional,
            WorldStageChannelReference[] produced,
            WorldStageChannelReference[] mutated)
        {
            StageId = stageId;
            SeedScope = seedScope;
            _required = required;
            _optional = optional;
            _produced = produced;
            _mutated = mutated;
        }
    }

    public sealed class WorldGenerationStageDescriptorBuilder
    {
        private readonly WorldChannelRegistry _registry;
        private readonly WorldGenerationStageId _stageId;
        private readonly List<WorldStageChannelReference> _required = new List<WorldStageChannelReference>();
        private readonly List<WorldStageChannelReference> _optional = new List<WorldStageChannelReference>();
        private readonly List<WorldStageChannelReference> _produced = new List<WorldStageChannelReference>();
        private readonly List<WorldStageChannelReference> _mutated = new List<WorldStageChannelReference>();
        private readonly HashSet<int> _allReferences = new HashSet<int>();
        private readonly List<WorldGenerationDiagnostic> _diagnostics = new List<WorldGenerationDiagnostic>();
        private WorldGenerationSeedScope _seedScope;

        internal WorldGenerationStageDescriptorBuilder(WorldChannelRegistry registry, WorldGenerationStageId stageId)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _stageId = stageId;
        }

        public void SetSeedScope(WorldGenerationSeedScope seedScope)
        {
            if (seedScope <= WorldGenerationSeedScope.None || seedScope > WorldGenerationSeedScope.RunKey)
                throw new ArgumentOutOfRangeException(nameof(seedScope));
            _seedScope = seedScope;
        }

        public bool Require<T>(ChannelHandle<T> handle) => Add(handle, _required);
        public bool Optional<T>(ChannelHandle<T> handle) => Add(handle, _optional);
        public bool Produce<T>(ChannelHandle<T> handle) => Add(handle, _produced);
        public bool Mutate<T>(ChannelHandle<T> handle) => Add(handle, _mutated);

        internal WorldGenerationStageDescriptor Build(out WorldGenerationDiagnostic[] diagnostics)
        {
            if (_seedScope == WorldGenerationSeedScope.None)
            {
                _diagnostics.Add(new WorldGenerationDiagnostic(
                    WorldGenerationDiagnosticSeverity.Error,
                    WorldGenerationDiagnosticIds.MissingSeedScope,
                    _stageId,
                    default(WorldDataChannelId),
                    "Stage did not declare a deterministic seed scope."));
            }

            diagnostics = _diagnostics.ToArray();
            return new WorldGenerationStageDescriptor(
                _stageId,
                _seedScope,
                _required.ToArray(),
                _optional.ToArray(),
                _produced.ToArray(),
                _mutated.ToArray());
        }

        private bool Add<T>(ChannelHandle<T> handle, List<WorldStageChannelReference> destination)
        {
            if (!_registry.IsHandleValid(handle))
            {
                _diagnostics.Add(new WorldGenerationDiagnostic(
                    WorldGenerationDiagnosticSeverity.Error,
                    WorldGenerationDiagnosticIds.InvalidChannelHandle,
                    _stageId,
                    default(WorldDataChannelId),
                    "Stage references a Channel handle from another registry or with the wrong type."));
                return false;
            }

            if (!_allReferences.Add(handle.Index))
            {
                _registry.TryGetDescriptor(handle.Index, out WorldChannelDescriptor descriptor);
                _diagnostics.Add(new WorldGenerationDiagnostic(
                    WorldGenerationDiagnosticSeverity.Error,
                    WorldGenerationDiagnosticIds.DuplicateChannelReference,
                    _stageId,
                    descriptor.Id,
                    "A stage may reference a Channel only once across Require/Optional/Produce/Mutate."));
                return false;
            }

            destination.Add(new WorldStageChannelReference(handle.Index));
            return true;
        }
    }
}
