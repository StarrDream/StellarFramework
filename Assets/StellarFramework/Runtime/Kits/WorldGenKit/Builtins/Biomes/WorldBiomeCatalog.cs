using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldBiomeCatalog
    {
        private readonly WorldBiomeDefinition[] _definitions;
        private readonly int[] _tieRanks;
        private readonly Dictionary<WorldBiomeId, int> _indexById;

        public int Count => _definitions.Length;
        public int FallbackIndex { get; }

        public WorldBiomeCatalog(
            ReadOnlySpan<WorldBiomeDefinition> definitions,
            WorldBiomeId fallbackId)
        {
            if (definitions.Length == 0) throw new ArgumentException("Biome catalog cannot be empty.", nameof(definitions));
            if (!fallbackId.IsValid) throw new ArgumentException("Fallback biome ID must be valid.", nameof(fallbackId));

            _definitions = definitions.ToArray();
            _indexById = new Dictionary<WorldBiomeId, int>(_definitions.Length);
            FallbackIndex = -1;

            for (int i = 0; i < _definitions.Length; i++)
            {
                WorldBiomeDefinition definition = _definitions[i] ?? throw new ArgumentException("Biome catalog contains null definition.", nameof(definitions));
                if (_indexById.ContainsKey(definition.Id))
                    throw new ArgumentException("Duplicate biome ID: " + definition.Id, nameof(definitions));
                _indexById.Add(definition.Id, i);
                if (definition.Id == fallbackId) FallbackIndex = i;
            }

            if (FallbackIndex < 0) throw new ArgumentException("Fallback biome is not present in catalog.", nameof(fallbackId));
            if (!_definitions[FallbackIndex].Criteria.IsUnconditional)
                throw new ArgumentException("Fallback biome criteria must be unconditional.", nameof(definitions));

            WorldBiomeId[] sortedIds = new WorldBiomeId[_definitions.Length];
            for (int i = 0; i < _definitions.Length; i++) sortedIds[i] = _definitions[i].Id;
            Array.Sort(sortedIds);
            Dictionary<WorldBiomeId, int> rankById = new Dictionary<WorldBiomeId, int>(_definitions.Length);
            for (int i = 0; i < sortedIds.Length; i++) rankById.Add(sortedIds[i], i);
            _tieRanks = new int[_definitions.Length];
            for (int i = 0; i < _definitions.Length; i++) _tieRanks[i] = rankById[_definitions[i].Id];
        }

        public WorldBiomeDefinition GetDefinition(int index)
        {
            if ((uint)index >= (uint)_definitions.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _definitions[index];
        }

        public bool TryGetIndex(WorldBiomeId id, out int index) => _indexById.TryGetValue(id, out index);

        internal int GetTieRank(int index) => _tieRanks[index];
    }
}
