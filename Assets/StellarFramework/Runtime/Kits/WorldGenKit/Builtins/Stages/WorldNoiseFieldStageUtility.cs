using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    internal static class WorldNoiseFieldStageUtility
    {
        internal static WorldGenerationStageResult Fill(
            in WorldGenerationContext context,
            ChannelHandle<float> output,
            in WorldPlanarSampleLayout layout,
            in WorldFractalNoiseSettings noise,
            float minValue,
            float maxValue)
        {
            if (!context.Data.TryGetStorage<float, DenseChannelStorage<float>>(output, out DenseChannelStorage<float> storage))
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.DenseStorageRequired);

            if (storage.Length < layout.Count)
                return WorldGenerationStageResult.Failed(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch);

            Span<float> values = storage.AsSpan();
            double range = (double)maxValue - minValue;
            WorldGenerationRunKey runKey = context.RunKey;
            int index = 0;
            for (int y = 0; y < layout.Height; y++)
            {
                long absoluteY = layout.GetAbsoluteY(in runKey, y);
                for (int x = 0; x < layout.Width; x++)
                {
                    long absoluteX = layout.GetAbsoluteX(in runKey, x);
                    double sample = WorldFractalValueNoise.Sample01(
                        context.StageSeed,
                        absoluteX,
                        absoluteY,
                        in noise);
                    values[index++] = (float)(minValue + (sample * range));
                }
            }

            return WorldGenerationStageResult.Succeeded();
        }

        internal static void ValidateRange(float minValue, float maxValue)
        {
            if (float.IsNaN(minValue) || float.IsInfinity(minValue))
                throw new ArgumentOutOfRangeException(nameof(minValue));
            if (float.IsNaN(maxValue) || float.IsInfinity(maxValue))
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            if (maxValue < minValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "Max value cannot be less than min value.");
        }
    }
}
