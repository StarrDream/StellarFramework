using System;

namespace StellarFramework
{
    /// <summary>一次限额批量派发的结果。</summary>
    public readonly struct SimulationCollectResult : IEquatable<SimulationCollectResult>
    {
        public int WrittenCount { get; }
        public bool HasBacklog { get; }

        public SimulationCollectResult(int writtenCount, bool hasBacklog)
        {
            if (writtenCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(writtenCount));
            }

            WrittenCount = writtenCount;
            HasBacklog = hasBacklog;
        }

        public bool Equals(SimulationCollectResult other) =>
            WrittenCount == other.WrittenCount && HasBacklog == other.HasBacklog;
        public override bool Equals(object obj) => obj is SimulationCollectResult other && Equals(other);
        public override int GetHashCode() => unchecked((WrittenCount * 397) ^ (HasBacklog ? 1 : 0));
        public override string ToString() => string.Format("WrittenCount={0}, HasBacklog={1}", WrittenCount, HasBacklog);

        public static bool operator ==(SimulationCollectResult left, SimulationCollectResult right) => left.Equals(right);
        public static bool operator !=(SimulationCollectResult left, SimulationCollectResult right) => !left.Equals(right);
    }
}
