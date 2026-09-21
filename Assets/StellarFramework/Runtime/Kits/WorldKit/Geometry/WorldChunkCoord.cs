using System;

namespace StellarFramework.WorldKit
{
    /// <summary>Planar V1 chunk coordinate. Negative coordinates are valid.</summary>
    public readonly struct WorldChunkCoord : IEquatable<WorldChunkCoord>
    {
        public long X { get; }
        public long Y { get; }

        public WorldChunkCoord(long x, long y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(WorldChunkCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is WorldChunkCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => string.Format("({0}, {1})", X, Y);

        public static bool operator ==(WorldChunkCoord left, WorldChunkCoord right) => left.Equals(right);
        public static bool operator !=(WorldChunkCoord left, WorldChunkCoord right) => !left.Equals(right);
    }
}
