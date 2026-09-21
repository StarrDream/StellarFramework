using System;

namespace StellarFramework.PlacementKit
{
    public readonly struct PlacementSiteFacts
    {
        public double MaxSlopeDegrees { get; }
        public double MinWaterDepth { get; }
        public double MaxWaterDepth { get; }
        public ulong ZoneMask { get; }
        public ulong ConflictMask { get; }
        public ulong ConnectionMask { get; }
        public double BaseSuitability { get; }

        public PlacementSiteFacts(
            double maxSlopeDegrees,
            double minWaterDepth,
            double maxWaterDepth,
            ulong zoneMask,
            ulong conflictMask,
            ulong connectionMask,
            double baseSuitability = 0d)
        {
            ValidateFiniteNonNegative(maxSlopeDegrees, nameof(maxSlopeDegrees));
            ValidateFinite(minWaterDepth, nameof(minWaterDepth));
            ValidateFinite(maxWaterDepth, nameof(maxWaterDepth));
            if (maxWaterDepth < minWaterDepth) throw new ArgumentOutOfRangeException(nameof(maxWaterDepth));
            ValidateFinite(baseSuitability, nameof(baseSuitability));
            MaxSlopeDegrees = maxSlopeDegrees;
            MinWaterDepth = minWaterDepth;
            MaxWaterDepth = maxWaterDepth;
            ZoneMask = zoneMask;
            ConflictMask = conflictMask;
            ConnectionMask = connectionMask;
            BaseSuitability = baseSuitability;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value < 0d) throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
