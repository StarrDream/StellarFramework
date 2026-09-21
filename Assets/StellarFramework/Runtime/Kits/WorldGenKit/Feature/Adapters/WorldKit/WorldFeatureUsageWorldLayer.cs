using System;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldGenKit.Feature.WorldKitAdapter
{
    public static class WorldFeatureUsageWorldLayer
    {
        public static readonly WorldDataLayerId LayerId =
            WorldDataLayerId.From("worldgen.feature.usage");

        public static WorldDataLayerHandle<WorldFeatureUsageState> Register(
            WorldDataLayerRegistryBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return builder.Register<WorldFeatureUsageState>(
                LayerId,
                WorldDataLayerScope.World);
        }

        public static WorldDataLayerStore<WorldFeatureUsageState> CreateStore(
            WorldDataLayerRegistry registry,
            WorldDataLayerHandle<WorldFeatureUsageState> handle,
            WorldFeatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            WorldDataLayerStore<WorldFeatureUsageState> store =
                new WorldDataLayerStore<WorldFeatureUsageState>(registry, handle);
            store.SetWorld(new WorldFeatureUsageState(catalog));
            return store;
        }
    }
}
