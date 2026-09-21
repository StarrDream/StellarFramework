using System;

namespace StellarFramework
{
    /// <summary>固定 Bounds、Row-Major、连续 T[] 存储的二维网格。</summary>
    /// <remarks>
    /// DenseGrid 不跟踪 Unity 对象，也不做线程同步。索引顺序稳定为 Y-major rows、row 内 X ascending；
    /// <see cref="AsSpan"/> 与 ref API 直接暴露内部连续存储，仅应在调用者能够遵守 Bounds/生命周期时使用。
    /// </remarks>
    public sealed class DenseGrid<T> : IGrid<T>
    {
        private readonly GridRect _bounds;
        private readonly T[] _cells;

        /// <summary>获取固定逻辑边界。</summary>
        public GridRect Bounds => _bounds;

        /// <summary>获取宽度（cell 数）。</summary>
        public int Width => _bounds.Size.Width;

        /// <summary>获取高度（cell 数）。</summary>
        public int Height => _bounds.Size.Height;

        /// <summary>获取底层连续存储元素数量。</summary>
        public int Count => _cells.Length;

        /// <summary>创建默认值填充的固定边界网格。</summary>
        public DenseGrid(GridRect bounds)
        {
            if (bounds.Area > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), "DenseGrid area cannot exceed Int32.MaxValue.");
            }

            _bounds = bounds;
            _cells = new T[(int)bounds.Area];
        }

        /// <summary>创建并以同一初始值填充整个固定边界网格。</summary>
        public DenseGrid(GridRect bounds, T initialValue)
            : this(bounds)
        {
            Fill(initialValue);
        }

        /// <summary>判断坐标是否位于固定边界内。</summary>
        public bool Contains(GridCoord coord) => _bounds.Contains(coord);

        /// <summary>按逻辑坐标直接读写；越界时抛出异常。</summary>
        public T this[GridCoord coord]
        {
            get => _cells[GetIndex(coord)];
            set => _cells[GetIndex(coord)] = value;
        }

        /// <summary>无异常地尝试读取一个逻辑坐标。</summary>
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

        /// <summary>无异常地尝试写入一个逻辑坐标；失败时不修改数据。</summary>
        public bool TrySet(GridCoord coord, T value)
        {
            if (!TryGetIndex(coord, out int index)) return false;
            _cells[index] = value;
            return true;
        }

        /// <summary>将逻辑坐标转换为稳定的 Row-Major 连续索引。</summary>
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

        /// <summary>将逻辑坐标转换为连续索引；越界时抛出异常。</summary>
        public int GetIndex(GridCoord coord)
        {
            if (!TryGetIndex(coord, out int index))
            {
                throw new ArgumentOutOfRangeException(nameof(coord), coord, "Coordinate is outside DenseGrid bounds.");
            }

            return index;
        }

        /// <summary>将连续索引转换回绝对逻辑坐标。</summary>
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

        /// <summary>将连续索引转换回逻辑坐标；索引非法时抛出异常。</summary>
        public GridCoord GetCoord(int index)
        {
            if (!TryGetCoord(index, out GridCoord coord))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside DenseGrid storage.");
            }

            return coord;
        }

        /// <summary>获取直接引用内部存储的可写 Span；不会产生元素副本。</summary>
        public Span<T> AsSpan() => _cells.AsSpan();

        /// <summary>获取直接引用内部存储的只读 Span；不会产生元素副本。</summary>
        public ReadOnlySpan<T> AsReadOnlySpan() => _cells.AsSpan();

        /// <summary>按坐标获取内部元素可写引用。</summary>
        public ref T GetRef(GridCoord coord) => ref _cells[GetIndex(coord)];

        /// <summary>按坐标获取内部元素只读引用。</summary>
        public ref readonly T GetRefReadOnly(GridCoord coord) => ref _cells[GetIndex(coord)];

        /// <summary>按连续索引获取内部元素可写引用。</summary>
        public ref T GetRefByIndex(int index) => ref _cells[ValidateIndex(index)];

        /// <summary>按连续索引获取内部元素只读引用。</summary>
        public ref readonly T GetRefReadOnlyByIndex(int index) => ref _cells[ValidateIndex(index)];

        /// <summary>将全部元素重置为 T 的默认值。</summary>
        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        /// <summary>以同一值覆盖全部元素。</summary>
        public void Fill(T value)
        {
            for (int i = 0; i < _cells.Length; i++) _cells[i] = value;
        }

        /// <summary>从等长调用方缓冲区完整覆盖 DenseGrid；长度不匹配时不复制。</summary>
        public void CopyFrom(ReadOnlySpan<T> source)
        {
            RequireLength(source.Length, nameof(source));
            source.CopyTo(_cells.AsSpan());
        }

        /// <summary>将全部元素复制到等长调用方缓冲区；长度不匹配时不复制。</summary>
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
