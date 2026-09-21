using System;

namespace StellarFramework.WorldKit.Streaming.SaveKitAdapter
{
    [Serializable]
    public sealed class WorldDeltaSnapshot
    {
        public string WorldId;
        public WorldDeltaSnapshotEntry[] Entries;
    }

    [Serializable]
    public sealed class WorldDeltaSnapshotEntry
    {
        public string TypeId;
        public int Version;
        public int TargetKind;
        public long RegionX;
        public long RegionY;
        public long ChunkX;
        public long ChunkY;
        public string Payload;
    }
}
