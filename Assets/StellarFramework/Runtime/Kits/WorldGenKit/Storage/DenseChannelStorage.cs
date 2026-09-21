using System;

namespace StellarFramework.WorldGenKit
{
    public sealed class DenseChannelStorage<T> : IWorldChannelStorage<T>
    {
        private readonly T[] _values;

        public WorldChannelStorageKind Kind => WorldChannelStorageKind.Dense;
        public int Length => _values.Length;
        public T this[int index]
        {
            get => _values[index];
            set => _values[index] = value;
        }

        public DenseChannelStorage(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            _values = new T[length];
        }

        public Span<T> AsSpan() => _values.AsSpan();
        public ReadOnlySpan<T> AsReadOnlySpan() => _values.AsSpan();
        public void Clear() => Array.Clear(_values, 0, _values.Length);
        public void Fill(T value) => _values.AsSpan().Fill(value);
    }
}
