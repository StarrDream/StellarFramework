using System;

namespace StellarFramework
{
    /// <summary>
    /// TimeKit 内部 Tick 算术工具。所有可能溢出的时间计算统一从这里经过，
    /// 避免调度器在边界条件下静默回绕为负数。
    /// </summary>
    internal static class TickMath
    {
        internal const long TicksPerSecond = 1000L;
        internal const long TicksPerMinute = 60L * TicksPerSecond;
        internal const long TicksPerHour = 60L * TicksPerMinute;
        internal const long TicksPerDay = 24L * TicksPerHour;

        internal static bool TryAdd(long left, long right, out long result)
        {
            if (right > 0 && left > long.MaxValue - right ||
                right < 0 && left < long.MinValue - right)
            {
                result = 0;
                return false;
            }

            result = left + right;
            return true;
        }

        internal static bool TryMultiply(long left, long right, out long result)
        {
            try
            {
                result = checked(left * right);
                return true;
            }
            catch (OverflowException)
            {
                result = 0;
                return false;
            }
        }

        internal static bool TryRoundToTicks(double value, out long ticks)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value > long.MaxValue - 0.5d || value < long.MinValue + 0.5d)
            {
                ticks = 0;
                return false;
            }

            ticks = (long)Math.Round(value, MidpointRounding.AwayFromZero);
            return true;
        }
    }
}
