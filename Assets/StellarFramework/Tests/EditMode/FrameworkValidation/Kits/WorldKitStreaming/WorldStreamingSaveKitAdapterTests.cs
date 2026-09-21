using System;
using NUnit.Framework;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming.SaveKitAdapter;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldStreamingSaveKitAdapterTests
    {
        [Test]
        public void DeltaSnapshotRoundTripPreservesOrderMetadataAndPayload()
        {
            WorldId worldId = WorldId.From("world.streaming_save");
            WorldDeltaCodecRegistry codecs = CreateCodecs();
            WorldDeltaPersistenceState state = new WorldDeltaPersistenceState(worldId, codecs);
            state.DeltaSet.Append(new SampleHeightDelta(
                WorldDeltaTarget.ForChunk(worldId, new WorldChunkCoord(-4, 7)),
                3,
                12.5f));
            state.DeltaSet.Append(new SampleHeightDelta(
                WorldDeltaTarget.ForChunk(worldId, new WorldChunkCoord(9, -2)),
                8,
                -6.25f));

            WorldDeltaSnapshot snapshot = state.CaptureSnapshot();
            Assert.That(state.ValidateSnapshot(snapshot, out string error), Is.True, error);

            state.Clear();
            state.RestoreSnapshot(snapshot);
            Assert.That(state.DeltaSet.Count, Is.EqualTo(2));

            WorldDeltaRecord[] restored = new WorldDeltaRecord[2];
            state.DeltaSet.WriteTo(restored);
            Assert.That(restored[0].Target.Chunk, Is.EqualTo(new WorldChunkCoord(-4, 7)));
            Assert.That(((SampleHeightDelta)restored[0].Payload).SampleIndex, Is.EqualTo(3));
            Assert.That(((SampleHeightDelta)restored[0].Payload).Height, Is.EqualTo(12.5f));
            Assert.That(restored[1].Target.Chunk, Is.EqualTo(new WorldChunkCoord(9, -2)));
            Assert.That(((SampleHeightDelta)restored[1].Payload).SampleIndex, Is.EqualTo(8));
        }

        [Test]
        public void InvalidSnapshotDoesNotReplaceExistingDeltaSet()
        {
            WorldId worldId = WorldId.From("world.streaming_atomic");
            WorldDeltaPersistenceState state = new WorldDeltaPersistenceState(worldId, CreateCodecs());
            SampleHeightDelta original = new SampleHeightDelta(
                WorldDeltaTarget.ForChunk(worldId, new WorldChunkCoord(1, 2)),
                1,
                2f);
            state.DeltaSet.Append(original);
            WorldDeltaSet before = state.DeltaSet;

            WorldDeltaSnapshot invalid = state.CaptureSnapshot();
            invalid.Entries[0].Payload = "not-a-valid-height-delta";

            Assert.That(state.ValidateSnapshot(invalid, out _), Is.False);
            Assert.Throws<ArgumentException>(() => state.RestoreSnapshot(invalid));
            Assert.That(state.DeltaSet, Is.SameAs(before));
            Assert.That(state.DeltaSet.Count, Is.EqualTo(1));
        }

        [Test]
        public void RealSaveKitRoundTripRestoresChunkDelta()
        {
            InMemorySaveStorage storage = new InMemorySaveStorage();
            SaveKit.Initialize(builder => builder
                .UseStorage(storage)
                .SetApplicationVersion("streaming-test"));

            WorldId worldId = WorldId.From("world.streaming_savekit");
            WorldDeltaPersistenceState state = new WorldDeltaPersistenceState(worldId, CreateCodecs());
            state.DeltaSet.Append(new SampleHeightDelta(
                WorldDeltaTarget.ForChunk(worldId, new WorldChunkCoord(-12, 15)),
                5,
                77.25f));
            SaveKit.Register(new WorldDeltaSaveSection(state));

            SaveResult save = SaveKit.SaveAsync("streaming-deltas").GetAwaiter().GetResult();
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);

            state.Clear();
            Assert.That(state.DeltaSet.Count, Is.EqualTo(0));
            SaveResult load = SaveKit.LoadAsync("streaming-deltas").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(state.DeltaSet.Count, Is.EqualTo(1));

            WorldDeltaRecord[] records = new WorldDeltaRecord[1];
            state.DeltaSet.WriteTo(records);
            SampleHeightDelta restored = (SampleHeightDelta)records[0].Payload;
            Assert.That(records[0].Target.Chunk, Is.EqualTo(new WorldChunkCoord(-12, 15)));
            Assert.That(restored.SampleIndex, Is.EqualTo(5));
            Assert.That(restored.Height, Is.EqualTo(77.25f));
        }

        [Test]
        public void CodecRegistryRejectsDuplicateTypeId()
        {
            WorldDeltaCodecRegistry registry = new WorldDeltaCodecRegistry();
            Assert.That(registry.TryRegister(new SampleHeightDeltaCodec(), out string firstError), Is.True, firstError);
            Assert.That(registry.TryRegister(new SampleHeightDeltaCodec(), out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("Duplicate"));
        }

        private static WorldDeltaCodecRegistry CreateCodecs()
        {
            WorldDeltaCodecRegistry registry = new WorldDeltaCodecRegistry();
            Assert.That(registry.TryRegister(new SampleHeightDeltaCodec(), out string error), Is.True, error);
            return registry;
        }

        private sealed class SampleHeightDelta : IWorldDelta
        {
            internal static readonly WorldDeltaTypeId StableId = WorldDeltaTypeId.From("terrain.sample_height");

            public WorldDeltaTypeId TypeId => StableId;
            public WorldDeltaVersion Version => new WorldDeltaVersion(1);
            public WorldDeltaTarget Target { get; }
            public int SampleIndex { get; }
            public float Height { get; }

            internal SampleHeightDelta(WorldDeltaTarget target, int sampleIndex, float height)
            {
                Target = target;
                SampleIndex = sampleIndex;
                Height = height;
            }
        }

        private sealed class SampleHeightDeltaCodec : IWorldDeltaCodec
        {
            public WorldDeltaTypeId TypeId => SampleHeightDelta.StableId;

            public bool TryEncode(IWorldDelta delta, out string payload, out string error)
            {
                if (!(delta is SampleHeightDelta typed))
                {
                    payload = null;
                    error = "Wrong delta type.";
                    return false;
                }

                payload = typed.SampleIndex + ":" + typed.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
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
                    !int.TryParse(parts[0], out int sampleIndex) ||
                    !float.TryParse(
                        parts[1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float height))
                {
                    error = "Invalid payload.";
                    return false;
                }

                delta = new SampleHeightDelta(target, sampleIndex, height);
                return true;
            }
        }
    }
}
