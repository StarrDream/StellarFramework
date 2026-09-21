using System;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming
{
    public readonly struct WorldChunkDemand : IEquatable<WorldChunkDemand>
    {
        public WorldChunkCoord Coord { get; }
        public WorldStreamingTier Tier { get; }

        public WorldChunkDemand(WorldChunkCoord coord, WorldStreamingTier tier)
        {
            if (tier < WorldStreamingTier.Metadata || tier > WorldStreamingTier.Presentation)
                throw new ArgumentOutOfRangeException(nameof(tier));

            Coord = coord;
            Tier = tier;
        }

        public bool Equals(WorldChunkDemand other) =>
            Coord == other.Coord && Tier == other.Tier;

        public override bool Equals(object obj) =>
            obj is WorldChunkDemand other && Equals(other);

        public override int GetHashCode() =>
            unchecked((Coord.GetHashCode() * 397) ^ (int)Tier);

        public static bool operator ==(WorldChunkDemand left, WorldChunkDemand right) => left.Equals(right);
        public static bool operator !=(WorldChunkDemand left, WorldChunkDemand right) => !left.Equals(right);
    }
}
