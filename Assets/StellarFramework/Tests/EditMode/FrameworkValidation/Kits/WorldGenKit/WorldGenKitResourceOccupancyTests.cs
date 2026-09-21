using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitResourceOccupancyTests
    {
        [Test]
        public void OccupancyRegistryCompilesStableIdsToBitMasksAndRejectsDuplicates()
        {
            WorldOccupancyRegistryBuilder builder = new WorldOccupancyRegistryBuilder();
            WorldOccupancyTypeId vegetationId = WorldOccupancyTypeId.From("occupancy.vegetation");
            WorldOccupancyTypeId mineralId = WorldOccupancyTypeId.From("occupancy.mineral");
            WorldOccupancyHandle vegetation = builder.Register(vegetationId);
            WorldOccupancyHandle mineral = builder.Register(mineralId);

            Assert.That(vegetation.IsValid, Is.True);
            Assert.That(mineral.IsValid, Is.True);
            Assert.That(vegetation, Is.Not.EqualTo(mineral));
            Assert.That(() => builder.Register(vegetationId), Throws.TypeOf<InvalidOperationException>());

            WorldOccupancyRegistry registry = builder.Build();
            WorldOccupancyMask mask = registry.CreateMask(vegetationId, mineralId);
            Assert.That(mask.Contains(vegetation), Is.True);
            Assert.That(mask.Contains(mineral), Is.True);
            Assert.That(registry.GetId(vegetation), Is.EqualTo(vegetationId));
        }

        [Test]
        public void FailedOccupancyAttemptLeavesCellStateUnchanged()
        {
            OccupancyFixture fixture = CreateOccupancyFixture();
            WorldOccupancyCellState state = default(WorldOccupancyCellState);
            WorldOccupancyMask building = fixture.Registry.CreateMask(fixture.BuildingId);
            WorldOccupancyMask blocksVegetation = fixture.Registry.CreateMask(fixture.VegetationId);
            state.Reserve(building, blocksVegetation);
            WorldOccupancyCellState before = state;

            WorldOccupancyMask treeOccupies = fixture.Registry.CreateMask(fixture.VegetationId);
            WorldOccupancyMask treeExcludes = fixture.Registry.CreateMask(fixture.BuildingId, fixture.RoadId);
            Assert.That(state.TryOccupy(treeOccupies, treeExcludes), Is.False);
            Assert.That(state.Occupied, Is.EqualTo(before.Occupied));
            Assert.That(state.Excluded, Is.EqualTo(before.Excluded));
        }

        [Test]
        public void TreeAndUndergroundOreCanCoexistInSameSample()
        {
            ResourceFixture fixture = CreateResourceFixture();
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            fixture.Catalog.TryGetIndex(fixture.Ore.Id, out int oreIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 100L, 200L, 0.8d, 20UL, 1d),
                new WorldSpawnCandidate(oreIndex, 0, 100L, 200L, 0.7d, 10UL, 2d)
            };
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[1];
            int[] order = new int[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];

            WorldScatterResolveResult result = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(),
                fixture.Catalog,
                occupancy.AsSpan(),
                order.AsSpan(),
                output.AsSpan());

            Assert.That(result.AcceptedCount, Is.EqualTo(2));
            Assert.That(result.RejectedOccupancyCount, Is.EqualTo(0));
            Assert.That(occupancy[0].Occupied.Contains(fixture.Vegetation), Is.True);
            Assert.That(occupancy[0].Occupied.Contains(fixture.Mineral), Is.True);
            Assert.That(occupancy[0].Occupied.Contains(fixture.Underground), Is.True);
        }

        [Test]
        public void BuildingReservationBlocksTreeButAllowsUndergroundOreWhenPolicyAllowsIt()
        {
            ResourceFixture fixture = CreateResourceFixture();
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            fixture.Catalog.TryGetIndex(fixture.Ore.Id, out int oreIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 0L, 0L, 1d, 1UL, 1d),
                new WorldSpawnCandidate(oreIndex, 0, 0L, 0L, 1d, 2UL, 1d)
            };
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[1];
            WorldOccupancyReservation[] reservations =
            {
                new WorldOccupancyReservation(
                    0,
                    fixture.Registry.CreateMask(fixture.BuildingId),
                    fixture.Registry.CreateMask(fixture.VegetationId))
            };
            WorldOccupancyReservations.Apply(reservations.AsSpan(), occupancy.AsSpan());

            int[] order = new int[2];
            WorldSpawnRecord[] output = new WorldSpawnRecord[2];
            WorldScatterResolveResult result = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(), fixture.Catalog, occupancy.AsSpan(), order.AsSpan(), output.AsSpan());

            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            Assert.That(result.RejectedOccupancyCount, Is.EqualTo(1));
            Assert.That(output[0].ResourceIndex, Is.EqualTo(oreIndex));
            Assert.That(occupancy[0].Occupied.Contains(fixture.Building), Is.True);
            Assert.That(occupancy[0].Occupied.Contains(fixture.Mineral), Is.True);
            Assert.That(occupancy[0].Occupied.Contains(fixture.Vegetation), Is.False);
        }

        [Test]
        public void ResolverOutputIsIndependentFromCandidateIterationOrder()
        {
            ResourceFixture fixture = CreateResourceFixture();
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            fixture.Catalog.TryGetIndex(fixture.Ore.Id, out int oreIndex);
            WorldSpawnCandidate tree = new WorldSpawnCandidate(treeIndex, 0, 5L, 5L, 0.5d, 90UL, 1d);
            WorldSpawnCandidate ore = new WorldSpawnCandidate(oreIndex, 0, 5L, 5L, 0.5d, 10UL, 2d);

            int[] first = ResolveResourceOrder(new[] { tree, ore }, fixture.Catalog);
            int[] second = ResolveResourceOrder(new[] { ore, tree }, fixture.Catalog);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Length, Is.EqualTo(2));
        }

        [Test]
        public void InvalidCandidateIsRejectedBeforeAnyOccupancyMutation()
        {
            ResourceFixture fixture = CreateResourceFixture();
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 0L, 0L, 1d, 1UL, 1d),
                new WorldSpawnCandidate(fixture.Catalog.Count + 10, 0, 0L, 0L, 1d, 2UL, 1d)
            };
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[1];
            int[] order = new int[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];

            Assert.That(
                () => WorldResourceScatterResolver.Resolve(
                    candidates.AsSpan(), fixture.Catalog, occupancy.AsSpan(), order.AsSpan(), output.AsSpan()),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(occupancy[0].Occupied.IsEmpty, Is.True);
            Assert.That(occupancy[0].Excluded.IsEmpty, Is.True);
        }

        private static int[] ResolveResourceOrder(WorldSpawnCandidate[] candidates, WorldResourceCatalog catalog)
        {
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[1];
            int[] order = new int[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];
            WorldScatterResolveResult result = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(), catalog, occupancy.AsSpan(), order.AsSpan(), output.AsSpan());
            int[] resourceOrder = new int[result.AcceptedCount];
            for (int i = 0; i < resourceOrder.Length; i++) resourceOrder[i] = output[i].ResourceIndex;
            return resourceOrder;
        }

        private static OccupancyFixture CreateOccupancyFixture()
        {
            WorldOccupancyRegistryBuilder builder = new WorldOccupancyRegistryBuilder();
            WorldOccupancyTypeId vegetationId = WorldOccupancyTypeId.From("occupancy.vegetation");
            WorldOccupancyTypeId mineralId = WorldOccupancyTypeId.From("occupancy.mineral");
            WorldOccupancyTypeId undergroundId = WorldOccupancyTypeId.From("occupancy.underground");
            WorldOccupancyTypeId buildingId = WorldOccupancyTypeId.From("occupancy.building");
            WorldOccupancyTypeId roadId = WorldOccupancyTypeId.From("occupancy.road");
            WorldOccupancyHandle vegetation = builder.Register(vegetationId);
            WorldOccupancyHandle mineral = builder.Register(mineralId);
            WorldOccupancyHandle underground = builder.Register(undergroundId);
            WorldOccupancyHandle building = builder.Register(buildingId);
            WorldOccupancyHandle road = builder.Register(roadId);
            return new OccupancyFixture(
                builder.Build(),
                vegetationId,
                mineralId,
                undergroundId,
                buildingId,
                roadId,
                vegetation,
                mineral,
                underground,
                building,
                road);
        }

        private static ResourceFixture CreateResourceFixture()
        {
            OccupancyFixture occupancy = CreateOccupancyFixture();
            WorldResourceDefinition tree = new WorldResourceDefinition(
                WorldResourceId.From("resource.oak_tree"),
                WorldResourceCategoryId.From("resource_category.vegetation"),
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Density, 0.25d, richness: 1d),
                occupancy.Registry.CreateMask(occupancy.VegetationId),
                occupancy.Registry.CreateMask(occupancy.BuildingId, occupancy.RoadId),
                priority: 10);
            WorldResourceDefinition ore = new WorldResourceDefinition(
                WorldResourceId.From("resource.iron_ore"),
                WorldResourceCategoryId.From("resource_category.mineral"),
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Density, 0.1d, richness: 2d),
                occupancy.Registry.CreateMask(occupancy.MineralId, occupancy.UndergroundId),
                WorldOccupancyMask.None,
                priority: 10);
            WorldResourceCatalog catalog = new WorldResourceCatalog(new[] { tree, ore }.AsSpan());
            return new ResourceFixture(occupancy, tree, ore, catalog);
        }

        private readonly struct OccupancyFixture
        {
            internal WorldOccupancyRegistry Registry { get; }
            internal WorldOccupancyTypeId VegetationId { get; }
            internal WorldOccupancyTypeId MineralId { get; }
            internal WorldOccupancyTypeId UndergroundId { get; }
            internal WorldOccupancyTypeId BuildingId { get; }
            internal WorldOccupancyTypeId RoadId { get; }
            internal WorldOccupancyHandle Vegetation { get; }
            internal WorldOccupancyHandle Mineral { get; }
            internal WorldOccupancyHandle Underground { get; }
            internal WorldOccupancyHandle Building { get; }
            internal WorldOccupancyHandle Road { get; }

            internal OccupancyFixture(
                WorldOccupancyRegistry registry,
                WorldOccupancyTypeId vegetationId,
                WorldOccupancyTypeId mineralId,
                WorldOccupancyTypeId undergroundId,
                WorldOccupancyTypeId buildingId,
                WorldOccupancyTypeId roadId,
                WorldOccupancyHandle vegetation,
                WorldOccupancyHandle mineral,
                WorldOccupancyHandle underground,
                WorldOccupancyHandle building,
                WorldOccupancyHandle road)
            {
                Registry = registry;
                VegetationId = vegetationId;
                MineralId = mineralId;
                UndergroundId = undergroundId;
                BuildingId = buildingId;
                RoadId = roadId;
                Vegetation = vegetation;
                Mineral = mineral;
                Underground = underground;
                Building = building;
                Road = road;
            }
        }

        private readonly struct ResourceFixture
        {
            internal WorldOccupancyRegistry Registry { get; }
            internal WorldOccupancyTypeId VegetationId { get; }
            internal WorldOccupancyTypeId MineralId { get; }
            internal WorldOccupancyTypeId UndergroundId { get; }
            internal WorldOccupancyTypeId BuildingId { get; }
            internal WorldOccupancyTypeId RoadId { get; }
            internal WorldOccupancyHandle Vegetation { get; }
            internal WorldOccupancyHandle Mineral { get; }
            internal WorldOccupancyHandle Underground { get; }
            internal WorldOccupancyHandle Building { get; }
            internal WorldResourceDefinition Tree { get; }
            internal WorldResourceDefinition Ore { get; }
            internal WorldResourceCatalog Catalog { get; }

            internal ResourceFixture(
                OccupancyFixture occupancy,
                WorldResourceDefinition tree,
                WorldResourceDefinition ore,
                WorldResourceCatalog catalog)
            {
                Registry = occupancy.Registry;
                VegetationId = occupancy.VegetationId;
                MineralId = occupancy.MineralId;
                UndergroundId = occupancy.UndergroundId;
                BuildingId = occupancy.BuildingId;
                RoadId = occupancy.RoadId;
                Vegetation = occupancy.Vegetation;
                Mineral = occupancy.Mineral;
                Underground = occupancy.Underground;
                Building = occupancy.Building;
                Tree = tree;
                Ore = ore;
                Catalog = catalog;
            }
        }
    }
}
