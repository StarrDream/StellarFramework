using System;
using StellarFramework.WorldGenKit.Authoring;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Feature.AuthoringAdapter
{
    public interface IWorldFeatureTerrainStampApplicator
    {
        void Apply(
            WorldFeatureTerrainStampId stampId,
            ReadOnlySpan<float> baseHeights,
            WorldDenseOverrideLayer<float> overrides,
            in WorldSampleRect bounds,
            float primaryValue,
            float falloff);
    }

    public readonly struct WorldFeatureTerrainAuthoringApplyResult
    {
        public bool Applied { get; }
        public WorldSampleRect? AffectedBounds { get; }
        public WorldBuiltinDerivedDirtyRegions? DerivedDirtyRegions { get; }

        internal WorldFeatureTerrainAuthoringApplyResult(
            bool applied,
            WorldSampleRect? affectedBounds,
            WorldBuiltinDerivedDirtyRegions? derivedDirtyRegions)
        {
            Applied = applied;
            AffectedBounds = affectedBounds;
            DerivedDirtyRegions = derivedDirtyRegions;
        }

        internal static WorldFeatureTerrainAuthoringApplyResult NotApplied =>
            new WorldFeatureTerrainAuthoringApplyResult(false, null, null);
    }

    public static class WorldFeatureTerrainAuthoringAdapter
    {
        public static WorldFeatureTerrainAuthoringApplyResult Apply(
            in WorldFeatureTerrainAdaptationRequest request,
            in WorldGenerationRunKey runKey,
            in WorldPlanarSampleLayout layout,
            ReadOnlySpan<float> baseHeights,
            WorldDenseOverrideLayer<float> overrides,
            IWorldFeatureTerrainStampApplicator stampApplicator = null)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            if (overrides.Layout != layout)
                throw new ArgumentException("Height override layout must match the supplied planar layout.", nameof(overrides));

            float primaryValue = ToFiniteFloat(request.PrimaryValue, nameof(request.PrimaryValue));
            float falloff = ToFiniteFloat(request.Falloff, nameof(request.Falloff));
            if ((request.Kind == WorldFeatureTerrainAdaptationKind.Carve || request.Kind == WorldFeatureTerrainAdaptationKind.Fill) && primaryValue <= 0f)
                throw new ArgumentOutOfRangeException(nameof(request), "Carve/Fill primary value must be > 0.");
            if (request.Kind == WorldFeatureTerrainAdaptationKind.Stamp && stampApplicator == null)
                throw new InvalidOperationException("Stamp terrain adaptation requires an explicit stamp applicator.");

            WorldFeatureBounds featureBounds = request.Bounds;
            if (!TryMapBounds(in featureBounds, in runKey, in layout, out WorldSampleRect bounds))
                return WorldFeatureTerrainAuthoringApplyResult.NotApplied;

            switch (request.Kind)
            {
                case WorldFeatureTerrainAdaptationKind.Flatten:
                    WorldHeightAuthoringOperations.Flatten(overrides, in bounds, primaryValue);
                    break;
                case WorldFeatureTerrainAdaptationKind.Carve:
                    WorldHeightAuthoringOperations.Lower(baseHeights, overrides, in bounds, primaryValue);
                    break;
                case WorldFeatureTerrainAdaptationKind.Fill:
                    WorldHeightAuthoringOperations.Raise(baseHeights, overrides, in bounds, primaryValue);
                    break;
                case WorldFeatureTerrainAdaptationKind.Stamp:
                    stampApplicator.Apply(request.StampId, baseHeights, overrides, in bounds, primaryValue, falloff);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request), "Unknown terrain adaptation kind.");
            }

            WorldBuiltinDerivedDirtyRegions dirty = WorldAuthoringDirtyPropagation.FromHeightEdit(in bounds, in layout);
            return new WorldFeatureTerrainAuthoringApplyResult(true, bounds, dirty);
        }

        public static bool TryMapBounds(
            in WorldFeatureBounds featureBounds,
            in WorldGenerationRunKey runKey,
            in WorldPlanarSampleLayout layout,
            out WorldSampleRect bounds)
        {
            if (!featureBounds.IsValid) throw new ArgumentException("Feature bounds must be valid.", nameof(featureBounds));
            int minX = ToClampedCeilIndex(featureBounds.MinX, runKey.X, layout.SampleStep, layout.Width);
            int minY = ToClampedCeilIndex(featureBounds.MinY, runKey.Y, layout.SampleStep, layout.Height);
            int maxXExclusive = ToClampedCeilIndex(featureBounds.MaxX, runKey.X, layout.SampleStep, layout.Width);
            int maxYExclusive = ToClampedCeilIndex(featureBounds.MaxY, runKey.Y, layout.SampleStep, layout.Height);
            int width = maxXExclusive - minX;
            int height = maxYExclusive - minY;
            if (width <= 0 || height <= 0)
            {
                bounds = default(WorldSampleRect);
                return false;
            }
            bounds = new WorldSampleRect(minX, minY, width, height);
            return true;
        }

        private static int ToClampedCeilIndex(double worldValue, long origin, long sampleStep, int sampleCount)
        {
            double local = (worldValue - origin) / sampleStep;
            if (local <= 0d) return 0;
            if (local >= sampleCount) return sampleCount;
            return (int)Math.Ceiling(local);
        }

        private static float ToFiniteFloat(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -float.MaxValue || value > float.MaxValue)
                throw new ArgumentOutOfRangeException(parameterName, "Value cannot be represented as a finite float.");
            return (float)value;
        }
    }
}
