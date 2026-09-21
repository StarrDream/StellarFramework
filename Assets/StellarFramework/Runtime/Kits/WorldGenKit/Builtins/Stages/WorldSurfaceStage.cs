using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldSurfaceStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.surface");

        private readonly ChannelHandle<int> _biomeIndex;
        private readonly ChannelHandle<int> _surfaceIndex;
        private readonly WorldPlanarSampleLayout _layout;
        private readonly int[] _biomeToSurface;

        public WorldGenerationStageId Id => StableId;

        public WorldSurfaceStage(
            ChannelHandle<int> biomeIndex,
            ChannelHandle<int> surfaceIndex,
            WorldPlanarSampleLayout layout,
            WorldBiomeCatalog biomes,
            WorldSurfaceCatalog surfaces)
        {
            if (biomes == null) throw new ArgumentNullException(nameof(biomes));
            if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
            _biomeIndex = biomeIndex;
            _surfaceIndex = surfaceIndex;
            _layout = layout;
            _biomeToSurface = new int[biomes.Count];

            for (int i = 0; i < biomes.Count; i++)
            {
                WorldSurfaceId surfaceId = biomes.GetDefinition(i).SurfaceId;
                if (!surfaces.TryGetIndex(surfaceId, out int mapped))
                    throw new ArgumentException("Biome surface is missing from surface catalog: " + surfaceId, nameof(surfaces));
                _biomeToSurface[i] = mapped;
            }
        }

        public void Describe(WorldGenerationStageDescriptorBuilder builder)
        {
            builder.SetSeedScope(WorldGenerationSeedScope.World);
            builder.Require(_biomeIndex);
            builder.Produce(_surfaceIndex);
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
            if (!data.TryGetStorage<int, DenseChannelStorage<int>>(_biomeIndex, out DenseChannelStorage<int> biomes))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (!data.TryGetStorage<int, DenseChannelStorage<int>>(_surfaceIndex, out DenseChannelStorage<int> surfaces))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (biomes.Length < _layout.Count || surfaces.Length < _layout.Count)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            ReadOnlySpan<int> biomeValues = biomes.AsReadOnlySpan();
            Span<int> surfaceValues = surfaces.AsSpan();
            for (int y = region.Y; y < region.TopExclusive; y++)
            {
                int row = y * _layout.Width;
                for (int x = region.X; x < region.RightExclusive; x++)
                {
                    int i = row + x;
                    int biome = biomeValues[i];
                    if ((uint)biome >= (uint)_biomeToSurface.Length)
                        return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.InvalidBiomeIndex);
                    surfaceValues[i] = _biomeToSurface[biome];
                }
            }

            return WorldGenerationStageResult.Succeeded();
        }
    }
}
