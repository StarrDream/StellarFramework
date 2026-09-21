using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public enum WorldFeatureKind
    {
        Landmark = 0,
        Area = 1,
        Compound = 2
    }

    public readonly struct WorldFeatureQuota
    {
        private readonly byte _isConstructed;
        public int MaxPerWorld { get; }
        public int MaxPerRegion { get; }
        public bool IsValid => _isConstructed == 1;
        public bool IsUniquePerWorld => IsValid && MaxPerWorld == 1;

        public WorldFeatureQuota(int maxPerWorld = -1, int maxPerRegion = -1)
        {
            if (maxPerWorld < -1) throw new ArgumentOutOfRangeException(nameof(maxPerWorld));
            if (maxPerRegion < -1) throw new ArgumentOutOfRangeException(nameof(maxPerRegion));
            MaxPerWorld = maxPerWorld;
            MaxPerRegion = maxPerRegion;
            _isConstructed = 1;
        }

        public static WorldFeatureQuota Unlimited() => new WorldFeatureQuota(-1, -1);
        public static WorldFeatureQuota UniquePerWorld() => new WorldFeatureQuota(1, -1);
    }

    public sealed class WorldFeatureDefinition
    {
        public WorldFeatureId Id { get; }
        public WorldFeatureCategoryId CategoryId { get; }
        public WorldFeatureKind Kind { get; }
        public WorldFeatureFootprint Footprint { get; }
        public WorldFeatureQuota Quota { get; }
        public int Priority { get; }

        public WorldFeatureDefinition(
            WorldFeatureId id,
            WorldFeatureCategoryId categoryId,
            WorldFeatureKind kind,
            WorldFeatureFootprint footprint,
            WorldFeatureQuota quota,
            int priority = 0)
        {
            if (!id.IsValid) throw new ArgumentException("Feature ID must be valid.", nameof(id));
            if (!categoryId.IsValid) throw new ArgumentException("Feature category ID must be valid.", nameof(categoryId));
            if ((int)kind < (int)WorldFeatureKind.Landmark || (int)kind > (int)WorldFeatureKind.Compound)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!footprint.IsValid) throw new ArgumentException("Feature footprint must be valid.", nameof(footprint));
            if (!quota.IsValid) throw new ArgumentException("Feature quota must be explicitly initialized.", nameof(quota));
            Id = id;
            CategoryId = categoryId;
            Kind = kind;
            Footprint = footprint;
            Quota = quota;
            Priority = priority;
        }
    }
}
