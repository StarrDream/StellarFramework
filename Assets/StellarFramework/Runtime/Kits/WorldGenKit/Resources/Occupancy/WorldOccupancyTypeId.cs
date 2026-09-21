using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldOccupancyTypeId : IEquatable<WorldOccupancyTypeId>, IComparable<WorldOccupancyTypeId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldOccupancyTypeId(string value) => Value = value;

        public static WorldOccupancyTypeId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldOccupancyTypeId(value);
        }

        public int CompareTo(WorldOccupancyTypeId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldOccupancyTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldOccupancyTypeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldOccupancyTypeId left, WorldOccupancyTypeId right) => left.Equals(right);
        public static bool operator !=(WorldOccupancyTypeId left, WorldOccupancyTypeId right) => !left.Equals(right);
    }
}
