using System;

namespace StellarFramework.WorldGenKit
{
    public static class WorldRuleMath
    {
        public static double Inverse01(double value)
        {
            RequireFinite(value, nameof(value));
            return 1d - Clamp01(value);
        }

        public static double WeightedSum(ReadOnlySpan<double> values, ReadOnlySpan<double> weights)
        {
            if (values.Length != weights.Length)
                throw new ArgumentException("Values and weights must have equal length.");
            if (values.Length == 0) throw new ArgumentException("WeightedSum requires at least one value.", nameof(values));

            double sum = 0d;
            double weightSum = 0d;
            for (int i = 0; i < values.Length; i++)
            {
                RequireFinite(values[i], nameof(values));
                RequireFinite(weights[i], nameof(weights));
                if (weights[i] < 0d) throw new ArgumentOutOfRangeException(nameof(weights), "Weights cannot be negative.");
                sum += values[i] * weights[i];
                weightSum += weights[i];
            }

            if (weightSum <= 0d) throw new ArgumentException("At least one weight must be greater than zero.", nameof(weights));
            return sum / weightSum;
        }

        public static double Multiply(ReadOnlySpan<double> values)
        {
            if (values.Length == 0) throw new ArgumentException("Multiply requires at least one value.", nameof(values));
            double result = 1d;
            for (int i = 0; i < values.Length; i++)
            {
                RequireFinite(values[i], nameof(values));
                result *= values[i];
            }
            if (double.IsNaN(result) || double.IsInfinity(result))
                throw new OverflowException("Rule multiplication produced a non-finite result.");
            return result;
        }

        public static double Min(ReadOnlySpan<double> values)
        {
            if (values.Length == 0) throw new ArgumentException("Min requires at least one value.", nameof(values));
            double result = values[0];
            RequireFinite(result, nameof(values));
            for (int i = 1; i < values.Length; i++)
            {
                RequireFinite(values[i], nameof(values));
                if (values[i] < result) result = values[i];
            }
            return result;
        }

        public static double Max(ReadOnlySpan<double> values)
        {
            if (values.Length == 0) throw new ArgumentException("Max requires at least one value.", nameof(values));
            double result = values[0];
            RequireFinite(result, nameof(values));
            for (int i = 1; i < values.Length; i++)
            {
                RequireFinite(values[i], nameof(values));
                if (values[i] > result) result = values[i];
            }
            return result;
        }

        public static bool And(ReadOnlySpan<bool> values)
        {
            if (values.Length == 0) throw new ArgumentException("And requires at least one value.", nameof(values));
            for (int i = 0; i < values.Length; i++) if (!values[i]) return false;
            return true;
        }

        public static bool Or(ReadOnlySpan<bool> values)
        {
            if (values.Length == 0) throw new ArgumentException("Or requires at least one value.", nameof(values));
            for (int i = 0; i < values.Length; i++) if (values[i]) return true;
            return false;
        }

        public static double Distance2D(double ax, double ay, double bx, double by)
        {
            RequireFinite(ax, nameof(ax));
            RequireFinite(ay, nameof(ay));
            RequireFinite(bx, nameof(bx));
            RequireFinite(by, nameof(by));
            double dx = ax - bx;
            double dy = ay - by;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        public static double Clamp01(double value)
        {
            RequireFinite(value, nameof(value));
            if (value <= 0d) return 0d;
            return value >= 1d ? 1d : value;
        }

        internal static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite.");
        }
    }
}
