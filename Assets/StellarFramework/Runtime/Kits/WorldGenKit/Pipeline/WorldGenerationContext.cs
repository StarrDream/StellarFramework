using System;

namespace StellarFramework.WorldGenKit
{
    public enum WorldGenerationSeedScope
    {
        None = 0,
        World = 1,
        Region = 2,
        Chunk = 3,
        RunKey = 4
    }

    public readonly struct WorldGenerationRunKey : IEquatable<WorldGenerationRunKey>
    {
        public long X { get; }
        public long Y { get; }
        public ulong LocalKey { get; }

        public WorldGenerationRunKey(long x, long y, ulong localKey = 0UL)
        {
            X = x;
            Y = y;
            LocalKey = localKey;
        }

        public bool Equals(WorldGenerationRunKey other) => X == other.X && Y == other.Y && LocalKey == other.LocalKey;
        public override bool Equals(object obj) => obj is WorldGenerationRunKey other && Equals(other);
        public override int GetHashCode() => unchecked(((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ LocalKey.GetHashCode());
        public static bool operator ==(WorldGenerationRunKey left, WorldGenerationRunKey right) => left.Equals(right);
        public static bool operator !=(WorldGenerationRunKey left, WorldGenerationRunKey right) => !left.Equals(right);
    }

    public readonly struct WorldGenerationContext
    {
        public WorldGenerationDataSet Data { get; }
        public WorldGenerationSeed StageSeed { get; }
        public WorldGenerationRunKey RunKey { get; }
        public WorldGenerationStageId StageId { get; }

        internal WorldGenerationContext(
            WorldGenerationDataSet data,
            WorldGenerationSeed stageSeed,
            WorldGenerationRunKey runKey,
            WorldGenerationStageId stageId)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            StageSeed = stageSeed;
            RunKey = runKey;
            StageId = stageId;
        }
    }
}
