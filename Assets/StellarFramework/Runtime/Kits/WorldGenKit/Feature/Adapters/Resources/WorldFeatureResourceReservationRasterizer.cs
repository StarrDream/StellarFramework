using System;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.WorldGenKit.Feature.ResourcesAdapter
{
    public readonly struct WorldFeatureResourceReservationApplyResult
    {
        public int BoundFeatureCount { get; }
        public int ReservedSampleCount { get; }

        internal WorldFeatureResourceReservationApplyResult(int boundFeatureCount, int reservedSampleCount)
        {
            BoundFeatureCount = boundFeatureCount;
            ReservedSampleCount = reservedSampleCount;
        }
    }

    public static class WorldFeatureResourceReservationRasterizer
    {
        public static WorldFeatureResourceReservationApplyResult Apply(
            ReadOnlySpan<WorldFeatureReservation> reservations,
            WorldCompiledFeatureResourceReservationProfile profile,
            in WorldResourcePlanarDomain domain,
            Span<WorldOccupancyCellState> occupancyCells)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (occupancyCells.Length < domain.Count)
                throw new ArgumentException("Occupancy cell buffer must cover the resource planar domain.", nameof(occupancyCells));

            for (int i = 0; i < reservations.Length; i++)
            {
                if ((uint)reservations[i].FeatureIndex >= (uint)profile.FeatureCount)
                    throw new ArgumentOutOfRangeException(nameof(reservations), "Feature reservation index is outside the compiled profile catalog.");
                if (!reservations[i].Bounds.IsValid)
                    throw new ArgumentException("Feature reservation bounds must be valid.", nameof(reservations));
            }

            int boundFeatureCount = 0;
            int reservedSampleCount = 0;

            // Pass 1 is validation only. If one target sample conflicts with existing resource occupancy,
            // no feature reservation is applied to any cell.
            for (int i = 0; i < reservations.Length; i++)
            {
                WorldFeatureReservation reservation = reservations[i];
                if (!profile.TryGet(reservation.FeatureIndex, out WorldOccupancyMask occupies, out WorldOccupancyMask excludes))
                    continue;
                if (occupies.IsEmpty && excludes.IsEmpty) continue;

                WorldFeatureBounds bounds = reservation.Bounds;
                GetSampleRange(in bounds, in domain, out int minX, out int minY, out int maxXExclusive, out int maxYExclusive);
                for (int y = minY; y < maxYExclusive; y++)
                {
                    int row = y * domain.Width;
                    for (int x = minX; x < maxXExclusive; x++)
                    {
                        if (!occupancyCells[row + x].CanAccept(occupies, excludes))
                            throw new InvalidOperationException("Feature resource reservation conflicts with existing occupancy. Features must reserve space before conflicting resources are accepted.");
                    }
                }
            }

            // Pass 2 applies only after the whole request set has passed preflight.
            for (int i = 0; i < reservations.Length; i++)
            {
                WorldFeatureReservation reservation = reservations[i];
                if (!profile.TryGet(reservation.FeatureIndex, out WorldOccupancyMask occupies, out WorldOccupancyMask excludes))
                    continue;
                if (occupies.IsEmpty && excludes.IsEmpty) continue;
                boundFeatureCount++;

                WorldFeatureBounds bounds = reservation.Bounds;
                GetSampleRange(in bounds, in domain, out int minX, out int minY, out int maxXExclusive, out int maxYExclusive);
                for (int y = minY; y < maxYExclusive; y++)
                {
                    int row = y * domain.Width;
                    for (int x = minX; x < maxXExclusive; x++)
                    {
                        occupancyCells[row + x].Reserve(occupies, excludes);
                        reservedSampleCount++;
                    }
                }
            }

            return new WorldFeatureResourceReservationApplyResult(boundFeatureCount, reservedSampleCount);
        }

        private static void GetSampleRange(
            in WorldFeatureBounds bounds,
            in WorldResourcePlanarDomain domain,
            out int minX,
            out int minY,
            out int maxXExclusive,
            out int maxYExclusive)
        {
            minX = ToClampedCeilIndex(bounds.MinX, domain.OriginX, domain.SampleStep, domain.Width);
            minY = ToClampedCeilIndex(bounds.MinY, domain.OriginY, domain.SampleStep, domain.Height);
            maxXExclusive = ToClampedCeilIndex(bounds.MaxX, domain.OriginX, domain.SampleStep, domain.Width);
            maxYExclusive = ToClampedCeilIndex(bounds.MaxY, domain.OriginY, domain.SampleStep, domain.Height);
        }

        private static int ToClampedCeilIndex(double worldValue, long origin, long sampleStep, int sampleCount)
        {
            double local = (worldValue - origin) / sampleStep;
            if (local <= 0d) return 0;
            if (local >= sampleCount) return sampleCount;
            return (int)Math.Ceiling(local);
        }
    }
}
