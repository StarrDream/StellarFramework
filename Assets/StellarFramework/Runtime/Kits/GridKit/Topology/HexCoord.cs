using System;

namespace StellarFramework
{
    /// <summary>
    /// Axial Hex coordinate (Q, R). The implicit cube S coordinate is -Q-R.
    /// Negative coordinates are valid.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        /// <summary>获取 axial Q 坐标。</summary>
        public int Q { get; }

        /// <summary>获取 axial R 坐标。</summary>
        public int R { get; }

        /// <summary>创建一个 axial 六边形坐标。</summary>
        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <inheritdoc />
        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((Q * 397) ^ R);

        /// <inheritdoc />
        public override string ToString() => string.Format("({0}, {1})", Q, R);

        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);
        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);
    }
}
