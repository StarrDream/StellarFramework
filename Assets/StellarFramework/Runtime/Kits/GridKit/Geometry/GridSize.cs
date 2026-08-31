using System;

namespace StellarFramework
{
    /// <summary>非负二维网格尺寸。Area 使用 long 计算，避免 int 乘法溢出。</summary>
    public readonly struct GridSize : IEquatable<GridSize>
    {
        public int Width { get; }
        public int Height { get; }
        public long Area => (long)Width * Height;

        public GridSize(int width, int height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width), "Grid width cannot be negative.");
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height), "Grid height cannot be negative.");
            Width = width;
            Height = height;
        }

        public bool Equals(GridSize other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is GridSize && Equals((GridSize)obj);
        public override int GetHashCode() => unchecked((Width * 397) ^ Height);
        public override string ToString() => string.Format("{0} x {1}", Width, Height);

        public static bool operator ==(GridSize left, GridSize right) => left.Equals(right);
        public static bool operator !=(GridSize left, GridSize right) => !left.Equals(right);
    }
}
