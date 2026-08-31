namespace StellarFramework
{
    /// <summary>
    /// 世界时间跨越多个周期时，周期 Timer 的追赶策略。
    /// </summary>
    public enum TimerCatchUpPolicy : byte
    {
        /// <summary>逐个执行所有遗漏周期，受每帧回调预算限制。</summary>
        All = 0,
        /// <summary>只执行一次，并从当前时间重新开始一个周期。</summary>
        Once = 1,
        /// <summary>把遗漏周期压缩为一次执行，并保持原始周期网格对齐。</summary>
        Latest = 2,
        /// <summary>不执行遗漏周期，直接推进到当前时间之后的下一个周期。</summary>
        Skip = 3
    }
}
