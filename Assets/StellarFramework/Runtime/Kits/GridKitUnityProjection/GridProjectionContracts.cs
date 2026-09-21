using System;
using UnityEngine;

namespace StellarFramework.GridKit.UnityProjection
{
    public readonly struct GridProjectionQuery
    {
        public GridCoord Coord { get; }
        public Vector3 SampleOrigin { get; }
        public Vector3 SampleDirection { get; }
        public float MaxDistance { get; }

        public GridProjectionQuery(
            GridCoord coord,
            Vector3 sampleOrigin,
            Vector3 sampleDirection,
            float maxDistance)
        {
            ValidateFinite(sampleOrigin, nameof(sampleOrigin));
            ValidateFinite(sampleDirection, nameof(sampleDirection));
            if (sampleDirection.sqrMagnitude <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(sampleDirection),
                    "Sample direction must be non-zero.");
            if (!IsFinite(maxDistance) || maxDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxDistance));

            Coord = coord;
            SampleOrigin = sampleOrigin;
            SampleDirection = sampleDirection.normalized;
            MaxDistance = maxDistance;
        }

        private static void ValidateFinite(
            Vector3 value,
            string parameterName)
        {
            if (!IsFinite(value.x) ||
                !IsFinite(value.y) ||
                !IsFinite(value.z))
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Projection vector values must be finite.");
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct GridProjectionSample
    {
        public bool Hit { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float Height { get; }
        public float SlopeDegrees { get; }
        public int SurfaceCategory { get; }
        public bool ObstacleHit { get; }

        private GridProjectionSample(
            bool hit,
            Vector3 point,
            Vector3 normal,
            float height,
            float slopeDegrees,
            int surfaceCategory,
            bool obstacleHit)
        {
            Hit = hit;
            Point = point;
            Normal = normal;
            Height = height;
            SlopeDegrees = slopeDegrees;
            SurfaceCategory = surfaceCategory;
            ObstacleHit = obstacleHit;
        }

        public static GridProjectionSample Missing() =>
            new GridProjectionSample(
                false,
                default(Vector3),
                Vector3.up,
                0f,
                0f,
                0,
                false);

        public static GridProjectionSample Surface(
            Vector3 point,
            Vector3 normal,
            float height,
            float slopeDegrees,
            int surfaceCategory,
            bool obstacleHit)
        {
            ValidateFinite(point, nameof(point));
            ValidateFinite(normal, nameof(normal));
            if (normal.sqrMagnitude <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(normal),
                    "Surface normal must be non-zero.");
            if (!IsFinite(height))
                throw new ArgumentOutOfRangeException(nameof(height));
            if (!IsFinite(slopeDegrees) ||
                slopeDegrees < 0f ||
                slopeDegrees > 180f)
                throw new ArgumentOutOfRangeException(nameof(slopeDegrees));

            return new GridProjectionSample(
                true,
                point,
                normal.normalized,
                height,
                slopeDegrees,
                surfaceCategory,
                obstacleHit);
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

    public interface IGridProjectionSource
    {
        bool TrySample(
            in GridProjectionQuery query,
            out GridProjectionSample sample);
    }
}
