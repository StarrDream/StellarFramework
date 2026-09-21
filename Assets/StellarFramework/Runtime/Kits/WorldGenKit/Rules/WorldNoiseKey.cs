using System;

namespace StellarFramework.WorldGenKit
{
    /// <summary>
    /// Precompiled deterministic noise identity for hot sampling paths.
    /// Existing WorldRuleId-based sampling remains available and unchanged.
    /// </summary>
    public readonly struct WorldNoiseKey : IEquatable<WorldNoiseKey>
    {
        private readonly ulong _stableHash;

        public WorldRuleId RuleId { get; }
        public bool IsValid => RuleId.IsValid;

        internal ulong StableHash => _stableHash;

        internal WorldNoiseKey(WorldRuleId ruleId, ulong stableHash)
        {
            RuleId = ruleId;
            _stableHash = stableHash;
        }

        public bool Equals(WorldNoiseKey other) => RuleId == other.RuleId && _stableHash == other._stableHash;
        public override bool Equals(object obj) => obj is WorldNoiseKey other && Equals(other);
        public override int GetHashCode() => unchecked((RuleId.GetHashCode() * 397) ^ _stableHash.GetHashCode());
        public override string ToString() => RuleId.ToString();
        public static bool operator ==(WorldNoiseKey left, WorldNoiseKey right) => left.Equals(right);
        public static bool operator !=(WorldNoiseKey left, WorldNoiseKey right) => !left.Equals(right);
    }
}
