using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceId : IEquatable<WorldResourceId>, IComparable<WorldResourceId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldResourceId(string value) => Value = value;

        public static WorldResourceId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldResourceId(value);
        }

        public int CompareTo(WorldResourceId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldResourceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldResourceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldResourceId left, WorldResourceId right) => left.Equals(right);
        public static bool operator !=(WorldResourceId left, WorldResourceId right) => !left.Equals(right);
    }
}
