using System;

namespace StellarFramework.WorldKit
{
    /// <summary>Planar V1 macro/generation region coordinate.</summary>
    public readonly struct WorldRegionCoord : IEquatable<WorldRegionCoord>
    {
        public long X { get; }
        public long Y { get; }

        public WorldRegionCoord(long x, long y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(WorldRegionCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is WorldRegionCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => string.Format("({0}, {1})", X, Y);

        public static bool operator ==(WorldRegionCoord left, WorldRegionCoord right) => left.Equals(right);
        public static bool operator !=(WorldRegionCoord left, WorldRegionCoord right) => !left.Equals(right);
    }
}
