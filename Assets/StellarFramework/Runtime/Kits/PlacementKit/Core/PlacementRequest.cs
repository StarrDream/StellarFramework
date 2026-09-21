using System;

namespace StellarFramework.PlacementKit
{
    public readonly struct PlacementRequest
    {
        public PlacementTypeId TypeId { get; }
        public double X { get; }
        public double Y { get; }
        public double RotationDegrees { get; }
        public PlacementFootprint Footprint { get; }
        public PlacementBounds2D Bounds => Footprint.GetAxisAlignedBounds(X, Y, RotationDegrees);

        public PlacementRequest(
            PlacementTypeId typeId,
            double x,
            double y,
            double rotationDegrees,
            PlacementFootprint footprint)
        {
            if (!typeId.IsValid) throw new ArgumentException("Placement type ID must be valid.", nameof(typeId));
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (double.IsNaN(y) || double.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(y));
            if (double.IsNaN(rotationDegrees) || double.IsInfinity(rotationDegrees)) throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
            if (!footprint.IsValid) throw new ArgumentException("Placement footprint must be valid.", nameof(footprint));
            TypeId = typeId;
            X = x;
            Y = y;
            RotationDegrees = rotationDegrees;
            Footprint = footprint;
        }
    }
}
