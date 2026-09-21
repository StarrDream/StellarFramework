using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureCategoryId : IEquatable<WorldFeatureCategoryId>, IComparable<WorldFeatureCategoryId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldFeatureCategoryId(string value) => Value = value;

        public static WorldFeatureCategoryId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldFeatureCategoryId(value);
        }

        public int CompareTo(WorldFeatureCategoryId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldFeatureCategoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldFeatureCategoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldFeatureCategoryId left, WorldFeatureCategoryId right) => left.Equals(right);
        public static bool operator !=(WorldFeatureCategoryId left, WorldFeatureCategoryId right) => !left.Equals(right);
    }
}
