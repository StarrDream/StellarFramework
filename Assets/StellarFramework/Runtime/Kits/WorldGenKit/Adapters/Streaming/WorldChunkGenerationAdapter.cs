using System;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming;

namespace StellarFramework.WorldGenKit.StreamingAdapter
{
    /// <summary>
    /// Stable mapping between WorldKit Chunk identity and WorldGen absolute planar run keys.
    /// This adapter owns no streaming state and does not allocate generation data.
    /// </summary>
    public static class WorldChunkGenerationAdapter
    {
        public static bool TryCreateRunKey(
            WorldChunkCoord chunk,
            in WorldPlanarSampleLayout layout,
            ulong localKey,
            out WorldGenerationRunKey runKey)
        {
            runKey = default(WorldGenerationRunKey);
            if (layout.Width <= 0 || layout.Height <= 0 || layout.SampleStep <= 0L)
                return false;

            try
            {
                long strideX = checked((long)layout.Width * layout.SampleStep);
                long strideY = checked((long)layout.Height * layout.SampleStep);
                long originX = checked(chunk.X * strideX);
                long originY = checked(chunk.Y * strideY);
                runKey = new WorldGenerationRunKey(originX, originY, localKey);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public static bool TryCreateRegionRunKey(
            WorldRegionCoord region,
            in WorldRegionLayout regionLayout,
            in WorldPlanarSampleLayout chunkLayout,
            ulong localKey,
            out WorldGenerationRunKey runKey)
        {
            runKey = default(WorldGenerationRunKey);
            if (!regionLayout.IsValid)
                return false;
            if (!regionLayout.TryGetChunkBounds(region, out WorldChunkBounds chunkBounds))
                return false;

            return TryCreateRunKey(
                chunkBounds.Min,
                in chunkLayout,
                localKey,
                out runKey);
        }

        public static WorldGenerationRunResult ExecuteChunk(
            WorldGenerationPlan plan,
            WorldGenerationDataSet data,
            WorldGenerationSeed worldSeed,
            WorldChunkCoord chunk,
            in WorldPlanarSampleLayout layout,
            ulong localKey,
            Span<WorldGenerationStageExecutionRecord> stageRecords,
            out WorldGenerationRunKey runKey)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!TryCreateRunKey(chunk, in layout, localKey, out runKey))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunk),
                    chunk,
                    "Chunk coordinate and planar layout cannot be represented as an Int64 absolute WorldGen run-key origin.");
            }

            return plan.Execute(data, worldSeed, runKey, stageRecords);
        }
    }
}
