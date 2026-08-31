using System;

namespace StellarFramework
{
    /// <summary>固定 Bounds、Row-Major、连续 T[] 存储的二维网格。</summary>
    public sealed class DenseGrid<T> : IGrid<T>
    {
        private readonly GridRect _bounds;
        private readonly T[] _cells;

        public GridRect Bounds => _bounds;
        public int Width => _bounds.Size.Width;
        public int Height => _bounds.Size.Height;
        public int Count => _cells.Length;

        public DenseGrid(GridRect bounds)
        {
            if (bounds.Area > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), "DenseGrid area cannot exceed Int32.MaxValue.");
            }

            _bounds = bounds;
            _cells = new T[(int)bounds.Area];
        }

        public DenseGrid(GridRect bounds, T initialValue)
            : this(bounds)
        {
            Fill(initialValue);
        }

        public bool Contains(GridCoord coord) => _bounds.Contains(coord);

        public T this[GridCoord coord]
        {
            get => _cells[GetIndex(coord)];
            set => _cells[GetIndex(coord)] = value;
        }

        public bool TryGet(GridCoord coord, out T value)
        {
            if (!TryGetIndex(coord, out int index))
            {
                value = default(T);
                return false;
            }

            value = _cells[index];
            return true;
        }

        public bool TrySet(GridCoord coord, T value)
        {
            if (!TryGetIndex(coord, out int index)) return false;
            _cells[index] = value;
            return true;
        }

        public bool TryGetIndex(GridCoord coord, out int index)
        {
            if (!_bounds.Contains(coord))
            {
                index = -1;
                return false;
            }

            long localX = (long)coord.X - _bounds.Min.X;
            long localY = (long)coord.Y - _bounds.Min.Y;
            long index64 = checked(localY * Width + localX);
            index = (int)index64;
            return true;
        }

        public int GetIndex(GridCoord coord)
        {
            if (!TryGetIndex(coord, out int index))
            {
                throw new ArgumentOutOfRangeException(nameof(coord), coord, "Coordinate is outside DenseGrid bounds.");
            }

            return index;
        }

        public bool TryGetCoord(int index, out GridCoord coord)
        {
            if (index < 0 || index >= Count || Width == 0)
            {
                coord = default(GridCoord);
                return false;
            }

            int localY = index / Width;
            int localX = index % Width;
            coord = new GridCoord(
                checked(_bounds.Min.X + localX),
                checked(_bounds.Min.Y + localY));
            return true;
        }

        public GridCoord GetCoord(int index)
        {
            if (!TryGetCoord(index, out GridCoord coord))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside DenseGrid storage.");
            }

            return coord;
        }

        public Span<T> AsSpan() => _cells.AsSpan();
        public ReadOnlySpan<T> AsReadOnlySpan() => _cells.AsSpan();

        public ref T GetRef(GridCoord coord) => ref _cells[GetIndex(coord)];
        public ref readonly T GetRefReadOnly(GridCoord coord) => ref _cells[GetIndex(coord)];
        public ref T GetRefByIndex(int index) => ref _cells[ValidateIndex(index)];
        public ref readonly T GetRefReadOnlyByIndex(int index) => ref _cells[ValidateIndex(index)];

        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        public void Fill(T value)
        {
            for (int i = 0; i < _cells.Length; i++) _cells[i] = value;
        }

        public void CopyFrom(ReadOnlySpan<T> source)
        {
            RequireLength(source.Length, nameof(source));
            source.CopyTo(_cells.AsSpan());
        }

        public void CopyTo(Span<T> destination)
        {
            RequireLength(destination.Length, nameof(destination));
            _cells.AsSpan().CopyTo(destination);
        }

        private int ValidateIndex(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside DenseGrid storage.");
            }

            return index;
        }

        private void RequireLength(int length, string parameterName)
        {
            if (length != Count)
            {
                throw new ArgumentException("Buffer length must equal DenseGrid.Count.", parameterName);
            }
        }
    }
}
