using System;
using StellarFramework.WorldGenKit.Builtins;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellarFramework.WorldGenKit.Unity.TilemapAdapter
{
    /// <summary>
    /// Projects a dense semantic/index channel into a Unity Tilemap.
    /// One logical sample maps to one Tilemap cell; physical scale remains a Grid/Transform presentation concern.
    /// </summary>
    public static class WorldTilemapAdapter
    {
        public static void Update(
            Tilemap tilemap,
            WorldGenerationDataSet data,
            ChannelHandle<int> channel,
            in WorldPlanarSampleLayout layout,
            TileBase[] tilePalette,
            TileBase[] tileBuffer,
            Vector3Int cellOrigin,
            bool flipY = false)
        {
            if (tilemap == null) throw new ArgumentNullException(nameof(tilemap));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (tilePalette == null) throw new ArgumentNullException(nameof(tilePalette));
            if (tilePalette.Length == 0) throw new ArgumentException("Tile palette cannot be empty.", nameof(tilePalette));
            if (tileBuffer == null) throw new ArgumentNullException(nameof(tileBuffer));
            if (tileBuffer.Length != layout.Count)
                throw new ArgumentException("Tile buffer length must exactly match the planar sample count.", nameof(tileBuffer));
            if (!data.TryGetStorage<int, DenseChannelStorage<int>>(channel, out DenseChannelStorage<int> storage))
                throw new InvalidOperationException("Tilemap projection requires a bound dense int channel.");
            if (storage.Length < layout.Count)
                throw new InvalidOperationException("Dense int channel is smaller than the planar sample layout.");

            ReadOnlySpan<int> values = storage.AsReadOnlySpan();
            for (int i = 0; i < layout.Count; i++)
            {
                if ((uint)values[i] >= (uint)tilePalette.Length)
                    throw new InvalidOperationException("Tilemap source contains an out-of-range palette index at sample " + i + ".");
            }

            for (int y = 0; y < layout.Height; y++)
            {
                int sourceRow = y * layout.Width;
                int destinationY = flipY ? layout.Height - 1 - y : y;
                int destinationRow = destinationY * layout.Width;
                for (int x = 0; x < layout.Width; x++)
                    tileBuffer[destinationRow + x] = tilePalette[values[sourceRow + x]];
            }

            BoundsInt bounds = new BoundsInt(
                cellOrigin.x,
                cellOrigin.y,
                cellOrigin.z,
                layout.Width,
                layout.Height,
                1);
            tilemap.SetTilesBlock(bounds, tileBuffer);
        }
    }
}
