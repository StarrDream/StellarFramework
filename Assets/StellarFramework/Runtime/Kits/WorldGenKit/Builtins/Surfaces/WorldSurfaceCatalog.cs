using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldSurfaceCatalog
    {
        private readonly WorldSurfaceId[] _ids;
        private readonly Dictionary<WorldSurfaceId, int> _indexById;

        public int Count => _ids.Length;

        public WorldSurfaceCatalog(ReadOnlySpan<WorldSurfaceId> ids)
        {
            if (ids.Length == 0) throw new ArgumentException("Surface catalog cannot be empty.", nameof(ids));
            _ids = ids.ToArray();
            _indexById = new Dictionary<WorldSurfaceId, int>(_ids.Length);
            for (int i = 0; i < _ids.Length; i++)
            {
                if (!_ids[i].IsValid) throw new ArgumentException("Surface catalog contains invalid ID.", nameof(ids));
                if (_indexById.ContainsKey(_ids[i]))
                    throw new ArgumentException("Duplicate surface ID: " + _ids[i], nameof(ids));
                _indexById.Add(_ids[i], i);
            }
        }

        public WorldSurfaceId GetId(int index)
        {
            if ((uint)index >= (uint)_ids.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _ids[index];
        }

        public bool TryGetIndex(WorldSurfaceId id, out int index) => _indexById.TryGetValue(id, out index);
    }
}
