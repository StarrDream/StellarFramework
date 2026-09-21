using System;

namespace StellarFramework.WorldGenKit
{
    public interface IWorldEligibilityRule<TContext>
    {
        WorldRuleId Id { get; }
        bool IsEligible(in TContext context);
    }

    public interface IWorldScoreRule<TContext>
    {
        WorldRuleId Id { get; }
        double EvaluateScore(in TContext context);
    }

    public readonly struct WorldRuleEvaluation
    {
        public bool Eligible { get; }
        public double Score { get; }

        public WorldRuleEvaluation(bool eligible, double score)
        {
            if (double.IsNaN(score) || double.IsInfinity(score))
                throw new ArgumentOutOfRangeException(nameof(score), score, "Rule score must be finite.");

            Eligible = eligible;
            Score = score;
        }
    }
}
