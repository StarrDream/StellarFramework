using System;

namespace StellarFramework.WorldGenKit.Feature.WorldKitAdapter
{
    [Serializable]
    public sealed class WorldFeatureUsageEntry
    {
        public string FeatureId;
        public int Count;
    }

    [Serializable]
    public sealed class WorldFeatureRegionUsageSnapshot
    {
        public long RegionX;
        public long RegionY;
        public WorldFeatureUsageEntry[] Entries = Array.Empty<WorldFeatureUsageEntry>();
    }

    [Serializable]
    public sealed class WorldFeatureUsageSnapshot
    {
        public WorldFeatureUsageEntry[] WorldEntries = Array.Empty<WorldFeatureUsageEntry>();
        public WorldFeatureRegionUsageSnapshot[] Regions = Array.Empty<WorldFeatureRegionUsageSnapshot>();
    }
}
