using System;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming
{
    /// <summary>
    /// Deterministic Chunk -> generation/macro Region mapping.
    /// Negative Chunk coordinates use mathematical floor division.
    /// </summary>
    public readonly struct WorldRegionLayout : IEquatable<WorldRegionLayout>
    {
        private readonly bool _initialized;

        public long ChunksPerRegionX { get; }
        public long ChunksPerRegionY { get; }
        public bool IsValid => _initialized && ChunksPerRegionX > 0L && ChunksPerRegionY > 0L;

        public WorldRegionLayout(long chunksPerRegionX, long chunksPerRegionY)
        {
            if (chunksPerRegionX <= 0L)
                throw new ArgumentOutOfRangeException(nameof(chunksPerRegionX));
            if (chunksPerRegionY <= 0L)
                throw new ArgumentOutOfRangeException(nameof(chunksPerRegionY));

            ChunksPerRegionX = chunksPerRegionX;
            ChunksPerRegionY = chunksPerRegionY;
            _initialized = true;
        }

        public WorldRegionCoord GetRegion(WorldChunkCoord chunk)
        {
            RequireValid();
            return new WorldRegionCoord(
                FloorDivide(chunk.X, ChunksPerRegionX),
                FloorDivide(chunk.Y, ChunksPerRegionY));
        }

        public bool TryGetChunkBounds(
            WorldRegionCoord region,
            out WorldChunkBounds bounds)
        {
            RequireValid();

            try
            {
                long minX = checked(region.X * ChunksPerRegionX);
                long minY = checked(region.Y * ChunksPerRegionY);
                long maxX = checked(minX + ChunksPerRegionX);
                long maxY = checked(minY + ChunksPerRegionY);
                bounds = new WorldChunkBounds(
                    new WorldChunkCoord(minX, minY),
                    new WorldChunkCoord(maxX, maxY));
                return true;
            }
            catch (OverflowException)
            {
                bounds = default(WorldChunkBounds);
                return false;
            }
        }

        public bool Equals(WorldRegionLayout other) =>
            _initialized == other._initialized &&
            ChunksPerRegionX == other.ChunksPerRegionX &&
            ChunksPerRegionY == other.ChunksPerRegionY;

        public override bool Equals(object obj) =>
            obj is WorldRegionLayout other && Equals(other);

        public override int GetHashCode() =>
            unchecked((((ChunksPerRegionX.GetHashCode() * 397) ^
                         ChunksPerRegionY.GetHashCode()) * 397) ^
                       (_initialized ? 1 : 0));

        public static bool operator ==(WorldRegionLayout left, WorldRegionLayout right) => left.Equals(right);
        public static bool operator !=(WorldRegionLayout left, WorldRegionLayout right) => !left.Equals(right);

        private void RequireValid()
        {
            if (!IsValid)
                throw new InvalidOperationException("WorldRegionLayout is not initialized.");
        }

        private static long FloorDivide(long value, long positiveDivisor)
        {
            long quotient = value / positiveDivisor;
            long remainder = value % positiveDivisor;
            return remainder < 0L ? quotient - 1L : quotient;
        }
    }
}
