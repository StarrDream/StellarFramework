using System;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming.SaveKitAdapter
{
    public sealed class WorldDeltaPersistenceState
    {
        private readonly WorldId _worldId;
        private readonly WorldDeltaCodecRegistry _codecs;

        public WorldDeltaSet DeltaSet { get; private set; }

        public WorldDeltaPersistenceState(
            WorldId worldId,
            WorldDeltaCodecRegistry codecs,
            int initialCapacity = 0)
        {
            if (!worldId.IsValid) throw new ArgumentException("World ID must be valid.", nameof(worldId));
            _codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _worldId = worldId;
            DeltaSet = new WorldDeltaSet(worldId, initialCapacity);
        }

        public WorldDeltaSnapshot CaptureSnapshot()
        {
            WorldDeltaRecord[] records = new WorldDeltaRecord[DeltaSet.Count];
            DeltaSet.WriteTo(records.AsSpan());
            WorldDeltaSnapshotEntry[] entries = new WorldDeltaSnapshotEntry[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                WorldDeltaRecord record = records[i];
                if (!_codecs.TryGet(record.TypeId, out IWorldDeltaCodec codec))
                    throw new InvalidOperationException("No codec registered for world delta type '" + record.TypeId + "'.");
                if (!codec.TryEncode(record.Payload, out string payload, out string error))
                    throw new InvalidOperationException("Failed to encode world delta '" + record.TypeId + "': " + error);

                WorldDeltaSnapshotEntry entry = new WorldDeltaSnapshotEntry
                {
                    TypeId = record.TypeId.Value,
                    Version = record.Version.Value,
                    TargetKind = (int)record.Target.Kind,
                    Payload = payload ?? string.Empty
                };
                if (record.Target.Kind == WorldDeltaTargetKind.Region)
                {
                    entry.RegionX = record.Target.Region.X;
                    entry.RegionY = record.Target.Region.Y;
                }
                else if (record.Target.Kind == WorldDeltaTargetKind.Chunk)
                {
                    entry.ChunkX = record.Target.Chunk.X;
                    entry.ChunkY = record.Target.Chunk.Y;
                }
                entries[i] = entry;
            }

            return new WorldDeltaSnapshot
            {
                WorldId = _worldId.Value,
                Entries = entries
            };
        }

        public WorldDeltaSnapshot CreateEmptySnapshot() => new WorldDeltaSnapshot
        {
            WorldId = _worldId.Value,
            Entries = Array.Empty<WorldDeltaSnapshotEntry>()
        };

        public bool ValidateSnapshot(WorldDeltaSnapshot snapshot, out string error)
        {
            return TryBuildDeltaSet(snapshot, out _, out error);
        }

        public void RestoreSnapshot(WorldDeltaSnapshot snapshot)
        {
            if (!TryBuildDeltaSet(snapshot, out WorldDeltaSet next, out string error))
                throw new ArgumentException(error, nameof(snapshot));
            DeltaSet = next;
        }

        public void Clear() => DeltaSet = new WorldDeltaSet(_worldId);

        private bool TryBuildDeltaSet(
            WorldDeltaSnapshot snapshot,
            out WorldDeltaSet next,
            out string error)
        {
            next = null;
            error = null;
            if (snapshot == null)
            {
                error = "World delta snapshot cannot be null.";
                return false;
            }
            if (!string.Equals(snapshot.WorldId, _worldId.Value, StringComparison.Ordinal))
            {
                error = "World delta snapshot belongs to a different WorldId.";
                return false;
            }

            WorldDeltaSnapshotEntry[] entries = snapshot.Entries ?? Array.Empty<WorldDeltaSnapshotEntry>();
            WorldDeltaSet candidate = new WorldDeltaSet(_worldId, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                WorldDeltaSnapshotEntry entry = entries[i];
                if (entry == null)
                {
                    error = "World delta snapshot contains a null entry at index " + i + ".";
                    return false;
                }
                if (!WorldDeltaTypeId.TryCreate(entry.TypeId, out WorldDeltaTypeId typeId, out string typeError))
                {
                    error = "Invalid world delta TypeId at index " + i + ": " + typeError;
                    return false;
                }
                WorldDeltaVersion version;
                try
                {
                    version = new WorldDeltaVersion(entry.Version);
                }
                catch (ArgumentOutOfRangeException)
                {
                    error = "Invalid world delta version at index " + i + ".";
                    return false;
                }

                if (!TryBuildTarget(entry, out WorldDeltaTarget target, out error))
                {
                    error = "Invalid world delta target at index " + i + ": " + error;
                    return false;
                }
                if (!_codecs.TryGet(typeId, out IWorldDeltaCodec codec))
                {
                    error = "No codec registered for world delta type '" + typeId + "'.";
                    return false;
                }
                if (!codec.TryDecode(target, version, entry.Payload ?? string.Empty, out IWorldDelta delta, out string decodeError))
                {
                    error = "Failed to decode world delta '" + typeId + "' at index " + i + ": " + decodeError;
                    return false;
                }
                if (delta == null || delta.TypeId != typeId || delta.Version != version || delta.Target != target)
                {
                    error = "Codec returned delta metadata that does not match the snapshot at index " + i + ".";
                    return false;
                }
                if (!candidate.TryAppend(delta, out _, out WorldDeltaAppendError appendError))
                {
                    error = "Decoded world delta could not be appended at index " + i + ": " + appendError + ".";
                    return false;
                }
            }

            next = candidate;
            return true;
        }

        private bool TryBuildTarget(
            WorldDeltaSnapshotEntry entry,
            out WorldDeltaTarget target,
            out string error)
        {
            error = null;
            switch ((WorldDeltaTargetKind)entry.TargetKind)
            {
                case WorldDeltaTargetKind.World:
                    target = WorldDeltaTarget.ForWorld(_worldId);
                    return true;
                case WorldDeltaTargetKind.Region:
                    target = WorldDeltaTarget.ForRegion(
                        _worldId,
                        new WorldRegionCoord(entry.RegionX, entry.RegionY));
                    return true;
                case WorldDeltaTargetKind.Chunk:
                    target = WorldDeltaTarget.ForChunk(
                        _worldId,
                        new WorldChunkCoord(entry.ChunkX, entry.ChunkY));
                    return true;
                default:
                    target = default(WorldDeltaTarget);
                    error = "Unknown target kind " + entry.TargetKind + ".";
                    return false;
            }
        }
    }
}
