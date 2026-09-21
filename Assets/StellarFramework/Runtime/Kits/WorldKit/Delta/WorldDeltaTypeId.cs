using System;

namespace StellarFramework.WorldKit
{
    public readonly struct WorldDeltaTypeId : IEquatable<WorldDeltaTypeId>
    {
        public const int MaxLength = 128;

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldDeltaTypeId(string value) => Value = value;

        public static WorldDeltaTypeId From(string value)
        {
            if (!TryCreate(value, out WorldDeltaTypeId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        public static bool TryCreate(string value, out WorldDeltaTypeId result, out string error)
        {
            result = default(WorldDeltaTypeId);
            if (!WorldStableIdUtility.TryValidate(value, MaxLength, "World delta type ID", out error)) return false;
            result = new WorldDeltaTypeId(value);
            return true;
        }

        public bool Equals(WorldDeltaTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldDeltaTypeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldDeltaTypeId left, WorldDeltaTypeId right) => left.Equals(right);
        public static bool operator !=(WorldDeltaTypeId left, WorldDeltaTypeId right) => !left.Equals(right);
    }
}
