using System;

namespace StellarFramework
{
    /// <summary>均匀空间哈希桶坐标。仅供 SpatialKit 内部使用。</summary>
    internal readonly struct SpatialBucketCoord : IEquatable<SpatialBucketCoord>
    {
        public readonly int X;
        public readonly int Y;

        public SpatialBucketCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(SpatialBucketCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is SpatialBucketCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public static bool operator ==(SpatialBucketCoord left, SpatialBucketCoord right) => left.Equals(right);
        public static bool operator !=(SpatialBucketCoord left, SpatialBucketCoord right) => !left.Equals(right);
    }
}
