using System;

namespace StellarFramework.WorldKit
{
    public enum WorldExtentKind
    {
        None = 0,
        Finite = 1,
        Infinite = 2
    }

    /// <summary>Explicit finite/infinite world extent. Default value is invalid.</summary>
    public readonly struct WorldExtent : IEquatable<WorldExtent>
    {
        private readonly WorldChunkBounds _finiteBounds;

        public WorldExtentKind Kind { get; }
        public bool IsValid => Kind == WorldExtentKind.Infinite ||
                               (Kind == WorldExtentKind.Finite && !_finiteBounds.IsEmpty);
        public bool IsFinite => Kind == WorldExtentKind.Finite;
        public bool IsInfinite => Kind == WorldExtentKind.Infinite;

        private WorldExtent(WorldExtentKind kind, WorldChunkBounds finiteBounds)
        {
            Kind = kind;
            _finiteBounds = finiteBounds;
        }

        public static WorldExtent Infinite => new WorldExtent(WorldExtentKind.Infinite, default(WorldChunkBounds));

        public static WorldExtent Finite(WorldChunkBounds bounds)
        {
            if (bounds.IsEmpty)
                throw new ArgumentException("Finite world bounds cannot be empty.", nameof(bounds));
            return new WorldExtent(WorldExtentKind.Finite, bounds);
        }

        public bool Contains(WorldChunkCoord coord)
        {
            if (!IsValid) return false;
            return IsInfinite || _finiteBounds.Contains(coord);
        }

        public bool TryGetFiniteBounds(out WorldChunkBounds bounds)
        {
            if (IsFinite && IsValid)
            {
                bounds = _finiteBounds;
                return true;
            }

            bounds = default(WorldChunkBounds);
            return false;
        }

        public bool Equals(WorldExtent other) => Kind == other.Kind && _finiteBounds == other._finiteBounds;
        public override bool Equals(object obj) => obj is WorldExtent other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ _finiteBounds.GetHashCode());

        public static bool operator ==(WorldExtent left, WorldExtent right) => left.Equals(right);
        public static bool operator !=(WorldExtent left, WorldExtent right) => !left.Equals(right);
    }
}
