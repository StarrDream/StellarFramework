using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Authoring;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Feature.AuthoringAdapter;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeatureAuthoringAdapterTests
    {
        [Test]
        public void RicePaddyFlattenMapsAbsoluteBoundsAndReturnsDerivedDirtyRegions()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(8, 8);
            WorldGenerationRunKey runKey = new WorldGenerationRunKey(100L, 200L);
            float[] baseHeights = CreateFilled(layout.Count, 20f);
            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout);
            WorldFeatureTerrainAdaptationRequest request = new WorldFeatureTerrainAdaptationRequest(
                featureIndex: 0,
                kind: WorldFeatureTerrainAdaptationKind.Flatten,
                bounds: new WorldFeatureBounds(102d, 203d, 105d, 206d),
                primaryValue: 7d);

            WorldFeatureTerrainAuthoringApplyResult result = WorldFeatureTerrainAuthoringAdapter.Apply(
                in request,
                in runKey,
                in layout,
                baseHeights.AsSpan(),
                overrides);

            Assert.That(result.Applied, Is.True);
            Assert.That(result.AffectedBounds.HasValue, Is.True);
            Assert.That(result.AffectedBounds.Value, Is.EqualTo(new WorldSampleRect(2, 3, 3, 3)));
            Assert.That(overrides.Count, Is.EqualTo(9));
            for (int y = 3; y < 6; y++)
                for (int x = 2; x < 5; x++)
                    Assert.That(overrides.GetComposedValue(baseHeights.AsSpan(), x, y), Is.EqualTo(7f));

            WorldBuiltinDerivedDirtyRegions dirty = result.DerivedDirtyRegions.Value;
            Assert.That(dirty.WaterDepth.Value, Is.EqualTo(new WorldSampleRect(2, 3, 3, 3)));
            Assert.That(dirty.Slope.Value, Is.EqualTo(new WorldSampleRect(1, 2, 5, 5)));
            Assert.That(dirty.Biome.Value, Is.EqualTo(new WorldSampleRect(1, 2, 5, 5)));
            Assert.That(dirty.Surface.Value, Is.EqualTo(new WorldSampleRect(1, 2, 5, 5)));
            Assert.That(dirty.Buildable.Value, Is.EqualTo(new WorldSampleRect(1, 2, 5, 5)));
        }

        [Test]
        public void CarveThenFillReuseExistingAuthoringOverrideSemantics()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 4);
            WorldGenerationRunKey runKey = new WorldGenerationRunKey(0L, 0L);
            float[] baseHeights = CreateFilled(layout.Count, 10f);
            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout);
            WorldFeatureBounds bounds = new WorldFeatureBounds(0d, 0d, 2d, 2d);
            WorldFeatureTerrainAdaptationRequest carve = new WorldFeatureTerrainAdaptationRequest(
                0, WorldFeatureTerrainAdaptationKind.Carve, bounds, primaryValue: 2d);
            WorldFeatureTerrainAdaptationRequest fill = new WorldFeatureTerrainAdaptationRequest(
                0, WorldFeatureTerrainAdaptationKind.Fill, bounds, primaryValue: 3d);

            WorldFeatureTerrainAuthoringAdapter.Apply(in carve, in runKey, in layout, baseHeights.AsSpan(), overrides);
            Assert.That(overrides.GetComposedValue(baseHeights.AsSpan(), 0, 0), Is.EqualTo(8f));
            Assert.That(overrides.GetComposedValue(baseHeights.AsSpan(), 1, 1), Is.EqualTo(8f));

            WorldFeatureTerrainAuthoringAdapter.Apply(in fill, in runKey, in layout, baseHeights.AsSpan(), overrides);
            Assert.That(overrides.GetComposedValue(baseHeights.AsSpan(), 0, 0), Is.EqualTo(11f));
            Assert.That(overrides.GetComposedValue(baseHeights.AsSpan(), 1, 1), Is.EqualTo(11f));
        }

        [Test]
        public void BoundsOutsideCurrentTileReturnNotAppliedWithoutDirtyMutation()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 4);
            WorldGenerationRunKey runKey = new WorldGenerationRunKey(0L, 0L);
            float[] baseHeights = CreateFilled(layout.Count, 10f);
            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout);
            WorldFeatureTerrainAdaptationRequest request = new WorldFeatureTerrainAdaptationRequest(
                0,
                WorldFeatureTerrainAdaptationKind.Flatten,
                new WorldFeatureBounds(100d, 100d, 104d, 104d),
                primaryValue: 5d);

            WorldFeatureTerrainAuthoringApplyResult result = WorldFeatureTerrainAuthoringAdapter.Apply(
                in request, in runKey, in layout, baseHeights.AsSpan(), overrides);

            Assert.That(result.Applied, Is.False);
            Assert.That(result.AffectedBounds.HasValue, Is.False);
            Assert.That(result.DerivedDirtyRegions.HasValue, Is.False);
            Assert.That(overrides.Count, Is.EqualTo(0));
            Assert.That(overrides.HasDirtyBounds, Is.False);
        }

        [Test]
        public void CarveAndFillRejectNonPositiveAmountBeforeMutation()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 4);
            WorldGenerationRunKey runKey = default(WorldGenerationRunKey);
            float[] baseHeights = CreateFilled(layout.Count, 10f);
            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout);
            WorldFeatureTerrainAdaptationRequest request = new WorldFeatureTerrainAdaptationRequest(
                0,
                WorldFeatureTerrainAdaptationKind.Carve,
                new WorldFeatureBounds(0d, 0d, 2d, 2d),
                primaryValue: 0d);

            Assert.That(
                () => WorldFeatureTerrainAuthoringAdapter.Apply(
                    in request, in runKey, in layout, baseHeights.AsSpan(), overrides),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(overrides.Count, Is.EqualTo(0));
            Assert.That(overrides.HasDirtyBounds, Is.False);
        }

        [Test]
        public void StampRequiresExplicitApplicatorAndCanDelegateWithoutCoreFallback()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 4);
            WorldGenerationRunKey runKey = default(WorldGenerationRunKey);
            float[] baseHeights = CreateFilled(layout.Count, 10f);
            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout);
            WorldFeatureTerrainStampId stampId = WorldFeatureTerrainStampId.From("feature_stamp.village_pad");
            WorldFeatureTerrainAdaptationRequest request = new WorldFeatureTerrainAdaptationRequest(
                0,
                WorldFeatureTerrainAdaptationKind.Stamp,
                new WorldFeatureBounds(1d, 1d, 3d, 3d),
                primaryValue: 6d,
                falloff: 0.25d,
                stampId: stampId);

            Assert.That(
                () => WorldFeatureTerrainAuthoringAdapter.Apply(
                    in request, in runKey, in layout, baseHeights.AsSpan(), overrides),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(overrides.Count, Is.EqualTo(0));

            FakeStampApplicator applicator = new FakeStampApplicator();
            WorldFeatureTerrainAuthoringApplyResult result = WorldFeatureTerrainAuthoringAdapter.Apply(
                in request, in runKey, in layout, baseHeights.AsSpan(), overrides, applicator);

            Assert.That(result.Applied, Is.True);
            Assert.That(applicator.CallCount, Is.EqualTo(1));
            Assert.That(applicator.LastStampId, Is.EqualTo(stampId));
            Assert.That(applicator.LastBounds, Is.EqualTo(new WorldSampleRect(1, 1, 2, 2)));
            Assert.That(applicator.LastPrimaryValue, Is.EqualTo(6f));
            Assert.That(applicator.LastFalloff, Is.EqualTo(0.25f));
            Assert.That(overrides.GetComposedValue(baseHeights.AsSpan(), 1, 1), Is.EqualTo(6f));
        }

        private static float[] CreateFilled(int count, float value)
        {
            float[] values = new float[count];
            for (int i = 0; i < values.Length; i++) values[i] = value;
            return values;
        }

        private sealed class FakeStampApplicator : IWorldFeatureTerrainStampApplicator
        {
            internal int CallCount { get; private set; }
            internal WorldFeatureTerrainStampId LastStampId { get; private set; }
            internal WorldSampleRect LastBounds { get; private set; }
            internal float LastPrimaryValue { get; private set; }
            internal float LastFalloff { get; private set; }

            public void Apply(
                WorldFeatureTerrainStampId stampId,
                ReadOnlySpan<float> baseHeights,
                WorldDenseOverrideLayer<float> overrides,
                in WorldSampleRect bounds,
                float primaryValue,
                float falloff)
            {
                CallCount++;
                LastStampId = stampId;
                LastBounds = bounds;
                LastPrimaryValue = primaryValue;
                LastFalloff = falloff;
                WorldHeightAuthoringOperations.SetHeight(overrides, in bounds, primaryValue);
            }
        }
    }
}
