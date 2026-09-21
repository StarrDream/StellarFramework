using System;
using StellarFramework.PlacementKit;

namespace StellarFramework.WorldGenKit.Feature.PlacementAdapter
{
    public static class WorldFeaturePlacementRequestAdapter
    {
        public static bool TryCreatePlacementRequest(in WorldFeatureCandidate candidate, WorldFeatureCatalog catalog, WorldCompiledFeaturePlacementProfile profile, out PlacementRequest request)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if ((uint)candidate.FeatureIndex >= (uint)catalog.Count)
                throw new ArgumentOutOfRangeException(nameof(candidate), "Candidate feature index is outside the catalog.");
            if (profile.FeatureCount != catalog.Count)
                throw new InvalidOperationException("Compiled placement profile and feature catalog do not have matching feature counts.");

            if (!profile.TryGetPlacementType(candidate.FeatureIndex, out PlacementTypeId placementTypeId))
            {
                request = default(PlacementRequest);
                return false;
            }

            WorldFeatureDefinition definition = catalog.GetDefinition(candidate.FeatureIndex);
            PlacementFootprint footprint = ConvertFootprint(definition.Footprint);
            request = new PlacementRequest(placementTypeId, candidate.X, candidate.Y, candidate.RotationDegrees, footprint);
            return true;
        }

        private static PlacementFootprint ConvertFootprint(WorldFeatureFootprint footprint)
        {
            if (!footprint.IsValid)
                throw new InvalidOperationException("Feature footprint must be valid before placement conversion.");
            switch (footprint.Kind)
            {
                case WorldFeatureFootprintKind.Rectangle:
                    return PlacementFootprint.Rectangle(footprint.SizeX, footprint.SizeY);
                case WorldFeatureFootprintKind.Circle:
                    return PlacementFootprint.Circle(footprint.SizeX);
                default:
                    throw new ArgumentOutOfRangeException(nameof(footprint), "Unsupported feature footprint kind.");
            }
        }
    }
}
