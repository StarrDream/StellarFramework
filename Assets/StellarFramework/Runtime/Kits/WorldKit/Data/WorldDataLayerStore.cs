using System;
using System.Collections.Generic;

namespace StellarFramework.WorldKit
{
    /// <summary>
    /// Typed storage for one registered WorldKit data layer.
    /// A Chunk-scoped value may itself be a DenseGrid, sparse page, graph page, custom DTO, etc.;
    /// WorldKit does not prescribe the payload representation.
    /// </summary>
    public sealed class WorldDataLayerStore<T>
    {
        private readonly Dictionary<WorldRegionCoord, T> _regionValues;
        private readonly Dictionary<WorldChunkCoord, T> _chunkValues;
        private T _worldValue;
        private bool _hasWorldValue;

        public WorldDataLayerHandle<T> Handle { get; }
        public WorldDataLayerId Id { get; }
        public WorldDataLayerScope Scope { get; }

        public int Count
        {
            get
            {
                switch (Scope)
                {
                    case WorldDataLayerScope.World:
                        return _hasWorldValue ? 1 : 0;
                    case WorldDataLayerScope.Region:
                        return _regionValues.Count;
                    case WorldDataLayerScope.Chunk:
                        return _chunkValues.Count;
                    default:
                        throw new InvalidOperationException("Unknown WorldDataLayerScope.");
                }
            }
        }

        public WorldDataLayerStore(
            WorldDataLayerRegistry registry,
            WorldDataLayerHandle<T> handle,
            int initialCapacity = 0)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            if (!registry.TryGetDescriptor(handle, out WorldDataLayerDescriptor descriptor))
                throw new ArgumentException("Handle does not belong to the supplied registry or has the wrong value type.", nameof(handle));

            Handle = handle;
            Id = descriptor.Id;
            Scope = descriptor.Scope;

            if (Scope == WorldDataLayerScope.Region)
                _regionValues = new Dictionary<WorldRegionCoord, T>(initialCapacity);
            else if (Scope == WorldDataLayerScope.Chunk)
                _chunkValues = new Dictionary<WorldChunkCoord, T>(initialCapacity);
        }

        public void SetWorld(T value)
        {
            RequireScope(WorldDataLayerScope.World);
            _worldValue = value;
            _hasWorldValue = true;
        }

        public bool TryGetWorld(out T value)
        {
            RequireScope(WorldDataLayerScope.World);
            value = _worldValue;
            return _hasWorldValue;
        }

        public bool ClearWorld()
        {
            RequireScope(WorldDataLayerScope.World);
            if (!_hasWorldValue) return false;
            _worldValue = default(T);
            _hasWorldValue = false;
            return true;
        }

        public void SetRegion(WorldRegionCoord region, T value)
        {
            RequireScope(WorldDataLayerScope.Region);
            _regionValues[region] = value;
        }

        public bool TryGetRegion(WorldRegionCoord region, out T value)
        {
            RequireScope(WorldDataLayerScope.Region);
            return _regionValues.TryGetValue(region, out value);
        }

        public bool RemoveRegion(WorldRegionCoord region)
        {
            RequireScope(WorldDataLayerScope.Region);
            return _regionValues.Remove(region);
        }

        public void SetChunk(WorldChunkCoord chunk, T value)
        {
            RequireScope(WorldDataLayerScope.Chunk);
            _chunkValues[chunk] = value;
        }

        public bool TryGetChunk(WorldChunkCoord chunk, out T value)
        {
            RequireScope(WorldDataLayerScope.Chunk);
            return _chunkValues.TryGetValue(chunk, out value);
        }

        public bool RemoveChunk(WorldChunkCoord chunk)
        {
            RequireScope(WorldDataLayerScope.Chunk);
            return _chunkValues.Remove(chunk);
        }

        public void Clear()
        {
            switch (Scope)
            {
                case WorldDataLayerScope.World:
                    _worldValue = default(T);
                    _hasWorldValue = false;
                    break;
                case WorldDataLayerScope.Region:
                    _regionValues.Clear();
                    break;
                case WorldDataLayerScope.Chunk:
                    _chunkValues.Clear();
                    break;
                default:
                    throw new InvalidOperationException("Unknown WorldDataLayerScope.");
            }
        }

        private void RequireScope(WorldDataLayerScope expected)
        {
            if (Scope != expected)
            {
                throw new InvalidOperationException(
                    string.Format("Layer '{0}' is scoped as {1}, not {2}.", Id, Scope, expected));
            }
        }
    }
}
