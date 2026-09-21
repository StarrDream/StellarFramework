using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public enum WorldResourceDistributionMode
    {
        Density = 0,
        Coverage = 1
    }

    public readonly struct WorldResourceDistributionDefinition : IEquatable<WorldResourceDistributionDefinition>
    {
        public WorldResourceDistributionMode Mode { get; }
        public double Occurrence { get; }
        public int ClusterSize { get; }
        public double Richness { get; }
        public double MinSpacing { get; }

        public WorldResourceDistributionDefinition(
            WorldResourceDistributionMode mode,
            double occurrence,
            int clusterSize = 1,
            double richness = 1d,
            double minSpacing = 0d)
        {
            if (mode != WorldResourceDistributionMode.Density && mode != WorldResourceDistributionMode.Coverage)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (double.IsNaN(occurrence) || double.IsInfinity(occurrence) || occurrence < 0d || occurrence > 1d)
                throw new ArgumentOutOfRangeException(nameof(occurrence), "Occurrence must be in [0,1].");
            if (clusterSize <= 0) throw new ArgumentOutOfRangeException(nameof(clusterSize));
            if (double.IsNaN(richness) || double.IsInfinity(richness) || richness < 0d)
                throw new ArgumentOutOfRangeException(nameof(richness));
            if (double.IsNaN(minSpacing) || double.IsInfinity(minSpacing) || minSpacing < 0d)
                throw new ArgumentOutOfRangeException(nameof(minSpacing));

            Mode = mode;
            Occurrence = occurrence;
            ClusterSize = clusterSize;
            Richness = richness;
            MinSpacing = minSpacing;
        }

        public bool Equals(WorldResourceDistributionDefinition other) =>
            Mode == other.Mode && Occurrence.Equals(other.Occurrence) && ClusterSize == other.ClusterSize &&
            Richness.Equals(other.Richness) && MinSpacing.Equals(other.MinSpacing);
        public override bool Equals(object obj) => obj is WorldResourceDistributionDefinition other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Mode;
                hash = (hash * 397) ^ Occurrence.GetHashCode();
                hash = (hash * 397) ^ ClusterSize;
                hash = (hash * 397) ^ Richness.GetHashCode();
                return (hash * 397) ^ MinSpacing.GetHashCode();
            }
        }
    }
}
