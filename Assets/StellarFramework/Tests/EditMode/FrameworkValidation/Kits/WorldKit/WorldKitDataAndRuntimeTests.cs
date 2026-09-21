using System;
using NUnit.Framework;
using StellarFramework.WorldKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldKitDataAndRuntimeTests
    {
        [Test]
        public void DataLayerRegistryUsesTypedGenerationProtectedHandles()
        {
            WorldDataLayerRegistryBuilder builder = new WorldDataLayerRegistryBuilder();
            WorldDataLayerId heightId = WorldDataLayerId.From("terrain.height");
            WorldDataLayerId metadataId = WorldDataLayerId.From("world.metadata");

            WorldDataLayerHandle<float> height = builder.Register<float>(heightId, WorldDataLayerScope.Chunk);
            WorldDataLayerHandle<int> metadata = builder.Register<int>(metadataId, WorldDataLayerScope.World);
            Assert.That(height.Index, Is.EqualTo(0));
            Assert.That(metadata.Index, Is.EqualTo(1));
            Assert.That(height.RegistryGeneration, Is.EqualTo(metadata.RegistryGeneration));
            Assert.That(default(WorldDataLayerHandle<float>).IsValid, Is.False);

            WorldDataLayerRegistry registry = builder.Build();
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.IsHandleValid(height), Is.True);
            Assert.That(registry.IsHandleValid(metadata), Is.True);

            Assert.That(registry.TryResolve(heightId, out WorldDataLayerHandle<float> resolved, out WorldDataLayerResolveError resolveError), Is.True);
            Assert.That(resolveError, Is.EqualTo(WorldDataLayerResolveError.None));
            Assert.That(resolved, Is.EqualTo(height));

            Assert.That(registry.TryResolve(heightId, out WorldDataLayerHandle<int> wrongType, out resolveError), Is.False);
            Assert.That(wrongType.IsValid, Is.False);
            Assert.That(resolveError, Is.EqualTo(WorldDataLayerResolveError.TypeMismatch));

            Assert.That(registry.TryGetDescriptor(height, out WorldDataLayerDescriptor descriptor), Is.True);
            Assert.That(descriptor.Id, Is.EqualTo(heightId));
            Assert.That(descriptor.Scope, Is.EqualTo(WorldDataLayerScope.Chunk));
            Assert.That(descriptor.Index, Is.EqualTo(0));

            WorldDataLayerRegistryBuilder otherBuilder = new WorldDataLayerRegistryBuilder();
            WorldDataLayerHandle<float> otherHandle = otherBuilder.Register<float>(heightId, WorldDataLayerScope.Chunk);
            WorldDataLayerRegistry otherRegistry = otherBuilder.Build();
            Assert.That(otherRegistry.Generation, Is.Not.EqualTo(registry.Generation));
            Assert.That(otherRegistry.IsHandleValid(height), Is.False);
            Assert.That(registry.IsHandleValid(otherHandle), Is.False);
        }

        [Test]
        public void DataLayerRegistryRejectsDuplicateInvalidAndPostBuildRegistration()
        {
            WorldDataLayerRegistryBuilder builder = new WorldDataLayerRegistryBuilder();
            WorldDataLayerId id = WorldDataLayerId.From("terrain.height");
            builder.Register<float>(id, WorldDataLayerScope.Chunk);

            Assert.That(builder.TryRegister(id, WorldDataLayerScope.Chunk, out WorldDataLayerHandle<int> duplicate, out WorldDataLayerRegistrationError error), Is.False);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldDataLayerRegistrationError.DuplicateId));

            Assert.That(builder.TryRegister(default(WorldDataLayerId), WorldDataLayerScope.Chunk, out WorldDataLayerHandle<int> invalid, out error), Is.False);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldDataLayerRegistrationError.InvalidId));

            builder.Build();
            Assert.That(builder.TryRegister(WorldDataLayerId.From("terrain.moisture"), WorldDataLayerScope.Chunk, out WorldDataLayerHandle<float> frozen, out error), Is.False);
            Assert.That(frozen.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldDataLayerRegistrationError.RegistryFrozen));
            Assert.That(() => builder.Build(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TypedLayerStoresKeepWorldRegionAndChunkPayloadsIndependent()
        {
            WorldDataLayerRegistryBuilder builder = new WorldDataLayerRegistryBuilder();
            WorldDataLayerHandle<string> worldHandle = builder.Register<string>(
                WorldDataLayerId.From("world.metadata"), WorldDataLayerScope.World);
            WorldDataLayerHandle<int> regionHandle = builder.Register<int>(
                WorldDataLayerId.From("region.danger"), WorldDataLayerScope.Region);
            WorldDataLayerHandle<float> chunkHandle = builder.Register<float>(
                WorldDataLayerId.From("chunk.temperature"), WorldDataLayerScope.Chunk);
            WorldDataLayerRegistry registry = builder.Build();

            WorldDataLayerStore<string> worldStore = new WorldDataLayerStore<string>(registry, worldHandle);
            WorldDataLayerStore<int> regionStore = new WorldDataLayerStore<int>(registry, regionHandle);
            WorldDataLayerStore<float> chunkStore = new WorldDataLayerStore<float>(registry, chunkHandle);

            worldStore.SetWorld("alpha");
            Assert.That(worldStore.TryGetWorld(out string worldValue), Is.True);
            Assert.That(worldValue, Is.EqualTo("alpha"));

            WorldRegionCoord region = new WorldRegionCoord(-8, 9);
            regionStore.SetRegion(region, 42);
            Assert.That(regionStore.TryGetRegion(region, out int regionValue), Is.True);
            Assert.That(regionValue, Is.EqualTo(42));

            WorldChunkCoord chunk = new WorldChunkCoord(long.MinValue + 10, long.MaxValue - 10);
            chunkStore.SetChunk(chunk, 18.5f);
            Assert.That(chunkStore.TryGetChunk(chunk, out float chunkValue), Is.True);
            Assert.That(chunkValue, Is.EqualTo(18.5f));

            Assert.That(() => chunkStore.SetWorld(1f), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => regionStore.TryGetChunk(default(WorldChunkCoord), out _), Throws.TypeOf<InvalidOperationException>());

            WorldDataLayerRegistryBuilder otherBuilder = new WorldDataLayerRegistryBuilder();
            WorldDataLayerHandle<float> foreign = otherBuilder.Register<float>(
                WorldDataLayerId.From("chunk.temperature"), WorldDataLayerScope.Chunk);
            otherBuilder.Build();
            Assert.That(() => new WorldDataLayerStore<float>(registry, foreign), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void FiniteChunkRegistryRejectsOutsideAndEnforcesLifecycleBeforeRemoval()
        {
            WorldId worldId = WorldId.From("world.finite_test");
            WorldExtent extent = WorldExtent.Finite(new WorldChunkBounds(
                new WorldChunkCoord(-2, -1),
                new WorldChunkCoord(3, 4)));
            WorldChunkRegistry registry = new WorldChunkRegistry(worldId, extent);

            WorldChunkCoord inside = new WorldChunkCoord(-2, 3);
            Assert.That(registry.TryRegister(inside).Success, Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryRegister(new WorldChunkCoord(3, 0)).Error, Is.EqualTo(WorldChunkRegistryError.OutOfExtent));
            Assert.That(registry.TryRegister(inside).Error, Is.EqualTo(WorldChunkRegistryError.AlreadyRegistered));

            WorldChunkTransitionResult transition = registry.TryTransition(inside, WorldChunkState.Metadata);
            Assert.That(transition.Success, Is.True);
            Assert.That(registry.TryRemove(inside).Error, Is.EqualTo(WorldChunkRegistryError.ChunkMustBeUnloaded));
            Assert.That(() => registry.Clear(), Throws.TypeOf<InvalidOperationException>());

            Assert.That(registry.TryTransition(inside, WorldChunkState.Unloaded).Success, Is.True);
            Assert.That(registry.TryRemove(inside).Success, Is.True);
            Assert.That(registry.Count, Is.EqualTo(0));
            Assert.That(registry.TryTransition(inside, WorldChunkState.Metadata).Error, Is.EqualTo(WorldChunkTransitionError.InvalidChunk));
        }

        [Test]
        public void InfiniteChunkRegistryAcceptsExtremePositiveAndNegativeCoordinatesOnDemand()
        {
            WorldChunkRegistry registry = new WorldChunkRegistry(WorldId.From("world.infinite_test"), WorldExtent.Infinite);
            WorldChunkCoord negative = new WorldChunkCoord(long.MinValue, -9000000000000L);
            WorldChunkCoord positive = new WorldChunkCoord(long.MaxValue, 9000000000000L);

            Assert.That(registry.TryRegister(negative).Success, Is.True);
            Assert.That(registry.TryRegister(positive).Success, Is.True);
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.TryGetState(negative, out WorldChunkState state), Is.True);
            Assert.That(state, Is.EqualTo(WorldChunkState.Unloaded));
        }

        [Test]
        public void DirtyTrackerIsIdempotentAndWritesStableActiveInsertionOrder()
        {
            WorldDirtyChunkTracker tracker = new WorldDirtyChunkTracker();
            WorldChunkCoord a = new WorldChunkCoord(1, 1);
            WorldChunkCoord b = new WorldChunkCoord(-2, 3);
            WorldChunkCoord c = new WorldChunkCoord(9, -4);

            Assert.That(tracker.MarkDirty(a), Is.True);
            Assert.That(tracker.MarkDirty(b), Is.True);
            Assert.That(tracker.MarkDirty(c), Is.True);
            Assert.That(tracker.MarkDirty(a), Is.False);
            Assert.That(tracker.ClearDirty(b), Is.True);
            Assert.That(tracker.MarkDirty(b), Is.True);
            Assert.That(tracker.Count, Is.EqualTo(3));

            WorldChunkCoord[] output = new WorldChunkCoord[3];
            Assert.That(tracker.WriteDirty(output.AsSpan()), Is.EqualTo(3));
            Assert.That(output[0], Is.EqualTo(a));
            Assert.That(output[1], Is.EqualTo(c));
            Assert.That(output[2], Is.EqualTo(b));

            WorldChunkCoord sentinel = new WorldChunkCoord(77, 88);
            WorldChunkCoord[] tooSmall = { sentinel, sentinel };
            Assert.That(() => tracker.WriteDirty(tooSmall.AsSpan()), Throws.TypeOf<ArgumentException>());
            Assert.That(tooSmall[0], Is.EqualTo(sentinel));
            Assert.That(tooSmall[1], Is.EqualTo(sentinel));
        }

        [Test]
        public void DeltaSetPreservesOrderSnapshotsMetadataAndRejectsWrongWorld()
        {
            WorldId worldId = WorldId.From("world.delta_test");
            WorldDeltaSet set = new WorldDeltaSet(worldId);
            MutableDelta first = new MutableDelta(
                WorldDeltaTypeId.From("terrain.height_changed"),
                new WorldDeltaVersion(1),
                WorldDeltaTarget.ForChunk(worldId, new WorldChunkCoord(-4, 8)));
            MutableDelta second = new MutableDelta(
                WorldDeltaTypeId.From("resource.removed"),
                new WorldDeltaVersion(2),
                WorldDeltaTarget.ForRegion(worldId, new WorldRegionCoord(2, -3)));

            WorldDeltaRecord firstRecord = set.Append(first);
            WorldDeltaRecord secondRecord = set.Append(second);
            Assert.That(firstRecord.Sequence, Is.EqualTo(1UL));
            Assert.That(secondRecord.Sequence, Is.EqualTo(2UL));

            WorldDeltaTarget originalTarget = firstRecord.Target;
            first.TargetValue = WorldDeltaTarget.ForWorld(worldId);
            Assert.That(firstRecord.Target, Is.EqualTo(originalTarget));

            WorldDeltaRecord[] records = new WorldDeltaRecord[2];
            Assert.That(set.WriteTo(records.AsSpan()), Is.EqualTo(2));
            Assert.That(records[0].TypeId.Value, Is.EqualTo("terrain.height_changed"));
            Assert.That(records[1].TypeId.Value, Is.EqualTo("resource.removed"));

            MutableDelta wrongWorld = new MutableDelta(
                WorldDeltaTypeId.From("terrain.height_changed"),
                new WorldDeltaVersion(1),
                WorldDeltaTarget.ForWorld(WorldId.From("world.other")));
            Assert.That(set.TryAppend(wrongWorld, out _, out WorldDeltaAppendError error), Is.False);
            Assert.That(error, Is.EqualTo(WorldDeltaAppendError.WrongWorld));

            set.Clear();
            Assert.That(set.Count, Is.EqualTo(0));
            WorldDeltaRecord afterClear = set.Append(second);
            Assert.That(afterClear.Sequence, Is.EqualTo(3UL), "Clear must not recycle diagnostic sequence IDs.");
        }

        [Test]
        public void DeltaSetRejectsInvalidPayloadMetadataExplicitly()
        {
            WorldId worldId = WorldId.From("world.delta_invalid");
            WorldDeltaSet set = new WorldDeltaSet(worldId);

            Assert.That(set.TryAppend(null, out _, out WorldDeltaAppendError error), Is.False);
            Assert.That(error, Is.EqualTo(WorldDeltaAppendError.NullDelta));

            MutableDelta invalidType = new MutableDelta(default(WorldDeltaTypeId), new WorldDeltaVersion(1), WorldDeltaTarget.ForWorld(worldId));
            Assert.That(set.TryAppend(invalidType, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(WorldDeltaAppendError.InvalidTypeId));

            MutableDelta invalidVersion = new MutableDelta(WorldDeltaTypeId.From("test.delta"), default(WorldDeltaVersion), WorldDeltaTarget.ForWorld(worldId));
            Assert.That(set.TryAppend(invalidVersion, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(WorldDeltaAppendError.InvalidVersion));

            MutableDelta invalidTarget = new MutableDelta(WorldDeltaTypeId.From("test.delta"), new WorldDeltaVersion(1), default(WorldDeltaTarget));
            Assert.That(set.TryAppend(invalidTarget, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(WorldDeltaAppendError.InvalidTarget));
        }

        private sealed class MutableDelta : IWorldDelta
        {
            public WorldDeltaTypeId TypeId { get; set; }
            public WorldDeltaVersion Version { get; set; }
            public WorldDeltaTarget Target => TargetValue;
            public WorldDeltaTarget TargetValue { get; set; }

            internal MutableDelta(
                WorldDeltaTypeId typeId,
                WorldDeltaVersion version,
                WorldDeltaTarget target)
            {
                TypeId = typeId;
                Version = version;
                TargetValue = target;
            }
        }
    }
}
