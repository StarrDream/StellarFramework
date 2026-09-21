using System;
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
    public sealed class WorldGenKitUnityPresentationAdapterTests
    {
        [Test]
        public void SameDenseHeightChannelCanDriveDebugTextureAndMesh()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 2, 2L);
            float[] values = { 0f, 1f, 2f, 3f };
            WorldGenerationDataSet data = CreateFloatData(values, out ChannelHandle<float> height);

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            try
            {
                Color32[] pixels = new Color32[layout.Count];
                WorldScalarDebugTextureSettings textureSettings = new WorldScalarDebugTextureSettings(
                    0f,
                    3f,
                    new Color32(0, 0, 0, 255),
                    new Color32(255, 255, 255, 255));
                WorldDebugTextureAdapter.UpdateScalarTexture(
                    texture, data, height, in layout, pixels, in textureSettings);

                Color32[] rendered = texture.GetPixels32();
                Assert.That(rendered[0].r, Is.EqualTo(0));
                Assert.That(rendered[3].r, Is.EqualTo(255));

                Vector3[] vertices = new Vector3[WorldHeightMeshAdapter.GetVertexCount(in layout)];
                Vector2[] uv = new Vector2[vertices.Length];
                int[] triangles = new int[WorldHeightMeshAdapter.GetTriangleIndexCount(in layout)];
                WorldHeightMeshSettings meshSettings = new WorldHeightMeshSettings(1f, 1f, false);
                WorldHeightMeshAdapter.Build(
                    mesh, data, height, in layout, vertices, uv, triangles, in meshSettings);

                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(vertices[0], Is.EqualTo(new Vector3(0f, 0f, 0f)));
                Assert.That(vertices[1], Is.EqualTo(new Vector3(2f, 1f, 0f)));
                Assert.That(vertices[2], Is.EqualTo(new Vector3(0f, 2f, 2f)));
                Assert.That(vertices[3], Is.EqualTo(new Vector3(2f, 3f, 2f)));
                Assert.That(triangles, Is.EqualTo(new[] { 0, 2, 1, 1, 2, 3 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void IndexedDebugTextureUsesCallerPaletteAndOptionalYFlip()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 2);
            int[] values = { 0, 1, 2, 1 };
            WorldGenerationDataSet data = CreateIntData(values, out ChannelHandle<int> surface);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Color32[] palette =
                {
                    new Color32(10, 0, 0, 255),
                    new Color32(20, 0, 0, 255),
                    new Color32(30, 0, 0, 255)
                };
                Color32[] pixels = new Color32[layout.Count];
                WorldDebugTextureAdapter.UpdateIndexedTexture(
                    texture, data, surface, in layout, palette, pixels, flipY: true);

                Color32[] rendered = texture.GetPixels32();
                Assert.That(rendered[0].r, Is.EqualTo(30));
                Assert.That(rendered[1].r, Is.EqualTo(20));
                Assert.That(rendered[2].r, Is.EqualTo(10));
                Assert.That(rendered[3].r, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void InvalidIndexedPaletteInputDoesNotMutateTexture()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 1);
            WorldGenerationDataSet data = CreateIntData(new[] { 0, 2 }, out ChannelHandle<int> surface);
            Texture2D texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            try
            {
                Color32 baseline = new Color32(7, 8, 9, 255);
                texture.SetPixels32(new[] { baseline, baseline });
                texture.Apply(false, false);
                Color32[] scratch = new Color32[2];

                Assert.Throws<InvalidOperationException>(() =>
                    WorldDebugTextureAdapter.UpdateIndexedTexture(
                        texture,
                        data,
                        surface,
                        in layout,
                        new[] { new Color32(1, 2, 3, 255) },
                        scratch));

                Color32[] rendered = texture.GetPixels32();
                Assert.That(rendered[0], Is.EqualTo(baseline));
                Assert.That(rendered[1], Is.EqualTo(baseline));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void InvalidHeightFailsBeforeExistingMeshMutation()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 2);
            WorldGenerationDataSet data = CreateFloatData(
                new[] { 0f, 1f, float.NaN, 3f },
                out ChannelHandle<float> height);
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            try
            {
                mesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.forward
                };
                mesh.triangles = new[] { 0, 2, 1 };

                Vector3[] vertices = new Vector3[4];
                Vector2[] uv = new Vector2[4];
                int[] triangles = new int[6];
                WorldHeightMeshSettings settings = new WorldHeightMeshSettings(1f, 1f);

                Assert.Throws<InvalidOperationException>(() =>
                    WorldHeightMeshAdapter.Build(
                        mesh, data, height, in layout, vertices, uv, triangles, in settings));

                Assert.That(mesh.vertexCount, Is.EqualTo(3));
                Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 2, 1 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MeshScratchBuffersArePreflightedBeforeMutation()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 2);
            WorldGenerationDataSet data = CreateFloatData(
                new[] { 0f, 1f, 2f, 3f },
                out ChannelHandle<float> height);
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero };
                WorldHeightMeshSettings settings = new WorldHeightMeshSettings(1f, 1f);
                Assert.Throws<ArgumentException>(() =>
                    WorldHeightMeshAdapter.Build(
                        mesh,
                        data,
                        height,
                        in layout,
                        new Vector3[4],
                        new Vector2[4],
                        new int[5],
                        in settings));
                Assert.That(mesh.vertexCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void SurfaceChannelCanDriveTilemapWithExplicitPaletteAndOrigin()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 2);
            WorldGenerationDataSet data = CreateIntData(
                new[] { 0, 1, 1, 0 },
                out ChannelHandle<int> surface);
            GameObject gridObject = new GameObject("Presentation_TestGrid", typeof(Grid));
            GameObject tilemapObject = new GameObject("Presentation_TestTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(gridObject.transform, false);
            Tile tileA = ScriptableObject.CreateInstance<Tile>();
            Tile tileB = ScriptableObject.CreateInstance<Tile>();
            try
            {
                Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
                TileBase[] palette = { tileA, tileB };
                TileBase[] scratch = new TileBase[layout.Count];
                Vector3Int origin = new Vector3Int(10, -4, 0);

                WorldTilemapAdapter.Update(
                    tilemap,
                    data,
                    surface,
                    in layout,
                    palette,
                    scratch,
                    origin);

                Assert.That(tilemap.GetTile(new Vector3Int(10, -4, 0)), Is.SameAs(tileA));
                Assert.That(tilemap.GetTile(new Vector3Int(11, -4, 0)), Is.SameAs(tileB));
                Assert.That(tilemap.GetTile(new Vector3Int(10, -3, 0)), Is.SameAs(tileB));
                Assert.That(tilemap.GetTile(new Vector3Int(11, -3, 0)), Is.SameAs(tileA));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tileA);
                UnityEngine.Object.DestroyImmediate(tileB);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void InvalidTilemapPaletteIndexFailsBeforeTilemapMutation()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 1);
            WorldGenerationDataSet data = CreateIntData(
                new[] { 0, 2 },
                out ChannelHandle<int> surface);
            GameObject gridObject = new GameObject("Presentation_TestGrid_Invalid", typeof(Grid));
            GameObject tilemapObject = new GameObject("Presentation_TestTilemap_Invalid", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform, false);
            Tile baseline = ScriptableObject.CreateInstance<Tile>();
            Tile replacement = ScriptableObject.CreateInstance<Tile>();
            try
            {
                Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
                Vector3Int origin = new Vector3Int(3, 5, 0);
                tilemap.SetTile(origin, baseline);

                Assert.Throws<InvalidOperationException>(() =>
                    WorldTilemapAdapter.Update(
                        tilemap,
                        data,
                        surface,
                        in layout,
                        new TileBase[] { replacement },
                        new TileBase[layout.Count],
                        origin));

                Assert.That(tilemap.GetTile(origin), Is.SameAs(baseline));
                Assert.That(tilemap.GetTile(origin + Vector3Int.right), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseline);
                UnityEngine.Object.DestroyImmediate(replacement);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void HeightChannelCanDriveUnityTerrainData()
        {
            const int resolution = 33;
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(resolution, resolution);
            float[] values = new float[layout.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = i * 100f / (values.Length - 1);
            WorldGenerationDataSet data = CreateFloatData(values, out ChannelHandle<float> height);
            TerrainData terrainData = new TerrainData();
            try
            {
                terrainData.heightmapResolution = resolution;
                float[,] buffer = new float[resolution, resolution];
                WorldTerrainHeightProjectionSettings settings =
                    new WorldTerrainHeightProjectionSettings(0f, 100f);

                WorldUnityTerrainAdapter.UpdateHeights(
                    terrainData,
                    data,
                    height,
                    in layout,
                    buffer,
                    in settings);

                float[,] rendered = terrainData.GetHeights(0, 0, resolution, resolution);
                Assert.That(rendered[0, 0], Is.EqualTo(0f).Within(0.000001f));
                Assert.That(rendered[resolution - 1, resolution - 1], Is.EqualTo(1f).Within(0.000001f));
                Assert.That(rendered[16, 16], Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void TerrainOutOfRangeHeightFailsBeforeTerrainMutation()
        {
            const int resolution = 33;
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(resolution, resolution);
            float[] values = new float[layout.Count];
            values[layout.Count / 2] = 2f;
            WorldGenerationDataSet data = CreateFloatData(values, out ChannelHandle<float> height);
            TerrainData terrainData = new TerrainData();
            try
            {
                terrainData.heightmapResolution = resolution;
                float[,] baseline = new float[resolution, resolution];
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                        baseline[y, x] = 0.25f;
                }
                terrainData.SetHeights(0, 0, baseline);
                float baselineStored = terrainData.GetHeights(0, 0, 1, 1)[0, 0];

                float[,] scratch = new float[resolution, resolution];
                WorldTerrainHeightProjectionSettings settings =
                    new WorldTerrainHeightProjectionSettings(0f, 1f);
                Assert.Throws<InvalidOperationException>(() =>
                    WorldUnityTerrainAdapter.UpdateHeights(
                        terrainData,
                        data,
                        height,
                        in layout,
                        scratch,
                        in settings));

                float[,] rendered = terrainData.GetHeights(0, 0, 1, 1);
                Assert.That(rendered[0, 0], Is.EqualTo(baselineStored));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(terrainData);
            }
        }

        private static WorldGenerationDataSet CreateFloatData(
            float[] values,
            out ChannelHandle<float> handle)
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            handle = builder.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                DenseSample(),
                WorldChannelSourceMode.ProvidedInput);
            WorldChannelRegistry registry = builder.Build();
            DenseChannelStorage<float> storage = new DenseChannelStorage<float>(values.Length);
            values.AsSpan().CopyTo(storage.AsSpan());
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(handle, storage);
            return data;
        }

        private static WorldGenerationDataSet CreateIntData(
            int[] values,
            out ChannelHandle<int> handle)
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            handle = builder.Register<int>(
                WorldDataChannelId.From("terrain.surface_index"),
                DenseSample(),
                WorldChannelSourceMode.ProvidedInput);
            WorldChannelRegistry registry = builder.Build();
            DenseChannelStorage<int> storage = new DenseChannelStorage<int>(values.Length);
            values.AsSpan().CopyTo(storage.AsSpan());
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(handle, storage);
            return data;
        }

        private static WorldChannelStorageDescriptor DenseSample() =>
            new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample);
    }
}
