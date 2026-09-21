using System;

namespace StellarFramework
{
    /// <summary>非负二维网格尺寸。Area 使用 long 计算，避免 int 乘法溢出。</summary>
    public readonly struct GridSize : IEquatable<GridSize>
    {
        /// <summary>获取非负宽度（cell 数）。</summary>
        public int Width { get; }

        /// <summary>获取非负高度（cell 数）。</summary>
        public int Height { get; }

        /// <summary>获取总 cell 数；使用 <see cref="long"/> 避免宽高乘法发生 Int32 溢出。</summary>
        public long Area => (long)Width * Height;

        /// <summary>创建一个非负二维网格尺寸。</summary>
        /// <exception cref="ArgumentOutOfRangeException">宽或高为负数。</exception>
        public GridSize(int width, int height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width), "Grid width cannot be negative.");
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height), "Grid height cannot be negative.");
            Width = width;
            Height = height;
        }

        /// <inheritdoc />
        public bool Equals(GridSize other) => Width == other.Width && Height == other.Height;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GridSize && Equals((GridSize)obj);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((Width * 397) ^ Height);

        /// <inheritdoc />
        public override string ToString() => string.Format("{0} x {1}", Width, Height);

        public static bool operator ==(GridSize left, GridSize right) => left.Equals(right);
        public static bool operator !=(GridSize left, GridSize right) => !left.Equals(right);
    }
}
