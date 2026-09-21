using System;
using NUnit.Framework;
using StellarFramework.PlacementKit;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Feature.PlacementAdapter;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeaturePlacementAdapterTests
    {
        [Test]
        public void ProfileCompilesStableBindingsAndRejectsUnknownFeatureIds()
        {
            AdapterFixture fixture = CreateFixture();
            WorldFeaturePlacementProfile profile = new WorldFeaturePlacementProfile(new[]
            {
                new WorldFeaturePlacementBinding(fixture.Tower.Id, PlacementTypeId.From("placement.tower"))
            }.AsSpan());
            WorldCompiledFeaturePlacementProfile compiled = profile.Compile(fixture.Catalog);
            fixture.Catalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            Assert.That(compiled.TryGetPlacementType(towerIndex, out PlacementTypeId typeId), Is.True);
            Assert.That(typeId, Is.EqualTo(PlacementTypeId.From("placement.tower")));

            WorldFeaturePlacementProfile invalid = new WorldFeaturePlacementProfile(new[]
            {
                new WorldFeaturePlacementBinding(WorldFeatureId.From("feature.missing"), PlacementTypeId.From("placement.missing"))
            }.AsSpan());
            Assert.That(() => invalid.Compile(fixture.Catalog), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void RectangleFeatureCandidateMapsPoseAndFootprintIntoPlacementRequest()
        {
            AdapterFixture fixture = CreateFixture();
            WorldCompiledFeaturePlacementProfile profile = CompileAll(fixture);
            fixture.Catalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            WorldFeatureCandidate candidate = new WorldFeatureCandidate(towerIndex, 10d, 20d, 90d, 1d, 123UL);

            Assert.That(
                WorldFeaturePlacementRequestAdapter.TryCreatePlacementRequest(in candidate, fixture.Catalog, profile, out PlacementRequest request),
                Is.True);
            Assert.That(request.TypeId, Is.EqualTo(PlacementTypeId.From("placement.tower")));
            Assert.That(request.X, Is.EqualTo(10d));
            Assert.That(request.Y, Is.EqualTo(20d));
            Assert.That(request.RotationDegrees, Is.EqualTo(90d));
            Assert.That(request.Bounds.Width, Is.EqualTo(4d).Within(0.0000001d));
            Assert.That(request.Bounds.Height, Is.EqualTo(10d).Within(0.0000001d));
        }

        [Test]
        public void CircleFeatureCandidateMapsToPlacementCircleWithoutRotationDistortion()
        {
            AdapterFixture fixture = CreateFixture();
            WorldCompiledFeaturePlacementProfile profile = CompileAll(fixture);
            fixture.Catalog.TryGetIndex(fixture.Shipwreck.Id, out int wreckIndex);
            WorldFeatureCandidate candidate = new WorldFeatureCandidate(wreckIndex, -5d, 8d, 137d, 1d, 77UL);

            Assert.That(
                WorldFeaturePlacementRequestAdapter.TryCreatePlacementRequest(in candidate, fixture.Catalog, profile, out PlacementRequest request),
                Is.True);
            Assert.That(request.Footprint.Kind, Is.EqualTo(PlacementFootprintKind.Circle));
            Assert.That(request.Bounds.Width, Is.EqualTo(6d));
            Assert.That(request.Bounds.Height, Is.EqualTo(6d));
        }

        [Test]
        public void UnboundFeatureReturnsFalseWithoutInventingPlacementType()
        {
            AdapterFixture fixture = CreateFixture();
            WorldFeaturePlacementProfile profile = new WorldFeaturePlacementProfile(ReadOnlySpan<WorldFeaturePlacementBinding>.Empty);
            WorldCompiledFeaturePlacementProfile compiled = profile.Compile(fixture.Catalog);
            fixture.Catalog.TryGetIndex(fixture.Paddy.Id, out int paddyIndex);
            WorldFeatureCandidate candidate = new WorldFeatureCandidate(paddyIndex, 0d, 0d, 0d, 1d, 1UL);

            Assert.That(
                WorldFeaturePlacementRequestAdapter.TryCreatePlacementRequest(in candidate, fixture.Catalog, compiled, out PlacementRequest request),
                Is.False);
            Assert.That(request.TypeId.IsValid, Is.False);
        }

        [Test]
        public void TowerAndShipwreckCanUseIndependentPlacementPolicies()
        {
            AdapterFixture fixture = CreateFixture();
            WorldCompiledFeaturePlacementProfile profile = CompileAll(fixture);
            fixture.Catalog.TryGetIndex(fixture.Tower.Id, out int towerIndex);
            fixture.Catalog.TryGetIndex(fixture.Shipwreck.Id, out int wreckIndex);
            WorldFeatureCandidate towerCandidate = new WorldFeatureCandidate(towerIndex, 0d, 0d, 0d, 1d, 1UL);
            WorldFeatureCandidate wreckCandidate = new WorldFeatureCandidate(wreckIndex, 100d, 100d, 0d, 1d, 2UL);
            WorldFeaturePlacementRequestAdapter.TryCreatePlacementRequest(in towerCandidate, fixture.Catalog, profile, out PlacementRequest towerRequest);
            WorldFeaturePlacementRequestAdapter.TryCreatePlacementRequest(in wreckCandidate, fixture.Catalog, profile, out PlacementRequest wreckRequest);

            PlacementSiteFacts steepLand = new PlacementSiteFacts(30d, 0d, 0d, 0UL, 0UL, 0UL);
            IPlacementRule<PlacementSiteFacts>[] towerRules = { new PlacementSlopeRule(12d) };
            PlacementFailureRecord[] towerFailures = new PlacementFailureRecord[1];
            PlacementEvaluationResult towerResult = PlacementEvaluator.Evaluate(
                in towerRequest, in steepLand, towerRules.AsSpan(), towerFailures.AsSpan());
            Assert.That(towerResult.Allowed, Is.False);
            Assert.That(towerFailures[0].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.Slope));

            PlacementSiteFacts deepWater = new PlacementSiteFacts(0d, 2d, 5d, 0UL, 0UL, 0UL);
            IPlacementRule<PlacementSiteFacts>[] wreckRules = { new PlacementWaterDepthRule(1d, 10d) };
            PlacementFailureRecord[] wreckFailures = new PlacementFailureRecord[1];
            PlacementEvaluationResult wreckResult = PlacementEvaluator.Evaluate(
                in wreckRequest, in deepWater, wreckRules.AsSpan(), wreckFailures.AsSpan());
            Assert.That(wreckResult.Allowed, Is.True);
            Assert.That(wreckResult.FailureCount, Is.EqualTo(0));
        }

        private static WorldCompiledFeaturePlacementProfile CompileAll(AdapterFixture fixture)
        {
            return new WorldFeaturePlacementProfile(new[]
            {
                new WorldFeaturePlacementBinding(fixture.Tower.Id, PlacementTypeId.From("placement.tower")),
                new WorldFeaturePlacementBinding(fixture.Paddy.Id, PlacementTypeId.From("placement.rice_paddy")),
                new WorldFeaturePlacementBinding(fixture.Village.Id, PlacementTypeId.From("placement.village")),
                new WorldFeaturePlacementBinding(fixture.Shipwreck.Id, PlacementTypeId.From("placement.shipwreck"))
            }.AsSpan()).Compile(fixture.Catalog);
        }

        private static AdapterFixture CreateFixture()
        {
            WorldFeatureCategoryId category = WorldFeatureCategoryId.From("feature_category.test");
            WorldFeatureDefinition tower = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.tower"), category, WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(10d, 4d), WorldFeatureQuota.UniquePerWorld(), 100);
            WorldFeatureDefinition paddy = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.rice_paddy"), category, WorldFeatureKind.Area,
                WorldFeatureFootprint.Rectangle(20d, 12d), WorldFeatureQuota.Unlimited(), 20);
            WorldFeatureDefinition village = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.village"), category, WorldFeatureKind.Compound,
                WorldFeatureFootprint.Rectangle(60d, 60d), new WorldFeatureQuota(-1, 2), 50);
            WorldFeatureDefinition shipwreck = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.shipwreck"), category, WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Circle(3d), WorldFeatureQuota.Unlimited(), 30);
            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { paddy, shipwreck, village, tower }.AsSpan());
            return new AdapterFixture(tower, paddy, village, shipwreck, catalog);
        }

        private readonly struct AdapterFixture
        {
            internal WorldFeatureDefinition Tower { get; }
            internal WorldFeatureDefinition Paddy { get; }
            internal WorldFeatureDefinition Village { get; }
            internal WorldFeatureDefinition Shipwreck { get; }
            internal WorldFeatureCatalog Catalog { get; }

            internal AdapterFixture(WorldFeatureDefinition tower, WorldFeatureDefinition paddy, WorldFeatureDefinition village, WorldFeatureDefinition shipwreck, WorldFeatureCatalog catalog)
            {
                Tower = tower;
                Paddy = paddy;
                Village = village;
                Shipwreck = shipwreck;
                Catalog = catalog;
            }
        }
    }
}
