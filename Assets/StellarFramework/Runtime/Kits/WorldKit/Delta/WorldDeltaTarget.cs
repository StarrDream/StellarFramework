using System;

namespace StellarFramework.WorldKit
{
    public enum WorldDeltaTargetKind
    {
        None = 0,
        World = 1,
        Region = 2,
        Chunk = 3
    }

    public readonly struct WorldDeltaTarget : IEquatable<WorldDeltaTarget>
    {
        public WorldId WorldId { get; }
        public WorldDeltaTargetKind Kind { get; }
        public WorldRegionCoord Region { get; }
        public WorldChunkCoord Chunk { get; }

        public bool IsValid => WorldId.IsValid &&
                               Kind >= WorldDeltaTargetKind.World &&
                               Kind <= WorldDeltaTargetKind.Chunk;

        private WorldDeltaTarget(
            WorldId worldId,
            WorldDeltaTargetKind kind,
            WorldRegionCoord region,
            WorldChunkCoord chunk)
        {
            WorldId = worldId;
            Kind = kind;
            Region = region;
            Chunk = chunk;
        }

        public static WorldDeltaTarget ForWorld(WorldId worldId)
        {
            ValidateWorld(worldId);
            return new WorldDeltaTarget(worldId, WorldDeltaTargetKind.World, default(WorldRegionCoord), default(WorldChunkCoord));
        }

        public static WorldDeltaTarget ForRegion(WorldId worldId, WorldRegionCoord region)
        {
            ValidateWorld(worldId);
            return new WorldDeltaTarget(worldId, WorldDeltaTargetKind.Region, region, default(WorldChunkCoord));
        }

        public static WorldDeltaTarget ForChunk(WorldId worldId, WorldChunkCoord chunk)
        {
            ValidateWorld(worldId);
            return new WorldDeltaTarget(worldId, WorldDeltaTargetKind.Chunk, default(WorldRegionCoord), chunk);
        }

        public bool Equals(WorldDeltaTarget other) =>
            WorldId == other.WorldId && Kind == other.Kind && Region == other.Region && Chunk == other.Chunk;

        public override bool Equals(object obj) => obj is WorldDeltaTarget other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = WorldId.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Region.GetHashCode();
                return (hash * 397) ^ Chunk.GetHashCode();
            }
        }

        public static bool operator ==(WorldDeltaTarget left, WorldDeltaTarget right) => left.Equals(right);
        public static bool operator !=(WorldDeltaTarget left, WorldDeltaTarget right) => !left.Equals(right);

        private static void ValidateWorld(WorldId worldId)
        {
            if (!worldId.IsValid) throw new ArgumentException("World ID must be valid.", nameof(worldId));
        }
    }
}
