using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceMultiplierExposure
    {
        public bool IsExposed { get; }
        public double MinMultiplier { get; }
        public double MaxMultiplier { get; }
        public double DefaultMultiplier { get; }

        public WorldResourceMultiplierExposure(
            bool isExposed,
            double minMultiplier = 1d,
            double maxMultiplier = 1d,
            double defaultMultiplier = 1d)
        {
            ValidateFiniteNonNegative(minMultiplier, nameof(minMultiplier));
            ValidateFiniteNonNegative(maxMultiplier, nameof(maxMultiplier));
            ValidateFiniteNonNegative(defaultMultiplier, nameof(defaultMultiplier));
            if (minMultiplier > maxMultiplier) throw new ArgumentException("Minimum multiplier cannot exceed maximum multiplier.");
            if (defaultMultiplier < minMultiplier || defaultMultiplier > maxMultiplier)
                throw new ArgumentOutOfRangeException(nameof(defaultMultiplier), "Default multiplier must be inside the exposure range.");

            IsExposed = isExposed;
            MinMultiplier = minMultiplier;
            MaxMultiplier = maxMultiplier;
            DefaultMultiplier = defaultMultiplier;
        }

        internal bool Allows(double value)
        {
            if (value.Equals(1d) && !IsExposed) return true;
            return IsExposed && value >= MinMultiplier && value <= MaxMultiplier;
        }

        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(parameterName, "Multiplier range values must be finite and >= 0.");
        }
    }

    public readonly struct WorldResourceGenerationExposure
    {
        public WorldResourceMultiplierExposure Occurrence { get; }
        public WorldResourceMultiplierExposure ClusterSize { get; }
        public WorldResourceMultiplierExposure Richness { get; }

        public WorldResourceGenerationExposure(
            WorldResourceMultiplierExposure occurrence,
            WorldResourceMultiplierExposure clusterSize,
            WorldResourceMultiplierExposure richness)
        {
            Occurrence = occurrence;
            ClusterSize = clusterSize;
            Richness = richness;
        }

        internal bool Allows(in WorldResourceGenerationMultiplier multiplier) =>
            Occurrence.Allows(multiplier.Occurrence) &&
            ClusterSize.Allows(multiplier.ClusterSize) &&
            Richness.Allows(multiplier.Richness);
    }

    public readonly struct WorldResourceCategoryExposureEntry
    {
        public WorldResourceCategoryId CategoryId { get; }
        public WorldResourceGenerationExposure Exposure { get; }

        public WorldResourceCategoryExposureEntry(
            WorldResourceCategoryId categoryId,
            WorldResourceGenerationExposure exposure)
        {
            if (!categoryId.IsValid) throw new ArgumentException("Category ID must be valid.", nameof(categoryId));
            CategoryId = categoryId;
            Exposure = exposure;
        }
    }

    public readonly struct WorldResourceExposureEntry
    {
        public WorldResourceId ResourceId { get; }
        public WorldResourceGenerationExposure Exposure { get; }

        public WorldResourceExposureEntry(
            WorldResourceId resourceId,
            WorldResourceGenerationExposure exposure)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Resource ID must be valid.", nameof(resourceId));
            ResourceId = resourceId;
            Exposure = exposure;
        }
    }
}
