using System;

namespace StellarFramework
{
    /// <summary>空间查询写入结果。MatchCount 包含所有匹配项，WrittenCount 受调用方缓冲区容量限制。</summary>
    public readonly struct SpatialQueryResult : IEquatable<SpatialQueryResult>
    {
        public int WrittenCount { get; }
        public int MatchCount { get; }
        public bool IsTruncated => WrittenCount < MatchCount;

        public SpatialQueryResult(int writtenCount, int matchCount)
        {
            if (writtenCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(writtenCount));
            }

            if (matchCount < writtenCount)
            {
                throw new ArgumentOutOfRangeException(nameof(matchCount), "MatchCount 不能小于 WrittenCount。");
            }

            WrittenCount = writtenCount;
            MatchCount = matchCount;
        }

        public bool Equals(SpatialQueryResult other) => WrittenCount == other.WrittenCount && MatchCount == other.MatchCount;
        public override bool Equals(object obj) => obj is SpatialQueryResult other && Equals(other);
        public override int GetHashCode() => unchecked((WrittenCount * 397) ^ MatchCount);
        public override string ToString() => string.Format("{0}/{1}", WrittenCount, MatchCount);

        public static bool operator ==(SpatialQueryResult left, SpatialQueryResult right) => left.Equals(right);
        public static bool operator !=(SpatialQueryResult left, SpatialQueryResult right) => !left.Equals(right);
    }
}
