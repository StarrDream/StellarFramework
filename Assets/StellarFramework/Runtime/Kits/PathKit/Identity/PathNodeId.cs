using System;

namespace StellarFramework
{
    /// <summary>
    /// Caller-owned identity for a node in an <see cref="IPathGraph"/>.
    /// Zero is reserved for an invalid id; positive values are valid.
    /// </summary>
    public readonly struct PathNodeId : IEquatable<PathNodeId>
    {
        public int Value { get; }

        public bool IsValid => Value > 0;
        public bool IsInvalid => Value == 0;

        public PathNodeId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Path node ids cannot be negative. Zero is reserved for Invalid.");
            }

            Value = value;
        }

        public bool Equals(PathNodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PathNodeId && Equals((PathNodeId)obj);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(PathNodeId left, PathNodeId right) => left.Equals(right);
        public static bool operator !=(PathNodeId left, PathNodeId right) => !left.Equals(right);
    }
}
