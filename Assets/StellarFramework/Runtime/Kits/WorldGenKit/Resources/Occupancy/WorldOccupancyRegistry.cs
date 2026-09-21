using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldOccupancyHandle : IEquatable<WorldOccupancyHandle>
    {
        public int Index { get; }
        internal ulong Bit { get; }
        public bool IsValid => Index >= 0 && Index < 64 && Bit != 0UL;

        internal WorldOccupancyHandle(int index)
        {
            Index = index;
            Bit = 1UL << index;
        }

        public bool Equals(WorldOccupancyHandle other) => Index == other.Index && Bit == other.Bit;
        public override bool Equals(object obj) => obj is WorldOccupancyHandle other && Equals(other);
        public override int GetHashCode() => Index;
        public static bool operator ==(WorldOccupancyHandle left, WorldOccupancyHandle right) => left.Equals(right);
        public static bool operator !=(WorldOccupancyHandle left, WorldOccupancyHandle right) => !left.Equals(right);
    }

    public sealed class WorldOccupancyRegistry
    {
        private readonly WorldOccupancyTypeId[] _ids;
        private readonly Dictionary<WorldOccupancyTypeId, int> _indexById;

        public int Count => _ids.Length;

        internal WorldOccupancyRegistry(WorldOccupancyTypeId[] ids)
        {
            _ids = ids;
            _indexById = new Dictionary<WorldOccupancyTypeId, int>(ids.Length);
            for (int i = 0; i < ids.Length; i++) _indexById.Add(ids[i], i);
        }

        public WorldOccupancyTypeId GetId(WorldOccupancyHandle handle)
        {
            if (!handle.IsValid || handle.Index >= _ids.Length) throw new ArgumentOutOfRangeException(nameof(handle));
            return _ids[handle.Index];
        }

        public bool TryResolve(WorldOccupancyTypeId id, out WorldOccupancyHandle handle)
        {
            if (_indexById.TryGetValue(id, out int index))
            {
                handle = new WorldOccupancyHandle(index);
                return true;
            }

            handle = default(WorldOccupancyHandle);
            return false;
        }

        public WorldOccupancyMask CreateMask(params WorldOccupancyTypeId[] ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            ulong bits = 0UL;
            for (int i = 0; i < ids.Length; i++)
            {
                if (!_indexById.TryGetValue(ids[i], out int index))
                    throw new ArgumentException("Occupancy ID is not registered: " + ids[i], nameof(ids));
                bits |= 1UL << index;
            }
            return new WorldOccupancyMask(bits);
        }
    }

    public sealed class WorldOccupancyRegistryBuilder
    {
        private readonly List<WorldOccupancyTypeId> _ids = new List<WorldOccupancyTypeId>();
        private readonly HashSet<WorldOccupancyTypeId> _unique = new HashSet<WorldOccupancyTypeId>();

        public WorldOccupancyHandle Register(WorldOccupancyTypeId id)
        {
            if (!id.IsValid) throw new ArgumentException("Occupancy ID must be valid.", nameof(id));
            if (_ids.Count >= 64) throw new InvalidOperationException("WorldGenKit.Resources supports at most 64 compiled occupancy types per registry.");
            if (!_unique.Add(id)) throw new InvalidOperationException("Duplicate occupancy ID: " + id);
            int index = _ids.Count;
            _ids.Add(id);
            return new WorldOccupancyHandle(index);
        }

        public WorldOccupancyRegistry Build() => new WorldOccupancyRegistry(_ids.ToArray());
    }
}
