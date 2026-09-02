namespace StellarFramework
{
    /// <summary>Outcome of a synchronous path search.</summary>
    public enum PathSearchStatus
    {
        /// <summary>No search has been executed; this is the default enum value.</summary>
        None = 0,

        /// <summary>The search reached the requested goal and wrote the complete path.</summary>
        Success,
        InvalidStart,
        InvalidGoal,
        StartNotFound,
        GoalNotFound,
        NoPath,
        OutputBufferTooSmall,
        ExpansionLimitReached,
        CostOverflow
    }
}
