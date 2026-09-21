using System;

namespace StellarFramework.WorldGenKit
{
    public enum WorldChannelStorageKind
    {
        Dense = 0,
        Sparse = 1,
        Chunked = 2,
        Constant = 3,
        Computed = 4,
        External = 5
    }

    public enum WorldChannelScope
    {
        World = 0,
        Region = 1,
        Chunk = 2,
        Sample = 3,
        Entity = 4
    }

    public enum WorldChannelSourceMode
    {
        ProducedByStage = 0,
        ProvidedInput = 1
    }

    public readonly struct WorldChannelStorageDescriptor : IEquatable<WorldChannelStorageDescriptor>
    {
        private readonly bool _initialized;

        public WorldChannelStorageKind Kind { get; }
        public WorldChannelScope Scope { get; }
        public bool IsValid => _initialized &&
            Kind >= WorldChannelStorageKind.Dense && Kind <= WorldChannelStorageKind.External &&
            Scope >= WorldChannelScope.World && Scope <= WorldChannelScope.Entity;

        public WorldChannelStorageDescriptor(WorldChannelStorageKind kind, WorldChannelScope scope)
        {
            if (kind < WorldChannelStorageKind.Dense || kind > WorldChannelStorageKind.External)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (scope < WorldChannelScope.World || scope > WorldChannelScope.Entity)
                throw new ArgumentOutOfRangeException(nameof(scope));
            Kind = kind;
            Scope = scope;
            _initialized = true;
        }

        public bool Equals(WorldChannelStorageDescriptor other) =>
            _initialized == other._initialized && Kind == other.Kind && Scope == other.Scope;
        public override bool Equals(object obj) => obj is WorldChannelStorageDescriptor other && Equals(other);
        public override int GetHashCode() => unchecked((((int)Kind * 397) ^ (int)Scope) * 397 ^ (_initialized ? 1 : 0));
        public static bool operator ==(WorldChannelStorageDescriptor left, WorldChannelStorageDescriptor right) => left.Equals(right);
        public static bool operator !=(WorldChannelStorageDescriptor left, WorldChannelStorageDescriptor right) => !left.Equals(right);
    }
}
