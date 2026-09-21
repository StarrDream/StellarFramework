using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceGenerationMultiplier : IEquatable<WorldResourceGenerationMultiplier>
    {
        public double Occurrence { get; }
        public double ClusterSize { get; }
        public double Richness { get; }

        public static WorldResourceGenerationMultiplier Identity =>
            new WorldResourceGenerationMultiplier(1d, 1d, 1d);

        public WorldResourceGenerationMultiplier(
            double occurrence,
            double clusterSize,
            double richness)
        {
            ValidateMultiplier(occurrence, nameof(occurrence));
            ValidateMultiplier(clusterSize, nameof(clusterSize));
            ValidateMultiplier(richness, nameof(richness));
            Occurrence = occurrence;
            ClusterSize = clusterSize;
            Richness = richness;
        }

        public WorldResourceGenerationMultiplier Multiply(in WorldResourceGenerationMultiplier other) =>
            new WorldResourceGenerationMultiplier(
                Occurrence * other.Occurrence,
                ClusterSize * other.ClusterSize,
                Richness * other.Richness);

        public bool Equals(WorldResourceGenerationMultiplier other) =>
            Occurrence.Equals(other.Occurrence) && ClusterSize.Equals(other.ClusterSize) && Richness.Equals(other.Richness);
        public override bool Equals(object obj) => obj is WorldResourceGenerationMultiplier other && Equals(other);
        public override int GetHashCode() => unchecked(((Occurrence.GetHashCode() * 397) ^ ClusterSize.GetHashCode()) * 397 ^ Richness.GetHashCode());

        private static void ValidateMultiplier(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(parameterName, "Generation multiplier must be finite and >= 0.");
        }
    }
}
