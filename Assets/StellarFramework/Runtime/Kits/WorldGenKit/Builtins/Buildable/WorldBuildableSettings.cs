using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldBuildableSettings
    {
        private readonly WorldBiomeId[] _blockedBiomes;

        public float MaxSlope { get; }
        public float MaxWaterDepth { get; }
        public ReadOnlySpan<WorldBiomeId> BlockedBiomes => _blockedBiomes;

        public WorldBuildableSettings(
            float maxSlope,
            float maxWaterDepth,
            ReadOnlySpan<WorldBiomeId> blockedBiomes = default(ReadOnlySpan<WorldBiomeId>))
        {
            if (float.IsNaN(maxSlope) || float.IsInfinity(maxSlope) || maxSlope < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxSlope));
            if (float.IsNaN(maxWaterDepth) || float.IsInfinity(maxWaterDepth) || maxWaterDepth < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxWaterDepth));

            MaxSlope = maxSlope;
            MaxWaterDepth = maxWaterDepth;
            _blockedBiomes = blockedBiomes.ToArray();
            for (int i = 0; i < _blockedBiomes.Length; i++)
            {
                if (!_blockedBiomes[i].IsValid)
                    throw new ArgumentException("Blocked biome list contains an invalid ID.", nameof(blockedBiomes));
                for (int j = 0; j < i; j++)
                {
                    if (_blockedBiomes[j] == _blockedBiomes[i])
                        throw new ArgumentException("Blocked biome list cannot contain duplicates.", nameof(blockedBiomes));
                }
            }
        }
    }
}
