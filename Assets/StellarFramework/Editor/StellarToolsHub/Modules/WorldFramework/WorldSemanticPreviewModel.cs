using System;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public readonly struct WorldResourcePreviewResult
    {
        public int Accepted { get; }
        public int RejectedOccupancy { get; }

        internal WorldResourcePreviewResult(int accepted, int rejectedOccupancy)
        {
            Accepted = accepted;
            RejectedOccupancy = rejectedOccupancy;
        }
    }

    public readonly struct WorldFeaturePreviewResult
    {
        public int Accepted { get; }
        public int RejectedQuota { get; }
        public int RejectedReservation { get; }

        internal WorldFeaturePreviewResult(
            int accepted,
            int rejectedQuota,
            int rejectedReservation)
        {
            Accepted = accepted;
            RejectedQuota = rejectedQuota;
            RejectedReservation = rejectedReservation;
        }
    }

    public static class WorldSemanticPreviewModel
    {
        public static WorldResourcePreviewResult ResolveResources(
            ReadOnlySpan<WorldSpawnCandidate> candidates,
            WorldResourceCatalog catalog,
            int sampleCount)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (sampleCount < 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));

            WorldOccupancyCellState[] occupancy =
                new WorldOccupancyCellState[sampleCount];
            int[] order = new int[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];
            WorldScatterResolveResult result = WorldResourceScatterResolver.Resolve(
                candidates,
                catalog,
                occupancy,
                order,
                output);
            return new WorldResourcePreviewResult(
                result.AcceptedCount,
                result.RejectedOccupancyCount);
        }

        public static WorldFeaturePreviewResult ResolveFeatures(
            ReadOnlySpan<WorldFeatureCandidate> candidates,
            WorldFeatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            int[] order = new int[candidates.Length];
            int[] acceptedPerFeature = new int[catalog.Count];
            WorldFeatureReservation[] reservations =
                new WorldFeatureReservation[candidates.Length];
            WorldFeatureInstanceData[] output =
                new WorldFeatureInstanceData[candidates.Length];
            WorldFeatureResolveResult result = WorldFeatureResolver.Resolve(
                candidates,
                catalog,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<WorldFeatureReservation>.Empty,
                order,
                acceptedPerFeature,
                reservations,
                output);
            return new WorldFeaturePreviewResult(
                result.AcceptedCount,
                result.RejectedQuotaCount,
                result.RejectedReservationCount);
        }
    }
}
