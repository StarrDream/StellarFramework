using System;
using System.Collections.Generic;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming
{
    public enum WorldStreamingTransitionError
    {
        None = 0,
        InvalidTier = 1,
        AlreadyInTier = 2,
        InvalidTransition = 3
    }

    public readonly struct WorldStreamingTransitionResult
    {
        public bool Success => Error == WorldStreamingTransitionError.None;
        public WorldStreamingTransitionError Error { get; }
        public WorldStreamingTier PreviousTier { get; }
        public WorldStreamingTier CurrentTier { get; }

        internal WorldStreamingTransitionResult(
            WorldStreamingTransitionError error,
            WorldStreamingTier previousTier,
            WorldStreamingTier currentTier)
        {
            Error = error;
            PreviousTier = previousTier;
            CurrentTier = currentTier;
        }
    }

    public readonly struct WorldChunkStreamingState : IEquatable<WorldChunkStreamingState>
    {
        public WorldChunkCoord Coord { get; }
        public WorldStreamingTier Tier { get; }

        public WorldChunkStreamingState(WorldChunkCoord coord, WorldStreamingTier tier)
        {
            if (tier < WorldStreamingTier.Metadata || tier > WorldStreamingTier.Presentation)
                throw new ArgumentOutOfRangeException(nameof(tier));
            Coord = coord;
            Tier = tier;
        }

        public bool Equals(WorldChunkStreamingState other) => Coord == other.Coord && Tier == other.Tier;
        public override bool Equals(object obj) => obj is WorldChunkStreamingState other && Equals(other);
        public override int GetHashCode() => unchecked((Coord.GetHashCode() * 397) ^ (int)Tier);
        public static bool operator ==(WorldChunkStreamingState left, WorldChunkStreamingState right) => left.Equals(right);
        public static bool operator !=(WorldChunkStreamingState left, WorldChunkStreamingState right) => !left.Equals(right);
    }

    /// <summary>
    /// Tracks streaming residency independently from frozen WorldChunkState.
    /// Entries at None are not stored.
    /// </summary>
    public sealed class WorldChunkStreamingRegistry
    {
        private readonly Dictionary<WorldChunkCoord, WorldStreamingTier> _tiers;
        private readonly List<Entry> _entries;
        private readonly Dictionary<WorldChunkCoord, int> _entryIndices;
        private int _inactiveCount;

        public int Count => _tiers.Count;

        public WorldChunkStreamingRegistry(int initialCapacity = 0)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _tiers = new Dictionary<WorldChunkCoord, WorldStreamingTier>(initialCapacity);
            _entries = new List<Entry>(initialCapacity);
            _entryIndices = new Dictionary<WorldChunkCoord, int>(initialCapacity);
        }

        public WorldStreamingTier GetTier(WorldChunkCoord coord) =>
            _tiers.TryGetValue(coord, out WorldStreamingTier tier)
                ? tier
                : WorldStreamingTier.None;

        public WorldStreamingTransitionResult TryTransition(
            WorldChunkCoord coord,
            WorldStreamingTier target)
        {
            WorldStreamingTier current = GetTier(coord);
            if (!IsDefined(target))
            {
                return new WorldStreamingTransitionResult(
                    WorldStreamingTransitionError.InvalidTier,
                    current,
                    current);
            }

            if (target == current)
            {
                return new WorldStreamingTransitionResult(
                    WorldStreamingTransitionError.AlreadyInTier,
                    current,
                    current);
            }

            int difference = (int)target - (int)current;
            if (difference != 1 && difference != -1)
            {
                return new WorldStreamingTransitionResult(
                    WorldStreamingTransitionError.InvalidTransition,
                    current,
                    current);
            }

            if (target == WorldStreamingTier.None)
            {
                _tiers.Remove(coord);
                if (!_entryIndices.TryGetValue(coord, out int entryIndex))
                    throw new InvalidOperationException("Streaming registry entry index is inconsistent.");
                Entry removed = _entries[entryIndex];
                _entries[entryIndex] = new Entry(removed.Coord, removed.Tier, false);
                _entryIndices.Remove(coord);
                _inactiveCount++;
                CompactIfNeeded();
            }
            else
            {
                _tiers[coord] = target;
                if (_entryIndices.TryGetValue(coord, out int entryIndex))
                {
                    Entry existing = _entries[entryIndex];
                    _entries[entryIndex] = new Entry(existing.Coord, target, true);
                }
                else
                {
                    int nextIndex = _entries.Count;
                    _entries.Add(new Entry(coord, target, true));
                    _entryIndices.Add(coord, nextIndex);
                }
            }

            return new WorldStreamingTransitionResult(
                WorldStreamingTransitionError.None,
                current,
                target);
        }

        public int WriteStates(Span<WorldChunkStreamingState> destination)
        {
            if (destination.Length < Count)
                throw new ArgumentException("Destination buffer is smaller than the resident Chunk count.", nameof(destination));

            int written = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!entry.Active) continue;
                destination[written++] = new WorldChunkStreamingState(entry.Coord, entry.Tier);
            }

            if (written != Count)
                throw new InvalidOperationException("Streaming registry state is inconsistent.");
            return written;
        }

        public void Clear()
        {
            _tiers.Clear();
            _entries.Clear();
            _entryIndices.Clear();
            _inactiveCount = 0;
        }

        private static bool IsDefined(WorldStreamingTier tier) =>
            tier >= WorldStreamingTier.None && tier <= WorldStreamingTier.Presentation;

        private void CompactIfNeeded()
        {
            if (_inactiveCount < 64 || _inactiveCount <= _entryIndices.Count) return;

            int write = 0;
            for (int read = 0; read < _entries.Count; read++)
            {
                Entry entry = _entries[read];
                if (!entry.Active) continue;
                if (write != read) _entries[write] = entry;
                _entryIndices[entry.Coord] = write;
                write++;
            }

            if (write < _entries.Count)
                _entries.RemoveRange(write, _entries.Count - write);
            _inactiveCount = 0;
        }

        private readonly struct Entry
        {
            internal WorldChunkCoord Coord { get; }
            internal WorldStreamingTier Tier { get; }
            internal bool Active { get; }

            internal Entry(WorldChunkCoord coord, WorldStreamingTier tier, bool active)
            {
                Coord = coord;
                Tier = tier;
                Active = active;
            }
        }
    }
}
