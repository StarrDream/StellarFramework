using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Feature;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeatureResolverTests
    {
        [Test]
        public void HigherPriorityWinsOverlappingReservationRegardlessOfInputOrder()
        {
            ResolverFixture fixture = CreateFixture();
            fixture.Catalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            fixture.Catalog.TryGetIndex(fixture.Paddy.Id, out int paddyIndex);
            WorldFeatureCandidate tower = Candidate(towerIndex, 10d, 10d, score: 0.5d, key: 20UL);
            WorldFeatureCandidate paddy = Candidate(paddyIndex, 10d, 10d, score: 1d, key: 10UL);

            WorldFeatureInstanceData[] first = Resolve(
                new[] { paddy, tower }, fixture.Catalog, out WorldFeatureResolveResult firstResult);
            WorldFeatureInstanceData[] second = Resolve(
                new[] { tower, paddy }, fixture.Catalog, out WorldFeatureResolveResult secondResult);

            Assert.That(firstResult.AcceptedCount, Is.EqualTo(1));
            Assert.That(firstResult.RejectedReservationCount, Is.EqualTo(1));
            Assert.That(secondResult.AcceptedCount, Is.EqualTo(1));
            Assert.That(first[0].FeatureIndex, Is.EqualTo(towerIndex));
            Assert.That(second[0].FeatureIndex, Is.EqualTo(towerIndex));
        }

        [Test]
        public void ScoreThenDeterministicKeyBreakTiesDeterministically()
        {
            WorldFeatureCategoryId category = WorldFeatureCategoryId.From("feature_category.tie");
            WorldFeatureDefinition alpha = Definition("feature.alpha", category, priority: 10);
            WorldFeatureDefinition beta = Definition("feature.beta", category, priority: 10);
            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { beta, alpha }.AsSpan());
            catalog.TryGetIndex(alpha.Id, out int alphaIndex);
            catalog.TryGetIndex(beta.Id, out int betaIndex);

            WorldFeatureCandidate[] candidates =
            {
                Candidate(alphaIndex, 0d, 0d, score: 0.9d, key: 50UL),
                Candidate(betaIndex, 0d, 0d, score: 1.0d, key: 99UL)
            };
            WorldFeatureInstanceData[] output = Resolve(candidates, catalog, out WorldFeatureResolveResult result);
            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            Assert.That(output[0].FeatureIndex, Is.EqualTo(betaIndex));

            candidates = new[]
            {
                Candidate(alphaIndex, 0d, 0d, score: 1d, key: 5UL),
                Candidate(betaIndex, 0d, 0d, score: 1d, key: 9UL)
            };
            output = Resolve(candidates, catalog, out result);
            Assert.That(output[0].FeatureIndex, Is.EqualTo(alphaIndex));
        }

        [Test]
        public void ExistingReservationBlocksOverlappingCandidate()
        {
            ResolverFixture fixture = CreateFixture();
            fixture.Catalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            WorldFeatureReservation[] existing =
            {
                new WorldFeatureReservation(
                    towerIndex,
                    new WorldFeatureBounds(8d, 8d, 12d, 12d))
            };
            WorldFeatureCandidate[] candidates =
            {
                Candidate(towerIndex, 10d, 10d, 1d, 1UL)
            };

            Resolve(
                candidates,
                fixture.Catalog,
                out WorldFeatureResolveResult result,
                existingReservations: existing);

            Assert.That(result.AcceptedCount, Is.EqualTo(0));
            Assert.That(result.RejectedReservationCount, Is.EqualTo(1));
            Assert.That(result.RejectedQuotaCount, Is.EqualTo(0));
        }

        [Test]
        public void UniquePerWorldQuotaRejectsWhenExternalWorldAlreadyContainsFeature()
        {
            ResolverFixture fixture = CreateFixture();
            fixture.Catalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            int[] worldCounts = new int[fixture.Catalog.Count];
            worldCounts[towerIndex] = 1;

            Resolve(
                new[] { Candidate(towerIndex, 100d, 100d, 1d, 1UL) },
                fixture.Catalog,
                out WorldFeatureResolveResult result,
                existingWorldCounts: worldCounts);

            Assert.That(result.AcceptedCount, Is.EqualTo(0));
            Assert.That(result.RejectedQuotaCount, Is.EqualTo(1));
        }

        [Test]
        public void PerRegionQuotaCountsCandidatesAcceptedInSameResolveCall()
        {
            WorldFeatureCategoryId category = WorldFeatureCategoryId.From("feature_category.region");
            WorldFeatureDefinition paddy = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.paddy"),
                category,
                WorldFeatureKind.Area,
                WorldFeatureFootprint.Rectangle(4d, 4d),
                new WorldFeatureQuota(maxPerWorld: -1, maxPerRegion: 1),
                priority: 10);
            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { paddy }.AsSpan());
            WorldFeatureCandidate[] candidates =
            {
                Candidate(0, 0d, 0d, 1d, 1UL),
                Candidate(0, 100d, 100d, 0.9d, 2UL)
            };

            WorldFeatureInstanceData[] output = Resolve(candidates, catalog, out WorldFeatureResolveResult result);
            Assert.That(result.AcceptedCount, Is.EqualTo(1));
            Assert.That(result.RejectedQuotaCount, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(0d));
        }

        [Test]
        public void NonOverlappingCandidatesCanBothBeAccepted()
        {
            ResolverFixture fixture = CreateFixture();
            fixture.Catalog.TryGetIndex(fixture.Paddy.Id, out int paddyIndex);
            WorldFeatureCandidate[] candidates =
            {
                Candidate(paddyIndex, 0d, 0d, 1d, 1UL),
                Candidate(paddyIndex, 100d, 100d, 0.9d, 2UL)
            };

            WorldFeatureInstanceData[] output = Resolve(candidates, fixture.Catalog, out WorldFeatureResolveResult result);
            Assert.That(result.AcceptedCount, Is.EqualTo(2));
            Assert.That(result.RejectedReservationCount, Is.EqualTo(0));
            Assert.That(output[0].DeterministicKey, Is.EqualTo(1UL));
            Assert.That(output[1].DeterministicKey, Is.EqualTo(2UL));
        }

        [Test]
        public void InvalidCandidateIndexFailsBeforeAcceptedCountsOrReservationOutputsAreMutated()
        {
            ResolverFixture fixture = CreateFixture();
            fixture.Catalog.TryGetIndex(fixture.Paddy.Id, out int paddyIndex);
            WorldFeatureCandidate[] candidates =
            {
                Candidate(paddyIndex, 0d, 0d, 1d, 1UL),
                Candidate(fixture.Catalog.Count + 10, 100d, 100d, 0.5d, 2UL)
            };
            int[] order = new int[candidates.Length];
            int[] acceptedCounts = new int[fixture.Catalog.Count];
            for (int i = 0; i < acceptedCounts.Length; i++) acceptedCounts[i] = 77;
            WorldFeatureReservation sentinel = new WorldFeatureReservation(
                paddyIndex,
                new WorldFeatureBounds(-10d, -10d, -5d, -5d));
            WorldFeatureReservation[] acceptedReservations = { sentinel, sentinel };
            WorldFeatureInstanceData[] output = new WorldFeatureInstanceData[candidates.Length];

            Assert.That(
                () => WorldFeatureResolver.Resolve(
                    candidates.AsSpan(),
                    fixture.Catalog,
                    ReadOnlySpan<int>.Empty,
                    ReadOnlySpan<int>.Empty,
                    ReadOnlySpan<WorldFeatureReservation>.Empty,
                    order.AsSpan(),
                    acceptedCounts.AsSpan(),
                    acceptedReservations.AsSpan(),
                    output.AsSpan()),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            for (int i = 0; i < acceptedCounts.Length; i++) Assert.That(acceptedCounts[i], Is.EqualTo(77));
            Assert.That(acceptedReservations[0].Bounds, Is.EqualTo(sentinel.Bounds));
            Assert.That(acceptedReservations[1].Bounds, Is.EqualTo(sentinel.Bounds));
        }

        private static WorldFeatureInstanceData[] Resolve(
            WorldFeatureCandidate[] candidates,
            WorldFeatureCatalog catalog,
            out WorldFeatureResolveResult result,
            int[] existingWorldCounts = null,
            int[] existingRegionCounts = null,
            WorldFeatureReservation[] existingReservations = null)
        {
            int[] order = new int[candidates.Length];
            int[] acceptedCounts = new int[catalog.Count];
            WorldFeatureReservation[] acceptedReservations = new WorldFeatureReservation[candidates.Length];
            WorldFeatureInstanceData[] output = new WorldFeatureInstanceData[candidates.Length];
            result = WorldFeatureResolver.Resolve(
                candidates.AsSpan(),
                catalog,
                existingWorldCounts == null ? ReadOnlySpan<int>.Empty : existingWorldCounts.AsSpan(),
                existingRegionCounts == null ? ReadOnlySpan<int>.Empty : existingRegionCounts.AsSpan(),
                existingReservations == null ? ReadOnlySpan<WorldFeatureReservation>.Empty : existingReservations.AsSpan(),
                order.AsSpan(),
                acceptedCounts.AsSpan(),
                acceptedReservations.AsSpan(),
                output.AsSpan());
            return output;
        }

        private static WorldFeatureCandidate Candidate(
            int featureIndex,
            double x,
            double y,
            double score,
            ulong key) =>
            new WorldFeatureCandidate(featureIndex, x, y, 0d, score, key);

        private static ResolverFixture CreateFixture()
        {
            WorldFeatureCategoryId landmark = WorldFeatureCategoryId.From("feature_category.landmark");
            WorldFeatureCategoryId agriculture = WorldFeatureCategoryId.From("feature_category.agriculture");
            WorldFeatureDefinition tower = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.tower"),
                landmark,
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(4d, 4d),
                WorldFeatureQuota.UniquePerWorld(),
                priority: 100);
            WorldFeatureDefinition paddy = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.paddy"),
                agriculture,
                WorldFeatureKind.Area,
                WorldFeatureFootprint.Rectangle(20d, 20d),
                WorldFeatureQuota.Unlimited(),
                priority: 10);
            return new ResolverFixture(tower, paddy, new WorldFeatureCatalog(new[] { paddy, tower }.AsSpan()));
        }

        private static WorldFeatureDefinition Definition(
            string id,
            WorldFeatureCategoryId category,
            int priority) =>
            new WorldFeatureDefinition(
                WorldFeatureId.From(id),
                category,
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(4d, 4d),
                WorldFeatureQuota.Unlimited(),
                priority);

        private readonly struct ResolverFixture
        {
            internal WorldFeatureDefinition Tower { get; }
            internal WorldFeatureDefinition Paddy { get; }
            internal WorldFeatureCatalog Catalog { get; }

            internal ResolverFixture(
                WorldFeatureDefinition tower,
                WorldFeatureDefinition paddy,
                WorldFeatureCatalog catalog)
            {
                Tower = tower;
                Paddy = paddy;
                Catalog = catalog;
            }
        }
    }
}
