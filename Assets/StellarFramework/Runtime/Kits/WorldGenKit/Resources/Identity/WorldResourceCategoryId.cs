using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceCategoryId : IEquatable<WorldResourceCategoryId>, IComparable<WorldResourceCategoryId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldResourceCategoryId(string value) => Value = value;

        public static WorldResourceCategoryId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldResourceCategoryId(value);
        }

        public int CompareTo(WorldResourceCategoryId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldResourceCategoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldResourceCategoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldResourceCategoryId left, WorldResourceCategoryId right) => left.Equals(right);
        public static bool operator !=(WorldResourceCategoryId left, WorldResourceCategoryId right) => !left.Equals(right);
    }
}
