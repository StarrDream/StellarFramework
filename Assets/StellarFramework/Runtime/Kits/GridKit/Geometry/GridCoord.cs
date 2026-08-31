using System;

namespace StellarFramework
{
    /// <summary>绝对二维逻辑网格坐标。+X 永远向右，+Y 永远向上。</summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public int X { get; }
        public int Y { get; }

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridCoord operator +(GridCoord coord, GridOffset offset) =>
            GridMath.OffsetChecked(coord, offset);

        public static GridCoord operator -(GridCoord coord, GridOffset offset)
        {
            long x = (long)coord.X - offset.X;
            long y = (long)coord.Y - offset.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                throw new OverflowException("GridCoord subtraction overflowed Int32.");
            }

            return new GridCoord((int)x, (int)y);
        }

        public static GridOffset operator -(GridCoord left, GridCoord right)
        {
            long x = (long)left.X - right.X;
            long y = (long)left.Y - right.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                throw new OverflowException("GridCoord difference cannot be represented by GridOffset.");
            }

            return new GridOffset((int)x, (int)y);
        }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord && Equals((GridCoord)obj);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => string.Format("({0}, {1})", X, Y);

        public static bool operator ==(GridCoord left, GridCoord right) => left.Equals(right);
        public static bool operator !=(GridCoord left, GridCoord right) => !left.Equals(right);
    }
}
