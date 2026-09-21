using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldBuildableStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.buildable");

        private readonly ChannelHandle<float> _slope;
        private readonly ChannelHandle<float> _waterDepth;
        private readonly ChannelHandle<byte> _buildable;
        private readonly ChannelHandle<int>? _biomeIndex;
        private readonly WorldPlanarSampleLayout _layout;
        private readonly float _maxSlope;
        private readonly float _maxWaterDepth;
        private readonly bool[] _blockedBiomeIndices;

        public WorldGenerationStageId Id => StableId;

        public WorldBuildableStage(
            ChannelHandle<float> slope,
            ChannelHandle<float> waterDepth,
            ChannelHandle<byte> buildable,
            WorldPlanarSampleLayout layout,
            WorldBuildableSettings settings,
            ChannelHandle<int>? biomeIndex = null,
            WorldBiomeCatalog biomeCatalog = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _slope = slope;
            _waterDepth = waterDepth;
            _buildable = buildable;
            _layout = layout;
            _maxSlope = settings.MaxSlope;
            _maxWaterDepth = settings.MaxWaterDepth;
            _biomeIndex = biomeIndex;

            if (settings.BlockedBiomes.Length > 0)
            {
                if (!biomeIndex.HasValue) throw new ArgumentException("Blocked biome policy requires a biome Channel handle.", nameof(biomeIndex));
                if (biomeCatalog == null) throw new ArgumentNullException(nameof(biomeCatalog));
                _blockedBiomeIndices = new bool[biomeCatalog.Count];
                ReadOnlySpan<WorldBiomeId> blocked = settings.BlockedBiomes;
                for (int i = 0; i < blocked.Length; i++)
                {
                    if (!biomeCatalog.TryGetIndex(blocked[i], out int index))
                        throw new ArgumentException("Blocked biome is missing from catalog: " + blocked[i], nameof(settings));
                    _blockedBiomeIndices[index] = true;
                }
            }
            else
            {
                _blockedBiomeIndices = Array.Empty<bool>();
            }
        }

        public void Describe(WorldGenerationStageDescriptorBuilder builder)
        {
            builder.SetSeedScope(WorldGenerationSeedScope.World);
            builder.Require(_slope);
            builder.Require(_waterDepth);
            if (_blockedBiomeIndices.Length > 0) builder.Require(_biomeIndex.Value);
            builder.Produce(_buildable);
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
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(_slope, out DenseChannelStorage<float> slope))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(_waterDepth, out DenseChannelStorage<float> water))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (!data.TryGetStorage<byte, DenseChannelStorage<byte>>(_buildable, out DenseChannelStorage<byte> buildable))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (slope.Length < _layout.Count || water.Length < _layout.Count || buildable.Length < _layout.Count)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            DenseChannelStorage<int> biome = null;
            if (_blockedBiomeIndices.Length > 0)
            {
                if (!data.TryGetStorage<int, DenseChannelStorage<int>>(_biomeIndex.Value, out biome))
                    return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
                if (biome.Length < _layout.Count)
                    return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);
            }

            ReadOnlySpan<float> slopes = slope.AsReadOnlySpan();
            ReadOnlySpan<float> waters = water.AsReadOnlySpan();
            ReadOnlySpan<int> biomes = biome == null ? default(ReadOnlySpan<int>) : biome.AsReadOnlySpan();
            Span<byte> output = buildable.AsSpan();

            for (int y = region.Y; y < region.TopExclusive; y++)
            {
                int row = y * _layout.Width;
                for (int x = region.X; x < region.RightExclusive; x++)
                {
                    int i = row + x;
                    bool allowed = slopes[i] <= _maxSlope && waters[i] <= _maxWaterDepth;
                    if (allowed && _blockedBiomeIndices.Length > 0)
                    {
                        int biomeIndex = biomes[i];
                        if ((uint)biomeIndex >= (uint)_blockedBiomeIndices.Length)
                            return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.InvalidBiomeIndex);
                        if (_blockedBiomeIndices[biomeIndex]) allowed = false;
                    }
                    output[i] = allowed ? (byte)1 : (byte)0;
                }
            }

            return WorldGenerationStageResult.Succeeded();
        }
    }
}
