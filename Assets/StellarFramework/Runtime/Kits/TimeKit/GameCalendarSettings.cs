using System;

namespace StellarFramework
{
    /// <summary>
    /// 游戏世界日历规则。TimeScheduler 只处理 Tick，不依赖此日历配置。
    /// </summary>
    public readonly struct GameCalendarSettings : IEquatable<GameCalendarSettings>
    {
        /// <summary>每周包含的天数。</summary>
        public int DaysPerWeek { get; }

        /// <summary>每月包含的天数。</summary>
        public int DaysPerMonth { get; }

        /// <summary>每年包含的月数。</summary>
        public int MonthsPerYear { get; }

        /// <summary>默认日历：7 天每周、30 天每月、12 月每年。</summary>
        public static GameCalendarSettings Default => new GameCalendarSettings(7, 30, 12);

        /// <summary>创建日历规则。</summary>
        public GameCalendarSettings(int daysPerWeek, int daysPerMonth, int monthsPerYear)
        {
            DaysPerWeek = daysPerWeek;
            DaysPerMonth = daysPerMonth;
            MonthsPerYear = monthsPerYear;
        }

        /// <summary>判断当前日历规则是否可用于时间转换。</summary>
        public bool IsValid => DaysPerWeek > 0 && DaysPerMonth > 0 && MonthsPerYear > 0;

        /// <summary>判断两个日历规则是否相等。</summary>
        public bool Equals(GameCalendarSettings other)
        {
            return DaysPerWeek == other.DaysPerWeek && DaysPerMonth == other.DaysPerMonth &&
                   MonthsPerYear == other.MonthsPerYear;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GameCalendarSettings other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DaysPerWeek;
                hash = hash * 397 ^ DaysPerMonth;
                return hash * 397 ^ MonthsPerYear;
            }
        }

        /// <summary>判断两个日历规则是否相等。</summary>
        public static bool operator ==(GameCalendarSettings left, GameCalendarSettings right) => left.Equals(right);

        /// <summary>判断两个日历规则是否不相等。</summary>
        public static bool operator !=(GameCalendarSettings left, GameCalendarSettings right) => !left.Equals(right);
    }
}
