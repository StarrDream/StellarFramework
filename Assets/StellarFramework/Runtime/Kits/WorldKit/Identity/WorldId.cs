using System;

namespace StellarFramework.WorldKit
{
    /// <summary>Stable authored/persisted world identity.</summary>
    public readonly struct WorldId : IEquatable<WorldId>
    {
        public const int MaxLength = 128;

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldId(string value)
        {
            Value = value;
        }

        public static WorldId From(string value)
        {
            if (!TryCreate(value, out WorldId result, out string error))
            {
                throw new ArgumentException(error, nameof(value));
            }

            return result;
        }

        public static bool TryCreate(string value, out WorldId result, out string error)
        {
            result = default(WorldId);
            if (!WorldStableIdUtility.TryValidate(value, MaxLength, "World ID", out error)) return false;

            result = new WorldId(value);
            return true;
        }

        public bool Equals(WorldId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(WorldId left, WorldId right) => left.Equals(right);
        public static bool operator !=(WorldId left, WorldId right) => !left.Equals(right);
    }
}
