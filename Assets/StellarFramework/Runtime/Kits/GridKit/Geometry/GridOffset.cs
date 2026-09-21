using System;

namespace StellarFramework
{
    /// <summary>相对二维网格位移，不表示绝对位置。</summary>
    public readonly struct GridOffset : IEquatable<GridOffset>
    {
        /// <summary>获取 X 方向相对位移。</summary>
        public int X { get; }

        /// <summary>获取 Y 方向相对位移。</summary>
        public int Y { get; }

        /// <summary>获取零位移。</summary>
        public static GridOffset Zero => new GridOffset(0, 0);

        /// <summary>创建一个相对二维网格位移。</summary>
        public GridOffset(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridOffset operator +(GridOffset left, GridOffset right)
        {
            long x = (long)left.X + right.X;
            long y = (long)left.Y + right.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                throw new OverflowException("GridOffset addition overflowed Int32.");
            }

            return new GridOffset((int)x, (int)y);
        }

        public static GridOffset operator -(GridOffset left, GridOffset right)
        {
            long x = (long)left.X - right.X;
            long y = (long)left.Y - right.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                throw new OverflowException("GridOffset subtraction overflowed Int32.");
            }

            return new GridOffset((int)x, (int)y);
        }

        /// <inheritdoc />
        public bool Equals(GridOffset other) => X == other.X && Y == other.Y;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GridOffset && Equals((GridOffset)obj);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        /// <inheritdoc />
        public override string ToString() => string.Format("({0}, {1})", X, Y);

        public static bool operator ==(GridOffset left, GridOffset right) => left.Equals(right);
        public static bool operator !=(GridOffset left, GridOffset right) => !left.Equals(right);
    }
}
