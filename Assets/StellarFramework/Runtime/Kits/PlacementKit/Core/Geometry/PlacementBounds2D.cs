using System;

namespace StellarFramework.PlacementKit
{
    public readonly struct PlacementBounds2D : IEquatable<PlacementBounds2D>
    {
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public bool IsValid =>
            IsFinite(MinX) && IsFinite(MinY) && IsFinite(MaxX) && IsFinite(MaxY) &&
            MaxX > MinX && MaxY > MinY;

        public PlacementBounds2D(double minX, double minY, double maxX, double maxY)
        {
            ValidateFinite(minX, nameof(minX));
            ValidateFinite(minY, nameof(minY));
            ValidateFinite(maxX, nameof(maxX));
            ValidateFinite(maxY, nameof(maxY));
            if (maxX <= minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxY <= minY) throw new ArgumentOutOfRangeException(nameof(maxY));
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Overlaps(in PlacementBounds2D other) =>
            MinX < other.MaxX && MaxX > other.MinX && MinY < other.MaxY && MaxY > other.MinY;

        public bool Equals(PlacementBounds2D other) =>
            MinX.Equals(other.MinX) && MinY.Equals(other.MinY) && MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);
        public override bool Equals(object obj) => obj is PlacementBounds2D other && Equals(other);
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

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void ValidateFinite(double value, string parameterName)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
