using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit
{
    public sealed class SparseChannelStorage<T> : IWorldChannelStorage<T>
    {
        private readonly Dictionary<int, T> _values;

        public WorldChannelStorageKind Kind => WorldChannelStorageKind.Sparse;
        public T DefaultValue { get; }
        public int StoredCount => _values.Count;

        public SparseChannelStorage(T defaultValue = default(T), int initialCapacity = 0)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            DefaultValue = defaultValue;
            _values = new Dictionary<int, T>(initialCapacity);
        }

        public T Get(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _values.TryGetValue(index, out T value) ? value : DefaultValue;
        }

        public bool TryGetStored(int index, out T value)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _values.TryGetValue(index, out value);
        }

        public void Set(int index, T value)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            _values[index] = value;
        }

        public bool Remove(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _values.Remove(index);
        }

        public void Clear() => _values.Clear();
    }
}
