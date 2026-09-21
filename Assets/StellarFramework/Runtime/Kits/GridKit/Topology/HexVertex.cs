using System;

namespace StellarFramework
{
    /// <summary>
    /// Canonical Hex vertex represented by the three sorted cells sharing that vertex.
    /// </summary>
    public readonly struct HexVertex : IEquatable<HexVertex>
    {
        public HexCoord First { get; }
        public HexCoord Second { get; }
        public HexCoord Third { get; }

        public bool IsValid => First != Second && First != Third && Second != Third;

        internal HexVertex(HexCoord a, HexCoord b, HexCoord c)
        {
            Sort(ref a, ref b);
            Sort(ref b, ref c);
            Sort(ref a, ref b);
            First = a;
            Second = b;
            Third = c;
        }

        public bool Equals(HexVertex other) =>
            First == other.First && Second == other.Second && Third == other.Third;

        public override bool Equals(object obj) => obj is HexVertex other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = First.GetHashCode();
                hash = (hash * 397) ^ Second.GetHashCode();
                return (hash * 397) ^ Third.GetHashCode();
            }
        }

        public override string ToString() => string.Format("{0}|{1}|{2}", First, Second, Third);

        public static bool operator ==(HexVertex left, HexVertex right) => left.Equals(right);
        public static bool operator !=(HexVertex left, HexVertex right) => !left.Equals(right);

        private static void Sort(ref HexCoord left, ref HexCoord right)
        {
            if (Compare(left, right) <= 0) return;
            HexCoord temp = left;
            left = right;
            right = temp;
        }

        private static int Compare(HexCoord left, HexCoord right)
        {
            int q = left.Q.CompareTo(right.Q);
            return q != 0 ? q : left.R.CompareTo(right.R);
        }
    }
}
