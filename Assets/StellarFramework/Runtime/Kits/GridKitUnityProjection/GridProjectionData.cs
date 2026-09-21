using System;

namespace StellarFramework.GridKit.UnityProjection
{
    [Flags]
    public enum GridBakeFlags
    {
        None = 0,
        HasSurface = 1 << 0,
        BaseWalkable = 1 << 1,
        Obstacle = 1 << 2
    }

    public readonly struct GridBakeCell
    {
        public GridBakeFlags Flags { get; }
        public float Height { get; }
        public float SlopeDegrees { get; }
        public long BaseMovementCost { get; }
        public int SurfaceCategory { get; }

        public bool HasSurface =>
            (Flags & GridBakeFlags.HasSurface) != 0;
        public bool BaseWalkable =>
            (Flags & GridBakeFlags.BaseWalkable) != 0;
        public bool HasObstacle =>
            (Flags & GridBakeFlags.Obstacle) != 0;

        public GridBakeCell(
            GridBakeFlags flags,
            float height,
            float slopeDegrees,
            long baseMovementCost,
            int surfaceCategory)
        {
            if (float.IsNaN(height) || float.IsInfinity(height))
                throw new ArgumentOutOfRangeException(nameof(height));
            if (float.IsNaN(slopeDegrees) ||
                float.IsInfinity(slopeDegrees) ||
                slopeDegrees < 0f ||
                slopeDegrees > 180f)
                throw new ArgumentOutOfRangeException(nameof(slopeDegrees));
            if (baseMovementCost <= 0L)
                throw new ArgumentOutOfRangeException(nameof(baseMovementCost));

            Flags = flags;
            Height = height;
            SlopeDegrees = slopeDegrees;
            BaseMovementCost = baseMovementCost;
            SurfaceCategory = surfaceCategory;
        }
    }

    public enum GridWalkabilityOverride
    {
        None = 0,
        ForceWalkable = 1,
        ForceBlocked = 2
    }

    public readonly struct GridTraversalOverrideCell
    {
        public GridWalkabilityOverride Walkability { get; }
        public bool HasMovementCostOverride { get; }
        public long MovementCost { get; }

        public GridTraversalOverrideCell(
            GridWalkabilityOverride walkability,
            bool hasMovementCostOverride = false,
            long movementCost = 0L)
        {
            if (walkability < GridWalkabilityOverride.None ||
                walkability > GridWalkabilityOverride.ForceBlocked)
                throw new ArgumentOutOfRangeException(nameof(walkability));
            if (hasMovementCostOverride && movementCost <= 0L)
                throw new ArgumentOutOfRangeException(nameof(movementCost));

            Walkability = walkability;
            HasMovementCostOverride = hasMovementCostOverride;
            MovementCost = hasMovementCostOverride
                ? movementCost
                : 0L;
        }

        public static GridTraversalOverrideCell ForceWalkable(
            long? movementCost = null) =>
            new GridTraversalOverrideCell(
                GridWalkabilityOverride.ForceWalkable,
                movementCost.HasValue,
                movementCost.GetValueOrDefault());

        public static GridTraversalOverrideCell ForceBlocked() =>
            new GridTraversalOverrideCell(
                GridWalkabilityOverride.ForceBlocked);

        public static GridTraversalOverrideCell Cost(
            long movementCost) =>
            new GridTraversalOverrideCell(
                GridWalkabilityOverride.None,
                true,
                movementCost);
    }

    public readonly struct GridTraversalCompositionSettings
    {
        public bool RequireSampledSurface { get; }
        public bool ObstacleIsHardBlock { get; }

        public GridTraversalCompositionSettings(
            bool requireSampledSurface = true,
            bool obstacleIsHardBlock = true)
        {
            RequireSampledSurface = requireSampledSurface;
            ObstacleIsHardBlock = obstacleIsHardBlock;
        }
    }

    public static class GridTraversalComposer
    {
        public static bool IsWalkable(
            in GridBakeCell baked,
            in GridTraversalOverrideCell authored,
            in GridTraversalCompositionSettings settings)
        {
            if (settings.RequireSampledSurface &&
                !baked.HasSurface)
                return false;
            if (settings.ObstacleIsHardBlock &&
                baked.HasObstacle)
                return false;

            switch (authored.Walkability)
            {
                case GridWalkabilityOverride.ForceBlocked:
                    return false;
                case GridWalkabilityOverride.ForceWalkable:
                    return true;
                case GridWalkabilityOverride.None:
                    return baked.BaseWalkable;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(authored),
                        "Unknown walkability override.");
            }
        }

        public static long GetMovementCost(
            in GridBakeCell baked,
            in GridTraversalOverrideCell authored) =>
            authored.HasMovementCostOverride
                ? authored.MovementCost
                : baked.BaseMovementCost;
    }

    public enum GridProjectionBakeError
    {
        None = 0,
        InvalidSettings = 1,
        SourceUnavailable = 2,
        SampleFailed = 3,
        Cancelled = 4
    }

    public readonly struct GridProjectionBakeResult
    {
        public bool Success =>
            Error == GridProjectionBakeError.None;
        public GridProjectionBakeError Error { get; }
        public int TotalCells { get; }
        public int WalkableCells { get; }
        public int BlockedCells { get; }
        public int MissingSurfaceCells { get; }

        internal GridProjectionBakeResult(
            GridProjectionBakeError error,
            int totalCells,
            int walkableCells,
            int blockedCells,
            int missingSurfaceCells)
        {
            Error = error;
            TotalCells = totalCells;
            WalkableCells = walkableCells;
            BlockedCells = blockedCells;
            MissingSurfaceCells = missingSurfaceCells;
        }
    }
}
