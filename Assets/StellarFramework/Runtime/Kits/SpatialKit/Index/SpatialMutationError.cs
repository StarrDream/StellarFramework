namespace StellarFramework
{
    /// <summary>空间索引写操作的稳定失败原因。</summary>
    public enum SpatialMutationError
    {
        None = 0,
        InvalidId = 1,
        DuplicateId = 2,
        NotFound = 3,
        PositionOutOfRange = 4
    }
}
