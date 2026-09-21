namespace StellarFramework.WorldKit.Streaming
{
    /// <summary>
    /// Independent streaming residency tier. It intentionally does not extend the frozen WorldChunkState enum.
    /// </summary>
    public enum WorldStreamingTier
    {
        None = 0,
        Metadata = 1,
        Data = 2,
        Simulation = 3,
        Presentation = 4
    }
}
