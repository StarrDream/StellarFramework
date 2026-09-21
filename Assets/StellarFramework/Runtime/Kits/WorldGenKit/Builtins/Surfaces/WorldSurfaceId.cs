using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public readonly struct WorldSurfaceId : IEquatable<WorldSurfaceId>, IComparable<WorldSurfaceId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldSurfaceId(string value) => Value = value;

        public static WorldSurfaceId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldSurfaceId(value);
        }

        public int CompareTo(WorldSurfaceId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldSurfaceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldSurfaceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldSurfaceId left, WorldSurfaceId right) => left.Equals(right);
        public static bool operator !=(WorldSurfaceId left, WorldSurfaceId right) => !left.Equals(right);
    }
}
