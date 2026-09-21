using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldGenKit.StreamingAdapter;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenStreamingAdapterTests
    {
        [Test]
        public void ChunkRunKeyUsesAbsolutePlanarSampleOriginIncludingNegativeChunks()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(8, 6, 2L);

            Assert.That(WorldChunkGenerationAdapter.TryCreateRunKey(
                new WorldChunkCoord(-3, 4), in layout, 99UL, out WorldGenerationRunKey key), Is.True);
            Assert.That(key.X, Is.EqualTo(-48L));
            Assert.That(key.Y, Is.EqualTo(48L));
            Assert.That(key.LocalKey, Is.EqualTo(99UL));
        }

        [Test]
        public void AdjacentChunkGenerationMatchesEquivalentMonolithicGeneration()
        {
            const int chunkWidth = 8;
            const int height = 6;
            WorldGenerationSeed seed = new WorldGenerationSeed(987654UL);
            WorldFractalNoiseSettings noise = HeightNoise();

            float[] whole = GenerateHeight(
                new WorldPlanarSampleLayout(chunkWidth * 2, height),
                new WorldGenerationRunKey(-8L, -6L),
                seed,
                noise);

            WorldPlanarSampleLayout chunkLayout = new WorldPlanarSampleLayout(chunkWidth, height);
            float[] left = GenerateChunkHeight(
                chunkLayout,
                new WorldChunkCoord(-1, -1),
                seed,
                noise);
            float[] right = GenerateChunkHeight(
                chunkLayout,
                new WorldChunkCoord(0, -1),
                seed,
                noise);

            for (int y = 0; y < height; y++)
            {
                int wholeRow = y * chunkWidth * 2;
                int chunkRow = y * chunkWidth;
                for (int x = 0; x < chunkWidth; x++)
                {
                    Assert.That(left[chunkRow + x], Is.EqualTo(whole[wholeRow + x]), "left y=" + y + " x=" + x);
                    Assert.That(right[chunkRow + x], Is.EqualTo(whole[wholeRow + chunkWidth + x]), "right y=" + y + " x=" + x);
                }
            }
        }

        [Test]
        public void ChunkGenerationIsIndependentFromExplorationOrder()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(16, 16);
            WorldGenerationSeed seed = new WorldGenerationSeed(123456789UL);
            WorldFractalNoiseSettings noise = HeightNoise();
            WorldChunkCoord a = new WorldChunkCoord(-20, 11);
            WorldChunkCoord b = new WorldChunkCoord(14, -9);

            float[] aFirst = GenerateChunkHeight(layout, a, seed, noise);
            float[] bSecond = GenerateChunkHeight(layout, b, seed, noise);
            float[] bFirst = GenerateChunkHeight(layout, b, seed, noise);
            float[] aSecond = GenerateChunkHeight(layout, a, seed, noise);

            Assert.That(aSecond, Is.EqualTo(aFirst));
            Assert.That(bFirst, Is.EqualTo(bSecond));
            Assert.That(aFirst, Is.Not.EqualTo(bFirst));
        }

        [Test]
        public void RunKeyOverflowIsExplicitAndDoesNotProduceWrappedCoordinates()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(64, 64, 4L);
            Assert.That(WorldChunkGenerationAdapter.TryCreateRunKey(
                new WorldChunkCoord(long.MaxValue, 0),
                in layout,
                0UL,
                out WorldGenerationRunKey key), Is.False);
            Assert.That(key, Is.EqualTo(default(WorldGenerationRunKey)));
        }

        [Test]
        public void RegionRunKeyUsesGenerationRegionMinimumChunkAsAbsoluteOrigin()
        {
            WorldRegionLayout regionLayout = new WorldRegionLayout(4, 3);
            WorldPlanarSampleLayout chunkLayout = new WorldPlanarSampleLayout(8, 6, 2L);

            Assert.That(WorldChunkGenerationAdapter.TryCreateRegionRunKey(
                new WorldRegionCoord(-2, 3),
                in regionLayout,
                in chunkLayout,
                77UL,
                out WorldGenerationRunKey key), Is.True);

            Assert.That(key.X, Is.EqualTo(-128L));
            Assert.That(key.Y, Is.EqualTo(108L));
            Assert.That(key.LocalKey, Is.EqualTo(77UL));
        }

        private static float[] GenerateChunkHeight(
            WorldPlanarSampleLayout layout,
            WorldChunkCoord chunk,
            WorldGenerationSeed seed,
            WorldFractalNoiseSettings noise)
        {
            BuildPlan(layout, noise, out WorldGenerationPlan plan, out ChannelHandle<float> height);
            DenseChannelStorage<float> storage = new DenseChannelStorage<float>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(plan.Channels);
            data.Bind(height, storage);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[plan.StageCount];

            WorldGenerationRunResult result = WorldChunkGenerationAdapter.ExecuteChunk(
                plan,
                data,
                seed,
                chunk,
                in layout,
                0UL,
                records,
                out _);
            Assert.That(result.Success, Is.True);
            return storage.AsReadOnlySpan().ToArray();
        }

        private static float[] GenerateHeight(
            WorldPlanarSampleLayout layout,
            WorldGenerationRunKey runKey,
            WorldGenerationSeed seed,
            WorldFractalNoiseSettings noise)
        {
            BuildPlan(layout, noise, out WorldGenerationPlan plan, out ChannelHandle<float> height);
            DenseChannelStorage<float> storage = new DenseChannelStorage<float>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(plan.Channels);
            data.Bind(height, storage);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[plan.StageCount];
            Assert.That(plan.Execute(data, seed, runKey, records).Success, Is.True);
            return storage.AsReadOnlySpan().ToArray();
        }

        private static void BuildPlan(
            WorldPlanarSampleLayout layout,
            WorldFractalNoiseSettings noise,
            out WorldGenerationPlan plan,
            out ChannelHandle<float> height)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            builder.AddStage(new WorldHeightStage(height, layout, noise, -100f, 250f));
            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True);
            plan = compile.Plan;
        }

        private static WorldFractalNoiseSettings HeightNoise() =>
            new WorldFractalNoiseSettings(
                WorldRuleId.From("noise.streaming_height"),
                64L,
                5,
                2,
                0.5d);
    }
}
