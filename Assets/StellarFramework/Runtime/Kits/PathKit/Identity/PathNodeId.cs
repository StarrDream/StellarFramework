using System;

namespace StellarFramework
{
    /// <summary>
    /// Caller-owned identity for a node in an <see cref="IPathGraph"/>.
    /// Zero is reserved for an invalid id; positive values are valid.
    /// </summary>
    public readonly struct PathNodeId : IEquatable<PathNodeId>
    {
        /// <summary>Gets the caller-defined positive identity value, or 0 for the invalid sentinel.</summary>
        public int Value { get; }

        /// <summary>Gets whether this identity can be used as a graph node.</summary>
        public bool IsValid => Value > 0;

        /// <summary>Gets whether this value is the reserved invalid identity.</summary>
        public bool IsInvalid => Value == 0;

        /// <summary>Creates a node identity without allocating or registering it in a graph.</summary>
        /// <param name="value">0 for Invalid, or any positive caller-owned id.</param>
        /// <exception cref="ArgumentOutOfRangeException">The supplied value is negative.</exception>
        public PathNodeId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Path node ids cannot be negative. Zero is reserved for Invalid.");
            }

            Value = value;
        }

        /// <inheritdoc />
        public bool Equals(PathNodeId other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PathNodeId && Equals((PathNodeId)obj);

        /// <inheritdoc />
        public override int GetHashCode() => Value;

        /// <inheritdoc />
        public override string ToString() => Value.ToString();

        public static bool operator ==(PathNodeId left, PathNodeId right) => left.Equals(right);
        public static bool operator !=(PathNodeId left, PathNodeId right) => !left.Equals(right);
    }
}
