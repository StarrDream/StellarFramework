using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitResourceBudgetSpacingTests
    {
        [Test]
        public void CompiledBudgetEnforcesGlobalCategoryAndResourceCapsDeterministically()
        {
            BudgetFixture fixture = CreateBudgetFixture();
            WorldResourceCategoryBudgetEntry[] categories =
            {
                new WorldResourceCategoryBudgetEntry(fixture.MineralCategory, 2)
            };
            WorldResourceBudgetEntry[] resources =
            {
                new WorldResourceBudgetEntry(fixture.Iron.Id, 1)
            };
            WorldCompiledResourceBudget budget = new WorldResourceBudget(
                4,
                categories.AsSpan(),
                resources.AsSpan()).Compile(fixture.Catalog);

            WorldSpawnCandidate[] candidates = CreateBudgetCandidates(fixture);
            WorldSpawnRecord[] first = ResolveFull(candidates, fixture.Catalog, budget, out WorldScatterResolveResult firstResult);
            Array.Reverse(candidates);
            WorldSpawnRecord[] second = ResolveFull(candidates, fixture.Catalog, budget, out WorldScatterResolveResult secondResult);

            Assert.That(firstResult.AcceptedCount, Is.EqualTo(4));
            Assert.That(firstResult.RejectedBudgetCount, Is.EqualTo(2));
            Assert.That(firstResult.RejectedOccupancyCount, Is.EqualTo(0));
            Assert.That(firstResult.RejectedSpacingCount, Is.EqualTo(0));
            Assert.That(secondResult.AcceptedCount, Is.EqualTo(firstResult.AcceptedCount));
            AssertRecordsEqual(first, second, firstResult.AcceptedCount);

            fixture.Catalog.TryGetIndex(fixture.Iron.Id, out int ironIndex);
            fixture.Catalog.TryGetIndex(fixture.Copper.Id, out int copperIndex);
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            int ironCount = 0;
            int mineralCount = 0;
            int treeCount = 0;
            for (int i = 0; i < firstResult.AcceptedCount; i++)
            {
                if (first[i].ResourceIndex == ironIndex) ironCount++;
                if (first[i].ResourceIndex == ironIndex || first[i].ResourceIndex == copperIndex) mineralCount++;
                if (first[i].ResourceIndex == treeIndex) treeCount++;
            }
            Assert.That(ironCount, Is.EqualTo(1));
            Assert.That(mineralCount, Is.EqualTo(2));
            Assert.That(treeCount, Is.EqualTo(2));
        }

        [Test]
        public void BudgetRejectedCandidateDoesNotMutateOccupancy()
        {
            OccupancyBudgetFixture fixture = CreateOccupancyBudgetFixture();
            WorldCompiledResourceBudget budget = new WorldResourceBudget(globalMaxAccepted: 1).Compile(fixture.Catalog);
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 0L, 0L, 1d, 1UL, 1d),
                new WorldSpawnCandidate(treeIndex, 1, 1L, 0L, 0.9d, 2UL, 1d)
            };
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(2, 1);
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[layout.Count];
            int[] order = new int[candidates.Length];
            int[] resourceCounts = new int[fixture.Catalog.Count];
            int[] categoryCounts = new int[fixture.Catalog.CategoryCount];
            int[] spacingHeads = new int[layout.Count];
            WorldScatterSpacingNode[] spacingNodes = new WorldScatterSpacingNode[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];

            WorldScatterResolveResult result = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(), fixture.Catalog, in layout, budget,
                occupancy.AsSpan(), order.AsSpan(), resourceCounts.AsSpan(), categoryCounts.AsSpan(),
                spacingHeads.AsSpan(), spacingNodes.AsSpan(), output.AsSpan());

            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            Assert.That(result.RejectedBudgetCount, Is.EqualTo(1));
            Assert.That(occupancy[0].Occupied.Contains(fixture.Vegetation), Is.True);
            Assert.That(occupancy[1].Occupied.IsEmpty, Is.True);
            Assert.That(occupancy[1].Excluded.IsEmpty, Is.True);
        }

        [Test]
        public void MinSpacingRejectsSameResourceInsideRadiusAndAllowsExactBoundary()
        {
            SpacingFixture fixture = CreateSpacingFixture(minSpacing: 2d);
            WorldCompiledResourceBudget budget = new WorldResourceBudget().Compile(fixture.Catalog);
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 0L, 0L, 1d, 1UL, 1d),
                new WorldSpawnCandidate(treeIndex, 1, 1L, 0L, 0.9d, 2UL, 1d),
                new WorldSpawnCandidate(treeIndex, 2, 2L, 0L, 0.8d, 3UL, 1d)
            };

            WorldSpawnRecord[] output = ResolveFull(
                candidates,
                fixture.Catalog,
                budget,
                out WorldScatterResolveResult result,
                width: 3,
                height: 1);

            Assert.That(result.AcceptedCount, Is.EqualTo(2));
            Assert.That(result.RejectedSpacingCount, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(0L));
            Assert.That(output[1].X, Is.EqualTo(2L));
        }

        [Test]
        public void MinSpacingIsPerResourceAndDoesNotBlockDifferentResourceByItself()
        {
            WorldResourceCategoryId vegetation = WorldResourceCategoryId.From("resource_category.vegetation");
            WorldResourceDefinition oak = Resource("resource.oak", vegetation, 3d);
            WorldResourceDefinition flower = Resource("resource.flower", vegetation, 3d);
            WorldResourceCatalog catalog = new WorldResourceCatalog(new[] { oak, flower }.AsSpan());
            catalog.TryGetIndex(oak.Id, out int oakIndex);
            catalog.TryGetIndex(flower.Id, out int flowerIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(oakIndex, 0, 0L, 0L, 1d, 1UL, 1d),
                new WorldSpawnCandidate(flowerIndex, 1, 1L, 0L, 0.9d, 2UL, 1d)
            };

            WorldSpawnRecord[] output = ResolveFull(
                candidates,
                catalog,
                new WorldResourceBudget().Compile(catalog),
                out WorldScatterResolveResult result,
                width: 2,
                height: 1);

            Assert.That(result.AcceptedCount, Is.EqualTo(2));
            Assert.That(result.RejectedSpacingCount, Is.EqualTo(0));
            Assert.That(output[0].ResourceIndex, Is.Not.EqualTo(output[1].ResourceIndex));
        }

        [Test]
        public void LightweightResolverRefusesToSilentlyIgnoreMinSpacing()
        {
            SpacingFixture fixture = CreateSpacingFixture(minSpacing: 2d);
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int treeIndex);
            WorldSpawnCandidate[] candidates =
            {
                new WorldSpawnCandidate(treeIndex, 0, 0L, 0L, 1d, 1UL, 1d)
            };
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[1];
            int[] order = new int[1];
            WorldSpawnRecord[] output = new WorldSpawnRecord[1];

            Assert.That(
                () => WorldResourceScatterResolver.Resolve(
                    candidates.AsSpan(), fixture.Catalog, occupancy.AsSpan(), order.AsSpan(), output.AsSpan()),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(occupancy[0].Occupied.IsEmpty, Is.True);
            Assert.That(occupancy[0].Excluded.IsEmpty, Is.True);
        }

        [Test]
        public void BudgetCompilationRejectsUnknownStableIdsBeforeResolution()
        {
            BudgetFixture fixture = CreateBudgetFixture();
            WorldResourceBudgetEntry[] resources =
            {
                new WorldResourceBudgetEntry(WorldResourceId.From("resource.missing"), 1)
            };
            WorldResourceBudget budget = new WorldResourceBudget(resourceBudgets: resources.AsSpan());
            Assert.That(() => budget.Compile(fixture.Catalog), Throws.TypeOf<InvalidOperationException>());
        }

        private static WorldSpawnRecord[] ResolveFull(
            WorldSpawnCandidate[] candidates,
            WorldResourceCatalog catalog,
            WorldCompiledResourceBudget budget,
            out WorldScatterResolveResult result,
            int width = 6,
            int height = 1)
        {
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(width, height);
            WorldOccupancyCellState[] occupancy = new WorldOccupancyCellState[layout.Count];
            int[] order = new int[candidates.Length];
            int[] resourceCounts = new int[catalog.Count];
            int[] categoryCounts = new int[catalog.CategoryCount];
            int[] spacingHeads = new int[layout.Count];
            WorldScatterSpacingNode[] spacingNodes = new WorldScatterSpacingNode[candidates.Length];
            WorldSpawnRecord[] output = new WorldSpawnRecord[candidates.Length];
            result = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(), catalog, in layout, budget,
                occupancy.AsSpan(), order.AsSpan(), resourceCounts.AsSpan(), categoryCounts.AsSpan(),
                spacingHeads.AsSpan(), spacingNodes.AsSpan(), output.AsSpan());
            return output;
        }

        private static void AssertRecordsEqual(WorldSpawnRecord[] left, WorldSpawnRecord[] right, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.That(right[i].ResourceIndex, Is.EqualTo(left[i].ResourceIndex));
                Assert.That(right[i].SampleIndex, Is.EqualTo(left[i].SampleIndex));
                Assert.That(right[i].X, Is.EqualTo(left[i].X));
                Assert.That(right[i].Y, Is.EqualTo(left[i].Y));
                Assert.That(right[i].Richness, Is.EqualTo(left[i].Richness));
            }
        }

        private static WorldSpawnCandidate[] CreateBudgetCandidates(BudgetFixture fixture)
        {
            fixture.Catalog.TryGetIndex(fixture.Iron.Id, out int iron);
            fixture.Catalog.TryGetIndex(fixture.Copper.Id, out int copper);
            fixture.Catalog.TryGetIndex(fixture.Tree.Id, out int tree);
            return new[]
            {
                new WorldSpawnCandidate(iron, 0, 0L, 0L, 1.00d, 1UL, 1d),
                new WorldSpawnCandidate(iron, 1, 1L, 0L, 0.95d, 2UL, 1d),
                new WorldSpawnCandidate(copper, 2, 2L, 0L, 0.90d, 3UL, 1d),
                new WorldSpawnCandidate(copper, 3, 3L, 0L, 0.85d, 4UL, 1d),
                new WorldSpawnCandidate(tree, 4, 4L, 0L, 0.80d, 5UL, 1d),
                new WorldSpawnCandidate(tree, 5, 5L, 0L, 0.75d, 6UL, 1d)
            };
        }

        private static BudgetFixture CreateBudgetFixture()
        {
            WorldResourceCategoryId mineral = WorldResourceCategoryId.From("resource_category.mineral");
            WorldResourceCategoryId vegetation = WorldResourceCategoryId.From("resource_category.vegetation");
            WorldResourceDefinition iron = Resource("resource.iron", mineral, 0d);
            WorldResourceDefinition copper = Resource("resource.copper", mineral, 0d);
            WorldResourceDefinition tree = Resource("resource.tree", vegetation, 0d);
            return new BudgetFixture(
                mineral,
                vegetation,
                iron,
                copper,
                tree,
                new WorldResourceCatalog(new[] { tree, copper, iron }.AsSpan()));
        }

        private static SpacingFixture CreateSpacingFixture(double minSpacing)
        {
            WorldResourceCategoryId vegetation = WorldResourceCategoryId.From("resource_category.vegetation");
            WorldResourceDefinition tree = Resource("resource.tree", vegetation, minSpacing);
            return new SpacingFixture(tree, new WorldResourceCatalog(new[] { tree }.AsSpan()));
        }

        private static OccupancyBudgetFixture CreateOccupancyBudgetFixture()
        {
            WorldOccupancyRegistryBuilder builder = new WorldOccupancyRegistryBuilder();
            WorldOccupancyTypeId vegetationId = WorldOccupancyTypeId.From("occupancy.vegetation");
            WorldOccupancyHandle vegetation = builder.Register(vegetationId);
            WorldOccupancyRegistry occupancy = builder.Build();
            WorldResourceDefinition tree = new WorldResourceDefinition(
                WorldResourceId.From("resource.tree"),
                WorldResourceCategoryId.From("resource_category.vegetation"),
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Density, 1d),
                occupancy.CreateMask(vegetationId),
                WorldOccupancyMask.None);
            return new OccupancyBudgetFixture(
                tree,
                new WorldResourceCatalog(new[] { tree }.AsSpan()),
                vegetation);
        }

        private static WorldResourceDefinition Resource(
            string id,
            WorldResourceCategoryId category,
            double minSpacing)
        {
            return new WorldResourceDefinition(
                WorldResourceId.From(id),
                category,
                new WorldResourceDistributionDefinition(
                    WorldResourceDistributionMode.Density,
                    1d,
                    clusterSize: 1,
                    richness: 1d,
                    minSpacing: minSpacing),
                WorldOccupancyMask.None,
                WorldOccupancyMask.None);
        }

        private readonly struct BudgetFixture
        {
            internal WorldResourceCategoryId MineralCategory { get; }
            internal WorldResourceCategoryId VegetationCategory { get; }
            internal WorldResourceDefinition Iron { get; }
            internal WorldResourceDefinition Copper { get; }
            internal WorldResourceDefinition Tree { get; }
            internal WorldResourceCatalog Catalog { get; }

            internal BudgetFixture(
                WorldResourceCategoryId mineralCategory,
                WorldResourceCategoryId vegetationCategory,
                WorldResourceDefinition iron,
                WorldResourceDefinition copper,
                WorldResourceDefinition tree,
                WorldResourceCatalog catalog)
            {
                MineralCategory = mineralCategory;
                VegetationCategory = vegetationCategory;
                Iron = iron;
                Copper = copper;
                Tree = tree;
                Catalog = catalog;
            }
        }

        private readonly struct SpacingFixture
        {
            internal WorldResourceDefinition Tree { get; }
            internal WorldResourceCatalog Catalog { get; }
            internal SpacingFixture(WorldResourceDefinition tree, WorldResourceCatalog catalog)
            {
                Tree = tree;
                Catalog = catalog;
            }
        }

        private readonly struct OccupancyBudgetFixture
        {
            internal WorldResourceDefinition Tree { get; }
            internal WorldResourceCatalog Catalog { get; }
            internal WorldOccupancyHandle Vegetation { get; }
            internal OccupancyBudgetFixture(
                WorldResourceDefinition tree,
                WorldResourceCatalog catalog,
                WorldOccupancyHandle vegetation)
            {
                Tree = tree;
                Catalog = catalog;
                Vegetation = vegetation;
            }
        }
    }
}
