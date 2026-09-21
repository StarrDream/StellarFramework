using System;
using System.Collections.Generic;

namespace StellarFramework.WorldKit
{
    /// <summary>
    /// Deterministic insertion-order dirty Chunk tracker with caller-owned output buffers.
    /// </summary>
    public sealed class WorldDirtyChunkTracker
    {
        private readonly List<Entry> _entries;
        private readonly Dictionary<WorldChunkCoord, int> _indexByCoord;
        private int _inactiveCount;

        public int Count => _indexByCoord.Count;

        public WorldDirtyChunkTracker(int initialCapacity = 0)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _entries = new List<Entry>(initialCapacity);
            _indexByCoord = new Dictionary<WorldChunkCoord, int>(initialCapacity);
        }

        public bool IsDirty(WorldChunkCoord coord) => _indexByCoord.ContainsKey(coord);

        public bool MarkDirty(WorldChunkCoord coord)
        {
            if (_indexByCoord.ContainsKey(coord)) return false;

            int index = _entries.Count;
            _entries.Add(new Entry(coord, true));
            _indexByCoord.Add(coord, index);
            return true;
        }

        public bool ClearDirty(WorldChunkCoord coord)
        {
            if (!_indexByCoord.TryGetValue(coord, out int index)) return false;

            Entry entry = _entries[index];
            if (!entry.Active)
                throw new InvalidOperationException("Dirty tracker index is inconsistent.");

            _entries[index] = new Entry(entry.Coord, false);
            _indexByCoord.Remove(coord);
            _inactiveCount++;
            CompactIfNeeded();
            return true;
        }

        public int WriteDirty(Span<WorldChunkCoord> destination)
        {
            if (destination.Length < Count)
                throw new ArgumentException("Destination buffer is smaller than the dirty Chunk count.", nameof(destination));

            int written = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!entry.Active) continue;
                destination[written++] = entry.Coord;
            }

            if (written != Count)
                throw new InvalidOperationException("Dirty tracker state is inconsistent.");

            return written;
        }

        public void Clear()
        {
            _entries.Clear();
            _indexByCoord.Clear();
            _inactiveCount = 0;
        }

        private void CompactIfNeeded()
        {
            if (_inactiveCount < 64 || _inactiveCount <= _indexByCoord.Count) return;

            int write = 0;
            for (int read = 0; read < _entries.Count; read++)
            {
                Entry entry = _entries[read];
                if (!entry.Active) continue;

                if (write != read) _entries[write] = entry;
                _indexByCoord[entry.Coord] = write;
                write++;
            }

            if (write < _entries.Count)
                _entries.RemoveRange(write, _entries.Count - write);

            _inactiveCount = 0;
        }

        private readonly struct Entry
        {
            internal WorldChunkCoord Coord { get; }
            internal bool Active { get; }

            internal Entry(WorldChunkCoord coord, bool active)
            {
                Coord = coord;
                Active = active;
            }
        }
    }
}
