namespace StellarFramework.WorldGenKit
{
    /// <summary>
    /// Binding marker only. Per-element hot loops should use the concrete storage type directly.
    /// </summary>
    public interface IWorldChannelStorage<T>
    {
        WorldChannelStorageKind Kind { get; }
    }
}
