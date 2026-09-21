using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public readonly struct WorldBiomeId : IEquatable<WorldBiomeId>, IComparable<WorldBiomeId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldBiomeId(string value) => Value = value;

        public static WorldBiomeId From(string value)
        {
            // Reuse Core's canonical stable-ID grammar without introducing a Core dependency on Biome semantics.
            WorldRuleId.From(value);
            return new WorldBiomeId(value);
        }

        public int CompareTo(WorldBiomeId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldBiomeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldBiomeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldBiomeId left, WorldBiomeId right) => left.Equals(right);
        public static bool operator !=(WorldBiomeId left, WorldBiomeId right) => !left.Equals(right);
    }
}
