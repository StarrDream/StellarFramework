namespace StellarFramework
{
    /// <summary>Outcome of a synchronous path search.</summary>
    public enum PathSearchStatus
    {
        Success = 0,
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
