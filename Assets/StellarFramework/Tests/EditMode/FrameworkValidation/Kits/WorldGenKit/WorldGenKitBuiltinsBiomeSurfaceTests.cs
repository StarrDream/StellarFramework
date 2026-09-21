using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitBuiltinsBiomeSurfaceTests
    {
        [Test]
        public void BiomeCatalogRequiresUniqueStableIdsAndUnconditionalFallback()
        {
            WorldBiomeDefinition fallback = Biome("biome.fallback", "surface.grass", new WorldBiomeCriteria());
            WorldBiomeDefinition wet = Biome(
                "biome.wet",
                "surface.mud",
                new WorldBiomeCriteria(moisture: new WorldRangeRule(0.6d, 1d)),
                priority: 10);

            WorldBiomeCatalog catalog = new WorldBiomeCatalog(
                new[] { wet, fallback }.AsSpan(),
                WorldBiomeId.From("biome.fallback"));

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.GetDefinition(catalog.FallbackIndex).Id.Value, Is.EqualTo("biome.fallback"));
            Assert.That(catalog.TryGetIndex(WorldBiomeId.From("biome.wet"), out int wetIndex), Is.True);
            Assert.That(catalog.GetDefinition(wetIndex).SurfaceId.Value, Is.EqualTo("surface.mud"));

            Assert.That(
                () => new WorldBiomeCatalog(new[] { fallback, fallback }.AsSpan(), fallback.Id),
                Throws.TypeOf<ArgumentException>());

            WorldBiomeDefinition conditionalFallback = Biome(
                "biome.conditional",
                "surface.grass",
                new WorldBiomeCriteria(height: new WorldRangeRule(0d, 10d)));
            Assert.That(
                () => new WorldBiomeCatalog(new[] { conditionalFallback }.AsSpan(), conditionalFallback.Id),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void BiomeSelectionUsesPriorityThenStableIdTieRegardlessOfCatalogOrder()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(1, 1);
            WorldBiomeDefinition alpha = Biome(
                "biome.alpha",
                "surface.alpha",
                new WorldBiomeCriteria(height: new WorldRangeRule(-100d, 100d)),
                priority: 5);
            WorldBiomeDefinition beta = Biome(
                "biome.beta",
                "surface.beta",
                new WorldBiomeCriteria(height: new WorldRangeRule(-100d, 100d)),
                priority: 5);
            WorldBiomeDefinition high = Biome(
                "biome.high_priority",
                "surface.high",
                new WorldBiomeCriteria(height: new WorldRangeRule(50d, 100d)),
                priority: 20);
            WorldBiomeDefinition fallback = Biome("biome.fallback", "surface.fallback", new WorldBiomeCriteria());

            WorldBiomeCatalog firstOrder = new WorldBiomeCatalog(
                new[] { beta, alpha, high, fallback }.AsSpan(), fallback.Id);
            WorldBiomeCatalog secondOrder = new WorldBiomeCatalog(
                new[] { fallback, high, alpha, beta }.AsSpan(), fallback.Id);

            Assert.That(SelectBiomeId(layout, firstOrder, 10f), Is.EqualTo("biome.alpha"));
            Assert.That(SelectBiomeId(layout, secondOrder, 10f), Is.EqualTo("biome.alpha"));
            Assert.That(SelectBiomeId(layout, firstOrder, 75f), Is.EqualTo("biome.high_priority"));
            Assert.That(SelectBiomeId(layout, secondOrder, 75f), Is.EqualTo("biome.high_priority"));
        }

        [Test]
        public void MoistureCriteriaOnlyParticipatesWhenOptionalMoistureStorageExists()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(1, 1);
            WorldBiomeDefinition wet = Biome(
                "biome.wet",
                "surface.mud",
                new WorldBiomeCriteria(moisture: new WorldRangeRule(0.7d, 1d)),
                priority: 10);
            WorldBiomeDefinition fallback = Biome("biome.dry", "surface.dry", new WorldBiomeCriteria());
            WorldBiomeCatalog catalog = new WorldBiomeCatalog(new[] { wet, fallback }.AsSpan(), fallback.Id);

            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<float> moisture = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.moisture"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<int> biome = builder.Channels.Register<int>(WorldDataChannelId.From("terrain.biome"), DenseSample());
            builder.AddStage(new WorldBiomeStage(height, biome, layout, catalog, moisture: moisture));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, Diagnostics(compile));

            DenseChannelStorage<float> heightData = new DenseChannelStorage<float>(1);
            heightData[0] = 10f;
            DenseChannelStorage<int> biomeData = new DenseChannelStorage<int>(1);
            WorldGenerationDataSet withoutMoisture = new WorldGenerationDataSet(compile.Plan.Channels);
            withoutMoisture.Bind(height, heightData);
            withoutMoisture.Bind(biome, biomeData);

            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            Assert.That(compile.Plan.Execute(
                withoutMoisture,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                records.AsSpan()).Success, Is.True);
            Assert.That(catalog.GetDefinition(biomeData[0]).Id.Value, Is.EqualTo("biome.dry"));

            WorldGenerationDataSet withMoisture = new WorldGenerationDataSet(compile.Plan.Channels);
            DenseChannelStorage<float> wetData = new DenseChannelStorage<float>(1);
            wetData[0] = 0.9f;
            withMoisture.Bind(height, heightData);
            withMoisture.Bind(moisture, wetData);
            withMoisture.Bind(biome, biomeData);
            Assert.That(compile.Plan.Execute(
                withMoisture,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                records.AsSpan()).Success, Is.True);
            Assert.That(catalog.GetDefinition(biomeData[0]).Id.Value, Is.EqualTo("biome.wet"));
        }

        [Test]
        public void SurfaceStageUsesPrecompiledStableIdMappingAndRejectsInvalidBiomeIndex()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(2, 1);
            WorldBiomeDefinition grass = Biome("biome.grass", "surface.grass", new WorldBiomeCriteria());
            WorldBiomeDefinition sand = Biome("biome.sand", "surface.sand", new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(new[] { grass, sand }.AsSpan(), grass.Id);
            WorldSurfaceCatalog surfaces = new WorldSurfaceCatalog(
                new[] { WorldSurfaceId.From("surface.sand"), WorldSurfaceId.From("surface.grass") }.AsSpan());

            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<int> biome = builder.Channels.Register<int>(
                WorldDataChannelId.From("terrain.biome"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<int> surface = builder.Channels.Register<int>(
                WorldDataChannelId.From("terrain.surface"), DenseSample());
            builder.AddStage(new WorldSurfaceStage(biome, surface, layout, biomes, surfaces));
            WorldGenerationCompileResult compile = builder.Compile();

            DenseChannelStorage<int> biomeData = new DenseChannelStorage<int>(2);
            DenseChannelStorage<int> surfaceData = new DenseChannelStorage<int>(2);
            biomes.TryGetIndex(grass.Id, out int grassIndex);
            biomes.TryGetIndex(sand.Id, out int sandIndex);
            biomeData[0] = grassIndex;
            biomeData[1] = sandIndex;

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(biome, biomeData);
            data.Bind(surface, surfaceData);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            WorldGenerationRunResult run = compile.Plan.Execute(
                data, new WorldGenerationSeed(1UL), default(WorldGenerationRunKey), records.AsSpan());
            Assert.That(run.Success, Is.True);
            Assert.That(surfaces.GetId(surfaceData[0]).Value, Is.EqualTo("surface.grass"));
            Assert.That(surfaces.GetId(surfaceData[1]).Value, Is.EqualTo("surface.sand"));

            biomeData[1] = 999;
            run = compile.Plan.Execute(
                data, new WorldGenerationSeed(1UL), default(WorldGenerationRunKey), records.AsSpan());
            Assert.That(run.Status, Is.EqualTo(WorldGenerationRunStatus.Failed));
            Assert.That(run.Code, Is.EqualTo(WorldGenBuiltinDiagnosticIds.InvalidBiomeIndex));
        }

        [Test]
        public void BuildableStageCombinesSlopeWaterAndBlockedBiomePolicy()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 1);
            WorldBiomeDefinition grass = Biome("biome.grass", "surface.grass", new WorldBiomeCriteria());
            WorldBiomeDefinition sacred = Biome("biome.sacred", "surface.grass", new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(new[] { grass, sacred }.AsSpan(), grass.Id);
            biomes.TryGetIndex(grass.Id, out int grassIndex);
            biomes.TryGetIndex(sacred.Id, out int sacredIndex);

            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> slope = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.slope"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<float> water = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.water_depth"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<int> biome = builder.Channels.Register<int>(
                WorldDataChannelId.From("terrain.biome"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<byte> buildable = builder.Channels.Register<byte>(
                WorldDataChannelId.From("terrain.buildable"), DenseSample());

            WorldBuildableSettings settings = new WorldBuildableSettings(
                maxSlope: 2f,
                maxWaterDepth: 0.1f,
                blockedBiomes: new[] { sacred.Id }.AsSpan());
            builder.AddStage(new WorldBuildableStage(slope, water, buildable, layout, settings, biome, biomes));
            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, Diagnostics(compile));

            DenseChannelStorage<float> slopeData = new DenseChannelStorage<float>(4);
            DenseChannelStorage<float> waterData = new DenseChannelStorage<float>(4);
            DenseChannelStorage<int> biomeData = new DenseChannelStorage<int>(4);
            DenseChannelStorage<byte> buildableData = new DenseChannelStorage<byte>(4);
            slopeData[0] = 1f; waterData[0] = 0f; biomeData[0] = grassIndex;
            slopeData[1] = 3f; waterData[1] = 0f; biomeData[1] = grassIndex;
            slopeData[2] = 1f; waterData[2] = 0.5f; biomeData[2] = grassIndex;
            slopeData[3] = 1f; waterData[3] = 0f; biomeData[3] = sacredIndex;

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(slope, slopeData);
            data.Bind(water, waterData);
            data.Bind(biome, biomeData);
            data.Bind(buildable, buildableData);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            WorldGenerationRunResult run = compile.Plan.Execute(
                data, new WorldGenerationSeed(1UL), default(WorldGenerationRunKey), records.AsSpan());

            Assert.That(run.Success, Is.True);
            Assert.That(buildableData.AsReadOnlySpan().ToArray(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
        }

        [Test]
        public void FullBuiltinPipelineProducesDeterministicValidTerrainSemanticChannels()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(32, 24);
            WorldBiomeDefinition waterBiome = Biome(
                "biome.water",
                "surface.water",
                new WorldBiomeCriteria(waterDepth: new WorldRangeRule(0.00001d, 1000d)),
                priority: 100);
            WorldBiomeDefinition wetBiome = Biome(
                "biome.wetland",
                "surface.mud",
                new WorldBiomeCriteria(moisture: new WorldRangeRule(0.62d, 1d)),
                priority: 20);
            WorldBiomeDefinition fallback = Biome("biome.grassland", "surface.grass", new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(
                new[] { wetBiome, fallback, waterBiome }.AsSpan(), fallback.Id);
            WorldSurfaceCatalog surfaces = new WorldSurfaceCatalog(
                new[]
                {
                    WorldSurfaceId.From("surface.grass"),
                    WorldSurfaceId.From("surface.mud"),
                    WorldSurfaceId.From("surface.water")
                }.AsSpan());

            FullPipeline pipeline = BuildFullPipeline(layout, biomes, surfaces);
            FullOutput first = ExecuteFull(pipeline, layout, new WorldGenerationSeed(987UL), new WorldGenerationRunKey(-32L, 48L));
            FullOutput second = ExecuteFull(pipeline, layout, new WorldGenerationSeed(987UL), new WorldGenerationRunKey(-32L, 48L));

            Assert.That(second.Height, Is.EqualTo(first.Height));
            Assert.That(second.Moisture, Is.EqualTo(first.Moisture));
            Assert.That(second.Water, Is.EqualTo(first.Water));
            Assert.That(second.Slope, Is.EqualTo(first.Slope));
            Assert.That(second.Biome, Is.EqualTo(first.Biome));
            Assert.That(second.Surface, Is.EqualTo(first.Surface));
            Assert.That(second.Buildable, Is.EqualTo(first.Buildable));

            for (int i = 0; i < layout.Count; i++)
            {
                Assert.That(first.Biome[i], Is.GreaterThanOrEqualTo(0).And.LessThan(biomes.Count));
                Assert.That(first.Surface[i], Is.GreaterThanOrEqualTo(0).And.LessThan(surfaces.Count));
                Assert.That(first.Buildable[i], Is.EqualTo((byte)0).Or.EqualTo((byte)1));
                Assert.That(first.Water[i], Is.GreaterThanOrEqualTo(0f));
                Assert.That(first.Slope[i], Is.GreaterThanOrEqualTo(0f));
                Assert.That(first.Moisture[i], Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            }
        }

        private static string SelectBiomeId(WorldPlanarSampleLayout layout, WorldBiomeCatalog catalog, float heightValue)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"), DenseSample(), WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<int> biome = builder.Channels.Register<int>(WorldDataChannelId.From("terrain.biome"), DenseSample());
            builder.AddStage(new WorldBiomeStage(height, biome, layout, catalog));
            WorldGenerationCompileResult compile = builder.Compile();
            DenseChannelStorage<float> heightData = new DenseChannelStorage<float>(1);
            DenseChannelStorage<int> biomeData = new DenseChannelStorage<int>(1);
            heightData[0] = heightValue;
            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(height, heightData);
            data.Bind(biome, biomeData);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            Assert.That(compile.Plan.Execute(data, new WorldGenerationSeed(1UL), default(WorldGenerationRunKey), records.AsSpan()).Success, Is.True);
            return catalog.GetDefinition(biomeData[0]).Id.Value;
        }

        private static FullPipeline BuildFullPipeline(
            WorldPlanarSampleLayout layout,
            WorldBiomeCatalog biomes,
            WorldSurfaceCatalog surfaces)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.height"), DenseSample());
            ChannelHandle<float> moisture = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.moisture"), DenseSample());
            ChannelHandle<float> water = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.water_depth"), DenseSample());
            ChannelHandle<float> slope = builder.Channels.Register<float>(WorldDataChannelId.From("terrain.slope"), DenseSample());
            ChannelHandle<int> biome = builder.Channels.Register<int>(WorldDataChannelId.From("terrain.biome"), DenseSample());
            ChannelHandle<int> surface = builder.Channels.Register<int>(WorldDataChannelId.From("terrain.surface"), DenseSample());
            ChannelHandle<byte> buildable = builder.Channels.Register<byte>(WorldDataChannelId.From("terrain.buildable"), DenseSample());

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
                new WorldFractalNoiseSettings(WorldRuleId.From("noise.moisture"), 72L, 4, 2, 0.55d)));
            builder.AddStage(new WorldHeightStage(
                height,
                layout,
                new WorldFractalNoiseSettings(WorldRuleId.From("noise.height"), 96L, 5, 2, 0.5d),
                -20f,
                20f));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, Diagnostics(compile));
            Assert.That(compile.Plan.StageCount, Is.EqualTo(7));
            return new FullPipeline(compile.Plan, height, moisture, water, slope, biome, surface, buildable);
        }

        private static FullOutput ExecuteFull(
            FullPipeline pipeline,
            WorldPlanarSampleLayout layout,
            WorldGenerationSeed seed,
            WorldGenerationRunKey runKey)
        {
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
            WorldGenerationRunResult run = pipeline.Plan.Execute(data, seed, runKey, records.AsSpan());
            Assert.That(run.Success, Is.True, run.Code.ToString());

            return new FullOutput(
                height.AsReadOnlySpan().ToArray(),
                moisture.AsReadOnlySpan().ToArray(),
                water.AsReadOnlySpan().ToArray(),
                slope.AsReadOnlySpan().ToArray(),
                biome.AsReadOnlySpan().ToArray(),
                surface.AsReadOnlySpan().ToArray(),
                buildable.AsReadOnlySpan().ToArray());
        }

        private static WorldBiomeDefinition Biome(
            string biomeId,
            string surfaceId,
            WorldBiomeCriteria criteria,
            int priority = 0) =>
            new WorldBiomeDefinition(
                WorldBiomeId.From(biomeId),
                WorldSurfaceId.From(surfaceId),
                criteria,
                priority);

        private static WorldChannelStorageDescriptor DenseSample() =>
            new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample);

        private static string Diagnostics(WorldGenerationCompileResult result)
        {
            if (result.Diagnostics == null || result.Diagnostics.Length == 0) return "<none>";
            string text = string.Empty;
            for (int i = 0; i < result.Diagnostics.Length; i++)
            {
                if (i > 0) text += " | ";
                text += result.Diagnostics[i].Code + ":" + result.Diagnostics[i].Message;
            }
            return text;
        }

        private readonly struct FullPipeline
        {
            internal WorldGenerationPlan Plan { get; }
            internal ChannelHandle<float> Height { get; }
            internal ChannelHandle<float> Moisture { get; }
            internal ChannelHandle<float> Water { get; }
            internal ChannelHandle<float> Slope { get; }
            internal ChannelHandle<int> Biome { get; }
            internal ChannelHandle<int> Surface { get; }
            internal ChannelHandle<byte> Buildable { get; }

            internal FullPipeline(
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

        private readonly struct FullOutput
        {
            internal float[] Height { get; }
            internal float[] Moisture { get; }
            internal float[] Water { get; }
            internal float[] Slope { get; }
            internal int[] Biome { get; }
            internal int[] Surface { get; }
            internal byte[] Buildable { get; }

            internal FullOutput(
                float[] height,
                float[] moisture,
                float[] water,
                float[] slope,
                int[] biome,
                int[] surface,
                byte[] buildable)
            {
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
