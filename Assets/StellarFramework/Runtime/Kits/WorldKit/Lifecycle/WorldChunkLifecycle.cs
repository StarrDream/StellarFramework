using System;

namespace StellarFramework.WorldKit
{
    /// <summary>Explicit infrastructure-only chunk lifecycle state machine.</summary>
    public sealed class WorldChunkLifecycle
    {
        public WorldChunkState State { get; private set; }

        public WorldChunkLifecycle(WorldChunkState initialState = WorldChunkState.Unloaded)
        {
            ValidateState(initialState);
            State = initialState;
        }

        public WorldChunkTransitionResult TryTransition(WorldChunkState target)
        {
            if (!IsDefined(target))
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.InvalidState, State);

            if (target == State)
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.AlreadyInState, State);

            if (!CanTransition(State, target))
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.InvalidTransition, State);

            WorldChunkState previous = State;
            State = target;
            return WorldChunkTransitionResult.Succeeded(previous, State);
        }

        public static bool CanTransition(WorldChunkState from, WorldChunkState to)
        {
            if (!IsDefined(from) || !IsDefined(to) || from == to) return false;
            int difference = (int)to - (int)from;
            return difference == 1 || difference == -1;
        }

        private static void ValidateState(WorldChunkState state)
        {
            if (!IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown WorldChunkState.");
        }

        private static bool IsDefined(WorldChunkState state)
        {
            return state >= WorldChunkState.Unloaded && state <= WorldChunkState.Active;
        }
    }
}
