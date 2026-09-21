using System;
using System.Globalization;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldGenKit.StreamingAdapter;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming.SaveKitAdapter;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldStreamingEndToEndTests
    {
        [Test]
        public void UnmodifiedChunkCanUnloadAndRebuildFromSeedWithoutStoredState()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(16, 16);
            WorldGenerationSeed seed = new WorldGenerationSeed(0x1234ABCDUL);
            WorldChunkCoord chunk = new WorldChunkCoord(-37, 22);

            float[] first = GenerateChunk(layout, seed, chunk);

            // Simulate a full unload: no generated Dense storage survives.
            float[] rebuilt = GenerateChunk(layout, seed, chunk);

            Assert.That(rebuilt, Is.EqualTo(first));
        }

        [Test]
        public void ModifiedChunkRebuildsBaseThenRestoresSavedDelta()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(16, 16);
            WorldGenerationSeed seed = new WorldGenerationSeed(0x99887766UL);
            WorldChunkCoord chunk = new WorldChunkCoord(-12, 15);
            int changedSample = 47;
            float changedHeight = 777.25f;

            float[] generated = GenerateChunk(layout, seed, chunk);
            float untouchedSample = generated[changedSample + 1];
            generated[changedSample] = changedHeight;

            InMemorySaveStorage storage = new InMemorySaveStorage();
            SaveKit.Initialize(builder => builder
                .UseStorage(storage)
                .SetApplicationVersion("streaming-e2e"));

            WorldId worldId = WorldId.From("world.streaming_e2e");
            WorldDeltaCodecRegistry codecs = new WorldDeltaCodecRegistry();
            Assert.That(codecs.TryRegister(new HeightDeltaCodec(), out string codecError), Is.True, codecError);
            WorldDeltaPersistenceState persisted = new WorldDeltaPersistenceState(worldId, codecs);
            persisted.DeltaSet.Append(new HeightDelta(
                WorldDeltaTarget.ForChunk(worldId, chunk),
                changedSample,
                changedHeight));
            SaveKit.Register(new WorldDeltaSaveSection(persisted));

            SaveResult save = SaveKit.SaveAsync("streaming-e2e").GetAwaiter().GetResult();
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);

            // Simulate unload and application restart: generated data and in-memory deltas are gone.
            persisted.Clear();
            float[] rebuilt = GenerateChunk(layout, seed, chunk);
            Assert.That(rebuilt[changedSample], Is.Not.EqualTo(changedHeight));

            SaveResult load = SaveKit.LoadAsync("streaming-e2e").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            ApplyChunkDeltas(persisted.DeltaSet, chunk, rebuilt);

            Assert.That(rebuilt[changedSample], Is.EqualTo(changedHeight));
            Assert.That(rebuilt[changedSample + 1], Is.EqualTo(untouchedSample));
        }

        private static float[] GenerateChunk(
            WorldPlanarSampleLayout layout,
            WorldGenerationSeed seed,
            WorldChunkCoord chunk)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                new WorldChannelStorageDescriptor(
                    WorldChannelStorageKind.Dense,
                    WorldChannelScope.Sample));
            builder.AddStage(new WorldHeightStage(
                height,
                layout,
                new WorldFractalNoiseSettings(
                    WorldRuleId.From("noise.streaming_e2e"),
                    64L,
                    4,
                    2,
                    0.5d),
                -50f,
                250f));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True);
            DenseChannelStorage<float> storage = new DenseChannelStorage<float>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(height, storage);
            WorldGenerationStageExecutionRecord[] records =
                new WorldGenerationStageExecutionRecord[compile.Plan.StageCount];
            WorldGenerationRunResult result = WorldChunkGenerationAdapter.ExecuteChunk(
                compile.Plan,
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

        private static void ApplyChunkDeltas(
            WorldDeltaSet deltaSet,
            WorldChunkCoord chunk,
            float[] heights)
        {
            WorldDeltaRecord[] records = new WorldDeltaRecord[deltaSet.Count];
            deltaSet.WriteTo(records);
            for (int i = 0; i < records.Length; i++)
            {
                WorldDeltaRecord record = records[i];
                if (record.Target.Kind != WorldDeltaTargetKind.Chunk ||
                    record.Target.Chunk != chunk)
                    continue;

                HeightDelta delta = record.Payload as HeightDelta;
                Assert.That(delta, Is.Not.Null);
                Assert.That(delta.SampleIndex, Is.InRange(0, heights.Length - 1));
                heights[delta.SampleIndex] = delta.Height;
            }
        }

        private sealed class HeightDelta : IWorldDelta
        {
            internal static readonly WorldDeltaTypeId StableId =
                WorldDeltaTypeId.From("terrain.streaming_height_override");

            public WorldDeltaTypeId TypeId => StableId;
            public WorldDeltaVersion Version => new WorldDeltaVersion(1);
            public WorldDeltaTarget Target { get; }
            public int SampleIndex { get; }
            public float Height { get; }

            internal HeightDelta(
                WorldDeltaTarget target,
                int sampleIndex,
                float height)
            {
                Target = target;
                SampleIndex = sampleIndex;
                Height = height;
            }
        }

        private sealed class HeightDeltaCodec : IWorldDeltaCodec
        {
            public WorldDeltaTypeId TypeId => HeightDelta.StableId;

            public bool TryEncode(
                IWorldDelta delta,
                out string payload,
                out string error)
            {
                HeightDelta typed = delta as HeightDelta;
                if (typed == null)
                {
                    payload = null;
                    error = "Wrong delta type.";
                    return false;
                }

                payload = typed.SampleIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                          typed.Height.ToString("R", CultureInfo.InvariantCulture);
                error = null;
                return true;
            }

            public bool TryDecode(
                WorldDeltaTarget target,
                WorldDeltaVersion version,
                string payload,
                out IWorldDelta delta,
                out string error)
            {
                delta = null;
                error = null;
                if (version.Value != 1)
                {
                    error = "Unsupported version.";
                    return false;
                }

                string[] parts = (payload ?? string.Empty).Split(':');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sampleIndex) ||
                    !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
                {
                    error = "Invalid payload.";
                    return false;
                }

                delta = new HeightDelta(target, sampleIndex, height);
                return true;
            }
        }
    }
}
