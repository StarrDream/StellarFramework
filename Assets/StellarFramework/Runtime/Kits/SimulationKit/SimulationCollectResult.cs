using System;

namespace StellarFramework
{
    /// <summary>一次限额批量派发的结果。</summary>
    public readonly struct SimulationCollectResult : IEquatable<SimulationCollectResult>
    {
        /// <summary>获取本次实际写入调用方 Span 的 ID 数量。</summary>
        public int WrittenCount { get; }

        /// <summary>获取调用方 buffer 用尽后是否仍存在已到期项。</summary>
        public bool HasBacklog { get; }

        /// <summary>创建一次批量派发结果。</summary>
        public SimulationCollectResult(int writtenCount, bool hasBacklog)
        {
            if (writtenCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(writtenCount));
            }

            WrittenCount = writtenCount;
            HasBacklog = hasBacklog;
        }

        /// <inheritdoc />
        public bool Equals(SimulationCollectResult other) =>
            WrittenCount == other.WrittenCount && HasBacklog == other.HasBacklog;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SimulationCollectResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((WrittenCount * 397) ^ (HasBacklog ? 1 : 0));

        /// <inheritdoc />
        public override string ToString() => string.Format("WrittenCount={0}, HasBacklog={1}", WrittenCount, HasBacklog);

        public static bool operator ==(SimulationCollectResult left, SimulationCollectResult right) => left.Equals(right);
        public static bool operator !=(SimulationCollectResult left, SimulationCollectResult right) => !left.Equals(right);
    }
}
