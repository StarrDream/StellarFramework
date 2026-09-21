using System;

namespace StellarFramework.WorldGenKit
{
    /// <summary>Deterministic hash-noise primitive in [0,1), independent from Unity Random/global RNG state.</summary>
    public static class WorldNoiseRule
    {
        private const double Inverse53 = 1d / 9007199254740992d;

        public static WorldNoiseKey Compile(WorldRuleId ruleId)
        {
            if (!ruleId.IsValid) throw new ArgumentException("Rule ID must be valid.", nameof(ruleId));
            return new WorldNoiseKey(ruleId, WorldGenStableHash64.HashString(ruleId.Value));
        }

        public static double Sample01(
            WorldGenerationSeed seed,
            long x,
            long y,
            WorldRuleId ruleId,
            ulong localKey = 0UL)
        {
            if (!ruleId.IsValid) throw new ArgumentException("Rule ID must be valid.", nameof(ruleId));
            ulong hash = WorldGenStableHash64.Derive(seed.Value, x, y, ruleId.Value, localKey);
            return (hash >> 11) * Inverse53;
        }

        public static double Sample01(
            WorldGenerationSeed seed,
            long x,
            long y,
            WorldNoiseKey noiseKey,
            ulong localKey = 0UL)
        {
            if (!noiseKey.IsValid) throw new ArgumentException("Noise key must be valid.", nameof(noiseKey));
            ulong hash = WorldGenStableHash64.DeriveCompiledNoise(
                seed.Value,
                x,
                y,
                noiseKey.StableHash,
                localKey);
            return (hash >> 11) * Inverse53;
        }
    }
}
