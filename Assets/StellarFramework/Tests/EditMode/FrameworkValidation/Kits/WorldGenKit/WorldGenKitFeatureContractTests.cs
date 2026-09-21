using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Feature;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeatureContractTests
    {
        [Test]
        public void StableIdsAndCatalogRejectDuplicateFeatureIds()
        {
            WorldFeatureId towerId = WorldFeatureId.From("feature.border_tower");
            WorldFeatureCategoryId landmarkCategory = WorldFeatureCategoryId.From("feature_category.landmark");
            WorldFeatureDefinition tower = Definition(towerId, landmarkCategory, WorldFeatureKind.Landmark);

            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { tower }.AsSpan());
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog.TryGetIndex(towerId, out int index), Is.True);
            Assert.That(catalog.GetDefinition(index), Is.SameAs(tower));

            Assert.That(
                () => new WorldFeatureCatalog(new[] { tower, tower }.AsSpan()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => WorldFeatureId.From(""), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void LandmarkAreaAndCompoundRemainDataKindsNotUnityTypes()
        {
            WorldFeatureCategoryId category = WorldFeatureCategoryId.From("feature_category.test");
            WorldFeatureDefinition landmark = Definition(
                WorldFeatureId.From("feature.tower"), category, WorldFeatureKind.Landmark);
            WorldFeatureDefinition area = Definition(
                WorldFeatureId.From("feature.rice_paddy"), category, WorldFeatureKind.Area);
            WorldFeatureDefinition compound = Definition(
                WorldFeatureId.From("feature.village"), category, WorldFeatureKind.Compound);

            Assert.That(landmark.Kind, Is.EqualTo(WorldFeatureKind.Landmark));
            Assert.That(area.Kind, Is.EqualTo(WorldFeatureKind.Area));
            Assert.That(compound.Kind, Is.EqualTo(WorldFeatureKind.Compound));
        }

        [Test]
        public void DefaultFootprintIsInvalidAndCannotEnterFeatureDefinition()
        {
            WorldFeatureFootprint footprint = default(WorldFeatureFootprint);
            Assert.That(footprint.IsValid, Is.False);
            Assert.That(() => footprint.GetAxisAlignedBounds(0d, 0d), Throws.TypeOf<InvalidOperationException>());

            Assert.That(
                () => new WorldFeatureDefinition(
                    WorldFeatureId.From("feature.invalid"),
                    WorldFeatureCategoryId.From("feature_category.invalid"),
                    WorldFeatureKind.Landmark,
                    footprint,
                    WorldFeatureQuota.Unlimited()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void RotatedRectangleAndCircleProduceDeterministicAxisAlignedReservationBounds()
        {
            WorldFeatureFootprint rectangle = WorldFeatureFootprint.Rectangle(10d, 4d);
            WorldFeatureBounds rotated90 = rectangle.GetAxisAlignedBounds(100d, 200d, 90d);
            Assert.That(rotated90.Width, Is.EqualTo(4d).Within(0.0000001d));
            Assert.That(rotated90.Height, Is.EqualTo(10d).Within(0.0000001d));
            Assert.That(rotated90.Contains(100d, 200d), Is.True);

            WorldFeatureFootprint circle = WorldFeatureFootprint.Circle(3d);
            WorldFeatureBounds circleBounds = circle.GetAxisAlignedBounds(-5d, 8d, 123d);
            Assert.That(circleBounds.MinX, Is.EqualTo(-8d));
            Assert.That(circleBounds.MaxX, Is.EqualTo(-2d));
            Assert.That(circleBounds.MinY, Is.EqualTo(5d));
            Assert.That(circleBounds.MaxY, Is.EqualTo(11d));
        }

        [Test]
        public void BoundsUseHalfOpenContainmentAndTouchingEdgesDoNotOverlap()
        {
            WorldFeatureBounds left = new WorldFeatureBounds(0d, 0d, 10d, 10d);
            WorldFeatureBounds touching = new WorldFeatureBounds(10d, 0d, 20d, 10d);
            WorldFeatureBounds overlapping = new WorldFeatureBounds(9.5d, 0d, 20d, 10d);

            Assert.That(left.Contains(0d, 0d), Is.True);
            Assert.That(left.Contains(10d, 5d), Is.False);
            Assert.That(left.Overlaps(in touching), Is.False);
            Assert.That(left.Overlaps(in overlapping), Is.True);
            Assert.That(default(WorldFeatureBounds).IsValid, Is.False);
        }

        [Test]
        public void QuotaSupportsUniquePerWorldAndRejectsValuesBelowUnlimitedSentinel()
        {
            WorldFeatureQuota unique = WorldFeatureQuota.UniquePerWorld();
            Assert.That(unique.IsUniquePerWorld, Is.True);
            Assert.That(unique.MaxPerWorld, Is.EqualTo(1));
            Assert.That(unique.MaxPerRegion, Is.EqualTo(-1));

            WorldFeatureQuota disabled = new WorldFeatureQuota(maxPerWorld: 0, maxPerRegion: 0);
            Assert.That(disabled.MaxPerWorld, Is.EqualTo(0));
            Assert.That(disabled.MaxPerRegion, Is.EqualTo(0));
            Assert.That(WorldFeatureQuota.Unlimited().IsValid, Is.True);
            Assert.That(default(WorldFeatureQuota).IsValid, Is.False);
            Assert.That(() => new WorldFeatureQuota(-2, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CandidateReservationUsesDefinitionFootprintAndRejectsNonFiniteCandidateData()
        {
            WorldFeatureDefinition tower = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.tower"),
                WorldFeatureCategoryId.From("feature_category.landmark"),
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(4d, 2d),
                WorldFeatureQuota.UniquePerWorld(),
                priority: 100);
            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { tower }.AsSpan());
            WorldFeatureCandidate candidate = new WorldFeatureCandidate(
                featureIndex: 0,
                x: 10d,
                y: 20d,
                rotationDegrees: 0d,
                score: 0.9d,
                deterministicKey: 123UL);

            WorldFeatureReservation reservation = WorldFeatureReservation.FromCandidate(in candidate, catalog);
            Assert.That(reservation.FeatureIndex, Is.EqualTo(0));
            Assert.That(reservation.Bounds.MinX, Is.EqualTo(8d));
            Assert.That(reservation.Bounds.MaxX, Is.EqualTo(12d));
            Assert.That(reservation.Bounds.MinY, Is.EqualTo(19d));
            Assert.That(reservation.Bounds.MaxY, Is.EqualTo(21d));

            Assert.That(
                () => new WorldFeatureCandidate(0, double.NaN, 0d, 0d, 1d, 0UL),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new WorldFeatureReservation(0, default(WorldFeatureBounds)),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TerrainAdaptationStampRequiresStableStampIdAndValidBounds()
        {
            WorldFeatureBounds bounds = new WorldFeatureBounds(0d, 0d, 8d, 8d);
            WorldFeatureTerrainAdaptationRequest flatten = new WorldFeatureTerrainAdaptationRequest(
                0,
                WorldFeatureTerrainAdaptationKind.Flatten,
                bounds,
                primaryValue: 15d,
                falloff: 2d);
            Assert.That(flatten.Kind, Is.EqualTo(WorldFeatureTerrainAdaptationKind.Flatten));
            Assert.That(flatten.StampId.IsValid, Is.False);

            WorldFeatureTerrainStampId stampId = WorldFeatureTerrainStampId.From("feature_stamp.village_pad");
            WorldFeatureTerrainAdaptationRequest stamp = new WorldFeatureTerrainAdaptationRequest(
                1,
                WorldFeatureTerrainAdaptationKind.Stamp,
                bounds,
                primaryValue: 1d,
                stampId: stampId);
            Assert.That(stamp.StampId, Is.EqualTo(stampId));

            Assert.That(
                () => new WorldFeatureTerrainAdaptationRequest(
                    0,
                    WorldFeatureTerrainAdaptationKind.Stamp,
                    bounds,
                    primaryValue: 1d),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new WorldFeatureTerrainAdaptationRequest(
                    0,
                    WorldFeatureTerrainAdaptationKind.Flatten,
                    default(WorldFeatureBounds),
                    primaryValue: 1d),
                Throws.TypeOf<ArgumentException>());
        }

        private static WorldFeatureDefinition Definition(
            WorldFeatureId id,
            WorldFeatureCategoryId category,
            WorldFeatureKind kind)
        {
            return new WorldFeatureDefinition(
                id,
                category,
                kind,
                WorldFeatureFootprint.Rectangle(4d, 4d),
                WorldFeatureQuota.Unlimited(),
                priority: 0);
        }
    }
}
