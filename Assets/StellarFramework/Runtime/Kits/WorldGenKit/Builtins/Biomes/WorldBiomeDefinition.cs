using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldBiomeDefinition
    {
        public WorldBiomeId Id { get; }
        public WorldSurfaceId SurfaceId { get; }
        public WorldBiomeCriteria Criteria { get; }
        public int Priority { get; }

        public WorldBiomeDefinition(
            WorldBiomeId id,
            WorldSurfaceId surfaceId,
            WorldBiomeCriteria criteria,
            int priority = 0)
        {
            if (!id.IsValid) throw new ArgumentException("Biome ID must be valid.", nameof(id));
            if (!surfaceId.IsValid) throw new ArgumentException("Surface ID must be valid.", nameof(surfaceId));
            Id = id;
            SurfaceId = surfaceId;
            Criteria = criteria;
            Priority = priority;
        }
    }
}
