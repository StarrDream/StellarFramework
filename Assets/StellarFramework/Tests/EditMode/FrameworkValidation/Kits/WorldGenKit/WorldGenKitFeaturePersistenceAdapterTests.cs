using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Feature.SaveKitAdapter;
using StellarFramework.WorldGenKit.Feature.WorldKitAdapter;
using StellarFramework.WorldKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeaturePersistenceAdapterTests
    {
        [Test]
        public void WorldKitAdapterRegistersTypedWorldScopedUsageLayer()
        {
            WorldFeatureCatalog catalog = CreateCatalog(towerFirst: true);
            WorldDataLayerRegistryBuilder builder = new WorldDataLayerRegistryBuilder();
            WorldDataLayerHandle<WorldFeatureUsageState> handle =
                WorldFeatureUsageWorldLayer.Register(builder);
            WorldDataLayerRegistry registry = builder.Build();
            WorldDataLayerStore<WorldFeatureUsageState> store =
                WorldFeatureUsageWorldLayer.CreateStore(registry, handle, catalog);

            Assert.That(store.Scope, Is.EqualTo(WorldDataLayerScope.World));
            Assert.That(store.Id, Is.EqualTo(WorldFeatureUsageWorldLayer.LayerId));
            Assert.That(store.TryGetWorld(out WorldFeatureUsageState state), Is.True);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.FeatureCount, Is.EqualTo(catalog.Count));
        }

        [Test]
        public void StableIdSnapshotRestoresCorrectlyWhenCatalogOrderChanges()
        {
            WorldFeatureCatalog sourceCatalog = CreateCatalog(towerFirst: true);
            WorldFeatureUsageState source = new WorldFeatureUsageState(sourceCatalog);
            WorldRegionCoord region = new WorldRegionCoord(-3L, 7L);
            sourceCatalog.TryGetIndex(WorldFeatureId.From("feature.tower"), out int sourceTowerIndex);
            sourceCatalog.TryGetIndex(WorldFeatureId.From("feature.paddy"), out int sourcePaddyIndex);

            WorldFeatureInstanceData[] tower = ResolveAccepted(
                sourceCatalog,
                sourceTowerIndex,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                x: 0d,
                y: 0d);
            WorldFeatureInstanceData[] paddyA = ResolveAccepted(
                sourceCatalog,
                sourcePaddyIndex,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                x: 100d,
                y: 100d);
            WorldFeatureInstanceData[] paddyB = ResolveAccepted(
                sourceCatalog,
                sourcePaddyIndex,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                x: 200d,
                y: 200d);
            int[] scratch = new int[sourceCatalog.Count];
            source.Commit(region, tower.AsSpan(), scratch.AsSpan());
            source.Commit(region, paddyA.AsSpan(), scratch.AsSpan());
            source.Commit(region, paddyB.AsSpan(), scratch.AsSpan());

            WorldFeatureUsageSnapshot snapshot = source.CaptureSnapshot();

            WorldFeatureCatalog reorderedCatalog = CreateCatalog(towerFirst: false);
            WorldFeatureUsageState restored = new WorldFeatureUsageState(reorderedCatalog);
            restored.RestoreSnapshot(snapshot);
            reorderedCatalog.TryGetIndex(WorldFeatureId.From("feature.tower"), out int restoredTowerIndex);
            reorderedCatalog.TryGetIndex(WorldFeatureId.From("feature.paddy"), out int restoredPaddyIndex);

            Assert.That(restored.WorldCounts[restoredTowerIndex], Is.EqualTo(1));
            Assert.That(restored.WorldCounts[restoredPaddyIndex], Is.EqualTo(2));
            ReadOnlySpan<int> regionCounts = restored.GetRegionCounts(region);
            Assert.That(regionCounts[restoredTowerIndex], Is.EqualTo(1));
            Assert.That(regionCounts[restoredPaddyIndex], Is.EqualTo(2));
        }

        [Test]
        public void InvalidSnapshotIsRejectedWithoutMutatingExistingUsage()
        {
            WorldFeatureCatalog catalog = CreateCatalog(towerFirst: true);
            WorldFeatureUsageState state = new WorldFeatureUsageState(catalog);
            WorldRegionCoord region = new WorldRegionCoord(1L, 2L);
            catalog.TryGetIndex(WorldFeatureId.From("feature.tower"), out int towerIndex);
            WorldFeatureInstanceData[] tower = ResolveAccepted(
                catalog,
                towerIndex,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                x: 0d,
                y: 0d);
            int[] scratch = new int[catalog.Count];
            state.Commit(region, tower.AsSpan(), scratch.AsSpan());

            WorldFeatureUsageSnapshot invalid = new WorldFeatureUsageSnapshot
            {
                WorldEntries = new[]
                {
                    new WorldFeatureUsageEntry { FeatureId = "feature.tower", Count = 1 },
                    new WorldFeatureUsageEntry { FeatureId = "feature.tower", Count = 2 }
                }
            };

            Assert.That(state.ValidateSnapshot(invalid, out string error), Is.False);
            Assert.That(error, Does.Contain("duplicate"));
            Assert.That(() => state.RestoreSnapshot(invalid), Throws.TypeOf<ArgumentException>());
            Assert.That(state.WorldCounts[towerIndex], Is.EqualTo(1));
            Assert.That(state.GetRegionCounts(region)[towerIndex], Is.EqualTo(1));
        }

        [Test]
        public void EmptyRegionCopiesAsZeroCountsWithoutCreatingRegionState()
        {
            WorldFeatureCatalog catalog = CreateCatalog(towerFirst: true);
            WorldFeatureUsageState state = new WorldFeatureUsageState(catalog);
            int[] destination = new int[catalog.Count];
            for (int i = 0; i < destination.Length; i++) destination[i] = 99;

            state.CopyRegionCounts(new WorldRegionCoord(999L, -999L), destination.AsSpan());

            for (int i = 0; i < destination.Length; i++)
                Assert.That(destination[i], Is.EqualTo(0));
            Assert.That(state.RegionCount, Is.EqualTo(0));
        }

        [Test]
        public void SaveLoadRoundTripRestoresUniqueWorldUsageAndPreventsDuplicateTower()
        {
            WorldFeatureCatalog catalog = CreateCatalog(towerFirst: true);
            WorldFeatureUsageState state = new WorldFeatureUsageState(catalog);
            WorldRegionCoord region = new WorldRegionCoord(4L, -2L);
            catalog.TryGetIndex(WorldFeatureId.From("feature.tower"), out int towerIndex);
            WorldFeatureInstanceData[] accepted = ResolveAccepted(
                catalog,
                towerIndex,
                state.WorldCounts,
                state.GetRegionCounts(region),
                x: 0d,
                y: 0d);
            int[] commitScratch = new int[catalog.Count];
            state.Commit(region, accepted.AsSpan(), commitScratch.AsSpan());
            Assert.That(state.WorldCounts[towerIndex], Is.EqualTo(1));

            InMemorySaveStorage storage = new InMemorySaveStorage();
            SaveKit.Initialize(builder => builder
                .UseStorage(storage)
                .SetApplicationVersion("feature-persistence-test"));
            Assert.That(SaveKit.Register(new WorldFeatureUsageSaveSection(state)), Is.True);
            SaveResult save = SaveKit.SaveAsync("feature-usage").GetAwaiter().GetResult();
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);

            state.Clear();
            Assert.That(state.WorldCounts[towerIndex], Is.EqualTo(0));
            SaveResult load = SaveKit.LoadAsync("feature-usage").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(state.WorldCounts[towerIndex], Is.EqualTo(1));
            Assert.That(state.GetRegionCounts(region)[towerIndex], Is.EqualTo(1));

            WorldFeatureCandidate[] secondCandidate =
            {
                new WorldFeatureCandidate(
                    towerIndex,
                    x: 100d,
                    y: 100d,
                    rotationDegrees: 0d,
                    score: 1d,
                    deterministicKey: 2UL)
            };
            int[] order = new int[1];
            int[] acceptedCounts = new int[catalog.Count];
            WorldFeatureReservation[] reservations = new WorldFeatureReservation[1];
            WorldFeatureInstanceData[] output = new WorldFeatureInstanceData[1];
            WorldFeatureResolveResult result = WorldFeatureResolver.Resolve(
                secondCandidate.AsSpan(),
                catalog,
                state.WorldCounts,
                state.GetRegionCounts(region),
                ReadOnlySpan<WorldFeatureReservation>.Empty,
                order.AsSpan(),
                acceptedCounts.AsSpan(),
                reservations.AsSpan(),
                output.AsSpan());

            Assert.That(result.AcceptedCount, Is.EqualTo(0));
            Assert.That(result.RejectedQuotaCount, Is.EqualTo(1));
        }

        private static WorldFeatureCatalog CreateCatalog(bool towerFirst)
        {
            WorldFeatureDefinition tower = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.tower"),
                WorldFeatureCategoryId.From("feature_category.landmark"),
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(4d, 4d),
                WorldFeatureQuota.UniquePerWorld(),
                priority: 100);
            WorldFeatureDefinition paddy = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.paddy"),
                WorldFeatureCategoryId.From("feature_category.agriculture"),
                WorldFeatureKind.Area,
                WorldFeatureFootprint.Rectangle(20d, 20d),
                WorldFeatureQuota.Unlimited(),
                priority: 10);
            return towerFirst
                ? new WorldFeatureCatalog(new[] { tower, paddy }.AsSpan())
                : new WorldFeatureCatalog(new[] { paddy, tower }.AsSpan());
        }

        private static WorldFeatureInstanceData[] ResolveAccepted(
            WorldFeatureCatalog catalog,
            int featureIndex,
            ReadOnlySpan<int> existingWorldCounts,
            ReadOnlySpan<int> existingRegionCounts,
            double x,
            double y)
        {
            WorldFeatureCandidate[] candidates =
            {
                new WorldFeatureCandidate(
                    featureIndex,
                    x,
                    y,
                    rotationDegrees: 0d,
                    score: 1d,
                    deterministicKey: 1UL)
            };
            int[] order = new int[1];
            int[] acceptedCounts = new int[catalog.Count];
            WorldFeatureReservation[] reservations = new WorldFeatureReservation[1];
            WorldFeatureInstanceData[] output = new WorldFeatureInstanceData[1];
            WorldFeatureResolveResult result = WorldFeatureResolver.Resolve(
                candidates.AsSpan(),
                catalog,
                existingWorldCounts,
                existingRegionCounts,
                ReadOnlySpan<WorldFeatureReservation>.Empty,
                order.AsSpan(),
                acceptedCounts.AsSpan(),
                reservations.AsSpan(),
                output.AsSpan());
            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            return new[] { output[0] };
        }
    }
}
