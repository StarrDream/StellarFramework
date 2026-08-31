namespace StellarFramework
{
    /// <summary>
    /// Timer 触发时提供的时间上下文。
    /// </summary>
    public readonly struct TimeTriggerContext
    {
        /// <summary>该次触发原本计划发生的 Tick。</summary>
        public long ScheduledTick { get; }

        /// <summary>当前游戏世界 Tick。</summary>
        public long CurrentTick { get; }

        /// <summary>本次触发相对于计划 Tick 的迟到量，最小为零。</summary>
        public long LateTicks { get; }

        /// <summary>本次回调代表的周期次数。</summary>
        public int ElapsedCount { get; }

        /// <summary>是否因跨越多个周期而触发追赶逻辑。</summary>
        public bool IsCatchUp { get; }

        internal TimeTriggerContext(long scheduledTick, long currentTick, int elapsedCount, bool isCatchUp)
        {
            ScheduledTick = scheduledTick;
            CurrentTick = currentTick;
            LateTicks = currentTick > scheduledTick ? currentTick - scheduledTick : 0L;
            ElapsedCount = elapsedCount;
            IsCatchUp = isCatchUp;
        }
    }
}
