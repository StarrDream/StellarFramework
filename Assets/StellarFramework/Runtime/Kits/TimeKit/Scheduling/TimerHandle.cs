using System;

namespace StellarFramework
{
    /// <summary>
    /// Timer 的安全句柄。Id 与 Version 共同防止旧句柄误操作已复用的 Slot。
    /// </summary>
    public readonly struct TimerHandle : IEquatable<TimerHandle>
    {
        internal readonly int Id;
        internal readonly uint Version;

        internal TimerHandle(int id, uint version)
        {
            Id = id;
            Version = version;
        }

        /// <summary>无效 Timer 句柄。</summary>
        public static TimerHandle Invalid => default;

        /// <summary>该句柄当前是否仍指向一个活动 Timer。</summary>
        public bool IsValid => TimeKit.IsHandleValid(this);

        /// <summary>取消该 Timer；句柄无效、已完成或已取消时返回 false。</summary>
        public bool Cancel() => TimeKit.Cancel(this);

        /// <summary>判断两个句柄是否相等。</summary>
        public bool Equals(TimerHandle other) => Id == other.Id && Version == other.Version;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TimerHandle other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return Id * 397 ^ (int)Version;
            }
        }

        /// <summary>判断两个句柄是否相等。</summary>
        public static bool operator ==(TimerHandle left, TimerHandle right) => left.Equals(right);

        /// <summary>判断两个句柄是否不相等。</summary>
        public static bool operator !=(TimerHandle left, TimerHandle right) => !left.Equals(right);
    }
}
