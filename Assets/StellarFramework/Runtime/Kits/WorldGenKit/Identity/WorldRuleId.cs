using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldRuleId : IEquatable<WorldRuleId>
    {
        public const int MaxLength = 128;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldRuleId(string value) => Value = value;

        public static WorldRuleId From(string value)
        {
            if (!TryCreate(value, out WorldRuleId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        public static bool TryCreate(string value, out WorldRuleId result, out string error)
        {
            result = default(WorldRuleId);
            if (!WorldGenStableIdUtility.TryValidate(value, MaxLength, "World rule ID", out error)) return false;
            result = new WorldRuleId(value);
            return true;
        }

        public bool Equals(WorldRuleId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldRuleId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldRuleId left, WorldRuleId right) => left.Equals(right);
        public static bool operator !=(WorldRuleId left, WorldRuleId right) => !left.Equals(right);
    }
}
