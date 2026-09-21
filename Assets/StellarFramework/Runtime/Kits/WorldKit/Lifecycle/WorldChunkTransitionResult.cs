namespace StellarFramework.WorldKit
{
    public readonly struct WorldChunkTransitionResult
    {
        public bool Success => Error == WorldChunkTransitionError.None;
        public WorldChunkTransitionError Error { get; }
        public WorldChunkState PreviousState { get; }
        public WorldChunkState CurrentState { get; }

        private WorldChunkTransitionResult(
            WorldChunkTransitionError error,
            WorldChunkState previousState,
            WorldChunkState currentState)
        {
            Error = error;
            PreviousState = previousState;
            CurrentState = currentState;
        }

        internal static WorldChunkTransitionResult Succeeded(WorldChunkState previous, WorldChunkState current) =>
            new WorldChunkTransitionResult(WorldChunkTransitionError.None, previous, current);

        internal static WorldChunkTransitionResult Failed(
            WorldChunkTransitionError error,
            WorldChunkState current) =>
            new WorldChunkTransitionResult(error, current, current);
    }
}
