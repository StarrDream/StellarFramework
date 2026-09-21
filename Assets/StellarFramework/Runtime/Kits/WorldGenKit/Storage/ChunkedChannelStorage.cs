using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit
{
    /// <summary>Topology-neutral paged/chunked storage. TKey is supplied by the project/adapter.</summary>
    public sealed class ChunkedChannelStorage<TKey, T> : IWorldChannelStorage<T>
        where TKey : struct
    {
        private readonly Dictionary<TKey, T> _values;

        public WorldChannelStorageKind Kind => WorldChannelStorageKind.Chunked;
        public int Count => _values.Count;

        public ChunkedChannelStorage(int initialCapacity = 0)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _values = new Dictionary<TKey, T>(initialCapacity);
        }

        public void Set(TKey key, T value) => _values[key] = value;
        public bool TryGet(TKey key, out T value) => _values.TryGetValue(key, out value);
        public bool Remove(TKey key) => _values.Remove(key);
        public void Clear() => _values.Clear();
    }
}
