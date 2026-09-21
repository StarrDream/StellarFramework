using System;

namespace StellarFramework.PlacementKit
{
    public readonly struct PlacementRuleEvaluation
    {
        public bool Allowed { get; }
        public PlacementFailureId FailureId { get; }
        public double ScoreDelta { get; }

        private PlacementRuleEvaluation(bool allowed, PlacementFailureId failureId, double scoreDelta)
        {
            if (double.IsNaN(scoreDelta) || double.IsInfinity(scoreDelta))
                throw new ArgumentOutOfRangeException(nameof(scoreDelta));
            if (!allowed && !failureId.IsValid)
                throw new ArgumentException("Rejected placement rule evaluation requires a valid failure ID.", nameof(failureId));
            Allowed = allowed;
            FailureId = failureId;
            ScoreDelta = scoreDelta;
        }

        public static PlacementRuleEvaluation Pass(double scoreDelta = 0d) =>
            new PlacementRuleEvaluation(true, default(PlacementFailureId), scoreDelta);

        public static PlacementRuleEvaluation Fail(PlacementFailureId failureId) =>
            new PlacementRuleEvaluation(false, failureId, 0d);
    }

    public interface IPlacementRule<TContext>
    {
        PlacementRuleId Id { get; }
        PlacementRuleEvaluation Evaluate(in PlacementRequest request, in TContext context);
    }

    public readonly struct PlacementFailureRecord
    {
        public PlacementRuleId RuleId { get; }
        public PlacementFailureId FailureId { get; }

        internal PlacementFailureRecord(PlacementRuleId ruleId, PlacementFailureId failureId)
        {
            RuleId = ruleId;
            FailureId = failureId;
        }
    }

    public readonly struct PlacementEvaluationResult
    {
        public bool Allowed { get; }
        public double Score { get; }
        public int FailureCount { get; }

        internal PlacementEvaluationResult(bool allowed, double score, int failureCount)
        {
            Allowed = allowed;
            Score = score;
            FailureCount = failureCount;
        }
    }
}
