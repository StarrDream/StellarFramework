using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureCandidate
    {
        public int FeatureIndex { get; }
        public double X { get; }
        public double Y { get; }
        public double RotationDegrees { get; }
        public double Score { get; }
        public ulong DeterministicKey { get; }

        public WorldFeatureCandidate(
            int featureIndex,
            double x,
            double y,
            double rotationDegrees,
            double score,
            ulong deterministicKey)
        {
            if (featureIndex < 0) throw new ArgumentOutOfRangeException(nameof(featureIndex));
            ValidateFinite(x, nameof(x));
            ValidateFinite(y, nameof(y));
            ValidateFinite(rotationDegrees, nameof(rotationDegrees));
            ValidateFinite(score, nameof(score));
            FeatureIndex = featureIndex;
            X = x;
            Y = y;
            RotationDegrees = rotationDegrees;
            Score = score;
            DeterministicKey = deterministicKey;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Feature candidate values must be finite.");
        }
    }
}
