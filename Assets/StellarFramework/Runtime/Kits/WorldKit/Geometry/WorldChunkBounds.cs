using System;

namespace StellarFramework.WorldKit
{
    /// <summary>Finite planar chunk bounds using Min-inclusive / Max-exclusive semantics.</summary>
    public readonly struct WorldChunkBounds : IEquatable<WorldChunkBounds>
    {
        public WorldChunkCoord Min { get; }
        public WorldChunkCoord MaxExclusive { get; }

        public bool IsEmpty => Min.X == MaxExclusive.X || Min.Y == MaxExclusive.Y;
        public ulong Width => unchecked((ulong)MaxExclusive.X - (ulong)Min.X);
        public ulong Height => unchecked((ulong)MaxExclusive.Y - (ulong)Min.Y);

        public WorldChunkBounds(WorldChunkCoord min, WorldChunkCoord maxExclusive)
        {
            if (maxExclusive.X < min.X)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "MaxExclusive.X cannot be less than Min.X.");
            if (maxExclusive.Y < min.Y)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "MaxExclusive.Y cannot be less than Min.Y.");

            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(WorldChunkCoord coord)
        {
            return coord.X >= Min.X && coord.X < MaxExclusive.X &&
                   coord.Y >= Min.Y && coord.Y < MaxExclusive.Y;
        }

        public bool TryGetArea(out ulong area)
        {
            ulong width = Width;
            ulong height = Height;
            if (width != 0UL && height > ulong.MaxValue / width)
            {
                area = 0UL;
                return false;
            }

            area = width * height;
            return true;
        }

        public bool Equals(WorldChunkBounds other) => Min == other.Min && MaxExclusive == other.MaxExclusive;
        public override bool Equals(object obj) => obj is WorldChunkBounds other && Equals(other);
        public override int GetHashCode() => unchecked((Min.GetHashCode() * 397) ^ MaxExclusive.GetHashCode());
        public override string ToString() => string.Format("[{0}, {1})", Min, MaxExclusive);

        public static bool operator ==(WorldChunkBounds left, WorldChunkBounds right) => left.Equals(right);
        public static bool operator !=(WorldChunkBounds left, WorldChunkBounds right) => !left.Equals(right);
    }
}
