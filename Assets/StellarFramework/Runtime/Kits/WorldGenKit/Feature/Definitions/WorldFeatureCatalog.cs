using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Feature
{
    public sealed class WorldFeatureCatalog
    {
        private readonly WorldFeatureDefinition[] _definitions;
        private readonly int[] _stableTieRanks;
        private readonly Dictionary<WorldFeatureId, int> _indexById;

        public int Count => _definitions.Length;

        public WorldFeatureCatalog(ReadOnlySpan<WorldFeatureDefinition> definitions)
        {
            if (definitions.Length == 0) throw new ArgumentException("Feature catalog cannot be empty.", nameof(definitions));
            _definitions = definitions.ToArray();
            _indexById = new Dictionary<WorldFeatureId, int>(_definitions.Length);
            WorldFeatureId[] sortedIds = new WorldFeatureId[_definitions.Length];

            for (int i = 0; i < _definitions.Length; i++)
            {
                WorldFeatureDefinition definition = _definitions[i] ??
                    throw new ArgumentException("Feature catalog contains null definition.", nameof(definitions));
                if (_indexById.ContainsKey(definition.Id))
                    throw new ArgumentException("Duplicate feature ID: " + definition.Id, nameof(definitions));
                _indexById.Add(definition.Id, i);
                sortedIds[i] = definition.Id;
            }

            Array.Sort(sortedIds);
            Dictionary<WorldFeatureId, int> stableRankById = new Dictionary<WorldFeatureId, int>(_definitions.Length);
            for (int i = 0; i < sortedIds.Length; i++) stableRankById.Add(sortedIds[i], i);
            _stableTieRanks = new int[_definitions.Length];
            for (int i = 0; i < _definitions.Length; i++) _stableTieRanks[i] = stableRankById[_definitions[i].Id];
        }

        public WorldFeatureDefinition GetDefinition(int index)
        {
            if ((uint)index >= (uint)_definitions.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _definitions[index];
        }

        public bool TryGetIndex(WorldFeatureId id, out int index) => _indexById.TryGetValue(id, out index);
        internal int GetStableTieRank(int featureIndex) => _stableTieRanks[featureIndex];
    }
}
