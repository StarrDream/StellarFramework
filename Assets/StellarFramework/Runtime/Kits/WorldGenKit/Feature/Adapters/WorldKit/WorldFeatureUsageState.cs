using System;
using System.Collections.Generic;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldGenKit.Feature.WorldKitAdapter
{
    public sealed class WorldFeatureUsageState
    {
        private readonly WorldFeatureCatalog _catalog;
        private readonly int[] _worldCounts;
        private Dictionary<WorldRegionCoord, int[]> _regionCounts;

        public int FeatureCount => _catalog.Count;
        public ReadOnlySpan<int> WorldCounts => _worldCounts;
        public int RegionCount => _regionCounts.Count;

        public WorldFeatureUsageState(WorldFeatureCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _worldCounts = new int[catalog.Count];
            _regionCounts = new Dictionary<WorldRegionCoord, int[]>();
        }

        public ReadOnlySpan<int> GetRegionCounts(WorldRegionCoord region) =>
            _regionCounts.TryGetValue(region, out int[] counts)
                ? counts.AsSpan()
                : ReadOnlySpan<int>.Empty;

        public void CopyRegionCounts(WorldRegionCoord region, Span<int> destination)
        {
            if (destination.Length < _catalog.Count)
                throw new ArgumentException("Destination must cover every Feature.", nameof(destination));
            destination.Slice(0, _catalog.Count).Clear();
            if (_regionCounts.TryGetValue(region, out int[] counts))
                counts.AsSpan().CopyTo(destination);
        }

        public void Commit(
            WorldRegionCoord region,
            ReadOnlySpan<WorldFeatureInstanceData> acceptedInstances,
            Span<int> incrementScratch)
        {
            if (incrementScratch.Length < _catalog.Count)
                throw new ArgumentException("Increment scratch must cover every Feature.", nameof(incrementScratch));

            Span<int> increments = incrementScratch.Slice(0, _catalog.Count);
            increments.Clear();

            for (int i = 0; i < acceptedInstances.Length; i++)
            {
                int featureIndex = acceptedInstances[i].FeatureIndex;
                if ((uint)featureIndex >= (uint)_catalog.Count)
                    throw new ArgumentOutOfRangeException(
                        nameof(acceptedInstances),
                        "Accepted Feature index is outside the catalog.");
                increments[featureIndex] = checked(increments[featureIndex] + 1);
            }

            _regionCounts.TryGetValue(region, out int[] existingRegion);
            for (int i = 0; i < _catalog.Count; i++)
            {
                int increment = increments[i];
                if (increment == 0) continue;
                checked
                {
                    _ = _worldCounts[i] + increment;
                    _ = (existingRegion == null ? 0 : existingRegion[i]) + increment;
                }
            }

            if (acceptedInstances.Length == 0) return;
            if (existingRegion == null)
            {
                existingRegion = new int[_catalog.Count];
                _regionCounts.Add(region, existingRegion);
            }

            for (int i = 0; i < _catalog.Count; i++)
            {
                int increment = increments[i];
                if (increment == 0) continue;
                _worldCounts[i] += increment;
                existingRegion[i] += increment;
            }
        }

        public WorldFeatureUsageSnapshot CaptureSnapshot()
        {
            WorldFeatureUsageSnapshot snapshot = new WorldFeatureUsageSnapshot
            {
                WorldEntries = BuildEntries(_worldCounts)
            };

            List<WorldRegionCoord> regions = new List<WorldRegionCoord>(_regionCounts.Keys);
            regions.Sort(CompareRegion);
            WorldFeatureRegionUsageSnapshot[] regionSnapshots =
                new WorldFeatureRegionUsageSnapshot[regions.Count];
            for (int i = 0; i < regions.Count; i++)
            {
                WorldRegionCoord region = regions[i];
                regionSnapshots[i] = new WorldFeatureRegionUsageSnapshot
                {
                    RegionX = region.X,
                    RegionY = region.Y,
                    Entries = BuildEntries(_regionCounts[region])
                };
            }

            snapshot.Regions = regionSnapshots;
            return snapshot;
        }

        public bool ValidateSnapshot(WorldFeatureUsageSnapshot snapshot, out string error)
        {
            return TryBuildState(snapshot, out _, out _, out error);
        }

        public void RestoreSnapshot(WorldFeatureUsageSnapshot snapshot)
        {
            if (!TryBuildState(
                    snapshot,
                    out int[] nextWorldCounts,
                    out Dictionary<WorldRegionCoord, int[]> nextRegionCounts,
                    out string error))
                throw new ArgumentException(error, nameof(snapshot));

            Array.Copy(nextWorldCounts, _worldCounts, _worldCounts.Length);
            _regionCounts = nextRegionCounts;
        }

        public void Clear()
        {
            Array.Clear(_worldCounts, 0, _worldCounts.Length);
            _regionCounts.Clear();
        }

        private WorldFeatureUsageEntry[] BuildEntries(int[] counts)
        {
            List<WorldFeatureUsageEntry> entries = new List<WorldFeatureUsageEntry>();
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                if (count <= 0) continue;
                entries.Add(new WorldFeatureUsageEntry
                {
                    FeatureId = _catalog.GetDefinition(i).Id.Value,
                    Count = count
                });
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.FeatureId, b.FeatureId));
            return entries.ToArray();
        }

        private bool TryBuildState(
            WorldFeatureUsageSnapshot snapshot,
            out int[] worldCounts,
            out Dictionary<WorldRegionCoord, int[]> regionCounts,
            out string error)
        {
            worldCounts = null;
            regionCounts = null;
            error = null;
            if (snapshot == null)
            {
                error = "Feature usage snapshot cannot be null.";
                return false;
            }

            int[] nextWorld = new int[_catalog.Count];
            if (!TryApplyEntries(snapshot.WorldEntries, nextWorld, "world", out error))
                return false;

            WorldFeatureRegionUsageSnapshot[] regions =
                snapshot.Regions ?? Array.Empty<WorldFeatureRegionUsageSnapshot>();
            Dictionary<WorldRegionCoord, int[]> nextRegions =
                new Dictionary<WorldRegionCoord, int[]>(regions.Length);
            for (int i = 0; i < regions.Length; i++)
            {
                WorldFeatureRegionUsageSnapshot regionSnapshot = regions[i];
                if (regionSnapshot == null)
                {
                    error = "Feature usage snapshot contains a null region entry.";
                    return false;
                }

                WorldRegionCoord region =
                    new WorldRegionCoord(regionSnapshot.RegionX, regionSnapshot.RegionY);
                if (nextRegions.ContainsKey(region))
                {
                    error = "Duplicate Feature usage region: " + region + ".";
                    return false;
                }

                int[] counts = new int[_catalog.Count];
                if (!TryApplyEntries(regionSnapshot.Entries, counts, "region " + region, out error))
                    return false;
                nextRegions.Add(region, counts);
            }

            worldCounts = nextWorld;
            regionCounts = nextRegions;
            return true;
        }

        private bool TryApplyEntries(
            WorldFeatureUsageEntry[] entries,
            int[] destination,
            string scope,
            out string error)
        {
            error = null;
            entries = entries ?? Array.Empty<WorldFeatureUsageEntry>();
            byte[] seen = new byte[_catalog.Count];

            for (int i = 0; i < entries.Length; i++)
            {
                WorldFeatureUsageEntry entry = entries[i];
                if (entry == null)
                {
                    error = "Feature usage " + scope + " contains a null entry.";
                    return false;
                }
                if (entry.Count < 0)
                {
                    error = "Feature usage count cannot be negative for '" + entry.FeatureId + "'.";
                    return false;
                }

                WorldFeatureId featureId;
                try
                {
                    featureId = WorldFeatureId.From(entry.FeatureId);
                }
                catch (ArgumentException)
                {
                    error = "Feature usage contains invalid Stable ID '" + entry.FeatureId + "'.";
                    return false;
                }

                if (!_catalog.TryGetIndex(featureId, out int featureIndex))
                {
                    error = "Feature usage references unknown Feature '" + featureId + "'.";
                    return false;
                }
                if (seen[featureIndex] != 0)
                {
                    error = "Feature usage " + scope + " contains duplicate Feature '" + featureId + "'.";
                    return false;
                }

                seen[featureIndex] = 1;
                destination[featureIndex] = entry.Count;
            }

            return true;
        }

        private static int CompareRegion(WorldRegionCoord a, WorldRegionCoord b)
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        }
    }
}
