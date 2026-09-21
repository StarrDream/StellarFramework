using System;

namespace StellarFramework
{
    /// <summary>
    /// Canonical undirected Hex edge represented by its two sorted adjacent cells.
    /// </summary>
    public readonly struct HexEdge : IEquatable<HexEdge>
    {
        public HexCoord First { get; }
        public HexCoord Second { get; }
        public bool IsValid => First != Second;

        internal HexEdge(HexCoord first, HexCoord second)
        {
            if (Compare(first, second) <= 0)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        public bool Equals(HexEdge other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is HexEdge other && Equals(other);
        public override int GetHashCode() => unchecked((First.GetHashCode() * 397) ^ Second.GetHashCode());
        public override string ToString() => string.Format("{0}<->{1}", First, Second);

        public static bool operator ==(HexEdge left, HexEdge right) => left.Equals(right);
        public static bool operator !=(HexEdge left, HexEdge right) => !left.Equals(right);

        private static int Compare(HexCoord left, HexCoord right)
        {
            int q = left.Q.CompareTo(right.Q);
            return q != 0 ? q : left.R.CompareTo(right.R);
        }
    }
}
