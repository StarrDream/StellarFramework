using System;

namespace StellarFramework
{
    /// <summary>
    /// 不可变的游戏世界时长。一个 Tick 等于一游戏毫秒。
    /// </summary>
    public readonly struct GameDuration : IEquatable<GameDuration>, IComparable<GameDuration>
    {
        /// <summary>时长包含的游戏 Tick 数。</summary>
        public long Ticks { get; }

        private GameDuration(long ticks)
        {
            Ticks = ticks;
        }

        /// <summary>由 Tick 数创建时长。</summary>
        public static GameDuration FromTicks(long ticks) => new GameDuration(ticks);

        /// <summary>由毫秒创建时长，统一采用远离零的中点舍入。</summary>
        public static GameDuration Milliseconds(double value) => FromUnits(value, 1L, nameof(Milliseconds));

        /// <summary>由秒创建时长，统一采用远离零的中点舍入。</summary>
        public static GameDuration Seconds(double value) => FromUnits(value, TickMath.TicksPerSecond, nameof(Seconds));

        /// <summary>由分钟创建时长，统一采用远离零的中点舍入。</summary>
        public static GameDuration Minutes(double value) => FromUnits(value, TickMath.TicksPerMinute, nameof(Minutes));

        /// <summary>由小时创建时长，统一采用远离零的中点舍入。</summary>
        public static GameDuration Hours(double value) => FromUnits(value, TickMath.TicksPerHour, nameof(Hours));

        /// <summary>由天创建时长，统一采用远离零的中点舍入。</summary>
        public static GameDuration Days(double value) => FromUnits(value, TickMath.TicksPerDay, nameof(Days));

        /// <summary>比较两个时长。</summary>
        public int CompareTo(GameDuration other) => Ticks.CompareTo(other.Ticks);

        /// <summary>判断两个时长是否相等。</summary>
        public bool Equals(GameDuration other) => Ticks == other.Ticks;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GameDuration other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Ticks.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Ticks + " ticks";

        /// <summary>相加两个时长；溢出时返回零时长并记录错误。</summary>
        public static GameDuration operator +(GameDuration left, GameDuration right)
        {
            if (TickMath.TryAdd(left.Ticks, right.Ticks, out long result))
            {
                return FromTicks(result);
            }

            LogKit.LogError("[TimeKit] GameDuration 相加溢出。");
            return default;
        }

        /// <summary>相减两个时长；溢出时返回零时长并记录错误。</summary>
        public static GameDuration operator -(GameDuration left, GameDuration right)
        {
            if (right.Ticks != long.MinValue && TickMath.TryAdd(left.Ticks, -right.Ticks, out long result))
            {
                return FromTicks(result);
            }

            LogKit.LogError("[TimeKit] GameDuration 相减溢出。");
            return default;
        }

        /// <summary>判断两个时长是否相等。</summary>
        public static bool operator ==(GameDuration left, GameDuration right) => left.Equals(right);

        /// <summary>判断两个时长是否不相等。</summary>
        public static bool operator !=(GameDuration left, GameDuration right) => !left.Equals(right);

        /// <summary>判断左侧时长是否小于右侧时长。</summary>
        public static bool operator <(GameDuration left, GameDuration right) => left.Ticks < right.Ticks;

        /// <summary>判断左侧时长是否大于右侧时长。</summary>
        public static bool operator >(GameDuration left, GameDuration right) => left.Ticks > right.Ticks;

        private static GameDuration FromUnits(double value, long ticksPerUnit, string apiName)
        {
            if (!TickMath.TryRoundToTicks(value * ticksPerUnit, out long ticks))
            {
                LogKit.LogError($"[TimeKit] GameDuration.{apiName} 参数非法或溢出: {value}");
                return default;
            }

            return FromTicks(ticks);
        }
    }
}
