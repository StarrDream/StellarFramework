using System;
using System.Collections.Generic;

namespace StellarFramework.WorldKit
{
    public enum WorldDeltaAppendError
    {
        None = 0,
        NullDelta,
        InvalidTypeId,
        InvalidVersion,
        InvalidTarget,
        WrongWorld,
        SequenceExhausted
    }

    public readonly struct WorldDeltaRecord
    {
        public ulong Sequence { get; }
        public WorldDeltaTypeId TypeId { get; }
        public WorldDeltaVersion Version { get; }
        public WorldDeltaTarget Target { get; }
        public IWorldDelta Payload { get; }

        internal WorldDeltaRecord(
            ulong sequence,
            WorldDeltaTypeId typeId,
            WorldDeltaVersion version,
            WorldDeltaTarget target,
            IWorldDelta payload)
        {
            Sequence = sequence;
            TypeId = typeId;
            Version = version;
            Target = target;
            Payload = payload;
        }
    }

    /// <summary>
    /// Ordered runtime delta collection. Core owns ordering and validation, not serialization or application semantics.
    /// </summary>
    public sealed class WorldDeltaSet
    {
        private readonly List<WorldDeltaRecord> _records;
        private ulong _nextSequence = 1UL;

        public WorldId WorldId { get; }
        public int Count => _records.Count;
        public bool SequenceExhausted => _nextSequence == 0UL;

        public WorldDeltaSet(WorldId worldId, int initialCapacity = 0)
        {
            if (!worldId.IsValid) throw new ArgumentException("World ID must be valid.", nameof(worldId));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            WorldId = worldId;
            _records = new List<WorldDeltaRecord>(initialCapacity);
        }

        public WorldDeltaRecord Append(IWorldDelta delta)
        {
            if (!TryAppend(delta, out WorldDeltaRecord record, out WorldDeltaAppendError error))
            {
                if (error == WorldDeltaAppendError.NullDelta ||
                    error == WorldDeltaAppendError.InvalidTypeId ||
                    error == WorldDeltaAppendError.InvalidVersion ||
                    error == WorldDeltaAppendError.InvalidTarget ||
                    error == WorldDeltaAppendError.WrongWorld)
                    throw new ArgumentException("World delta append failed: " + error + ".", nameof(delta));

                throw new InvalidOperationException("World delta append failed: " + error + ".");
            }

            return record;
        }

        public bool TryAppend(
            IWorldDelta delta,
            out WorldDeltaRecord record,
            out WorldDeltaAppendError error)
        {
            record = default(WorldDeltaRecord);
            if (delta == null)
            {
                error = WorldDeltaAppendError.NullDelta;
                return false;
            }

            if (!delta.TypeId.IsValid)
            {
                error = WorldDeltaAppendError.InvalidTypeId;
                return false;
            }

            if (!delta.Version.IsValid)
            {
                error = WorldDeltaAppendError.InvalidVersion;
                return false;
            }

            if (!delta.Target.IsValid)
            {
                error = WorldDeltaAppendError.InvalidTarget;
                return false;
            }

            if (delta.Target.WorldId != WorldId)
            {
                error = WorldDeltaAppendError.WrongWorld;
                return false;
            }

            if (_nextSequence == 0UL)
            {
                error = WorldDeltaAppendError.SequenceExhausted;
                return false;
            }

            ulong sequence = _nextSequence;
            _nextSequence = sequence == ulong.MaxValue ? 0UL : sequence + 1UL;
            record = new WorldDeltaRecord(sequence, delta.TypeId, delta.Version, delta.Target, delta);
            _records.Add(record);
            error = WorldDeltaAppendError.None;
            return true;
        }

        public bool TryGetAt(int index, out WorldDeltaRecord record)
        {
            if ((uint)index >= (uint)_records.Count)
            {
                record = default(WorldDeltaRecord);
                return false;
            }

            record = _records[index];
            return true;
        }

        public int WriteTo(Span<WorldDeltaRecord> destination)
        {
            if (destination.Length < _records.Count)
                throw new ArgumentException("Destination buffer is smaller than the delta count.", nameof(destination));

            for (int i = 0; i < _records.Count; i++) destination[i] = _records[i];
            return _records.Count;
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}
