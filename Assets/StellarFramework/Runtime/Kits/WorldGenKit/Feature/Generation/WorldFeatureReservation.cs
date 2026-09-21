using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureReservation
    {
        public int FeatureIndex { get; }
        public WorldFeatureBounds Bounds { get; }

        public WorldFeatureReservation(int featureIndex, WorldFeatureBounds bounds)
        {
            if (featureIndex < 0) throw new ArgumentOutOfRangeException(nameof(featureIndex));
            if (!bounds.IsValid) throw new ArgumentException("Feature reservation bounds must be valid.", nameof(bounds));
            FeatureIndex = featureIndex;
            Bounds = bounds;
        }

        public static WorldFeatureReservation FromCandidate(
            in WorldFeatureCandidate candidate,
            WorldFeatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            WorldFeatureDefinition definition = catalog.GetDefinition(candidate.FeatureIndex);
            WorldFeatureBounds bounds = definition.Footprint.GetAxisAlignedBounds(
                candidate.X,
                candidate.Y,
                candidate.RotationDegrees);
            return new WorldFeatureReservation(candidate.FeatureIndex, bounds);
        }
    }
}
