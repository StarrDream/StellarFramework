using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Feature.ResourcesAdapter;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeatureResourcesAdapterTests
    {
        [Test]
        public void ProfileCompilesStableFeatureBindingsAndRejectsUnknownIds()
        {
            AdapterFixture fixture = CreateFixture();
            WorldFeatureResourceReservationProfile profile = new WorldFeatureResourceReservationProfile(
                new[]
                {
                    new WorldFeatureResourceReservationBinding(
                        fixture.Tower.Id,
                        fixture.BuildingMask,
                        fixture.VegetationMask)
                }.AsSpan());
            WorldCompiledFeatureResourceReservationProfile compiled = profile.Compile(fixture.FeatureCatalog);
            fixture.FeatureCatalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);

            Assert.That(compiled.TryGet(towerIndex, out WorldOccupancyMask occupies, out WorldOccupancyMask excludes), Is.True);
            Assert.That(occupies, Is.EqualTo(fixture.BuildingMask));
            Assert.That(excludes, Is.EqualTo(fixture.VegetationMask));

            WorldFeatureResourceReservationProfile unknown = new WorldFeatureResourceReservationProfile(
                new[]
                {
                    new WorldFeatureResourceReservationBinding(
                        WorldFeatureId.From("feature.missing"),
                        fixture.BuildingMask,
                        fixture.VegetationMask)
                }.AsSpan());
            Assert.That(() => unknown.Compile(fixture.FeatureCatalog), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ContinuousFeatureBoundsRasterizeToExpectedResourceSamples()
        {
            AdapterFixture fixture = CreateFixture();
            WorldCompiledFeatureResourceReservationProfile profile = CompileTowerProfile(fixture);
            fixture.FeatureCatalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            WorldFeatureReservation[] reservations =
            {
                new WorldFeatureReservation(towerIndex, new WorldFeatureBounds(1d, 1d, 3d, 3d))
            };
            WorldResourcePlanarDomain domain = new WorldResourcePlanarDomain(4, 4);
            WorldOccupancyCellState[] cells = new WorldOccupancyCellState[domain.Count];

            WorldFeatureResourceReservationApplyResult result = WorldFeatureResourceReservationRasterizer.Apply(
                reservations.AsSpan(), profile, in domain, cells.AsSpan());

            Assert.That(result.BoundFeatureCount, Is.EqualTo(1));
            Assert.That(result.ReservedSampleCount, Is.EqualTo(4));
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    bool expected = x >= 1 && x < 3 && y >= 1 && y < 3;
                    WorldOccupancyCellState cell = cells[(y * 4) + x];
                    Assert.That(cell.Occupied.Overlaps(fixture.BuildingMask), Is.EqualTo(expected), "occupied at " + x + "," + y);
                    Assert.That(cell.Excluded.Overlaps(fixture.VegetationMask), Is.EqualTo(expected), "excluded at " + x + "," + y);
                }
            }
        }

        [Test]
        public void FeatureReservationAppliedBeforeScatterBlocksTreeCandidate()
        {
            AdapterFixture fixture = CreateFixture();
            WorldCompiledFeatureResourceReservationProfile profile = CompileTowerProfile(fixture);
            fixture.FeatureCatalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            WorldResourcePlanarDomain domain = new WorldResourcePlanarDomain(2, 1);
            WorldOccupancyCellState[] cells = new WorldOccupancyCellState[domain.Count];
            WorldFeatureReservation[] reservations =
            {
                new WorldFeatureReservation(towerIndex, new WorldFeatureBounds(0d, -0.5d, 1d, 0.5d))
            };
            WorldFeatureResourceReservationRasterizer.Apply(reservations.AsSpan(), profile, in domain, cells.AsSpan());

            fixture.ResourceCatalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 0L, 0L, 1d, 1UL, 1d),
                new WorldSpawnCandidate(treeIndex, 1, 1L, 0L, 0.9d, 2UL, 1d)
            };
            int[] order = new int[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];

            WorldScatterResolveResult result = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(),
                fixture.ResourceCatalog,
                cells.AsSpan(),
                order.AsSpan(),
                output.AsSpan());

            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            Assert.That(result.RejectedOccupancyCount, Is.EqualTo(1));
            Assert.That(output[0].SampleIndex, Is.EqualTo(1));
        }

        [Test]
        public void PreExistingConflictingResourceCausesAtomicFailureBeforeAnyFeatureReservationMutation()
        {
            AdapterFixture fixture = CreateFixture();
            WorldCompiledFeatureResourceReservationProfile profile = CompileTowerProfile(fixture);
            fixture.FeatureCatalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            WorldResourcePlanarDomain domain = new WorldResourcePlanarDomain(2, 1);
            WorldOccupancyCellState[] cells = new WorldOccupancyCellState[domain.Count];
            Assert.That(cells[1].TryOccupy(fixture.VegetationMask, WorldOccupancyMask.None), Is.True);
            WorldFeatureReservation[] reservations =
            {
                new WorldFeatureReservation(towerIndex, new WorldFeatureBounds(0d, -0.5d, 2d, 0.5d))
            };

            Assert.That(
                () => WorldFeatureResourceReservationRasterizer.Apply(
                    reservations.AsSpan(), profile, in domain, cells.AsSpan()),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(cells[0].Occupied.IsEmpty, Is.True);
            Assert.That(cells[0].Excluded.IsEmpty, Is.True);
            Assert.That(cells[1].Occupied, Is.EqualTo(fixture.VegetationMask));
            Assert.That(cells[1].Excluded.IsEmpty, Is.True);
        }

        [Test]
        public void UnboundFeatureProducesNoResourceOccupancyMutation()
        {
            AdapterFixture fixture = CreateFixture();
            WorldFeatureResourceReservationProfile emptyProfile = new WorldFeatureResourceReservationProfile(
                ReadOnlySpan<WorldFeatureResourceReservationBinding>.Empty);
            WorldCompiledFeatureResourceReservationProfile compiled = emptyProfile.Compile(fixture.FeatureCatalog);
            fixture.FeatureCatalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            WorldResourcePlanarDomain domain = new WorldResourcePlanarDomain(2, 2);
            WorldOccupancyCellState[] cells = new WorldOccupancyCellState[domain.Count];
            WorldFeatureReservation[] reservations =
            {
                new WorldFeatureReservation(towerIndex, new WorldFeatureBounds(0d, 0d, 2d, 2d))
            };

            WorldFeatureResourceReservationApplyResult result = WorldFeatureResourceReservationRasterizer.Apply(
                reservations.AsSpan(), compiled, in domain, cells.AsSpan());

            Assert.That(result.BoundFeatureCount, Is.EqualTo(0));
            Assert.That(result.ReservedSampleCount, Is.EqualTo(0));
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.That(cells[i].Occupied.IsEmpty, Is.True);
                Assert.That(cells[i].Excluded.IsEmpty, Is.True);
            }
        }

        private static WorldCompiledFeatureResourceReservationProfile CompileTowerProfile(AdapterFixture fixture)
        {
            return new WorldFeatureResourceReservationProfile(
                new[]
                {
                    new WorldFeatureResourceReservationBinding(
                        fixture.Tower.Id,
                        fixture.BuildingMask,
                        fixture.VegetationMask)
                }.AsSpan()).Compile(fixture.FeatureCatalog);
        }

        private static AdapterFixture CreateFixture()
        {
            WorldOccupancyRegistryBuilder occupancyBuilder = new WorldOccupancyRegistryBuilder();
            WorldOccupancyTypeId vegetationId = WorldOccupancyTypeId.From("occupancy.vegetation");
            WorldOccupancyTypeId buildingId = WorldOccupancyTypeId.From("occupancy.building");
            occupancyBuilder.Register(vegetationId);
            occupancyBuilder.Register(buildingId);
            WorldOccupancyRegistry occupancyRegistry = occupancyBuilder.Build();
            WorldOccupancyMask vegetationMask = occupancyRegistry.CreateMask(vegetationId);
            WorldOccupancyMask buildingMask = occupancyRegistry.CreateMask(buildingId);

            WorldFeatureDefinition tower = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.tower"),
                WorldFeatureCategoryId.From("feature_category.landmark"),
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(1d, 1d),
                WorldFeatureQuota.UniquePerWorld(),
                priority: 100);
            WorldFeatureCatalog featureCatalog = new WorldFeatureCatalog(new[] { tower }.AsSpan());

            WorldResourceDefinition tree = new WorldResourceDefinition(
                WorldResourceId.From("resource.tree"),
                WorldResourceCategoryId.From("resource_category.vegetation"),
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Density, 1d),
                vegetationMask,
                buildingMask,
                priority: 10);
            WorldResourceCatalog resourceCatalog = new WorldResourceCatalog(new[] { tree }.AsSpan());

            return new AdapterFixture(
                tower,
                featureCatalog,
                tree,
                resourceCatalog,
                vegetationMask,
                buildingMask);
        }

        private readonly struct AdapterFixture
        {
            internal WorldFeatureDefinition Tower { get; }
            internal WorldFeatureCatalog FeatureCatalog { get; }
            internal WorldResourceDefinition Tree { get; }
            internal WorldResourceCatalog ResourceCatalog { get; }
            internal WorldOccupancyMask VegetationMask { get; }
            internal WorldOccupancyMask BuildingMask { get; }

            internal AdapterFixture(
                WorldFeatureDefinition tower,
                WorldFeatureCatalog featureCatalog,
                WorldResourceDefinition tree,
                WorldResourceCatalog resourceCatalog,
                WorldOccupancyMask vegetationMask,
                WorldOccupancyMask buildingMask)
            {
                Tower = tower;
                FeatureCatalog = featureCatalog;
                Tree = tree;
                ResourceCatalog = resourceCatalog;
                VegetationMask = vegetationMask;
                BuildingMask = buildingMask;
            }
        }
    }
}
