using System;
using StellarFramework.PlacementKit;

namespace StellarFramework.WorldGenKit.Feature.PlacementAdapter
{
    public readonly struct WorldFeaturePlacementBinding
    {
        public WorldFeatureId FeatureId { get; }
        public PlacementTypeId PlacementTypeId { get; }

        public WorldFeaturePlacementBinding(WorldFeatureId featureId, PlacementTypeId placementTypeId)
        {
            if (!featureId.IsValid) throw new ArgumentException("Feature ID must be valid.", nameof(featureId));
            if (!placementTypeId.IsValid) throw new ArgumentException("Placement type ID must be valid.", nameof(placementTypeId));
            FeatureId = featureId;
            PlacementTypeId = placementTypeId;
        }
    }

    public sealed class WorldFeaturePlacementProfile
    {
        private readonly WorldFeaturePlacementBinding[] _bindings;

        public WorldFeaturePlacementProfile(ReadOnlySpan<WorldFeaturePlacementBinding> bindings)
        {
            _bindings = bindings.ToArray();
            for (int i = 0; i < _bindings.Length; i++)
            {
                for (int j = i + 1; j < _bindings.Length; j++)
                {
                    if (_bindings[i].FeatureId == _bindings[j].FeatureId)
                        throw new ArgumentException("Duplicate feature placement binding: " + _bindings[i].FeatureId, nameof(bindings));
                }
            }
        }

        public WorldCompiledFeaturePlacementProfile Compile(WorldFeatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            PlacementTypeId[] placementTypes = new PlacementTypeId[catalog.Count];
            byte[] bound = new byte[catalog.Count];

            for (int i = 0; i < _bindings.Length; i++)
            {
                WorldFeaturePlacementBinding binding = _bindings[i];
                if (!catalog.TryGetIndex(binding.FeatureId, out int featureIndex))
                    throw new InvalidOperationException("Feature placement profile references unknown feature: " + binding.FeatureId);
                placementTypes[featureIndex] = binding.PlacementTypeId;
                bound[featureIndex] = 1;
            }

            return new WorldCompiledFeaturePlacementProfile(placementTypes, bound);
        }
    }

    public sealed class WorldCompiledFeaturePlacementProfile
    {
        private readonly PlacementTypeId[] _placementTypes;
        private readonly byte[] _bound;

        public int FeatureCount => _bound.Length;

        internal WorldCompiledFeaturePlacementProfile(PlacementTypeId[] placementTypes, byte[] bound)
        {
            _placementTypes = placementTypes;
            _bound = bound;
        }

        public bool TryGetPlacementType(int featureIndex, out PlacementTypeId placementTypeId)
        {
            if ((uint)featureIndex >= (uint)_bound.Length) throw new ArgumentOutOfRangeException(nameof(featureIndex));
            if (_bound[featureIndex] == 0)
            {
                placementTypeId = default(PlacementTypeId);
                return false;
            }
            placementTypeId = _placementTypes[featureIndex];
            return true;
        }
    }
}
