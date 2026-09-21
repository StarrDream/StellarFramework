using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public enum WorldFeatureFootprintKind
    {
        Rectangle = 0,
        Circle = 1
    }

    public readonly struct WorldFeatureFootprint : IEquatable<WorldFeatureFootprint>
    {
        public WorldFeatureFootprintKind Kind { get; }
        public double SizeX { get; }
        public double SizeY { get; }
        public bool IsValid =>
            (Kind == WorldFeatureFootprintKind.Rectangle || Kind == WorldFeatureFootprintKind.Circle) &&
            !double.IsNaN(SizeX) && !double.IsInfinity(SizeX) && SizeX > 0d &&
            !double.IsNaN(SizeY) && !double.IsInfinity(SizeY) && SizeY > 0d;

        private WorldFeatureFootprint(WorldFeatureFootprintKind kind, double sizeX, double sizeY)
        {
            Kind = kind;
            SizeX = sizeX;
            SizeY = sizeY;
        }

        public static WorldFeatureFootprint Rectangle(double width, double height)
        {
            ValidatePositiveFinite(width, nameof(width));
            ValidatePositiveFinite(height, nameof(height));
            return new WorldFeatureFootprint(WorldFeatureFootprintKind.Rectangle, width, height);
        }

        public static WorldFeatureFootprint Circle(double radius)
        {
            ValidatePositiveFinite(radius, nameof(radius));
            return new WorldFeatureFootprint(WorldFeatureFootprintKind.Circle, radius, radius);
        }

        public WorldFeatureBounds GetAxisAlignedBounds(double centerX, double centerY, double rotationDegrees = 0d)
        {
            if (!IsValid) throw new InvalidOperationException("Feature footprint is not initialized with a valid shape.");
            ValidateFinite(centerX, nameof(centerX));
            ValidateFinite(centerY, nameof(centerY));
            ValidateFinite(rotationDegrees, nameof(rotationDegrees));

            if (Kind == WorldFeatureFootprintKind.Circle)
            {
                double radius = SizeX;
                return new WorldFeatureBounds(centerX - radius, centerY - radius, centerX + radius, centerY + radius);
            }

            double halfX = SizeX * 0.5d;
            double halfY = SizeY * 0.5d;
            double radians = rotationDegrees * (Math.PI / 180d);
            double cos = Math.Abs(Math.Cos(radians));
            double sin = Math.Abs(Math.Sin(radians));
            double extentX = (halfX * cos) + (halfY * sin);
            double extentY = (halfX * sin) + (halfY * cos);
            return new WorldFeatureBounds(centerX - extentX, centerY - extentY, centerX + extentX, centerY + extentY);
        }

        public bool Equals(WorldFeatureFootprint other) => Kind == other.Kind && SizeX.Equals(other.SizeX) && SizeY.Equals(other.SizeY);
        public override bool Equals(object obj) => obj is WorldFeatureFootprint other && Equals(other);
        public override int GetHashCode() => unchecked((((int)Kind * 397) ^ SizeX.GetHashCode()) * 397 ^ SizeY.GetHashCode());

        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value <= 0d) throw new ArgumentOutOfRangeException(parameterName, "Feature footprint values must be > 0.");
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Feature footprint values must be finite.");
        }
    }
}
