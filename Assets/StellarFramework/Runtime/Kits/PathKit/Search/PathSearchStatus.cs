namespace StellarFramework
{
    /// <summary>Outcome of a synchronous path search.</summary>
    public enum PathSearchStatus
    {
        /// <summary>No search has been executed; this is the default enum value.</summary>
        None = 0,

        /// <summary>The search reached the requested goal and wrote the complete path.</summary>
        Success,

        /// <summary>The request start id is the reserved invalid value.</summary>
        InvalidStart,

        /// <summary>The request goal id is the reserved invalid value.</summary>
        InvalidGoal,

        /// <summary>The graph does not currently contain the requested start node.</summary>
        StartNotFound,

        /// <summary>The graph does not currently contain the requested goal node.</summary>
        GoalNotFound,

        /// <summary>The open set was exhausted without reaching the goal.</summary>
        NoPath,

        /// <summary>The path exists, but the destination buffer is too small; no partial path was written.</summary>
        OutputBufferTooSmall,

        /// <summary>The configured node-expansion budget was exhausted before reaching the goal.</summary>
        ExpansionLimitReached,

        /// <summary>A traversal or heuristic cost could not be represented safely by <see cref="long"/>.</summary>
        CostOverflow
    }
}
