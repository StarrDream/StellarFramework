namespace StellarFramework
{
    /// <summary>SimulationScheduler 写操作的失败原因。</summary>
    public enum SimulationMutationError
    {
        None = 0,
        InvalidId,
        DuplicateId,
        NotFound,
        InvalidInterval,
        InvalidDelay,
        TickOverflow
    }
}
