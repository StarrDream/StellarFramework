using System;

namespace StellarFramework.WorldKit
{
    public readonly struct WorldDataLayerId : IEquatable<WorldDataLayerId>
    {
        public const int MaxLength = 128;

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldDataLayerId(string value) => Value = value;

        public static WorldDataLayerId From(string value)
        {
            if (!TryCreate(value, out WorldDataLayerId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        public static bool TryCreate(string value, out WorldDataLayerId result, out string error)
        {
            result = default(WorldDataLayerId);
            if (!WorldStableIdUtility.TryValidate(value, MaxLength, "World data layer ID", out error)) return false;
            result = new WorldDataLayerId(value);
            return true;
        }

        public bool Equals(WorldDataLayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldDataLayerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldDataLayerId left, WorldDataLayerId right) => left.Equals(right);
        public static bool operator !=(WorldDataLayerId left, WorldDataLayerId right) => !left.Equals(right);
    }
}
