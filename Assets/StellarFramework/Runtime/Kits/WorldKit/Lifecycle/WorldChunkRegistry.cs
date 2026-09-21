using System;
using System.Collections.Generic;

namespace StellarFramework.WorldKit
{
    public enum WorldChunkRegistryError
    {
        None = 0,
        OutOfExtent,
        AlreadyRegistered,
        NotRegistered,
        ChunkMustBeUnloaded
    }

    public readonly struct WorldChunkRegistryResult
    {
        public bool Success => Error == WorldChunkRegistryError.None;
        public WorldChunkRegistryError Error { get; }
        public WorldChunkCoord Coord { get; }
        public WorldChunkState State { get; }

        internal WorldChunkRegistryResult(
            WorldChunkRegistryError error,
            WorldChunkCoord coord,
            WorldChunkState state)
        {
            Error = error;
            Coord = coord;
            State = state;
        }
    }

    /// <summary>
    /// On-demand chunk identity/lifecycle registry. It does not generate or persist chunk content.
    /// </summary>
    public sealed class WorldChunkRegistry
    {
        private readonly Dictionary<WorldChunkCoord, WorldChunkState> _states;

        public WorldId WorldId { get; }
        public WorldExtent Extent { get; }
        public int Count => _states.Count;

        public WorldChunkRegistry(
            WorldId worldId,
            WorldExtent extent,
            int initialCapacity = 0)
        {
            if (!worldId.IsValid) throw new ArgumentException("World ID must be valid.", nameof(worldId));
            if (!extent.IsValid) throw new ArgumentException("World extent must be valid.", nameof(extent));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            WorldId = worldId;
            Extent = extent;
            _states = new Dictionary<WorldChunkCoord, WorldChunkState>(initialCapacity);
        }

        public bool Contains(WorldChunkCoord coord) => _states.ContainsKey(coord);

        public bool TryGetState(WorldChunkCoord coord, out WorldChunkState state) =>
            _states.TryGetValue(coord, out state);

        public WorldChunkRegistryResult TryRegister(WorldChunkCoord coord)
        {
            if (!Extent.Contains(coord))
                return new WorldChunkRegistryResult(WorldChunkRegistryError.OutOfExtent, coord, WorldChunkState.Unloaded);

            if (_states.TryGetValue(coord, out WorldChunkState existing))
                return new WorldChunkRegistryResult(WorldChunkRegistryError.AlreadyRegistered, coord, existing);

            _states.Add(coord, WorldChunkState.Unloaded);
            return new WorldChunkRegistryResult(WorldChunkRegistryError.None, coord, WorldChunkState.Unloaded);
        }

        public WorldChunkTransitionResult TryTransition(WorldChunkCoord coord, WorldChunkState target)
        {
            if (!_states.TryGetValue(coord, out WorldChunkState current))
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.InvalidChunk, WorldChunkState.Unloaded);

            if (target < WorldChunkState.Unloaded || target > WorldChunkState.Active)
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.InvalidState, current);

            if (target == current)
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.AlreadyInState, current);

            if (!WorldChunkLifecycle.CanTransition(current, target))
                return WorldChunkTransitionResult.Failed(WorldChunkTransitionError.InvalidTransition, current);

            _states[coord] = target;
            return WorldChunkTransitionResult.Succeeded(current, target);
        }

        public WorldChunkRegistryResult TryRemove(WorldChunkCoord coord)
        {
            if (!_states.TryGetValue(coord, out WorldChunkState current))
                return new WorldChunkRegistryResult(WorldChunkRegistryError.NotRegistered, coord, WorldChunkState.Unloaded);

            if (current != WorldChunkState.Unloaded)
                return new WorldChunkRegistryResult(WorldChunkRegistryError.ChunkMustBeUnloaded, coord, current);

            _states.Remove(coord);
            return new WorldChunkRegistryResult(WorldChunkRegistryError.None, coord, WorldChunkState.Unloaded);
        }

        public void Clear()
        {
            foreach (KeyValuePair<WorldChunkCoord, WorldChunkState> pair in _states)
            {
                if (pair.Value != WorldChunkState.Unloaded)
                    throw new InvalidOperationException("Cannot clear a WorldChunkRegistry while registered chunks are loaded.");
            }

            _states.Clear();
        }
    }
}
