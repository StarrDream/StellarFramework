using System;

namespace StellarFramework
{
    /// <summary>
    /// 连续二维空间中的半开矩形 [Min, MaxExclusive)。宽度或高度可以为零。
    /// </summary>
    public readonly struct SpatialRect : IEquatable<SpatialRect>
    {
        public float MinX { get; }
        public float MinY { get; }
        public float MaxExclusiveX { get; }
        public float MaxExclusiveY { get; }

        public bool IsEmpty => MinX == MaxExclusiveX || MinY == MaxExclusiveY;

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

        public bool Contains(SpatialPoint point) =>
            point.X >= MinX && point.X < MaxExclusiveX &&
            point.Y >= MinY && point.Y < MaxExclusiveY;

        public bool Equals(SpatialRect other) => MinX == other.MinX && MinY == other.MinY &&
            MaxExclusiveX == other.MaxExclusiveX && MaxExclusiveY == other.MaxExclusiveY;
        public override bool Equals(object obj) => obj is SpatialRect other && Equals(other);
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
