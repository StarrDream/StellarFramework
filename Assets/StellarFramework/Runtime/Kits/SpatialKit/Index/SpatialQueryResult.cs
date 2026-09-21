using System;

namespace StellarFramework
{
    /// <summary>空间查询写入结果。MatchCount 包含所有匹配项，WrittenCount 受调用方缓冲区容量限制。</summary>
    public readonly struct SpatialQueryResult : IEquatable<SpatialQueryResult>
    {
        /// <summary>获取实际写入结果 Span 的 ID 数量。</summary>
        public int WrittenCount { get; }

        /// <summary>获取查询条件的完整匹配数量，即使调用方 buffer 更小。</summary>
        public int MatchCount { get; }

        /// <summary>获取查询结果是否因为调用方 buffer 容量不足而被截断。</summary>
        public bool IsTruncated => WrittenCount < MatchCount;

        /// <summary>创建查询计数结果。</summary>
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

        /// <inheritdoc />
        public bool Equals(SpatialQueryResult other) => WrittenCount == other.WrittenCount && MatchCount == other.MatchCount;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SpatialQueryResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((WrittenCount * 397) ^ MatchCount);

        /// <inheritdoc />
        public override string ToString() => string.Format("{0}/{1}", WrittenCount, MatchCount);

        public static bool operator ==(SpatialQueryResult left, SpatialQueryResult right) => left.Equals(right);
        public static bool operator !=(SpatialQueryResult left, SpatialQueryResult right) => !left.Equals(right);
    }
}
