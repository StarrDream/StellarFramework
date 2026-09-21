using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldWaterDepthStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.water_depth");

        private readonly ChannelHandle<float> _height;
        private readonly ChannelHandle<float> _waterDepth;
        private readonly WorldPlanarSampleLayout _layout;
        private readonly float _seaLevel;

        public WorldGenerationStageId Id => StableId;

        public WorldWaterDepthStage(
            ChannelHandle<float> height,
            ChannelHandle<float> waterDepth,
            WorldPlanarSampleLayout layout,
            float seaLevel)
        {
            if (float.IsNaN(seaLevel) || float.IsInfinity(seaLevel))
                throw new ArgumentOutOfRangeException(nameof(seaLevel));
            _height = height;
            _waterDepth = waterDepth;
            _layout = layout;
            _seaLevel = seaLevel;
        }

        public void Describe(WorldGenerationStageDescriptorBuilder builder)
        {
            builder.SetSeedScope(WorldGenerationSeedScope.World);
            builder.Require(_height);
            builder.Produce(_waterDepth);
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
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(_waterDepth, out DenseChannelStorage<float> water))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (height.Length < _layout.Count || water.Length < _layout.Count)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            ReadOnlySpan<float> heights = height.AsReadOnlySpan();
            Span<float> depths = water.AsSpan();
            for (int y = region.Y; y < region.TopExclusive; y++)
            {
                int row = y * _layout.Width;
                for (int x = region.X; x < region.RightExclusive; x++)
                {
                    int i = row + x;
                    float depth = _seaLevel - heights[i];
                    depths[i] = depth > 0f ? depth : 0f;
                }
            }

            return WorldGenerationStageResult.Succeeded();
        }
    }
}
