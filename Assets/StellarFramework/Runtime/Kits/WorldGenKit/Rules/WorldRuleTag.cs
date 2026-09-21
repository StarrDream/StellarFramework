using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldRuleTagId : IEquatable<WorldRuleTagId>, IComparable<WorldRuleTagId>
    {
        public const int MaxLength = 128;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldRuleTagId(string value) => Value = value;

        public static WorldRuleTagId From(string value)
        {
            if (!WorldGenStableIdUtility.TryValidate(value, MaxLength, "World rule tag ID", out string error))
                throw new ArgumentException(error, nameof(value));
            return new WorldRuleTagId(value);
        }

        public int CompareTo(WorldRuleTagId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(WorldRuleTagId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldRuleTagId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldRuleTagId left, WorldRuleTagId right) => left.Equals(right);
        public static bool operator !=(WorldRuleTagId left, WorldRuleTagId right) => !left.Equals(right);
    }

    public sealed class WorldRuleTagSet
    {
        private readonly WorldRuleTagId[] _tags;

        public int Count => _tags.Length;

        public WorldRuleTagSet(ReadOnlySpan<WorldRuleTagId> tags)
        {
            _tags = tags.ToArray();
            Array.Sort(_tags);
            for (int i = 0; i < _tags.Length; i++)
            {
                if (!_tags[i].IsValid) throw new ArgumentException("Tag set contains an invalid tag.", nameof(tags));
                if (i > 0 && _tags[i] == _tags[i - 1])
                    throw new ArgumentException("Tag set cannot contain duplicate tags.", nameof(tags));
            }
        }

        public bool Contains(WorldRuleTagId tag)
        {
            if (!tag.IsValid) return false;
            return Array.BinarySearch(_tags, tag) >= 0;
        }
    }
}
