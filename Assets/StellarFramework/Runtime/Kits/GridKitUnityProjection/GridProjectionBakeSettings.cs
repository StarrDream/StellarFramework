using System;
using UnityEngine;

namespace StellarFramework.GridKit.UnityProjection
{
    public sealed class GridProjectionBakeSettings
    {
        public GridRect Bounds { get; }
        public Vector3 WorldOrigin { get; }
        public Vector2 CellSize { get; }
        public Vector3 SampleDirection { get; }
        public float MaxDistance { get; }
        public float MaxWalkableSlope { get; }
        public float SteepSlopeThreshold { get; }
        public long DefaultMovementCost { get; }
        public long SteepMovementCost { get; }

        public GridProjectionBakeSettings(
            GridRect bounds,
            Vector3 worldOrigin,
            Vector2 cellSize,
            float maxWalkableSlope,
            long defaultMovementCost,
            long steepMovementCost,
            float steepSlopeThreshold = 20f,
            Vector3? sampleDirection = null,
            float maxDistance = 10000f)
        {
            if (bounds.IsEmpty)
                throw new ArgumentException(
                    "Projection bounds cannot be empty.",
                    nameof(bounds));
            ValidateFinite(worldOrigin, nameof(worldOrigin));
            if (!IsFinite(cellSize.x) ||
                !IsFinite(cellSize.y) ||
                cellSize.x <= 0f ||
                cellSize.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (!IsFinite(maxWalkableSlope) ||
                maxWalkableSlope < 0f ||
                maxWalkableSlope > 90f)
                throw new ArgumentOutOfRangeException(nameof(maxWalkableSlope));
            if (!IsFinite(steepSlopeThreshold) ||
                steepSlopeThreshold < 0f ||
                steepSlopeThreshold > maxWalkableSlope)
                throw new ArgumentOutOfRangeException(nameof(steepSlopeThreshold));
            if (defaultMovementCost <= 0L)
                throw new ArgumentOutOfRangeException(nameof(defaultMovementCost));
            if (steepMovementCost <= 0L)
                throw new ArgumentOutOfRangeException(nameof(steepMovementCost));
            if (!IsFinite(maxDistance) || maxDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxDistance));

            Vector3 direction = sampleDirection ?? Vector3.down;
            ValidateFinite(direction, nameof(sampleDirection));
            if (direction.sqrMagnitude <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(sampleDirection),
                    "Sample direction must be non-zero.");

            Bounds = bounds;
            WorldOrigin = worldOrigin;
            CellSize = cellSize;
            SampleDirection = direction.normalized;
            MaxDistance = maxDistance;
            MaxWalkableSlope = maxWalkableSlope;
            SteepSlopeThreshold = steepSlopeThreshold;
            DefaultMovementCost = defaultMovementCost;
            SteepMovementCost = steepMovementCost;
        }

        public Vector3 GetSampleOrigin(GridCoord coord)
        {
            if (!Bounds.Contains(coord))
                throw new ArgumentOutOfRangeException(nameof(coord));

            long localX = (long)coord.X - Bounds.Min.X;
            long localY = (long)coord.Y - Bounds.Min.Y;
            double x =
                WorldOrigin.x +
                ((localX + 0.5d) * CellSize.x);
            double z =
                WorldOrigin.z +
                ((localY + 0.5d) * CellSize.y);
            if (x < -float.MaxValue || x > float.MaxValue ||
                z < -float.MaxValue || z > float.MaxValue)
                throw new OverflowException(
                    "Projection sample origin exceeds Unity float range.");

            return new Vector3(
                (float)x,
                WorldOrigin.y,
                (float)z);
        }

        private static void ValidateFinite(
            Vector3 value,
            string parameterName)
        {
            if (!IsFinite(value.x) ||
                !IsFinite(value.y) ||
                !IsFinite(value.z))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
