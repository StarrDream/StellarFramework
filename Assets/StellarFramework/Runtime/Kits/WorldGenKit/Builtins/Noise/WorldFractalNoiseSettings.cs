using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public readonly struct WorldFractalNoiseSettings : IEquatable<WorldFractalNoiseSettings>
    {
        public WorldRuleId NoiseId { get; }
        public WorldNoiseKey NoiseKey { get; }
        public long BasePeriod { get; }
        public int Octaves { get; }
        public int Lacunarity { get; }
        public double Persistence { get; }

        public WorldFractalNoiseSettings(
            WorldRuleId noiseId,
            long basePeriod,
            int octaves = 4,
            int lacunarity = 2,
            double persistence = 0.5d)
        {
            if (!noiseId.IsValid) throw new ArgumentException("Noise ID must be valid.", nameof(noiseId));
            if (basePeriod <= 0L) throw new ArgumentOutOfRangeException(nameof(basePeriod));
            if (octaves <= 0 || octaves > 16) throw new ArgumentOutOfRangeException(nameof(octaves));
            if (lacunarity < 2) throw new ArgumentOutOfRangeException(nameof(lacunarity));
            if (double.IsNaN(persistence) || double.IsInfinity(persistence) || persistence <= 0d || persistence > 1d)
                throw new ArgumentOutOfRangeException(nameof(persistence));

            NoiseId = noiseId;
            NoiseKey = WorldNoiseRule.Compile(noiseId);
            BasePeriod = basePeriod;
            Octaves = octaves;
            Lacunarity = lacunarity;
            Persistence = persistence;
        }

        public bool Equals(WorldFractalNoiseSettings other) =>
            NoiseId == other.NoiseId && BasePeriod == other.BasePeriod && Octaves == other.Octaves &&
            Lacunarity == other.Lacunarity && Persistence.Equals(other.Persistence);

        public override bool Equals(object obj) => obj is WorldFractalNoiseSettings other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = NoiseId.GetHashCode();
                hash = (hash * 397) ^ BasePeriod.GetHashCode();
                hash = (hash * 397) ^ Octaves;
                hash = (hash * 397) ^ Lacunarity;
                return (hash * 397) ^ Persistence.GetHashCode();
            }
        }
    }
}
