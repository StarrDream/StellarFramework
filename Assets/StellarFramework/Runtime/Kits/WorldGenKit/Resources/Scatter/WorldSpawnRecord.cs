namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldSpawnRecord
    {
        public int ResourceIndex { get; }
        public int SampleIndex { get; }
        public long X { get; }
        public long Y { get; }
        public double Richness { get; }

        internal WorldSpawnRecord(in WorldSpawnCandidate candidate)
        {
            ResourceIndex = candidate.ResourceIndex;
            SampleIndex = candidate.SampleIndex;
            X = candidate.X;
            Y = candidate.Y;
            Richness = candidate.Richness;
        }
    }
}
