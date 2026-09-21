using System;

namespace StellarFramework.PlacementKit
{
    internal static class PlacementStableIdUtility
    {
        internal const int MaxLength = 128;

        internal static void Validate(string value, string displayName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(displayName + " cannot be null, empty or whitespace.", nameof(value));
            if (value.Length > MaxLength)
                throw new ArgumentException(displayName + " cannot exceed " + MaxLength + " characters.", nameof(value));
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException(displayName + " cannot contain leading or trailing whitespace.", nameof(value));
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]) || char.IsWhiteSpace(value[i]))
                    throw new ArgumentException(displayName + " cannot contain whitespace/control characters.", nameof(value));
            }
        }
    }

    public readonly struct PlacementTypeId : IEquatable<PlacementTypeId>, IComparable<PlacementTypeId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        private PlacementTypeId(string value) => Value = value;
        public static PlacementTypeId From(string value)
        {
            PlacementStableIdUtility.Validate(value, "Placement type ID");
            return new PlacementTypeId(value);
        }
        public int CompareTo(PlacementTypeId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(PlacementTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlacementTypeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PlacementTypeId left, PlacementTypeId right) => left.Equals(right);
        public static bool operator !=(PlacementTypeId left, PlacementTypeId right) => !left.Equals(right);
    }

    public readonly struct PlacementRuleId : IEquatable<PlacementRuleId>, IComparable<PlacementRuleId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        private PlacementRuleId(string value) => Value = value;
        public static PlacementRuleId From(string value)
        {
            PlacementStableIdUtility.Validate(value, "Placement rule ID");
            return new PlacementRuleId(value);
        }
        public int CompareTo(PlacementRuleId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(PlacementRuleId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlacementRuleId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PlacementRuleId left, PlacementRuleId right) => left.Equals(right);
        public static bool operator !=(PlacementRuleId left, PlacementRuleId right) => !left.Equals(right);
    }

    public readonly struct PlacementFailureId : IEquatable<PlacementFailureId>, IComparable<PlacementFailureId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        private PlacementFailureId(string value) => Value = value;
        public static PlacementFailureId From(string value)
        {
            PlacementStableIdUtility.Validate(value, "Placement failure ID");
            return new PlacementFailureId(value);
        }
        public int CompareTo(PlacementFailureId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(PlacementFailureId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlacementFailureId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PlacementFailureId left, PlacementFailureId right) => left.Equals(right);
        public static bool operator !=(PlacementFailureId left, PlacementFailureId right) => !left.Equals(right);
    }
}
