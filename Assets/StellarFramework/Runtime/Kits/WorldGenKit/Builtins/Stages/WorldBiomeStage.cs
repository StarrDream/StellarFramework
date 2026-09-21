using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldBiomeStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.biome");

        private readonly ChannelHandle<float> _height;
        private readonly ChannelHandle<float>? _moisture;
        private readonly ChannelHandle<float>? _waterDepth;
        private readonly ChannelHandle<float>? _slope;
        private readonly ChannelHandle<int> _biomeIndex;
        private readonly WorldPlanarSampleLayout _layout;
        private readonly WorldBiomeCatalog _catalog;

        public WorldGenerationStageId Id => StableId;

        public WorldBiomeStage(
            ChannelHandle<float> height,
            ChannelHandle<int> biomeIndex,
            WorldPlanarSampleLayout layout,
            WorldBiomeCatalog catalog,
            ChannelHandle<float>? moisture = null,
            ChannelHandle<float>? waterDepth = null,
            ChannelHandle<float>? slope = null)
        {
            _height = height;
            _biomeIndex = biomeIndex;
            _layout = layout;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _moisture = moisture;
            _waterDepth = waterDepth;
            _slope = slope;
        }

        public void Describe(WorldGenerationStageDescriptorBuilder builder)
        {
            builder.SetSeedScope(WorldGenerationSeedScope.World);
            builder.Require(_height);
            if (_moisture.HasValue) builder.Optional(_moisture.Value);
            if (_waterDepth.HasValue) builder.Optional(_waterDepth.Value);
            if (_slope.HasValue) builder.Optional(_slope.Value);
            builder.Produce(_biomeIndex);
        }

        public WorldGenerationStageResult Execute(in WorldGenerationContext context)
        {
            WorldPlanarSampleRegion whole = WorldPlanarSampleRegion.Whole(in _layout);
            return ExecuteRegion(context.Data, in whole);
        }

        public WorldGenerationStageResult ExecuteRegion(
            WorldGenerationDataSet data,
            in WorldPlanarSampleRegion region)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!region.FitsWithin(in _layout))
                throw new ArgumentOutOfRangeException(nameof(region));
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(_height, out DenseChannelStorage<float> height))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (!data.TryGetStorage<int, DenseChannelStorage<int>>(_biomeIndex, out DenseChannelStorage<int> biomes))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (height.Length < _layout.Count || biomes.Length < _layout.Count)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            bool hasMoisture = TryOptional(data, _moisture, out DenseChannelStorage<float> moisture, out bool moistureInvalid);
            bool hasWater = TryOptional(data, _waterDepth, out DenseChannelStorage<float> water, out bool waterInvalid);
            bool hasSlope = TryOptional(data, _slope, out DenseChannelStorage<float> slope, out bool slopeInvalid);
            if (moistureInvalid || waterInvalid || slopeInvalid)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if ((hasMoisture && moisture.Length < _layout.Count) ||
                (hasWater && water.Length < _layout.Count) ||
                (hasSlope && slope.Length < _layout.Count))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            ReadOnlySpan<float> heights = height.AsReadOnlySpan();
            ReadOnlySpan<float> moistures = hasMoisture ? moisture.AsReadOnlySpan() : default(ReadOnlySpan<float>);
            ReadOnlySpan<float> waters = hasWater ? water.AsReadOnlySpan() : default(ReadOnlySpan<float>);
            ReadOnlySpan<float> slopes = hasSlope ? slope.AsReadOnlySpan() : default(ReadOnlySpan<float>);
            Span<int> output = biomes.AsSpan();

            for (int y = region.Y; y < region.TopExclusive; y++)
            {
                int row = y * _layout.Width;
                for (int x = region.X; x < region.RightExclusive; x++)
                {
                    int sample = row + x;
                    int selected = _catalog.FallbackIndex;
                    int bestPriority = int.MinValue;
                    int bestTieRank = int.MaxValue;

                    for (int biomeIndex = 0; biomeIndex < _catalog.Count; biomeIndex++)
                    {
                        if (biomeIndex == _catalog.FallbackIndex) continue;
                        WorldBiomeDefinition definition = _catalog.GetDefinition(biomeIndex);
                        if (!definition.Criteria.Matches(
                                heights[sample],
                                hasMoisture,
                                hasMoisture ? moistures[sample] : 0f,
                                hasWater,
                                hasWater ? waters[sample] : 0f,
                                hasSlope,
                                hasSlope ? slopes[sample] : 0f))
                            continue;

                        int tieRank = _catalog.GetTieRank(biomeIndex);
                        if (definition.Priority > bestPriority ||
                            (definition.Priority == bestPriority && tieRank < bestTieRank))
                        {
                            selected = biomeIndex;
                            bestPriority = definition.Priority;
                            bestTieRank = tieRank;
                        }
                    }

                    output[sample] = selected;
                }
            }

            return WorldGenerationStageResult.Succeeded();
        }

        private static bool TryOptional(
            WorldGenerationDataSet data,
            ChannelHandle<float>? handle,
            out DenseChannelStorage<float> storage,
            out bool invalidConcreteStorage)
        {
            storage = null;
            invalidConcreteStorage = false;
            if (!handle.HasValue || !data.IsBound(handle.Value)) return false;
            if (data.TryGetStorage<float, DenseChannelStorage<float>>(handle.Value, out storage)) return true;
            invalidConcreteStorage = true;
            return false;
        }
    }
}
