using System;

namespace StellarFramework
{
    /// <summary>
    /// 只负责游戏 Tick 推进的时钟，不知道日历和 Timer 的存在。
    /// </summary>
    internal sealed class TimeClock
    {
        private long _tick;
        private double _fractionalTicks;
        private double _timeScale = 1d;
        private bool _isPaused;

        internal long Tick => _tick;
        internal double TimeScale => _timeScale;
        internal bool IsPaused => _isPaused;

        internal long Advance(double unscaledDeltaSeconds)
        {
            if (_isPaused || unscaledDeltaSeconds <= 0d)
            {
                return 0L;
            }

            if (double.IsNaN(unscaledDeltaSeconds) || double.IsInfinity(unscaledDeltaSeconds))
            {
                LogKit.LogError("[TimeKit] TimeClock.Advance 忽略非法 delta。");
                return 0L;
            }

            double preciseTicks = unscaledDeltaSeconds * _timeScale * TickMath.TicksPerSecond + _fractionalTicks;
            if (!TickMath.TryRoundToTicks(Math.Floor(preciseTicks), out long wholeTicks) || wholeTicks < 0L ||
                !TickMath.TryAdd(_tick, wholeTicks, out long nextTick))
            {
                LogKit.LogError("[TimeKit] 世界 Tick 推进溢出，当前帧被忽略。");
                return 0L;
            }

            _fractionalTicks = preciseTicks - wholeTicks;
            _tick = nextTick;
            return wholeTicks;
        }

        internal bool SetTimeScale(double timeScale)
        {
            if (double.IsNaN(timeScale) || double.IsInfinity(timeScale) || timeScale < 0d)
            {
                LogKit.LogError($"[TimeKit] TimeScale 非法: {timeScale}");
                return false;
            }

            _timeScale = timeScale;
            return true;
        }

        internal bool AddTicks(long ticks)
        {
            if (ticks < 0L || !TickMath.TryAdd(_tick, ticks, out long nextTick))
            {
                LogKit.LogError($"[TimeKit] AddTicks 非法或溢出: {ticks}");
                return false;
            }

            _tick = nextTick;
            return true;
        }

        internal void Pause() => _isPaused = true;
        internal void Resume() => _isPaused = false;

        internal void Reset(long tick, double timeScale, bool isPaused)
        {
            _tick = tick;
            _fractionalTicks = 0d;
            _timeScale = timeScale;
            _isPaused = isPaused;
        }
    }
}
