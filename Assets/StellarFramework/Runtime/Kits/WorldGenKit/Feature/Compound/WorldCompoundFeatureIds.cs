using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldCompoundTemplateId : IEquatable<WorldCompoundTemplateId>, IComparable<WorldCompoundTemplateId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        private WorldCompoundTemplateId(string value) => Value = value;
        public static WorldCompoundTemplateId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldCompoundTemplateId(value);
        }
        public int CompareTo(WorldCompoundTemplateId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldCompoundTemplateId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldCompoundTemplateId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldCompoundTemplateId left, WorldCompoundTemplateId right) => left.Equals(right);
        public static bool operator !=(WorldCompoundTemplateId left, WorldCompoundTemplateId right) => !left.Equals(right);
    }

    public readonly struct WorldCompoundElementTypeId : IEquatable<WorldCompoundElementTypeId>, IComparable<WorldCompoundElementTypeId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        private WorldCompoundElementTypeId(string value) => Value = value;
        public static WorldCompoundElementTypeId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldCompoundElementTypeId(value);
        }
        public int CompareTo(WorldCompoundElementTypeId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldCompoundElementTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldCompoundElementTypeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldCompoundElementTypeId left, WorldCompoundElementTypeId right) => left.Equals(right);
        public static bool operator !=(WorldCompoundElementTypeId left, WorldCompoundElementTypeId right) => !left.Equals(right);
    }

    public readonly struct WorldCompoundSlotId : IEquatable<WorldCompoundSlotId>, IComparable<WorldCompoundSlotId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        private WorldCompoundSlotId(string value) => Value = value;
        public static WorldCompoundSlotId From(string value)
        {
            WorldRuleId.From(value);
            return new WorldCompoundSlotId(value);
        }
        public int CompareTo(WorldCompoundSlotId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldCompoundSlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldCompoundSlotId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldCompoundSlotId left, WorldCompoundSlotId right) => left.Equals(right);
        public static bool operator !=(WorldCompoundSlotId left, WorldCompoundSlotId right) => !left.Equals(right);
    }
}
