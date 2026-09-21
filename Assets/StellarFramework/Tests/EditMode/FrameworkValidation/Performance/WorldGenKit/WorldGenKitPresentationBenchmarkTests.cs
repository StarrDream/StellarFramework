using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldGenKit.Unity.DebugTextureAdapter;
using StellarFramework.WorldGenKit.Unity.MeshAdapter;
using StellarFramework.WorldGenKit.Unity.TerrainAdapter;
using StellarFramework.WorldGenKit.Unity.TilemapAdapter;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitPresentationBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void UnityPresentationAdaptersBenchmark()
        {
            const int measuredIterations = 5;
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(128, 128);
            WorldGenerationDataSet data = CreatePresentationData(
                in layout,
                out ChannelHandle<float> height,
                out ChannelHandle<int> surface);

            Texture2D texture = new Texture2D(layout.Width, layout.Height, TextureFormat.RGBA32, false, true);
            Color32[] pixels = new Color32[layout.Count];
            WorldScalarDebugTextureSettings textureSettings = new WorldScalarDebugTextureSettings(
                0f, 100f, new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255));

            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            Vector3[] vertices = new Vector3[WorldHeightMeshAdapter.GetVertexCount(in layout)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[WorldHeightMeshAdapter.GetTriangleIndexCount(in layout)];
            WorldHeightMeshSettings meshSettings = new WorldHeightMeshSettings(1f, 1f, false);

            GameObject gridObject = new GameObject("Presentation_Benchmark_Grid", typeof(Grid));
            GameObject tilemapObject = new GameObject("Presentation_Benchmark_Tilemap", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform, false);
            Tile tileA = ScriptableObject.CreateInstance<Tile>();
            Tile tileB = ScriptableObject.CreateInstance<Tile>();
            TileBase[] tilePalette = { tileA, tileB };
            TileBase[] tileBuffer = new TileBase[layout.Count];
            Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();

            WorldPlanarSampleLayout terrainLayout = new WorldPlanarSampleLayout(129, 129);
            WorldGenerationDataSet terrainDataSet = CreateHeightData(in terrainLayout, out ChannelHandle<float> terrainHeight);
            TerrainData terrainData = new TerrainData { heightmapResolution = terrainLayout.Width };
            float[,] terrainBuffer = new float[terrainLayout.Height, terrainLayout.Width];
            WorldTerrainHeightProjectionSettings terrainSettings = new WorldTerrainHeightProjectionSettings(0f, 100f);

            try
            {
                WorldDebugTextureAdapter.UpdateScalarTexture(
                    texture, data, height, in layout, pixels, in textureSettings);
                WorldHeightMeshAdapter.Build(
                    mesh, data, height, in layout, vertices, uv, triangles, in meshSettings);
                WorldTilemapAdapter.Update(
                    tilemap, data, surface, in layout, tilePalette, tileBuffer, Vector3Int.zero);
                WorldUnityTerrainAdapter.UpdateHeights(
                    terrainData, terrainDataSet, terrainHeight, in terrainLayout, terrainBuffer, in terrainSettings);

                double[] textureMs = new double[measuredIterations];
                double[] meshMs = new double[measuredIterations];
                double[] tilemapMs = new double[measuredIterations];
                double[] terrainMs = new double[measuredIterations];
                long heapBefore = GC.GetTotalMemory(false);

                for (int iteration = 0; iteration < measuredIterations; iteration++)
                {
                    long start = Stopwatch.GetTimestamp();
                    WorldDebugTextureAdapter.UpdateScalarTexture(
                        texture, data, height, in layout, pixels, in textureSettings);
                    textureMs[iteration] = ElapsedMs(start);

                    start = Stopwatch.GetTimestamp();
                    WorldHeightMeshAdapter.Build(
                        mesh, data, height, in layout, vertices, uv, triangles, in meshSettings);
                    meshMs[iteration] = ElapsedMs(start);

                    start = Stopwatch.GetTimestamp();
                    WorldTilemapAdapter.Update(
                        tilemap, data, surface, in layout, tilePalette, tileBuffer, Vector3Int.zero);
                    tilemapMs[iteration] = ElapsedMs(start);

                    start = Stopwatch.GetTimestamp();
                    WorldUnityTerrainAdapter.UpdateHeights(
                        terrainData, terrainDataSet, terrainHeight, in terrainLayout, terrainBuffer, in terrainSettings);
                    terrainMs[iteration] = ElapsedMs(start);
                }

                long heapDelta = GC.GetTotalMemory(false) - heapBefore;
                Array.Sort(textureMs);
                Array.Sort(meshMs);
                Array.Sort(tilemapMs);
                Array.Sort(terrainMs);
                long checksum = pixels[pixels.Length - 1].r +
                    (long)vertices[vertices.Length - 1].y +
                    triangles.Length +
                    (tilemap.GetTile(Vector3Int.zero) == tileA ? 1L : 2L) +
                    (long)(terrainBuffer[terrainLayout.Height - 1, terrainLayout.Width - 1] * 1000f);

                string message = string.Format(
                    "Unity presentation benchmark env={0} samples2D3D={1} textureMinMedianMs={2:F3}/{3:F3} meshMinMedianMs={4:F3}/{5:F3} tilemapMinMedianMs={6:F3}/{7:F3} terrainSamples={8} terrainMinMedianMs={9:F3}/{10:F3} checksum={11} allocationDelta={12}",
                    Application.unityVersion,
                    layout.Count,
                    textureMs[0], textureMs[measuredIterations / 2],
                    meshMs[0], meshMs[measuredIterations / 2],
                    tilemapMs[0], tilemapMs[measuredIterations / 2],
                    terrainLayout.Count,
                    terrainMs[0], terrainMs[measuredIterations / 2],
                    checksum,
                    heapDelta);
                TestContext.Progress.WriteLine(message);
                UnityEngine.Debug.Log(message);

                Assert.That(checksum, Is.Not.EqualTo(0L));
                Assert.That(mesh.vertexCount, Is.EqualTo(layout.Count));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(tileA);
                UnityEngine.Object.DestroyImmediate(tileB);
                UnityEngine.Object.DestroyImmediate(gridObject);
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        private static double ElapsedMs(long start) =>
            (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;

        private static WorldGenerationDataSet CreatePresentationData(
            in WorldPlanarSampleLayout layout,
            out ChannelHandle<float> height,
            out ChannelHandle<int> surface)
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            WorldChannelStorageDescriptor dense = DenseSample();
            height = builder.Register<float>(WorldDataChannelId.From("terrain.height"), dense, WorldChannelSourceMode.ProvidedInput);
            surface = builder.Register<int>(WorldDataChannelId.From("terrain.surface_index"), dense, WorldChannelSourceMode.ProvidedInput);
            WorldChannelRegistry registry = builder.Build();

            DenseChannelStorage<float> heightStorage = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<int> surfaceStorage = new DenseChannelStorage<int>(layout.Count);
            for (int y = 0; y < layout.Height; y++)
            {
                int row = y * layout.Width;
                for (int x = 0; x < layout.Width; x++)
                {
                    int index = row + x;
                    heightStorage[index] = x * 100f / (layout.Width - 1);
                    surfaceStorage[index] = (x + y) & 1;
                }
            }

            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(height, heightStorage);
            data.Bind(surface, surfaceStorage);
            return data;
        }

        private static WorldGenerationDataSet CreateHeightData(
            in WorldPlanarSampleLayout layout,
            out ChannelHandle<float> height)
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            height = builder.Register<float>(
                WorldDataChannelId.From("terrain.height"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            WorldChannelRegistry registry = builder.Build();
            DenseChannelStorage<float> storage = new DenseChannelStorage<float>(layout.Count);
            for (int i = 0; i < layout.Count; i++)
                storage[i] = i * 100f / (layout.Count - 1);
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(height, storage);
            return data;
        }

        private static WorldChannelStorageDescriptor DenseSample() =>
            new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample);
    }
}
