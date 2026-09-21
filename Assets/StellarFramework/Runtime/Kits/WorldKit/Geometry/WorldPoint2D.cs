using System;

namespace StellarFramework.WorldKit
{
    /// <summary>Continuous logical planar position, independent from Unity Transform precision.</summary>
    public readonly struct WorldPoint2D : IEquatable<WorldPoint2D>
    {
        public double X { get; }
        public double Y { get; }

        public WorldPoint2D(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                throw new ArgumentOutOfRangeException(nameof(x), x, "World X must be finite.");
            if (double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(y), y, "World Y must be finite.");

            X = x;
            Y = y;
        }

        public bool Equals(WorldPoint2D other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is WorldPoint2D other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => string.Format("({0:R}, {1:R})", X, Y);

        public static bool operator ==(WorldPoint2D left, WorldPoint2D right) => left.Equals(right);
        public static bool operator !=(WorldPoint2D left, WorldPoint2D right) => !left.Equals(right);
    }
}
