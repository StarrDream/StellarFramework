using System;

namespace StellarFramework
{
    /// <summary>固定顺序、caller-owned buffer 的邻居查询。</summary>
    public static class GridNeighbors
    {
        public static int WriteNeighbors4(GridCoord center, Span<GridCoord> destination)
        {
            RequireCapacity(destination.Length, 4);
            int written = 0;
            TryWrite(center, new GridOffset(0, 1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(1, 0), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(0, -1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(-1, 0), default(GridRect), false, destination, ref written);
            return written;
        }

        public static int WriteNeighbors4(GridCoord center, GridRect bounds, Span<GridCoord> destination)
        {
            RequireCapacity(destination.Length, 4);
            int written = 0;
            TryWrite(center, new GridOffset(0, 1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(1, 0), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(0, -1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(-1, 0), bounds, true, destination, ref written);
            return written;
        }

        public static int WriteNeighbors8(GridCoord center, Span<GridCoord> destination)
        {
            RequireCapacity(destination.Length, 8);
            int written = 0;
            TryWrite(center, new GridOffset(0, 1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(1, 1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(1, 0), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(1, -1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(0, -1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(-1, -1), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(-1, 0), default(GridRect), false, destination, ref written);
            TryWrite(center, new GridOffset(-1, 1), default(GridRect), false, destination, ref written);
            return written;
        }

        public static int WriteNeighbors8(GridCoord center, GridRect bounds, Span<GridCoord> destination)
        {
            RequireCapacity(destination.Length, 8);
            int written = 0;
            TryWrite(center, new GridOffset(0, 1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(1, 1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(1, 0), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(1, -1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(0, -1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(-1, -1), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(-1, 0), bounds, true, destination, ref written);
            TryWrite(center, new GridOffset(-1, 1), bounds, true, destination, ref written);
            return written;
        }

        private static void TryWrite(GridCoord center, GridOffset offset, GridRect bounds, bool filterBounds,
            Span<GridCoord> destination, ref int written)
        {
            if (!GridMath.TryOffset(center, offset, out GridCoord candidate)) return;
            if (filterBounds && !bounds.Contains(candidate)) return;
            destination[written++] = candidate;
        }

        private static void RequireCapacity(int actual, int required)
        {
            if (actual < required)
            {
                throw new ArgumentException("Destination buffer is smaller than the maximum neighbor count.", nameof(actual));
            }
        }
    }
}
