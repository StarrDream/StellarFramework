using System;
using System.Globalization;

namespace StellarFramework.WorldKit
{
    public readonly struct WorldDeltaVersion : IEquatable<WorldDeltaVersion>, IComparable<WorldDeltaVersion>
    {
        public int Value { get; }
        public bool IsValid => Value > 0;

        public WorldDeltaVersion(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Delta version must be positive.");
            Value = value;
        }

        public int CompareTo(WorldDeltaVersion other) => Value.CompareTo(other.Value);
        public bool Equals(WorldDeltaVersion other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldDeltaVersion other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
        public static bool operator ==(WorldDeltaVersion left, WorldDeltaVersion right) => left.Equals(right);
        public static bool operator !=(WorldDeltaVersion left, WorldDeltaVersion right) => !left.Equals(right);
    }
}
