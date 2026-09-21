using System;
using UnityEngine;

namespace StellarFramework.GridKit.UnityProjection
{
    public sealed class TerrainGridProjectionSource :
        IGridProjectionSource
    {
        private readonly Terrain _terrain;
        private readonly TerrainData _data;
        private readonly int _surfaceCategory;

        public Terrain Terrain => _terrain;

        public TerrainGridProjectionSource(
            Terrain terrain,
            int surfaceCategory = 0)
        {
            _terrain = terrain != null
                ? terrain
                : throw new ArgumentNullException(
                    nameof(terrain));
            _data = terrain.terrainData != null
                ? terrain.terrainData
                : throw new ArgumentException(
                    "Terrain must have TerrainData.",
                    nameof(terrain));
            _surfaceCategory = surfaceCategory;
        }

        public bool TrySample(
            in GridProjectionQuery query,
            out GridProjectionSample sample)
        {
            Vector3 terrainPosition =
                _terrain.transform.position;
            Vector3 size = _data.size;
            if (size.x <= 0f || size.z <= 0f)
            {
                sample = default(GridProjectionSample);
                return false;
            }

            float normalizedX =
                (query.SampleOrigin.x -
                 terrainPosition.x) / size.x;
            float normalizedZ =
                (query.SampleOrigin.z -
                 terrainPosition.z) / size.z;
            if (normalizedX < 0f ||
                normalizedX > 1f ||
                normalizedZ < 0f ||
                normalizedZ > 1f)
            {
                sample = GridProjectionSample.Missing();
                return true;
            }

            float height =
                terrainPosition.y +
                _data.GetInterpolatedHeight(
                    normalizedX,
                    normalizedZ);
            Vector3 normal =
                _terrain.transform.TransformDirection(
                    _data.GetInterpolatedNormal(
                        normalizedX,
                        normalizedZ)).normalized;
            float slope =
                Vector3.Angle(
                    normal,
                    Vector3.up);
            Vector3 point =
                new Vector3(
                    query.SampleOrigin.x,
                    height,
                    query.SampleOrigin.z);

            Vector3 toPoint =
                point - query.SampleOrigin;
            float alongRay =
                Vector3.Dot(
                    toPoint,
                    query.SampleDirection);
            if (alongRay < 0f ||
                alongRay > query.MaxDistance)
            {
                sample = GridProjectionSample.Missing();
                return true;
            }

            sample =
                GridProjectionSample.Surface(
                    point,
                    normal,
                    height,
                    slope,
                    _surfaceCategory,
                    false);
            return true;
        }
    }
}
