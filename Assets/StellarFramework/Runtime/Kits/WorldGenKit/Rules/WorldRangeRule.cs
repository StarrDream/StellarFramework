using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldRangeRule
    {
        public double Min { get; }
        public double Max { get; }

        public WorldRangeRule(double min, double max)
        {
            WorldRuleMath.RequireFinite(min, nameof(min));
            WorldRuleMath.RequireFinite(max, nameof(max));
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max), "Max cannot be less than Min.");
            Min = min;
            Max = max;
        }

        public bool Contains(double value)
        {
            WorldRuleMath.RequireFinite(value, nameof(value));
            return value >= Min && value <= Max;
        }

        public double NormalizeClamped(double value)
        {
            WorldRuleMath.RequireFinite(value, nameof(value));
            if (Max == Min) return value >= Max ? 1d : 0d;
            return WorldRuleMath.Clamp01((value - Min) / (Max - Min));
        }
    }

    public enum WorldThresholdComparison
    {
        GreaterOrEqual = 0,
        Greater = 1,
        LessOrEqual = 2,
        Less = 3
    }

    public readonly struct WorldThresholdRule
    {
        public double Threshold { get; }
        public WorldThresholdComparison Comparison { get; }

        public WorldThresholdRule(double threshold, WorldThresholdComparison comparison)
        {
            WorldRuleMath.RequireFinite(threshold, nameof(threshold));
            if (comparison < WorldThresholdComparison.GreaterOrEqual || comparison > WorldThresholdComparison.Less)
                throw new ArgumentOutOfRangeException(nameof(comparison));
            Threshold = threshold;
            Comparison = comparison;
        }

        public bool Evaluate(double value)
        {
            WorldRuleMath.RequireFinite(value, nameof(value));
            switch (Comparison)
            {
                case WorldThresholdComparison.GreaterOrEqual: return value >= Threshold;
                case WorldThresholdComparison.Greater: return value > Threshold;
                case WorldThresholdComparison.LessOrEqual: return value <= Threshold;
                case WorldThresholdComparison.Less: return value < Threshold;
                default: throw new InvalidOperationException("Unknown threshold comparison.");
            }
        }
    }
}
