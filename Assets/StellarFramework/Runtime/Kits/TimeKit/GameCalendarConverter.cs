namespace StellarFramework
{
    /// <summary>Tick 与游戏日期时间之间的纯转换层，调度器不会引用它。</summary>
    internal static class GameCalendarConverter
    {
        internal static GameDateTime ToDateTime(long tick, GameCalendarSettings calendar)
        {
            if (tick < 0L || !calendar.IsValid)
            {
                return default;
            }

            long totalDays = tick / TickMath.TicksPerDay;
            long timeOfDay = tick % TickMath.TicksPerDay;
            long daysPerYear = (long)calendar.DaysPerMonth * calendar.MonthsPerYear;
            long yearIndex = totalDays / daysPerYear;
            long dayOfYear = totalDays % daysPerYear;
            if (yearIndex >= int.MaxValue)
            {
                LogKit.LogError("[TimeKit] Tick 超出 GameDateTime 可表示年份范围。");
                return default;
            }

            int year = (int)yearIndex + 1;
            int month = (int)(dayOfYear / calendar.DaysPerMonth) + 1;
            int day = (int)(dayOfYear % calendar.DaysPerMonth) + 1;
            int hour = (int)(timeOfDay / TickMath.TicksPerHour);
            timeOfDay %= TickMath.TicksPerHour;
            int minute = (int)(timeOfDay / TickMath.TicksPerMinute);
            timeOfDay %= TickMath.TicksPerMinute;
            int second = (int)(timeOfDay / TickMath.TicksPerSecond);
            int millisecond = (int)(timeOfDay % TickMath.TicksPerSecond);
            return new GameDateTime(year, month, day, hour, minute, second, millisecond, calendar);
        }

        internal static bool TryToTick(GameDateTime dateTime, GameCalendarSettings calendar, out long tick)
        {
            tick = 0L;
            if (!calendar.IsValid || dateTime.Year < 1 || dateTime.Month < 1 ||
                dateTime.Month > calendar.MonthsPerYear || dateTime.Day < 1 ||
                dateTime.Day > calendar.DaysPerMonth || dateTime.Hour < 0 || dateTime.Hour > 23 ||
                dateTime.Minute < 0 || dateTime.Minute > 59 || dateTime.Second < 0 || dateTime.Second > 59 ||
                dateTime.Millisecond < 0 || dateTime.Millisecond > 999)
            {
                return false;
            }

            long daysPerYear = (long)calendar.DaysPerMonth * calendar.MonthsPerYear;
            if (!TickMath.TryMultiply(dateTime.Year - 1L, daysPerYear, out long totalDays) ||
                !TickMath.TryAdd(totalDays, (long)(dateTime.Month - 1) * calendar.DaysPerMonth + dateTime.Day - 1L,
                    out totalDays) || !TickMath.TryMultiply(totalDays, TickMath.TicksPerDay, out long dateTicks))
            {
                return false;
            }

            long timeTicks = (long)dateTime.Hour * TickMath.TicksPerHour +
                             (long)dateTime.Minute * TickMath.TicksPerMinute +
                             (long)dateTime.Second * TickMath.TicksPerSecond + dateTime.Millisecond;
            return TickMath.TryAdd(dateTicks, timeTicks, out tick);
        }
    }
}
