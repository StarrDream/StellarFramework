namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureInstanceData
    {
        public int FeatureIndex { get; }
        public double X { get; }
        public double Y { get; }
        public double RotationDegrees { get; }
        public ulong DeterministicKey { get; }
        public WorldFeatureBounds ReservedBounds { get; }

        internal WorldFeatureInstanceData(in WorldFeatureCandidate candidate, in WorldFeatureBounds reservedBounds)
        {
            FeatureIndex = candidate.FeatureIndex;
            X = candidate.X;
            Y = candidate.Y;
            RotationDegrees = candidate.RotationDegrees;
            DeterministicKey = candidate.DeterministicKey;
            ReservedBounds = reservedBounds;
        }
    }
}
