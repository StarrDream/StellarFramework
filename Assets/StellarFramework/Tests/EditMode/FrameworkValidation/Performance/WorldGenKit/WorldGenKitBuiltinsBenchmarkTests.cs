using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitBuiltinsBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void WorldGenKitBuiltinsBenchmark_512x512FullTerrainSemanticPipeline()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(512, 512);
            WorldBiomeDefinition waterBiome = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.water"),
                WorldSurfaceId.From("surface.water"),
                new WorldBiomeCriteria(waterDepth: new WorldRangeRule(0.00001d, 1000d)),
                100);
            WorldBiomeDefinition wetland = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.wetland"),
                WorldSurfaceId.From("surface.mud"),
                new WorldBiomeCriteria(moisture: new WorldRangeRule(0.62d, 1d)),
                20);
            WorldBiomeDefinition grassland = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.grassland"),
                WorldSurfaceId.From("surface.grass"),
                new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(
                new[] { wetland, grassland, waterBiome }.AsSpan(),
                grassland.Id);
            WorldSurfaceCatalog surfaces = new WorldSurfaceCatalog(
                new[]
                {
                    WorldSurfaceId.From("surface.grass"),
                    WorldSurfaceId.From("surface.mud"),
                    WorldSurfaceId.From("surface.water")
                }.AsSpan());

            Stopwatch compileWatch = Stopwatch.StartNew();
            BenchmarkPipeline pipeline = BuildPipeline(layout, biomes, surfaces);
            compileWatch.Stop();

            DenseChannelStorage<float> height = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> moisture = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> water = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> slope = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<int> biome = new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<int> surface = new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<byte> buildable = new DenseChannelStorage<byte>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(pipeline.Plan.Channels);
            data.Bind(pipeline.Height, height);
            data.Bind(pipeline.Moisture, moisture);
            data.Bind(pipeline.Water, water);
            data.Bind(pipeline.Slope, slope);
            data.Bind(pipeline.Biome, biome);
            data.Bind(pipeline.Surface, surface);
            data.Bind(pipeline.Buildable, buildable);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[pipeline.Plan.StageCount];

            long heapBefore = GC.GetTotalMemory(false);
            Stopwatch runWatch = Stopwatch.StartNew();
            WorldGenerationRunResult run = pipeline.Plan.Execute(
                data,
                new WorldGenerationSeed(0x123456789ABCDEF0UL),
                new WorldGenerationRunKey(-8192L, 16384L),
                records.AsSpan());
            runWatch.Stop();
            long heapDelta = GC.GetTotalMemory(false) - heapBefore;

            Assert.That(run.Success, Is.True, run.Code.ToString());

            long buildableCount = 0L;
            long waterCount = 0L;
            long checksum = 0L;
            ReadOnlySpan<byte> buildableValues = buildable.AsReadOnlySpan();
            ReadOnlySpan<float> waterValues = water.AsReadOnlySpan();
            ReadOnlySpan<int> biomeValues = biome.AsReadOnlySpan();
            ReadOnlySpan<int> surfaceValues = surface.AsReadOnlySpan();
            for (int i = 0; i < layout.Count; i++)
            {
                if (buildableValues[i] != 0) buildableCount++;
                if (waterValues[i] > 0f) waterCount++;
                checksum += biomeValues[i] * 31L + surfaceValues[i] * 17L + buildableValues[i];
            }

            string message = string.Format(
                "WorldGenKit Builtins benchmark env={0} samples={1} stages={2} compileMs={3:F3} runMs={4:F3} buildable={5} water={6} checksum={7} allocationDelta={8}",
                Application.unityVersion,
                layout.Count,
                pipeline.Plan.StageCount,
                compileWatch.Elapsed.TotalMilliseconds,
                runWatch.Elapsed.TotalMilliseconds,
                buildableCount,
                waterCount,
                checksum,
                heapDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(buildableCount, Is.GreaterThanOrEqualTo(0L).And.LessThanOrEqualTo(layout.Count));
            Assert.That(waterCount, Is.GreaterThanOrEqualTo(0L).And.LessThanOrEqualTo(layout.Count));
            Assert.That(checksum, Is.GreaterThanOrEqualTo(0L));
        }

        private static BenchmarkPipeline BuildPipeline(
            WorldPlanarSampleLayout layout,
            WorldBiomeCatalog biomes,
            WorldSurfaceCatalog surfaces)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            WorldChannelStorageDescriptor dense = new WorldChannelStorageDescriptor(
                WorldChannelStorageKind.Dense,
                WorldChannelScope.Sample);
            ChannelHandle<float> height = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.height"), dense);
            ChannelHandle<float> moisture = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.moisture"), dense);
            ChannelHandle<float> water = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.water_depth"), dense);
            ChannelHandle<float> slope = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.slope"), dense);
            ChannelHandle<int> biome = builder.Channels.Register<int>(WorldDataChannelId.From("terrain.biome"), dense);
            ChannelHandle<int> surface = builder.Channels.Register<int>(WorldDataChannelId.From("terrain.surface"), dense);
            ChannelHandle<byte> buildable = builder.Channels.Register<byte>(WorldDataChannelId.From("terrain.buildable"), dense);

            builder.AddStage(new WorldBuildableStage(
                slope,
                water,
                buildable,
                layout,
                new WorldBuildableSettings(8f, 0f, new[] { WorldBiomeId.From("biome.water") }.AsSpan()),
                biome,
                biomes));
            builder.AddStage(new WorldSurfaceStage(biome, surface, layout, biomes, surfaces));
            builder.AddStage(new WorldBiomeStage(height, biome, layout, biomes, moisture, water, slope));
            builder.AddStage(new WorldSlopeStage(height, slope, layout));
            builder.AddStage(new WorldWaterDepthStage(height, water, layout, 0f));
            builder.AddStage(new WorldMoistureStage(
                moisture,
                layout,
                new WorldFractalNoiseSettings(WorldRuleId.From("noise.moisture"), 96L, 4, 2, 0.55d)));
            builder.AddStage(new WorldHeightStage(
                height,
                layout,
                new WorldFractalNoiseSettings(WorldRuleId.From("noise.height"), 128L, 5, 2, 0.5d),
                -40f,
                60f));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True);
            return new BenchmarkPipeline(compile.Plan, height, moisture, water, slope, biome, surface, buildable);
        }

        private readonly struct BenchmarkPipeline
        {
            internal WorldGenerationPlan Plan { get; }
            internal ChannelHandle<float> Height { get; }
            internal ChannelHandle<float> Moisture { get; }
            internal ChannelHandle<float> Water { get; }
            internal ChannelHandle<float> Slope { get; }
            internal ChannelHandle<int> Biome { get; }
            internal ChannelHandle<int> Surface { get; }
            internal ChannelHandle<byte> Buildable { get; }

            internal BenchmarkPipeline(
                WorldGenerationPlan plan,
                ChannelHandle<float> height,
                ChannelHandle<float> moisture,
                ChannelHandle<float> water,
                ChannelHandle<float> slope,
                ChannelHandle<int> biome,
                ChannelHandle<int> surface,
                ChannelHandle<byte> buildable)
            {
                Plan = plan;
                Height = height;
                Moisture = moisture;
                Water = water;
                Slope = slope;
                Biome = biome;
                Surface = surface;
                Buildable = buildable;
            }
        }
    }
}
