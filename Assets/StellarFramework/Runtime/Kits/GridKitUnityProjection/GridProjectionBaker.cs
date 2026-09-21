using System;

namespace StellarFramework.GridKit.UnityProjection
{
    public static class GridProjectionBaker
    {
        public static GridProjectionBakeResult Bake(
            IGridProjectionSource source,
            GridProjectionBakeSettings settings,
            DenseGrid<GridBakeCell> destination,
            Span<GridBakeCell> scratch)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (destination.Bounds != settings.Bounds)
                throw new ArgumentException(
                    "Destination bounds must match bake settings.",
                    nameof(destination));
            if (scratch.Length < destination.Count)
                throw new ArgumentException(
                    "Scratch buffer must cover every destination cell.",
                    nameof(scratch));

            int walkable = 0;
            int blocked = 0;
            int missing = 0;
            for (int index = 0;
                 index < destination.Count;
                 index++)
            {
                GridCoord coord =
                    destination.GetCoord(index);
                GridProjectionQuery query =
                    new GridProjectionQuery(
                        coord,
                        settings.GetSampleOrigin(coord),
                        settings.SampleDirection,
                        settings.MaxDistance);
                if (!source.TrySample(
                        in query,
                        out GridProjectionSample sample))
                {
                    return new GridProjectionBakeResult(
                        GridProjectionBakeError.SampleFailed,
                        destination.Count,
                        walkable,
                        blocked,
                        missing);
                }

                if (!sample.Hit)
                {
                    scratch[index] =
                        new GridBakeCell(
                            GridBakeFlags.None,
                            0f,
                            0f,
                            settings.DefaultMovementCost,
                            0);
                    missing++;
                    blocked++;
                    continue;
                }

                GridBakeFlags flags =
                    GridBakeFlags.HasSurface;
                if (sample.ObstacleHit)
                    flags |= GridBakeFlags.Obstacle;
                bool baseWalkable =
                    !sample.ObstacleHit &&
                    sample.SlopeDegrees <=
                    settings.MaxWalkableSlope;
                if (baseWalkable)
                {
                    flags |= GridBakeFlags.BaseWalkable;
                    walkable++;
                }
                else
                {
                    blocked++;
                }

                long cost =
                    sample.SlopeDegrees >=
                    settings.SteepSlopeThreshold
                        ? settings.SteepMovementCost
                        : settings.DefaultMovementCost;
                scratch[index] =
                    new GridBakeCell(
                        flags,
                        sample.Height,
                        sample.SlopeDegrees,
                        cost,
                        sample.SurfaceCategory);
            }

            destination.CopyFrom(
                scratch.Slice(
                    0,
                    destination.Count));
            return new GridProjectionBakeResult(
                GridProjectionBakeError.None,
                destination.Count,
                walkable,
                blocked,
                missing);
        }
    }
}
