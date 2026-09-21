namespace StellarFramework.WorldKit
{
    public enum WorldChunkState
    {
        Unloaded = 0,
        Metadata = 1,
        DataReady = 2,
        Active = 3
    }

    public enum WorldChunkTransitionError
    {
        None = 0,
        InvalidChunk = 1,
        AlreadyInState = 2,
        InvalidTransition = 3,
        InvalidState = 4,
        DataUnavailable = 5,
        Busy = 6
    }
}
