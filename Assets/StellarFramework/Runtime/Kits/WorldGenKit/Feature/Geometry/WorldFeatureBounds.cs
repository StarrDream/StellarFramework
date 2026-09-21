using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureBounds : IEquatable<WorldFeatureBounds>
    {
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public bool IsValid =>
            !double.IsNaN(MinX) && !double.IsInfinity(MinX) &&
            !double.IsNaN(MinY) && !double.IsInfinity(MinY) &&
            !double.IsNaN(MaxX) && !double.IsInfinity(MaxX) &&
            !double.IsNaN(MaxY) && !double.IsInfinity(MaxY) &&
            MaxX > MinX && MaxY > MinY;

        public WorldFeatureBounds(double minX, double minY, double maxX, double maxY)
        {
            ValidateFinite(minX, nameof(minX));
            ValidateFinite(minY, nameof(minY));
            ValidateFinite(maxX, nameof(maxX));
            ValidateFinite(maxY, nameof(maxY));
            if (maxX <= minX) throw new ArgumentOutOfRangeException(nameof(maxX), "Feature bounds must have positive width.");
            if (maxY <= minY) throw new ArgumentOutOfRangeException(nameof(maxY), "Feature bounds must have positive height.");
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Overlaps(in WorldFeatureBounds other) =>
            MinX < other.MaxX && MaxX > other.MinX && MinY < other.MaxY && MaxY > other.MinY;

        public bool Contains(double x, double y) =>
            x >= MinX && x < MaxX && y >= MinY && y < MaxY;

        public bool Equals(WorldFeatureBounds other) =>
            MinX.Equals(other.MinX) && MinY.Equals(other.MinY) && MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);
        public override bool Equals(object obj) => obj is WorldFeatureBounds other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MinX.GetHashCode();
                hash = (hash * 397) ^ MinY.GetHashCode();
                hash = (hash * 397) ^ MaxX.GetHashCode();
                return (hash * 397) ^ MaxY.GetHashCode();
            }
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Feature bound coordinates must be finite.");
        }
    }
}
