using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    public readonly struct WorldBuiltinDerivedDirtyRegions
    {
        public WorldSampleRect? WaterDepth { get; }
        public WorldSampleRect? Slope { get; }
        public WorldSampleRect? Biome { get; }
        public WorldSampleRect? Surface { get; }
        public WorldSampleRect? Buildable { get; }

        internal WorldBuiltinDerivedDirtyRegions(
            WorldSampleRect? waterDepth,
            WorldSampleRect? slope,
            WorldSampleRect? biome,
            WorldSampleRect? surface,
            WorldSampleRect? buildable)
        {
            WaterDepth = waterDepth;
            Slope = slope;
            Biome = biome;
            Surface = surface;
            Buildable = buildable;
        }
    }

    public static class WorldAuthoringDirtyPropagation
    {
        public static WorldBuiltinDerivedDirtyRegions FromHeightEdit(
            in WorldSampleRect heightDirty,
            in WorldPlanarSampleLayout layout)
        {
            WorldSampleRect slope = heightDirty.ExpandClamped(1, in layout);
            return new WorldBuiltinDerivedDirtyRegions(
                heightDirty,
                slope,
                slope,
                slope,
                slope);
        }

        public static WorldBuiltinDerivedDirtyRegions FromMoistureEdit(
            in WorldSampleRect moistureDirty)
        {
            return new WorldBuiltinDerivedDirtyRegions(
                null,
                null,
                moistureDirty,
                moistureDirty,
                moistureDirty);
        }

        public static WorldBuiltinDerivedDirtyRegions FromBiomePaint(
            in WorldSampleRect biomeDirty)
        {
            return new WorldBuiltinDerivedDirtyRegions(
                null,
                null,
                null,
                biomeDirty,
                biomeDirty);
        }

        public static WorldBuiltinDerivedDirtyRegions FromSurfacePaint()
        {
            return new WorldBuiltinDerivedDirtyRegions(null, null, null, null, null);
        }

        public static WorldPlanarSampleRegion ToRegion(in WorldSampleRect rect) =>
            new WorldPlanarSampleRegion(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
