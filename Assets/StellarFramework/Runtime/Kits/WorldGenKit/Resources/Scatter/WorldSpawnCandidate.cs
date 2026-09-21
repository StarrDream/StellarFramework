using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldSpawnCandidate
    {
        public int ResourceIndex { get; }
        public int SampleIndex { get; }
        public long X { get; }
        public long Y { get; }
        public double Score { get; }
        public ulong DeterministicKey { get; }
        public double Richness { get; }

        public WorldSpawnCandidate(
            int resourceIndex,
            int sampleIndex,
            long x,
            long y,
            double score,
            ulong deterministicKey,
            double richness)
        {
            if (resourceIndex < 0) throw new ArgumentOutOfRangeException(nameof(resourceIndex));
            if (sampleIndex < 0) throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            if (double.IsNaN(score) || double.IsInfinity(score)) throw new ArgumentOutOfRangeException(nameof(score));
            if (double.IsNaN(richness) || double.IsInfinity(richness) || richness < 0d)
                throw new ArgumentOutOfRangeException(nameof(richness));

            ResourceIndex = resourceIndex;
            SampleIndex = sampleIndex;
            X = x;
            Y = y;
            Score = score;
            DeterministicKey = deterministicKey;
            Richness = richness;
        }
    }
}
