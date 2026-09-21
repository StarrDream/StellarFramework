using System;
using StellarFramework.WorldGenKit.Builtins;
using UnityEngine;

namespace StellarFramework.WorldGenKit.Unity.TerrainAdapter
{
    public readonly struct WorldTerrainHeightProjectionSettings
    {
        private readonly bool _initialized;

        public float SourceMinHeight { get; }
        public float SourceMaxHeight { get; }
        public bool FlipY { get; }
        public bool ClampOutOfRange { get; }

        public bool IsValid => _initialized &&
            IsFinite(SourceMinHeight) &&
            IsFinite(SourceMaxHeight) &&
            SourceMaxHeight > SourceMinHeight;

        public WorldTerrainHeightProjectionSettings(
            float sourceMinHeight,
            float sourceMaxHeight,
            bool flipY = false,
            bool clampOutOfRange = false)
        {
            if (!IsFinite(sourceMinHeight)) throw new ArgumentOutOfRangeException(nameof(sourceMinHeight));
            if (!IsFinite(sourceMaxHeight)) throw new ArgumentOutOfRangeException(nameof(sourceMaxHeight));
            if (sourceMaxHeight <= sourceMinHeight) throw new ArgumentOutOfRangeException(nameof(sourceMaxHeight));

            SourceMinHeight = sourceMinHeight;
            SourceMaxHeight = sourceMaxHeight;
            FlipY = flipY;
            ClampOutOfRange = clampOutOfRange;
            _initialized = true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Unity Terrain height projection. The adapter does not resize TerrainData or alter Terrain world size;
    /// those are explicit presentation-authoring responsibilities owned by the caller.
    /// </summary>
    public static class WorldUnityTerrainAdapter
    {
        public static void UpdateHeights(
            TerrainData terrainData,
            WorldGenerationDataSet data,
            ChannelHandle<float> heightChannel,
            in WorldPlanarSampleLayout layout,
            float[,] heightBuffer,
            in WorldTerrainHeightProjectionSettings settings)
        {
            if (terrainData == null) throw new ArgumentNullException(nameof(terrainData));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!settings.IsValid) throw new ArgumentException("Terrain height projection settings are invalid.", nameof(settings));
            if (terrainData.heightmapResolution != layout.Width || terrainData.heightmapResolution != layout.Height)
            {
                throw new ArgumentException(
                    "TerrainData heightmap resolution must exactly match the square planar sample layout. " +
                    "The adapter does not resize TerrainData implicitly.",
                    nameof(terrainData));
            }
            if (heightBuffer == null) throw new ArgumentNullException(nameof(heightBuffer));
            if (heightBuffer.GetLength(0) != layout.Height || heightBuffer.GetLength(1) != layout.Width)
                throw new ArgumentException("Terrain height buffer dimensions must exactly match the planar sample layout.", nameof(heightBuffer));
            if (!data.TryGetStorage<float, DenseChannelStorage<float>>(heightChannel, out DenseChannelStorage<float> storage))
                throw new InvalidOperationException("Terrain projection requires a bound dense float height channel.");
            if (storage.Length < layout.Count)
                throw new InvalidOperationException("Dense height channel is smaller than the planar sample layout.");

            ReadOnlySpan<float> values = storage.AsReadOnlySpan();
            for (int i = 0; i < layout.Count; i++)
            {
                float value = values[i];
                if (!IsFinite(value))
                    throw new InvalidOperationException("Terrain height source contains a non-finite value at sample " + i + ".");
                if (!settings.ClampOutOfRange &&
                    (value < settings.SourceMinHeight || value > settings.SourceMaxHeight))
                {
                    throw new InvalidOperationException(
                        "Terrain height source is outside the declared projection range at sample " + i + ".");
                }
            }

            float range = settings.SourceMaxHeight - settings.SourceMinHeight;
            for (int y = 0; y < layout.Height; y++)
            {
                int row = y * layout.Width;
                int destinationY = settings.FlipY ? layout.Height - 1 - y : y;
                for (int x = 0; x < layout.Width; x++)
                {
                    float normalized = (values[row + x] - settings.SourceMinHeight) / range;
                    heightBuffer[destinationY, x] = settings.ClampOutOfRange
                        ? Mathf.Clamp01(normalized)
                        : normalized;
                }
            }

            terrainData.SetHeights(0, 0, heightBuffer);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
