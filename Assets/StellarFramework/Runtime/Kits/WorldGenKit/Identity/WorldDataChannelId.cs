using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldDataChannelId : IEquatable<WorldDataChannelId>
    {
        public const int MaxLength = 128;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldDataChannelId(string value) => Value = value;

        public static WorldDataChannelId From(string value)
        {
            if (!TryCreate(value, out WorldDataChannelId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        public static bool TryCreate(string value, out WorldDataChannelId result, out string error)
        {
            result = default(WorldDataChannelId);
            if (!WorldGenStableIdUtility.TryValidate(value, MaxLength, "World data channel ID", out error)) return false;
            result = new WorldDataChannelId(value);
            return true;
        }

        public bool Equals(WorldDataChannelId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldDataChannelId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldDataChannelId left, WorldDataChannelId right) => left.Equals(right);
        public static bool operator !=(WorldDataChannelId left, WorldDataChannelId right) => !left.Equals(right);
    }
}
