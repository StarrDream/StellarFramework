using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Resources
{
    public sealed class WorldResourceCatalog
    {
        private readonly WorldResourceDefinition[] _definitions;
        private readonly int[] _stableTieRanks;
        private readonly WorldResourceCategoryId[] _categoryIds;
        private readonly int[] _categoryIndexByResource;
        private readonly Dictionary<WorldResourceId, int> _indexById;
        private readonly Dictionary<WorldResourceCategoryId, int> _categoryIndexById;

        public int Count => _definitions.Length;
        public int CategoryCount => _categoryIds.Length;

        public WorldResourceCatalog(ReadOnlySpan<WorldResourceDefinition> definitions)
        {
            if (definitions.Length == 0) throw new ArgumentException("Resource catalog cannot be empty.", nameof(definitions));
            _definitions = definitions.ToArray();
            _indexById = new Dictionary<WorldResourceId, int>(_definitions.Length);
            _categoryIndexById = new Dictionary<WorldResourceCategoryId, int>();

            WorldResourceId[] sortedIds = new WorldResourceId[_definitions.Length];
            WorldResourceCategoryId[] categoryBuffer = new WorldResourceCategoryId[_definitions.Length];
            int categoryCount = 0;
            for (int i = 0; i < _definitions.Length; i++)
            {
                WorldResourceDefinition definition = _definitions[i] ??
                    throw new ArgumentException("Resource catalog contains null definition.", nameof(definitions));
                if (_indexById.ContainsKey(definition.Id))
                    throw new ArgumentException("Duplicate resource ID: " + definition.Id, nameof(definitions));
                _indexById.Add(definition.Id, i);
                sortedIds[i] = definition.Id;
                if (!_categoryIndexById.ContainsKey(definition.CategoryId))
                {
                    _categoryIndexById.Add(definition.CategoryId, categoryCount);
                    categoryBuffer[categoryCount++] = definition.CategoryId;
                }
            }

            _categoryIds = new WorldResourceCategoryId[categoryCount];
            Array.Copy(categoryBuffer, _categoryIds, categoryCount);
            _categoryIndexByResource = new int[_definitions.Length];
            for (int i = 0; i < _definitions.Length; i++)
                _categoryIndexByResource[i] = _categoryIndexById[_definitions[i].CategoryId];

            Array.Sort(sortedIds);
            Dictionary<WorldResourceId, int> stableRankById = new Dictionary<WorldResourceId, int>(_definitions.Length);
            for (int i = 0; i < sortedIds.Length; i++) stableRankById.Add(sortedIds[i], i);
            _stableTieRanks = new int[_definitions.Length];
            for (int i = 0; i < _definitions.Length; i++)
                _stableTieRanks[i] = stableRankById[_definitions[i].Id];
        }

        public WorldResourceDefinition GetDefinition(int index)
        {
            if ((uint)index >= (uint)_definitions.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _definitions[index];
        }

        public bool TryGetIndex(WorldResourceId id, out int index) => _indexById.TryGetValue(id, out index);
        public bool TryGetCategoryIndex(WorldResourceCategoryId id, out int index) => _categoryIndexById.TryGetValue(id, out index);
        public WorldResourceCategoryId GetCategoryId(int categoryIndex)
        {
            if ((uint)categoryIndex >= (uint)_categoryIds.Length) throw new ArgumentOutOfRangeException(nameof(categoryIndex));
            return _categoryIds[categoryIndex];
        }

        internal int GetCategoryIndexForResource(int resourceIndex)
        {
            if ((uint)resourceIndex >= (uint)_categoryIndexByResource.Length) throw new ArgumentOutOfRangeException(nameof(resourceIndex));
            return _categoryIndexByResource[resourceIndex];
        }
        internal int GetStableTieRank(int resourceIndex) => _stableTieRanks[resourceIndex];
    }
}
