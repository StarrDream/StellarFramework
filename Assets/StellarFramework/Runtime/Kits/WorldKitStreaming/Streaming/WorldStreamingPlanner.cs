using System;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming
{
    /// <summary>
    /// Stateless deterministic demand collector. Output order is row-major Y then X.
    /// All capacity/coordinate checks happen before destination mutation.
    /// </summary>
    public static class WorldStreamingPlanner
    {
        public static WorldStreamingTier GetDesiredTier(
            WorldChunkCoord focus,
            WorldChunkCoord coord,
            WorldExtent extent,
            in WorldStreamingPolicy policy)
        {
            if (!extent.IsValid)
                throw new ArgumentException("WorldExtent must be valid.", nameof(extent));
            if (!policy.IsValid)
                throw new ArgumentException("WorldStreamingPolicy must be valid.", nameof(policy));
            if (!extent.Contains(coord)) return WorldStreamingTier.None;

            ulong deltaX = AbsoluteDifference(focus.X, coord.X);
            ulong deltaY = AbsoluteDifference(focus.Y, coord.Y);
            ulong distance = deltaX >= deltaY ? deltaX : deltaY;
            if (distance > (ulong)policy.MetadataRadius)
                return WorldStreamingTier.None;
            return policy.GetTier((int)distance);
        }

        public static int GetRequiredDemandCount(
            WorldChunkCoord focus,
            WorldExtent extent,
            in WorldStreamingPolicy policy)
        {
            ValidateInputs(focus, extent, in policy, out long minX, out long maxX, out long minY, out long maxY);

            if (extent.IsFinite)
            {
                if (!extent.TryGetFiniteBounds(out WorldChunkBounds finite))
                    throw new InvalidOperationException("Finite WorldExtent did not expose finite bounds.");

                minX = Math.Max(minX, finite.Min.X);
                minY = Math.Max(minY, finite.Min.Y);
                maxX = Math.Min(maxX, finite.MaxExclusive.X - 1L);
                maxY = Math.Min(maxY, finite.MaxExclusive.Y - 1L);
                if (minX > maxX || minY > maxY) return 0;
            }

            ulong width = unchecked((ulong)maxX - (ulong)minX) + 1UL;
            ulong height = unchecked((ulong)maxY - (ulong)minY) + 1UL;
            if (width != 0UL && height > ulong.MaxValue / width)
                throw new InvalidOperationException("Streaming demand area exceeds the supported count range.");

            ulong count = width * height;
            if (count > int.MaxValue)
                throw new InvalidOperationException("Streaming demand count exceeds Int32 caller-buffer capacity.");
            return (int)count;
        }

        public static int CollectDesired(
            WorldChunkCoord focus,
            WorldExtent extent,
            in WorldStreamingPolicy policy,
            Span<WorldChunkDemand> destination)
        {
            int required = GetRequiredDemandCount(focus, extent, in policy);
            if (destination.Length < required)
                throw new ArgumentException(
                    "Destination buffer is smaller than the required streaming demand count.",
                    nameof(destination));

            if (required == 0) return 0;

            int radius = policy.MetadataRadius;
            int written = 0;
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                long y = focus.Y + offsetY;
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    long x = focus.X + offsetX;
                    WorldChunkCoord coord = new WorldChunkCoord(x, y);
                    if (!extent.Contains(coord)) continue;

                    int distance = Math.Max(Math.Abs(offsetX), Math.Abs(offsetY));
                    WorldStreamingTier tier = policy.GetTier(distance);
                    if (tier == WorldStreamingTier.None)
                        throw new InvalidOperationException("Demand planner produced a None tier inside Metadata radius.");
                    destination[written++] = new WorldChunkDemand(coord, tier);
                }
            }

            if (written != required)
                throw new InvalidOperationException("Streaming demand preflight count does not match emitted output.");
            return written;
        }

        private static void ValidateInputs(
            WorldChunkCoord focus,
            WorldExtent extent,
            in WorldStreamingPolicy policy,
            out long minX,
            out long maxX,
            out long minY,
            out long maxY)
        {
            if (!extent.IsValid)
                throw new ArgumentException("WorldExtent must be valid.", nameof(extent));
            if (!policy.IsValid)
                throw new ArgumentException("WorldStreamingPolicy must be valid.", nameof(policy));

            long radius = policy.MetadataRadius;
            try
            {
                minX = checked(focus.X - radius);
                maxX = checked(focus.X + radius);
                minY = checked(focus.Y - radius);
                maxY = checked(focus.Y + radius);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(focus),
                    focus,
                    "Streaming demand radius exceeds the representable Int64 Chunk coordinate range. " + exception.Message);
            }
        }

        private static ulong AbsoluteDifference(long a, long b)
        {
            ulong ua = unchecked((ulong)a);
            ulong ub = unchecked((ulong)b);
            if ((a < 0L) == (b < 0L))
                return a >= b ? ua - ub : ub - ua;

            return a >= 0L
                ? ua + unchecked((ulong)(-(b + 1L))) + 1UL
                : ub + unchecked((ulong)(-(a + 1L))) + 1UL;
        }
    }
}
