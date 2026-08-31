using System;

namespace StellarFramework
{
    /// <summary>
    /// 游戏世界日期时间视图。世界时间唯一真值仍是 <see cref="TimeKit.Tick"/>。
    /// </summary>
    public readonly struct GameDateTime : IEquatable<GameDateTime>, IComparable<GameDateTime>
    {
        /// <summary>从 1 开始的年份。</summary>
        public int Year { get; }
        /// <summary>从 1 开始的月份。</summary>
        public int Month { get; }
        /// <summary>从 1 开始的日期。</summary>
        public int Day { get; }
        /// <summary>从 1 开始的年内周序号。</summary>
        public int WeekOfYear { get; }
        /// <summary>从 1 开始的周内天序号。</summary>
        public int DayOfWeek { get; }
        /// <summary>小时，范围 0~23。</summary>
        public int Hour { get; }
        /// <summary>分钟，范围 0~59。</summary>
        public int Minute { get; }
        /// <summary>秒，范围 0~59。</summary>
        public int Second { get; }
        /// <summary>毫秒，范围 0~999。</summary>
        public int Millisecond { get; }

        /// <summary>使用默认游戏日历创建日期时间。</summary>
        public GameDateTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0,
            int millisecond = 0)
            : this(year, month, day, hour, minute, second, millisecond, GameCalendarSettings.Default)
        {
        }

        internal GameDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond,
            GameCalendarSettings calendar)
        {
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            Second = second;
            Millisecond = millisecond;
            int dayOfYear = (month - 1) * calendar.DaysPerMonth + day;
            WeekOfYear = (dayOfYear - 1) / calendar.DaysPerWeek + 1;
            DayOfWeek = (dayOfYear - 1) % calendar.DaysPerWeek + 1;
        }

        /// <summary>比较两个日期时间字段。</summary>
        public int CompareTo(GameDateTime other)
        {
            int result = Year.CompareTo(other.Year);
            if (result != 0) return result;
            result = Month.CompareTo(other.Month);
            if (result != 0) return result;
            result = Day.CompareTo(other.Day);
            if (result != 0) return result;
            result = Hour.CompareTo(other.Hour);
            if (result != 0) return result;
            result = Minute.CompareTo(other.Minute);
            if (result != 0) return result;
            result = Second.CompareTo(other.Second);
            return result != 0 ? result : Millisecond.CompareTo(other.Millisecond);
        }

        /// <summary>判断两个日期时间是否相等。</summary>
        public bool Equals(GameDateTime other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day && Hour == other.Hour &&
                   Minute == other.Minute && Second == other.Second && Millisecond == other.Millisecond;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GameDateTime other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Year;
                hash = hash * 397 ^ Month;
                hash = hash * 397 ^ Day;
                hash = hash * 397 ^ Hour;
                hash = hash * 397 ^ Minute;
                hash = hash * 397 ^ Second;
                return hash * 397 ^ Millisecond;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Y{Year} M{Month:D2} D{Day:D2} {Hour:D2}:{Minute:D2}:{Second:D2}.{Millisecond:D3}";
        }

        /// <summary>判断两个日期时间是否相等。</summary>
        public static bool operator ==(GameDateTime left, GameDateTime right) => left.Equals(right);
        /// <summary>判断两个日期时间是否不相等。</summary>
        public static bool operator !=(GameDateTime left, GameDateTime right) => !left.Equals(right);
        /// <summary>判断左侧日期是否更早。</summary>
        public static bool operator <(GameDateTime left, GameDateTime right) => left.CompareTo(right) < 0;
        /// <summary>判断左侧日期是否更晚。</summary>
        public static bool operator >(GameDateTime left, GameDateTime right) => left.CompareTo(right) > 0;
    }
}
