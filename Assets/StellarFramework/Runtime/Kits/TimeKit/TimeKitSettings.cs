namespace StellarFramework
{
    /// <summary>
    /// TimeKit 的运行时配置。未显式配置时会使用安全的默认值。
    /// </summary>
    public sealed class TimeKitSettings
    {
        /// <summary>首次分配的 Timer Slot 数量。</summary>
        public int InitialTimerCapacity = 256;

        /// <summary>单次更新最多执行的回调数。</summary>
        public int MaxCallbacksPerUpdate = 4096;

        /// <summary>默认世界时间倍率。</summary>
        public double DefaultTimeScale = 1d;

        /// <summary>日期时间视图使用的日历规则。</summary>
        public GameCalendarSettings Calendar = GameCalendarSettings.Default;

        internal TimeKitSettings CloneValidated()
        {
            var result = new TimeKitSettings
            {
                InitialTimerCapacity = InitialTimerCapacity < 1 ? 256 : InitialTimerCapacity,
                MaxCallbacksPerUpdate = MaxCallbacksPerUpdate < 1 ? 1 : MaxCallbacksPerUpdate,
                DefaultTimeScale = DefaultTimeScale,
                Calendar = Calendar.IsValid ? Calendar : GameCalendarSettings.Default
            };

            if (double.IsNaN(result.DefaultTimeScale) || double.IsInfinity(result.DefaultTimeScale) ||
                result.DefaultTimeScale < 0d)
            {
                result.DefaultTimeScale = 1d;
            }

            return result;
        }
    }
}
