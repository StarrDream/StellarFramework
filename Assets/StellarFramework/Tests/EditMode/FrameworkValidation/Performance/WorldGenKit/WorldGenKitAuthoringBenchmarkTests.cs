using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Authoring;
using StellarFramework.WorldGenKit.Builtins;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitAuthoringBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void WorldGenKitAuthoringBenchmark_512x512SparseEditComposeAndRegionalRecompute()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(512, 512);
            WorldSampleRect editBounds = new WorldSampleRect(224, 224, 64, 64);
            WorldChannelStorageDescriptor dense = new WorldChannelStorageDescriptor(
                WorldChannelStorageKind.Dense,
                WorldChannelScope.Sample);

            WorldChannelRegistryBuilder registryBuilder = new WorldChannelRegistryBuilder();
            ChannelHandle<float> heightHandle = registryBuilder.Register<float>(WorldDataChannelId.From("terrain.height"), dense);
            ChannelHandle<float> waterHandle = registryBuilder.Register<float>(WorldDataChannelId.From("terrain.water_depth"), dense);
            ChannelHandle<float> slopeHandle = registryBuilder.Register<float>(WorldDataChannelId.From("terrain.slope"), dense);
            ChannelHandle<int> biomeHandle = registryBuilder.Register<int>(WorldDataChannelId.From("terrain.biome"), dense);
            ChannelHandle<int> surfaceHandle = registryBuilder.Register<int>(WorldDataChannelId.From("terrain.surface"), dense);
            ChannelHandle<byte> buildableHandle = registryBuilder.Register<byte>(WorldDataChannelId.From("terrain.buildable"), dense);
            WorldChannelRegistry registry = registryBuilder.Build();

            WorldBiomeDefinition waterBiome = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.water"),
                WorldSurfaceId.From("surface.water"),
                new WorldBiomeCriteria(waterDepth: new WorldRangeRule(0.00001d, 1000d)),
                100);
            WorldBiomeDefinition landBiome = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.land"),
                WorldSurfaceId.From("surface.grass"),
                new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(new[] { waterBiome, landBiome }.AsSpan(), landBiome.Id);
            WorldSurfaceCatalog surfaces = new WorldSurfaceCatalog(
                new[] { WorldSurfaceId.From("surface.grass"), WorldSurfaceId.From("surface.water") }.AsSpan());

            float[] baseHeight = new float[layout.Count];
            Array.Fill(baseHeight, 10f);
            DenseChannelStorage<float> finalHeight = new DenseChannelStorage<float>(layout.Count);
            WorldDenseChannelImport.CopyExact(baseHeight.AsSpan(), finalHeight);
            DenseChannelStorage<float> water = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> slope = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<int> biome = new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<int> surface = new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<byte> buildable = new DenseChannelStorage<byte>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(heightHandle, finalHeight);
            data.Bind(waterHandle, water);
            data.Bind(slopeHandle, slope);
            data.Bind(biomeHandle, biome);
            data.Bind(surfaceHandle, surface);
            data.Bind(buildableHandle, buildable);

            WorldWaterDepthStage waterStage = new WorldWaterDepthStage(heightHandle, waterHandle, layout, 0f);
            WorldSlopeStage slopeStage = new WorldSlopeStage(heightHandle, slopeHandle, layout);
            WorldBiomeStage biomeStage = new WorldBiomeStage(
                heightHandle,
                biomeHandle,
                layout,
                biomes,
                waterDepth: waterHandle,
                slope: slopeHandle);
            WorldSurfaceStage surfaceStage = new WorldSurfaceStage(biomeHandle, surfaceHandle, layout, biomes, surfaces);
            WorldBuildableStage buildableStage = new WorldBuildableStage(
                slopeHandle,
                waterHandle,
                buildableHandle,
                layout,
                new WorldBuildableSettings(5f, 0f, new[] { waterBiome.Id }.AsSpan()),
                biomeHandle,
                biomes);

            WorldPlanarSampleRegion whole = WorldPlanarSampleRegion.Whole(in layout);
            Assert.That(waterStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(slopeStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(biomeStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(surfaceStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(buildableStage.ExecuteRegion(data, in whole).Success, Is.True);

            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout, editBounds.Count);

            long heapBefore = GC.GetTotalMemory(false);
            Stopwatch editWatch = Stopwatch.StartNew();
            WorldHeightAuthoringOperations.Lower(baseHeight.AsSpan(), overrides, in editBounds, 20f);
            editWatch.Stop();

            Stopwatch composeWatch = Stopwatch.StartNew();
            overrides.Compose(baseHeight.AsSpan(), finalHeight.AsSpan());
            composeWatch.Stop();

            WorldBuiltinDerivedDirtyRegions dirty = WorldAuthoringDirtyPropagation.FromHeightEdit(in editBounds, in layout);
            WorldSampleRect waterDirty = dirty.WaterDepth.Value;
            WorldSampleRect slopeDirty = dirty.Slope.Value;
            WorldSampleRect biomeDirty = dirty.Biome.Value;
            WorldSampleRect surfaceDirty = dirty.Surface.Value;
            WorldSampleRect buildableDirty = dirty.Buildable.Value;
            WorldPlanarSampleRegion waterRegion = WorldAuthoringDirtyPropagation.ToRegion(in waterDirty);
            WorldPlanarSampleRegion slopeRegion = WorldAuthoringDirtyPropagation.ToRegion(in slopeDirty);
            WorldPlanarSampleRegion biomeRegion = WorldAuthoringDirtyPropagation.ToRegion(in biomeDirty);
            WorldPlanarSampleRegion surfaceRegion = WorldAuthoringDirtyPropagation.ToRegion(in surfaceDirty);
            WorldPlanarSampleRegion buildableRegion = WorldAuthoringDirtyPropagation.ToRegion(in buildableDirty);

            Stopwatch recomputeWatch = Stopwatch.StartNew();
            Assert.That(waterStage.ExecuteRegion(data, in waterRegion).Success, Is.True);
            Assert.That(slopeStage.ExecuteRegion(data, in slopeRegion).Success, Is.True);
            Assert.That(biomeStage.ExecuteRegion(data, in biomeRegion).Success, Is.True);
            Assert.That(surfaceStage.ExecuteRegion(data, in surfaceRegion).Success, Is.True);
            Assert.That(buildableStage.ExecuteRegion(data, in buildableRegion).Success, Is.True);
            recomputeWatch.Stop();
            long heapDelta = GC.GetTotalMemory(false) - heapBefore;

            long waterCount = 0L;
            long blockedCount = 0L;
            long checksum = 0L;
            for (int y = slopeDirty.Y; y < slopeDirty.TopExclusive; y++)
            {
                int row = y * layout.Width;
                for (int x = slopeDirty.X; x < slopeDirty.RightExclusive; x++)
                {
                    int index = row + x;
                    if (water[index] > 0f) waterCount++;
                    if (buildable[index] == 0) blockedCount++;
                    checksum += biome[index] * 31L + surface[index] * 17L + buildable[index];
                }
            }

            string message = string.Format(
                "WorldGenKit Authoring benchmark env={0} mapSamples={1} editSamples={2} cascadeSamples={3} editMs={4:F3} composeMs={5:F3} recomputeMs={6:F3} water={7} blocked={8} checksum={9} allocationDelta={10}",
                Application.unityVersion,
                layout.Count,
                editBounds.Count,
                slopeDirty.Count,
                editWatch.Elapsed.TotalMilliseconds,
                composeWatch.Elapsed.TotalMilliseconds,
                recomputeWatch.Elapsed.TotalMilliseconds,
                waterCount,
                blockedCount,
                checksum,
                heapDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(overrides.Count, Is.EqualTo(editBounds.Count));
            Assert.That(waterCount, Is.EqualTo(editBounds.Count));
            Assert.That(blockedCount, Is.GreaterThanOrEqualTo(editBounds.Count));
            Assert.That(checksum, Is.GreaterThanOrEqualTo(0L));
        }
    }
}
