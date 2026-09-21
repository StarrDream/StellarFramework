using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureTerrainStampId : IEquatable<WorldFeatureTerrainStampId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldFeatureTerrainStampId(string value) => Value = value;

        public static WorldFeatureTerrainStampId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldFeatureTerrainStampId(value);
        }

        public bool Equals(WorldFeatureTerrainStampId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldFeatureTerrainStampId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldFeatureTerrainStampId left, WorldFeatureTerrainStampId right) => left.Equals(right);
        public static bool operator !=(WorldFeatureTerrainStampId left, WorldFeatureTerrainStampId right) => !left.Equals(right);
    }
}
