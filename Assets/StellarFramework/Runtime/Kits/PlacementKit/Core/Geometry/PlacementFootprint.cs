using System;

namespace StellarFramework.PlacementKit
{
    public enum PlacementFootprintKind
    {
        Rectangle = 0,
        Circle = 1
    }

    public readonly struct PlacementFootprint : IEquatable<PlacementFootprint>
    {
        public PlacementFootprintKind Kind { get; }
        public double SizeX { get; }
        public double SizeY { get; }
        public bool IsValid =>
            (Kind == PlacementFootprintKind.Rectangle || Kind == PlacementFootprintKind.Circle) &&
            IsPositiveFinite(SizeX) && IsPositiveFinite(SizeY);

        private PlacementFootprint(PlacementFootprintKind kind, double sizeX, double sizeY)
        {
            Kind = kind;
            SizeX = sizeX;
            SizeY = sizeY;
        }

        public static PlacementFootprint Rectangle(double width, double height)
        {
            ValidatePositiveFinite(width, nameof(width));
            ValidatePositiveFinite(height, nameof(height));
            return new PlacementFootprint(PlacementFootprintKind.Rectangle, width, height);
        }

        public static PlacementFootprint Circle(double radius)
        {
            ValidatePositiveFinite(radius, nameof(radius));
            return new PlacementFootprint(PlacementFootprintKind.Circle, radius, radius);
        }

        public PlacementBounds2D GetAxisAlignedBounds(double x, double y, double rotationDegrees)
        {
            if (!IsValid) throw new InvalidOperationException("Placement footprint is not initialized.");
            ValidateFinite(x, nameof(x));
            ValidateFinite(y, nameof(y));
            ValidateFinite(rotationDegrees, nameof(rotationDegrees));

            if (Kind == PlacementFootprintKind.Circle)
            {
                double radius = SizeX;
                return new PlacementBounds2D(x - radius, y - radius, x + radius, y + radius);
            }

            double halfX = SizeX * 0.5d;
            double halfY = SizeY * 0.5d;
            double radians = rotationDegrees * (Math.PI / 180d);
            double cos = Math.Abs(Math.Cos(radians));
            double sin = Math.Abs(Math.Sin(radians));
            double extentX = (halfX * cos) + (halfY * sin);
            double extentY = (halfX * sin) + (halfY * cos);
            return new PlacementBounds2D(x - extentX, y - extentY, x + extentX, y + extentY);
        }

        public bool Equals(PlacementFootprint other) => Kind == other.Kind && SizeX.Equals(other.SizeX) && SizeY.Equals(other.SizeY);
        public override bool Equals(object obj) => obj is PlacementFootprint other && Equals(other);
        public override int GetHashCode() => unchecked((((int)Kind * 397) ^ SizeX.GetHashCode()) * 397 ^ SizeY.GetHashCode());

        private static bool IsPositiveFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            if (!IsPositiveFinite(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
