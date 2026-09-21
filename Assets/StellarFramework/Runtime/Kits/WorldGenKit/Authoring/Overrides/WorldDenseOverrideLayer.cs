using System;
using System.Collections.Generic;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    /// <summary>
    /// Sparse authoring patch over an immutable/generated/imported Dense base layer.
    /// The base source is never mutated by this type.
    /// </summary>
    public sealed class WorldDenseOverrideLayer<T>
    {
        private readonly WorldPlanarSampleLayout _layout;
        private readonly Dictionary<int, T> _overrides;
        private bool _hasDirtyBounds;
        private WorldSampleRect _dirtyBounds;

        public WorldPlanarSampleLayout Layout => _layout;
        public int Count => _overrides.Count;
        public bool HasDirtyBounds => _hasDirtyBounds;

        public WorldDenseOverrideLayer(WorldPlanarSampleLayout layout, int initialCapacity = 0)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _layout = layout;
            _overrides = new Dictionary<int, T>(initialCapacity);
        }

        public bool TryGetOverride(int x, int y, out T value) =>
            _overrides.TryGetValue(GetIndex(x, y), out value);

        public bool TryGetOverrideByIndex(int index, out T value)
        {
            ValidateIndex(index);
            return _overrides.TryGetValue(index, out value);
        }

        public void Set(int x, int y, T value)
        {
            int index = GetIndex(x, y);
            _overrides[index] = value;
            MarkDirty(new WorldSampleRect(x, y, 1, 1));
        }

        public void SetByIndex(int index, T value)
        {
            ValidateIndex(index);
            _overrides[index] = value;
            int x = index % _layout.Width;
            int y = index / _layout.Width;
            MarkDirty(new WorldSampleRect(x, y, 1, 1));
        }

        internal void SetByIndexUnchecked(int index, T value)
        {
            _overrides[index] = value;
        }

        internal T GetComposedValueByIndexUnchecked(ReadOnlySpan<T> baseValues, int index)
        {
            return _overrides.TryGetValue(index, out T value) ? value : baseValues[index];
        }

        public bool Remove(int x, int y)
        {
            int index = GetIndex(x, y);
            if (!_overrides.Remove(index)) return false;
            MarkDirty(new WorldSampleRect(x, y, 1, 1));
            return true;
        }

        public bool RemoveByIndex(int index)
        {
            ValidateIndex(index);
            if (!_overrides.Remove(index)) return false;
            int x = index % _layout.Width;
            int y = index / _layout.Width;
            MarkDirty(new WorldSampleRect(x, y, 1, 1));
            return true;
        }

        public T GetComposedValue(ReadOnlySpan<T> baseValues, int x, int y)
        {
            ValidateBase(baseValues);
            int index = GetIndex(x, y);
            return _overrides.TryGetValue(index, out T value) ? value : baseValues[index];
        }

        public T GetComposedValueByIndex(ReadOnlySpan<T> baseValues, int index)
        {
            ValidateBase(baseValues);
            ValidateIndex(index);
            return _overrides.TryGetValue(index, out T value) ? value : baseValues[index];
        }

        public void Compose(ReadOnlySpan<T> baseValues, Span<T> destination)
        {
            ValidateBase(baseValues);
            if (destination.Length != _layout.Count)
                throw new ArgumentException("Destination length must match authoring layout sample count.", nameof(destination));

            baseValues.CopyTo(destination);
            foreach (KeyValuePair<int, T> entry in _overrides)
                destination[entry.Key] = entry.Value;
        }

        public bool TryGetDirtyBounds(out WorldSampleRect bounds)
        {
            bounds = _dirtyBounds;
            return _hasDirtyBounds;
        }

        public bool TryConsumeDirtyBounds(out WorldSampleRect bounds)
        {
            if (!_hasDirtyBounds)
            {
                bounds = default(WorldSampleRect);
                return false;
            }

            bounds = _dirtyBounds;
            _dirtyBounds = default(WorldSampleRect);
            _hasDirtyBounds = false;
            return true;
        }

        public void MarkDirty(in WorldSampleRect bounds)
        {
            if (!bounds.FitsWithin(in _layout))
                throw new ArgumentOutOfRangeException(nameof(bounds), "Dirty bounds exceed authoring layout.");
            _dirtyBounds = _hasDirtyBounds ? _dirtyBounds.Union(in bounds) : bounds;
            _hasDirtyBounds = true;
        }

        public void Clear()
        {
            if (_overrides.Count == 0) return;
            _overrides.Clear();
            MarkDirty(new WorldSampleRect(0, 0, _layout.Width, _layout.Height));
        }

        private int GetIndex(int x, int y) => _layout.GetIndex(x, y);

        private void ValidateIndex(int index)
        {
            if ((uint)index >= (uint)_layout.Count) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void ValidateBase(ReadOnlySpan<T> baseValues)
        {
            if (baseValues.Length != _layout.Count)
                throw new ArgumentException("Base length must match authoring layout sample count.", nameof(baseValues));
        }
    }
}
