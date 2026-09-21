using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitBuiltinsTerrainTests
    {
        [Test]
        public void PlanarLayoutUsesAbsoluteOriginAndSupportsNegativeCoordinates()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 3, 2L);
            WorldGenerationRunKey runKey = new WorldGenerationRunKey(-10L, -20L);

            Assert.That(layout.Count, Is.EqualTo(12));
            Assert.That(layout.GetIndex(3, 2), Is.EqualTo(11));
            Assert.That(layout.GetAbsoluteX(in runKey, 0), Is.EqualTo(-10L));
            Assert.That(layout.GetAbsoluteX(in runKey, 3), Is.EqualTo(-4L));
            Assert.That(layout.GetAbsoluteY(in runKey, 2), Is.EqualTo(-16L));
        }

        [Test]
        public void FractalNoiseIsDeterministicAcrossPositiveAndNegativeLogicalCoordinates()
        {
            WorldFractalNoiseSettings settings = new WorldFractalNoiseSettings(
                WorldRuleId.From("noise.test_field"),
                64L,
                5,
                2,
                0.5d);
            WorldGenerationSeed seed = new WorldGenerationSeed(987654321UL);

            double a = WorldFractalValueNoise.Sample01(seed, -12345L, 67890L, in settings);
            double b = WorldFractalValueNoise.Sample01(seed, -12345L, 67890L, in settings);
            double c = WorldFractalValueNoise.Sample01(seed, -12344L, 67890L, in settings);

            Assert.That(a, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(c, Is.Not.EqualTo(a));
        }

        [Test]
        public void HeightFieldMatchesMonolithicGenerationAcrossAdjacentTileBoundary()
        {
            const int tileWidth = 8;
            const int height = 6;
            WorldGenerationSeed worldSeed = new WorldGenerationSeed(123456UL);
            WorldFractalNoiseSettings noise = HeightNoise();

            float[] whole = GenerateHeight(
                new WorldPlanarSampleLayout(tileWidth * 2, height),
                new WorldGenerationRunKey(-8L, -3L),
                worldSeed,
                noise);
            float[] left = GenerateHeight(
                new WorldPlanarSampleLayout(tileWidth, height),
                new WorldGenerationRunKey(-8L, -3L),
                worldSeed,
                noise);
            float[] right = GenerateHeight(
                new WorldPlanarSampleLayout(tileWidth, height),
                new WorldGenerationRunKey(0L, -3L),
                worldSeed,
                noise);

            for (int y = 0; y < height; y++)
            {
                int wholeRow = y * tileWidth * 2;
                int tileRow = y * tileWidth;
                for (int x = 0; x < tileWidth; x++)
                {
                    Assert.That(left[tileRow + x], Is.EqualTo(whole[wholeRow + x]), "left y=" + y + " x=" + x);
                    Assert.That(right[tileRow + x], Is.EqualTo(whole[wholeRow + tileWidth + x]), "right y=" + y + " x=" + x);
                }
            }
        }

        [Test]
        public void ImportedHeightCanDriveWaterAndSlopeWithoutHeightGenerator()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(3, 3);
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            WorldChannelStorageDescriptor dense = DenseSample();

            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                dense,
                WorldChannelSourceMode.ProvidedInput);
            ChannelHandle<float> water = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.water_depth"), dense);
            ChannelHandle<float> slope = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.slope"), dense);

            builder.AddStage(new WorldWaterDepthStage(height, water, layout, 2.5f));
            builder.AddStage(new WorldSlopeStage(height, slope, layout));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, Diagnostics(compile));

            DenseChannelStorage<float> heightData = new DenseChannelStorage<float>(9);
            float[] authored =
            {
                0f, 1f, 2f,
                1f, 2f, 3f,
                2f, 3f, 4f
            };
            authored.AsSpan().CopyTo(heightData.AsSpan());

            DenseChannelStorage<float> waterData = new DenseChannelStorage<float>(9);
            DenseChannelStorage<float> slopeData = new DenseChannelStorage<float>(9);
            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(height, heightData);
            data.Bind(water, waterData);
            data.Bind(slope, slopeData);

            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[2];
            WorldGenerationRunResult run = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                records.AsSpan());

            Assert.That(run.Success, Is.True);
            Assert.That(waterData[0], Is.EqualTo(2.5f));
            Assert.That(waterData[4], Is.EqualTo(0.5f));
            Assert.That(waterData[8], Is.EqualTo(0f));
            Assert.That(slopeData[4], Is.EqualTo((float)Math.Sqrt(2d)).Within(0.00001f));
        }

        [Test]
        public void MoistureStageIsOptionalAndWhenUsedStaysNormalized()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(8, 8);

            WorldGenerationPipelineBuilder withoutMoisture = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> heightOnly = withoutMoisture.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"), DenseSample());
            withoutMoisture.AddStage(new WorldHeightStage(heightOnly, layout, HeightNoise(), -10f, 30f));
            WorldGenerationCompileResult noMoistureCompile = withoutMoisture.Compile();
            Assert.That(noMoistureCompile.Success, Is.True, Diagnostics(noMoistureCompile));
            Assert.That(noMoistureCompile.Plan.Channels.TryResolve<float>(
                WorldDataChannelId.From("terrain.moisture"), out _, out WorldChannelResolveError error), Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelResolveError.NotFound));

            WorldGenerationDataSet noMoistureData = new WorldGenerationDataSet(noMoistureCompile.Plan.Channels);
            noMoistureData.Bind(heightOnly, new DenseChannelStorage<float>(layout.Count));
            WorldGenerationStageExecutionRecord[] oneRecord = new WorldGenerationStageExecutionRecord[1];
            Assert.That(noMoistureCompile.Plan.Execute(
                noMoistureData,
                new WorldGenerationSeed(7UL),
                new WorldGenerationRunKey(100L, 200L),
                oneRecord.AsSpan()).Success, Is.True);

            WorldGenerationPipelineBuilder withMoisture = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> moisture = withMoisture.Channels.Register<float>(
                WorldDataChannelId.From("terrain.moisture"), DenseSample());
            withMoisture.AddStage(new WorldMoistureStage(
                moisture,
                layout,
                new WorldFractalNoiseSettings(WorldRuleId.From("noise.moisture"), 48L, 4, 2, 0.55d)));
            WorldGenerationCompileResult moistureCompile = withMoisture.Compile();
            DenseChannelStorage<float> moistureData = new DenseChannelStorage<float>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(moistureCompile.Plan.Channels);
            data.Bind(moisture, moistureData);
            Assert.That(moistureCompile.Plan.Execute(
                data,
                new WorldGenerationSeed(7UL),
                new WorldGenerationRunKey(100L, 200L),
                oneRecord.AsSpan()).Success, Is.True);

            for (int i = 0; i < moistureData.Length; i++)
                Assert.That(moistureData[i], Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
        }

        [Test]
        public void BuiltinStageReportsTooSmallDenseStorageExplicitly()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 4);
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"), DenseSample());
            builder.AddStage(new WorldHeightStage(height, layout, HeightNoise(), 0f, 1f));
            WorldGenerationCompileResult compile = builder.Compile();

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(height, new DenseChannelStorage<float>(15));
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            WorldGenerationRunResult run = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                records.AsSpan());

            Assert.That(run.Status, Is.EqualTo(WorldGenerationRunStatus.Failed));
            Assert.That(run.Code, Is.EqualTo(WorldGenBuiltinDiagnosticIds.StorageLengthMismatch));
        }

        private static float[] GenerateHeight(
            WorldPlanarSampleLayout layout,
            WorldGenerationRunKey runKey,
            WorldGenerationSeed seed,
            WorldFractalNoiseSettings noise)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"), DenseSample());
            builder.AddStage(new WorldHeightStage(height, layout, noise, -100f, 250f));
            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, Diagnostics(compile));

            DenseChannelStorage<float> storage = new DenseChannelStorage<float>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(height, storage);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            WorldGenerationRunResult result = compile.Plan.Execute(data, seed, runKey, records.AsSpan());
            Assert.That(result.Success, Is.True);

            return storage.AsReadOnlySpan().ToArray();
        }

        private static WorldFractalNoiseSettings HeightNoise() =>
            new WorldFractalNoiseSettings(
                WorldRuleId.From("noise.height"),
                64L,
                5,
                2,
                0.5d);

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
    }
}
