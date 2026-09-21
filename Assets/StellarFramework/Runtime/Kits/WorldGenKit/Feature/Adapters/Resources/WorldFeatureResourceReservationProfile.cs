using System;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.WorldGenKit.Feature.ResourcesAdapter
{
    public readonly struct WorldFeatureResourceReservationBinding
    {
        public WorldFeatureId FeatureId { get; }
        public WorldOccupancyMask Occupies { get; }
        public WorldOccupancyMask Excludes { get; }

        public WorldFeatureResourceReservationBinding(
            WorldFeatureId featureId,
            WorldOccupancyMask occupies,
            WorldOccupancyMask excludes)
        {
            if (!featureId.IsValid) throw new ArgumentException("Feature ID must be valid.", nameof(featureId));
            FeatureId = featureId;
            Occupies = occupies;
            Excludes = excludes;
        }
    }

    public sealed class WorldFeatureResourceReservationProfile
    {
        private readonly WorldFeatureResourceReservationBinding[] _bindings;

        public ReadOnlySpan<WorldFeatureResourceReservationBinding> Bindings => _bindings;

        public WorldFeatureResourceReservationProfile(ReadOnlySpan<WorldFeatureResourceReservationBinding> bindings)
        {
            _bindings = bindings.ToArray();
            for (int i = 0; i < _bindings.Length; i++)
            {
                for (int j = i + 1; j < _bindings.Length; j++)
                {
                    if (_bindings[i].FeatureId == _bindings[j].FeatureId)
                        throw new ArgumentException("Duplicate feature resource reservation binding: " + _bindings[i].FeatureId, nameof(bindings));
                }
            }
        }

        public WorldCompiledFeatureResourceReservationProfile Compile(WorldFeatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            WorldOccupancyMask[] occupies = new WorldOccupancyMask[catalog.Count];
            WorldOccupancyMask[] excludes = new WorldOccupancyMask[catalog.Count];
            byte[] bound = new byte[catalog.Count];

            for (int i = 0; i < _bindings.Length; i++)
            {
                WorldFeatureResourceReservationBinding binding = _bindings[i];
                if (!catalog.TryGetIndex(binding.FeatureId, out int featureIndex))
                    throw new InvalidOperationException("Feature reservation profile references unknown feature: " + binding.FeatureId);
                occupies[featureIndex] = binding.Occupies;
                excludes[featureIndex] = binding.Excludes;
                bound[featureIndex] = 1;
            }

            return new WorldCompiledFeatureResourceReservationProfile(occupies, excludes, bound);
        }
    }

    public sealed class WorldCompiledFeatureResourceReservationProfile
    {
        private readonly WorldOccupancyMask[] _occupies;
        private readonly WorldOccupancyMask[] _excludes;
        private readonly byte[] _bound;

        public int FeatureCount => _bound.Length;

        internal WorldCompiledFeatureResourceReservationProfile(
            WorldOccupancyMask[] occupies,
            WorldOccupancyMask[] excludes,
            byte[] bound)
        {
            _occupies = occupies;
            _excludes = excludes;
            _bound = bound;
        }

        public bool TryGet(
            int featureIndex,
            out WorldOccupancyMask occupies,
            out WorldOccupancyMask excludes)
        {
            if ((uint)featureIndex >= (uint)_bound.Length) throw new ArgumentOutOfRangeException(nameof(featureIndex));
            if (_bound[featureIndex] == 0)
            {
                occupies = WorldOccupancyMask.None;
                excludes = WorldOccupancyMask.None;
                return false;
            }
            occupies = _occupies[featureIndex];
            excludes = _excludes[featureIndex];
            return true;
        }
    }
}
