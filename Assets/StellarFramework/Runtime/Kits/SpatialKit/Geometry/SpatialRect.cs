using System;

namespace StellarFramework
{
    /// <summary>
    /// 连续二维空间中的半开矩形 [Min, MaxExclusive)。宽度或高度可以为零。
    /// </summary>
    public readonly struct SpatialRect : IEquatable<SpatialRect>
    {
        /// <summary>获取 inclusive 最小 X。</summary>
        public float MinX { get; }

        /// <summary>获取 inclusive 最小 Y。</summary>
        public float MinY { get; }

        /// <summary>获取 exclusive 最大 X。</summary>
        public float MaxExclusiveX { get; }

        /// <summary>获取 exclusive 最大 Y。</summary>
        public float MaxExclusiveY { get; }

        /// <summary>获取矩形是否为空。</summary>
        public bool IsEmpty => MinX == MaxExclusiveX || MinY == MaxExclusiveY;

        /// <summary>创建一个 [Min, MaxExclusive) 连续空间矩形。</summary>
        public SpatialRect(float minX, float minY, float maxExclusiveX, float maxExclusiveY)
        {
            ValidateFinite(minX, nameof(minX));
            ValidateFinite(minY, nameof(minY));
            ValidateFinite(maxExclusiveX, nameof(maxExclusiveX));
            ValidateFinite(maxExclusiveY, nameof(maxExclusiveY));

            if (maxExclusiveX < minX)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusiveX), maxExclusiveX,
                    "SpatialRect.MaxExclusiveX 不能小于 MinX。");
            }

            if (maxExclusiveY < minY)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusiveY), maxExclusiveY,
                    "SpatialRect.MaxExclusiveY 不能小于 MinY。");
            }

            MinX = minX;
            MinY = minY;
            MaxExclusiveX = maxExclusiveX;
            MaxExclusiveY = maxExclusiveY;
        }

        /// <summary>使用两个角点创建半开矩形。</summary>
        public SpatialRect(SpatialPoint min, SpatialPoint maxExclusive)
            : this(min.X, min.Y, maxExclusive.X, maxExclusive.Y)
        {
        }

        /// <summary>判断点是否位于半开矩形内。</summary>
        public bool Contains(SpatialPoint point) =>
            point.X >= MinX && point.X < MaxExclusiveX &&
            point.Y >= MinY && point.Y < MaxExclusiveY;

        /// <inheritdoc />
        public bool Equals(SpatialRect other) => MinX == other.MinX && MinY == other.MinY &&
            MaxExclusiveX == other.MaxExclusiveX && MaxExclusiveY == other.MaxExclusiveY;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SpatialRect other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MinX.GetHashCode();
                hash = (hash * 397) ^ MinY.GetHashCode();
                hash = (hash * 397) ^ MaxExclusiveX.GetHashCode();
                return (hash * 397) ^ MaxExclusiveY.GetHashCode();
            }
        }
        /// <inheritdoc />
        public override string ToString() => string.Format("[{0}, {1}) - [{2}, {3})", MinX, MinY, MaxExclusiveX, MaxExclusiveY);

        public static bool operator ==(SpatialRect left, SpatialRect right) => left.Equals(right);
        public static bool operator !=(SpatialRect left, SpatialRect right) => !left.Equals(right);

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "SpatialRect 坐标必须是有限值。");
            }
        }
    }
}
