using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public enum WorldFeatureTerrainAdaptationKind
    {
        Flatten = 0,
        Carve = 1,
        Fill = 2,
        Stamp = 3
    }

    public readonly struct WorldFeatureTerrainAdaptationRequest
    {
        public int FeatureIndex { get; }
        public WorldFeatureTerrainAdaptationKind Kind { get; }
        public WorldFeatureBounds Bounds { get; }
        public double PrimaryValue { get; }
        public double Falloff { get; }
        public WorldFeatureTerrainStampId StampId { get; }

        public WorldFeatureTerrainAdaptationRequest(
            int featureIndex,
            WorldFeatureTerrainAdaptationKind kind,
            WorldFeatureBounds bounds,
            double primaryValue,
            double falloff = 0d,
            WorldFeatureTerrainStampId stampId = default(WorldFeatureTerrainStampId))
        {
            if (featureIndex < 0) throw new ArgumentOutOfRangeException(nameof(featureIndex));
            if ((int)kind < (int)WorldFeatureTerrainAdaptationKind.Flatten ||
                (int)kind > (int)WorldFeatureTerrainAdaptationKind.Stamp)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (double.IsNaN(primaryValue) || double.IsInfinity(primaryValue))
                throw new ArgumentOutOfRangeException(nameof(primaryValue));
            if (double.IsNaN(falloff) || double.IsInfinity(falloff) || falloff < 0d)
                throw new ArgumentOutOfRangeException(nameof(falloff));
            if (!bounds.IsValid) throw new ArgumentException("Terrain adaptation bounds must be valid.", nameof(bounds));
            if (kind == WorldFeatureTerrainAdaptationKind.Stamp && !stampId.IsValid)
                throw new ArgumentException("Stamp terrain adaptation requires a valid stamp ID.", nameof(stampId));
            FeatureIndex = featureIndex;
            Kind = kind;
            Bounds = bounds;
            PrimaryValue = primaryValue;
            Falloff = falloff;
            StampId = stampId;
        }
    }
}
