using System;

namespace StellarFramework
{
    /// <summary>GridKit 所需的安全整数数学，不包含世界、Chunk 或 Unity 语义。</summary>
    public static class GridMath
    {
        public static int FloorDiv(int value, int positiveDivisor)
        {
            ValidateDivisor(positiveDivisor);
            int quotient = value / positiveDivisor;
            int remainder = value % positiveDivisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        public static int FloorMod(int value, int positiveDivisor)
        {
            ValidateDivisor(positiveDivisor);
            int remainder = value % positiveDivisor;
            return remainder < 0 ? remainder + positiveDivisor : remainder;
        }

        public static bool TryOffset(GridCoord coord, GridOffset offset, out GridCoord result)
        {
            long x = (long)coord.X + offset.X;
            long y = (long)coord.Y + offset.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                result = default(GridCoord);
                return false;
            }

            result = new GridCoord((int)x, (int)y);
            return true;
        }

        public static GridCoord OffsetChecked(GridCoord coord, GridOffset offset)
        {
            if (!TryOffset(coord, offset, out GridCoord result))
            {
                throw new OverflowException("GridCoord offset overflowed Int32.");
            }

            return result;
        }

        private static void ValidateDivisor(int positiveDivisor)
        {
            if (positiveDivisor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(positiveDivisor), "Divisor must be positive.");
            }
        }
    }
}
