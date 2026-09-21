using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureId : IEquatable<WorldFeatureId>, IComparable<WorldFeatureId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldFeatureId(string value) => Value = value;

        public static WorldFeatureId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldFeatureId(value);
        }

        public int CompareTo(WorldFeatureId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldFeatureId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldFeatureId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldFeatureId left, WorldFeatureId right) => left.Equals(right);
        public static bool operator !=(WorldFeatureId left, WorldFeatureId right) => !left.Equals(right);
    }
}
