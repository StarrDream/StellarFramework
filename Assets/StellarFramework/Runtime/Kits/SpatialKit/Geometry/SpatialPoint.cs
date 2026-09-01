using System;

namespace StellarFramework
{
    /// <summary>连续二维空间中的点。坐标必须是有限浮点数。</summary>
    public readonly struct SpatialPoint : IEquatable<SpatialPoint>
    {
        public float X { get; }
        public float Y { get; }

        public SpatialPoint(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "SpatialPoint.X 必须是有限值。");
            }

            if (float.IsNaN(y) || float.IsInfinity(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "SpatialPoint.Y 必须是有限值。");
            }

            X = x;
            Y = y;
        }

        public bool Equals(SpatialPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is SpatialPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => string.Format("({0}, {1})", X, Y);

        public static bool operator ==(SpatialPoint left, SpatialPoint right) => left.Equals(right);
        public static bool operator !=(SpatialPoint left, SpatialPoint right) => !left.Equals(right);
    }
}
