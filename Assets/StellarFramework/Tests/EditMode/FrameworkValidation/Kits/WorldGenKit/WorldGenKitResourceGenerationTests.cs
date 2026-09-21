using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitResourceGenerationTests
    {
        [Test]
        public void GenerationSettingsResolveGlobalCategoryAndResourceMultipliersAndRemainEnumerable()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourceGenerationSettings settings = CreatePlayerSettings(fixture);

            WorldResolvedResourceGenerationSettings iron = settings.Resolve(fixture.Iron);
            WorldResolvedResourceGenerationSettings copper = settings.Resolve(fixture.Copper);
            WorldResolvedResourceGenerationSettings forest = settings.Resolve(fixture.Forest);

            Assert.That(iron.Occurrence, Is.EqualTo(0.4d).Within(0.0000001d));
            Assert.That(iron.ClusterSize, Is.EqualTo(8));
            Assert.That(iron.Richness, Is.EqualTo(300d).Within(0.0000001d));
            Assert.That(copper.Occurrence, Is.EqualTo(0.1d).Within(0.0000001d));
            Assert.That(copper.ClusterSize, Is.EqualTo(8));
            Assert.That(copper.Richness, Is.EqualTo(75d).Within(0.0000001d));
            Assert.That(forest.Mode, Is.EqualTo(WorldResourceDistributionMode.Coverage));
            Assert.That(forest.Occurrence, Is.EqualTo(0.5d).Within(0.0000001d));
            Assert.That(forest.Richness, Is.EqualTo(1.5d).Within(0.0000001d));
            Assert.That(settings.ApplicationPolicy, Is.EqualTo(WorldResourceGenerationApplicationPolicy.NewChunksOnly));
            Assert.That(settings.CategoryModifiers.Length, Is.EqualTo(1));
            Assert.That(settings.ResourceModifiers.Length, Is.EqualTo(2));
            Assert.That(settings.ResourceModifiers[0].ResourceId.IsValid, Is.True);
        }

        [Test]
        public void IronX2AndCopperHalfRemainDeterministicAndMonotonicAgainstSameBaseNoise()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(64, 64, originX: -128L, originY: 256L);
            byte[] eligible = CreateFilledMask(layout.Count, 1);
            WorldSpawnCandidate[] baseOutput = new WorldSpawnCandidate[layout.Count * 8];
            WorldSpawnCandidate[] modifiedOutput = new WorldSpawnCandidate[layout.Count * 8];
            WorldCoverageSampleRank[] scratch = new WorldCoverageSampleRank[layout.Count];
            WorldGenerationSeed seed = new WorldGenerationSeed(0xABCDEFUL);

            WorldResourceGenerationSettings identity = new WorldResourceGenerationSettings(WorldResourceGenerationMultiplier.Identity);
            WorldResourceGenerationSettings player = CreateOccurrenceOnlyPlayerSettings(fixture);
            fixture.Catalog.TryGetIndex(fixture.Iron.Id, out int ironIndex);
            fixture.Catalog.TryGetIndex(fixture.Copper.Id, out int copperIndex);

            WorldResourceCandidateGenerationResult ironBase = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in layout, seed, identity.Resolve(fixture.Iron),
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, baseOutput.AsSpan(), scratch.AsSpan());
            WorldResourceCandidateGenerationResult ironModified = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in layout, seed, player.Resolve(fixture.Iron),
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, modifiedOutput.AsSpan(), scratch.AsSpan());
            Assert.That(ironModified.GeneratedCount, Is.GreaterThanOrEqualTo(ironBase.GeneratedCount));
            AssertSubset(baseOutput, ironBase.GeneratedCount, modifiedOutput, ironModified.GeneratedCount);

            WorldResourceCandidateGenerationResult copperBase = WorldResourceCandidateGenerator.Generate(
                copperIndex, fixture.Catalog, in layout, seed, identity.Resolve(fixture.Copper),
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, baseOutput.AsSpan(), scratch.AsSpan());
            WorldResourceCandidateGenerationResult copperModified = WorldResourceCandidateGenerator.Generate(
                copperIndex, fixture.Catalog, in layout, seed, player.Resolve(fixture.Copper),
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, modifiedOutput.AsSpan(), scratch.AsSpan());
            Assert.That(copperModified.GeneratedCount, Is.LessThanOrEqualTo(copperBase.GeneratedCount));
            AssertSubset(modifiedOutput, copperModified.GeneratedCount, baseOutput, copperBase.GeneratedCount);
        }

        [Test]
        public void DensityGenerationRepeatsExactlyForSameSeedRunKeyAndSettings()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(32, 24, originX: -1000L, originY: 500L);
            byte[] eligible = CreateFilledMask(layout.Count, 1);
            WorldSpawnCandidate[] first = new WorldSpawnCandidate[layout.Count * 8];
            WorldSpawnCandidate[] second = new WorldSpawnCandidate[first.Length];
            WorldCoverageSampleRank[] scratch = new WorldCoverageSampleRank[layout.Count];
            WorldGenerationSeed seed = new WorldGenerationSeed(123456789UL);
            fixture.Catalog.TryGetIndex(fixture.Iron.Id, out int ironIndex);
            WorldResolvedResourceGenerationSettings resolved = CreatePlayerSettings(fixture).Resolve(fixture.Iron);

            WorldResourceCandidateGenerationResult a = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in layout, seed, in resolved,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, first.AsSpan(), scratch.AsSpan());
            WorldResourceCandidateGenerationResult b = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in layout, seed, in resolved,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, second.AsSpan(), scratch.AsSpan());

            Assert.That(b.GeneratedCount, Is.EqualTo(a.GeneratedCount));
            for (int i = 0; i < a.GeneratedCount; i++) AssertCandidateEqual(first[i], second[i]);
        }

        [Test]
        public void ForestCoverageFiftyPercentSelectsExactEligibleTargetAndRepeats()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(10, 10, originX: 100L, originY: -50L);
            byte[] eligible = CreateFilledMask(layout.Count, 1);
            for (int i = 0; i < 20; i++) eligible[i] = 0;
            WorldSpawnCandidate[] first = new WorldSpawnCandidate[layout.Count];
            WorldSpawnCandidate[] second = new WorldSpawnCandidate[layout.Count];
            WorldCoverageSampleRank[] scratchA = new WorldCoverageSampleRank[layout.Count];
            WorldCoverageSampleRank[] scratchB = new WorldCoverageSampleRank[layout.Count];
            fixture.Catalog.TryGetIndex(fixture.Forest.Id, out int forestIndex);
            WorldResolvedResourceGenerationSettings settings = CreatePlayerSettings(fixture).Resolve(fixture.Forest);
            WorldGenerationSeed seed = new WorldGenerationSeed(42UL);

            WorldResourceCandidateGenerationResult a = WorldResourceCandidateGenerator.Generate(
                forestIndex, fixture.Catalog, in layout, seed, in settings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, first.AsSpan(), scratchA.AsSpan());
            WorldResourceCandidateGenerationResult b = WorldResourceCandidateGenerator.Generate(
                forestIndex, fixture.Catalog, in layout, seed, in settings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, second.AsSpan(), scratchB.AsSpan());

            Assert.That(a.EligibleCount, Is.EqualTo(80));
            Assert.That(a.TargetCount, Is.EqualTo(40));
            Assert.That(a.GeneratedCount, Is.EqualTo(40));
            Assert.That(b.GeneratedCount, Is.EqualTo(40));
            for (int i = 0; i < 40; i++)
            {
                Assert.That(eligible[first[i].SampleIndex], Is.EqualTo(1));
                AssertCandidateEqual(first[i], second[i]);
            }
        }

        [Test]
        public void CoveragePrioritizesCallerSuitabilityBeforeDeterministicClusterTieBreak()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(10, 10);
            byte[] eligible = CreateFilledMask(layout.Count, 1);
            float[] scores = new float[layout.Count];
            for (int i = 0; i < 10; i++) scores[i] = 1f;
            WorldSpawnCandidate[] output = new WorldSpawnCandidate[layout.Count];
            WorldCoverageSampleRank[] scratch = new WorldCoverageSampleRank[layout.Count];
            fixture.Catalog.TryGetIndex(fixture.Forest.Id, out int forestIndex);
            WorldResourceGenerationSettings tenPercent = new WorldResourceGenerationSettings(
                new WorldResourceGenerationMultiplier(0.2d, 1d, 1d));
            WorldResolvedResourceGenerationSettings settings = tenPercent.Resolve(fixture.Forest);
            WorldResourceCandidateGenerationResult result = WorldResourceCandidateGenerator.Generate(
                forestIndex, fixture.Catalog, in layout, new WorldGenerationSeed(99UL), in settings,
                eligible.AsSpan(), scores.AsSpan(), output.AsSpan(), scratch.AsSpan());

            Assert.That(result.GeneratedCount, Is.EqualTo(10));
            for (int i = 0; i < result.GeneratedCount; i++)
                Assert.That(output[i].SampleIndex, Is.LessThan(10));
        }

        [Test]
        public void ReconstructedPersistableSettingsProduceSameFutureTileCandidates()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourceGenerationSettings original = CreatePlayerSettings(fixture);
            WorldResourceCategoryModifierEntry[] categories = original.CategoryModifiers.ToArray();
            WorldResourceModifierEntry[] resources = original.ResourceModifiers.ToArray();
            WorldResourceGenerationSettings reconstructed = new WorldResourceGenerationSettings(
                original.GlobalMultiplier,
                categories.AsSpan(),
                resources.AsSpan(),
                original.ApplicationPolicy);

            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(16, 16, originX: 8192L, originY: -4096L);
            byte[] eligible = CreateFilledMask(layout.Count, 1);
            WorldSpawnCandidate[] first = new WorldSpawnCandidate[layout.Count * 8];
            WorldSpawnCandidate[] second = new WorldSpawnCandidate[first.Length];
            WorldCoverageSampleRank[] scratch = new WorldCoverageSampleRank[layout.Count];
            fixture.Catalog.TryGetIndex(fixture.Iron.Id, out int ironIndex);
            WorldResolvedResourceGenerationSettings aSettings = original.Resolve(fixture.Iron);
            WorldResolvedResourceGenerationSettings bSettings = reconstructed.Resolve(fixture.Iron);

            WorldResourceCandidateGenerationResult a = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in layout, new WorldGenerationSeed(555UL), in aSettings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, first.AsSpan(), scratch.AsSpan());
            WorldResourceCandidateGenerationResult b = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in layout, new WorldGenerationSeed(555UL), in bSettings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, second.AsSpan(), scratch.AsSpan());

            Assert.That(a.GeneratedCount, Is.EqualTo(b.GeneratedCount));
            for (int i = 0; i < a.GeneratedCount; i++) AssertCandidateEqual(first[i], second[i]);
        }

        [Test]
        public void NonFiniteSuitabilityIsRejectedBeforeGeneration()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(2, 2);
            float[] scores = { 0f, float.NaN, 0f, 0f };
            WorldSpawnCandidate[] output = new WorldSpawnCandidate[layout.Count];
            WorldCoverageSampleRank[] scratch = new WorldCoverageSampleRank[layout.Count];
            fixture.Catalog.TryGetIndex(fixture.Forest.Id, out int forestIndex);
            WorldResolvedResourceGenerationSettings settings = CreatePlayerSettings(fixture).Resolve(fixture.Forest);
            Assert.That(
                () => WorldResourceCandidateGenerator.Generate(
                    forestIndex, fixture.Catalog, in layout, new WorldGenerationSeed(1UL), in settings,
                    ReadOnlySpan<byte>.Empty, scores.AsSpan(), output.AsSpan(), scratch.AsSpan()),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AdjacentTilesProduceSameCandidatesRegardlessOfGenerationCallOrder()
        {
            ResourceGenerationFixture fixture = CreateFixture();
            WorldResourcePlanarDomain left = new WorldResourcePlanarDomain(16, 16, originX: 0L, originY: 0L);
            WorldResourcePlanarDomain right = new WorldResourcePlanarDomain(16, 16, originX: 16L, originY: 0L);
            byte[] eligible = CreateFilledMask(left.Count, 1);
            WorldCoverageSampleRank[] scratch = new WorldCoverageSampleRank[left.Count];
            WorldSpawnCandidate[] leftFirst = new WorldSpawnCandidate[left.Count * 8];
            WorldSpawnCandidate[] rightSecond = new WorldSpawnCandidate[left.Count * 8];
            WorldSpawnCandidate[] rightFirst = new WorldSpawnCandidate[left.Count * 8];
            WorldSpawnCandidate[] leftSecond = new WorldSpawnCandidate[left.Count * 8];
            fixture.Catalog.TryGetIndex(fixture.Iron.Id, out int ironIndex);
            WorldResolvedResourceGenerationSettings settings = CreatePlayerSettings(fixture).Resolve(fixture.Iron);
            WorldGenerationSeed seed = new WorldGenerationSeed(987654321UL);

            WorldResourceCandidateGenerationResult leftA = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in left, seed, in settings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, leftFirst.AsSpan(), scratch.AsSpan());
            WorldResourceCandidateGenerationResult rightA = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in right, seed, in settings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, rightSecond.AsSpan(), scratch.AsSpan());

            WorldResourceCandidateGenerationResult rightB = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in right, seed, in settings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, rightFirst.AsSpan(), scratch.AsSpan());
            WorldResourceCandidateGenerationResult leftB = WorldResourceCandidateGenerator.Generate(
                ironIndex, fixture.Catalog, in left, seed, in settings,
                eligible.AsSpan(), ReadOnlySpan<float>.Empty, leftSecond.AsSpan(), scratch.AsSpan());

            Assert.That(leftB.GeneratedCount, Is.EqualTo(leftA.GeneratedCount));
            Assert.That(rightB.GeneratedCount, Is.EqualTo(rightA.GeneratedCount));
            for (int i = 0; i < leftA.GeneratedCount; i++) AssertCandidateEqual(leftFirst[i], leftSecond[i]);
            for (int i = 0; i < rightA.GeneratedCount; i++) AssertCandidateEqual(rightSecond[i], rightFirst[i]);
        }

        private static void AssertSubset(
            WorldSpawnCandidate[] expectedSubset,
            int subsetCount,
            WorldSpawnCandidate[] superset,
            int supersetCount)
        {
            bool[] present = new bool[4096];
            for (int i = 0; i < supersetCount; i++) present[superset[i].SampleIndex] = true;
            for (int i = 0; i < subsetCount; i++)
                Assert.That(present[expectedSubset[i].SampleIndex], Is.True, "Missing sample " + expectedSubset[i].SampleIndex);
        }

        private static void AssertCandidateEqual(in WorldSpawnCandidate left, in WorldSpawnCandidate right)
        {
            Assert.That(right.ResourceIndex, Is.EqualTo(left.ResourceIndex));
            Assert.That(right.SampleIndex, Is.EqualTo(left.SampleIndex));
            Assert.That(right.X, Is.EqualTo(left.X));
            Assert.That(right.Y, Is.EqualTo(left.Y));
            Assert.That(right.Score, Is.EqualTo(left.Score));
            Assert.That(right.DeterministicKey, Is.EqualTo(left.DeterministicKey));
            Assert.That(right.Richness, Is.EqualTo(left.Richness));
        }

        private static byte[] CreateFilledMask(int count, byte value)
        {
            byte[] result = new byte[count];
            Array.Fill(result, value);
            return result;
        }

        private static WorldResourceGenerationSettings CreatePlayerSettings(ResourceGenerationFixture fixture)
        {
            WorldResourceCategoryModifierEntry[] categories =
            {
                new WorldResourceCategoryModifierEntry(
                    fixture.MineralCategory,
                    new WorldResourceGenerationMultiplier(1d, 2d, 1d))
            };
            WorldResourceModifierEntry[] resources =
            {
                new WorldResourceModifierEntry(
                    fixture.Iron.Id,
                    new WorldResourceGenerationMultiplier(2d, 1d, 2d)),
                new WorldResourceModifierEntry(
                    fixture.Copper.Id,
                    new WorldResourceGenerationMultiplier(0.5d, 1d, 1d))
            };
            return new WorldResourceGenerationSettings(
                new WorldResourceGenerationMultiplier(1d, 1d, 1.5d),
                categories.AsSpan(),
                resources.AsSpan(),
                WorldResourceGenerationApplicationPolicy.NewChunksOnly);
        }

        private static WorldResourceGenerationSettings CreateOccurrenceOnlyPlayerSettings(ResourceGenerationFixture fixture)
        {
            WorldResourceModifierEntry[] resources =
            {
                new WorldResourceModifierEntry(
                    fixture.Iron.Id,
                    new WorldResourceGenerationMultiplier(2d, 1d, 1d)),
                new WorldResourceModifierEntry(
                    fixture.Copper.Id,
                    new WorldResourceGenerationMultiplier(0.5d, 1d, 1d))
            };

            return new WorldResourceGenerationSettings(
                WorldResourceGenerationMultiplier.Identity,
                resourceModifiers: resources.AsSpan());
        }

        private static ResourceGenerationFixture CreateFixture()
        {
            WorldResourceCategoryId mineral = WorldResourceCategoryId.From("resource_category.mineral");
            WorldResourceCategoryId vegetation = WorldResourceCategoryId.From("resource_category.vegetation");
            WorldResourceDefinition iron = new WorldResourceDefinition(
                WorldResourceId.From("resource.iron_ore"),
                mineral,
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Density, 0.2d, 4, 100d),
                WorldOccupancyMask.None,
                WorldOccupancyMask.None);
            WorldResourceDefinition copper = new WorldResourceDefinition(
                WorldResourceId.From("resource.copper_ore"),
                mineral,
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Density, 0.2d, 4, 50d),
                WorldOccupancyMask.None,
                WorldOccupancyMask.None);
            WorldResourceDefinition forest = new WorldResourceDefinition(
                WorldResourceId.From("resource.forest"),
                vegetation,
                new WorldResourceDistributionDefinition(WorldResourceDistributionMode.Coverage, 0.5d, 9, 1d),
                WorldOccupancyMask.None,
                WorldOccupancyMask.None);
            return new ResourceGenerationFixture(
                mineral,
                vegetation,
                iron,
                copper,
                forest,
                new WorldResourceCatalog(new[] { forest, copper, iron }.AsSpan()));
        }

        private readonly struct ResourceGenerationFixture
        {
            internal WorldResourceCategoryId MineralCategory { get; }
            internal WorldResourceCategoryId VegetationCategory { get; }
            internal WorldResourceDefinition Iron { get; }
            internal WorldResourceDefinition Copper { get; }
            internal WorldResourceDefinition Forest { get; }
            internal WorldResourceCatalog Catalog { get; }

            internal ResourceGenerationFixture(
                WorldResourceCategoryId mineralCategory,
                WorldResourceCategoryId vegetationCategory,
                WorldResourceDefinition iron,
                WorldResourceDefinition copper,
                WorldResourceDefinition forest,
                WorldResourceCatalog catalog)
            {
                MineralCategory = mineralCategory;
                VegetationCategory = vegetationCategory;
                Iron = iron;
                Copper = copper;
                Forest = forest;
                Catalog = catalog;
            }
        }
    }
}
