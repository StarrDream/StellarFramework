namespace StellarFramework.WorldKit
{
    /// <summary>
    /// Semantic world-change payload contract. Serialization and migration belong to adapters/domain code.
    /// </summary>
    public interface IWorldDelta
    {
        WorldDeltaTypeId TypeId { get; }
        WorldDeltaVersion Version { get; }
        WorldDeltaTarget Target { get; }
    }
}
