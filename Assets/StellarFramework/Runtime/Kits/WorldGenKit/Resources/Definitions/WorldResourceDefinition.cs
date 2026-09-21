using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public sealed class WorldResourceDefinition
    {
        public WorldResourceId Id { get; }
        public WorldResourceCategoryId CategoryId { get; }
        public WorldResourceDistributionDefinition Distribution { get; }
        public WorldOccupancyMask Occupies { get; }
        public WorldOccupancyMask Excludes { get; }
        public int Priority { get; }

        public WorldResourceDefinition(
            WorldResourceId id,
            WorldResourceCategoryId categoryId,
            WorldResourceDistributionDefinition distribution,
            WorldOccupancyMask occupies,
            WorldOccupancyMask excludes,
            int priority = 0)
        {
            if (!id.IsValid) throw new ArgumentException("Resource ID must be valid.", nameof(id));
            if (!categoryId.IsValid) throw new ArgumentException("Resource category ID must be valid.", nameof(categoryId));
            Id = id;
            CategoryId = categoryId;
            Distribution = distribution;
            Occupies = occupies;
            Excludes = excludes;
            Priority = priority;
        }
    }
}
