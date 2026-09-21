using System;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    public static class WorldSemanticAuthoringPaint
    {
        public static void PaintBiome(
            WorldDenseOverrideLayer<int> overrides,
            in WorldSampleRect bounds,
            WorldBiomeId biomeId,
            WorldBiomeCatalog catalog)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!catalog.TryGetIndex(biomeId, out int biomeIndex))
                throw new ArgumentException("Biome ID is not registered in the supplied catalog: " + biomeId, nameof(biomeId));

            WorldAuthoringPaint.Fill(overrides, in bounds, biomeIndex);
        }

        public static void PaintSurface(
            WorldDenseOverrideLayer<int> overrides,
            in WorldSampleRect bounds,
            WorldSurfaceId surfaceId,
            WorldSurfaceCatalog catalog)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!catalog.TryGetIndex(surfaceId, out int surfaceIndex))
                throw new ArgumentException("Surface ID is not registered in the supplied catalog: " + surfaceId, nameof(surfaceId));

            WorldAuthoringPaint.Fill(overrides, in bounds, surfaceIndex);
        }
    }
}
