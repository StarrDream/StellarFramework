using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldSlopeStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.slope");

        private readonly ChannelHandle<float> _height;
        private readonly ChannelHandle<float> _slope;
        private readonly WorldPlanarSampleLayout _layout;

        public WorldGenerationStageId Id => StableId;

        public WorldSlopeStage(
            ChannelHandle<float> height,
            ChannelHandle<float> slope,
            WorldPlanarSampleLayout layout)
        {
            _height = height;
            _slope = slope;
            _layout = layout;
        }

        public void Describe(WorldGenerationStageDescriptorBuilder builder)
        {
            builder.SetSeedScope(WorldGenerationSeedScope.World);
            builder.Require(_height);
            builder.Produce(_slope);
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
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(_slope, out DenseChannelStorage<float> slope))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);
            if (height.Length < _layout.Count || slope.Length < _layout.Count)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            ReadOnlySpan<float> heights = height.AsReadOnlySpan();
            Span<float> slopes = slope.AsSpan();
            double step = _layout.SampleStep;

            for (int y = region.Y; y < region.TopExclusive; y++)
            {
                int row = y * _layout.Width;
                for (int x = region.X; x < region.RightExclusive; x++)
                {
                    int index = row + x;
                    double dx = DerivativeX(heights, x, y, step);
                    double dy = DerivativeY(heights, x, y, step);
                    slopes[index] = (float)Math.Sqrt((dx * dx) + (dy * dy));
                }
            }

            return WorldGenerationStageResult.Succeeded();
        }

        private double DerivativeX(ReadOnlySpan<float> values, int x, int y, double step)
        {
            if (_layout.Width == 1) return 0d;
            int row = y * _layout.Width;
            if (x == 0) return (values[row + 1] - values[row]) / step;
            if (x == _layout.Width - 1) return (values[row + x] - values[row + x - 1]) / step;
            return (values[row + x + 1] - values[row + x - 1]) / (2d * step);
        }

        private double DerivativeY(ReadOnlySpan<float> values, int x, int y, double step)
        {
            if (_layout.Height == 1) return 0d;
            if (y == 0) return (values[_layout.Width + x] - values[x]) / step;
            if (y == _layout.Height - 1)
            {
                int row = y * _layout.Width;
                return (values[row + x] - values[row - _layout.Width + x]) / step;
            }
            int previous = ((y - 1) * _layout.Width) + x;
            int next = ((y + 1) * _layout.Width) + x;
            return (values[next] - values[previous]) / (2d * step);
        }
    }
}
