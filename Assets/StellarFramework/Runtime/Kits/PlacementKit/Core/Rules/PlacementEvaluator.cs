using System;

namespace StellarFramework.PlacementKit
{
    public static class PlacementEvaluator
    {
        public static PlacementEvaluationResult Evaluate<TContext>(
            in PlacementRequest request,
            in TContext context,
            ReadOnlySpan<IPlacementRule<TContext>> rules,
            Span<PlacementFailureRecord> failures,
            bool collectAllFailures = true)
        {
            int requiredFailureCapacity = collectAllFailures ? rules.Length : (rules.Length == 0 ? 0 : 1);
            if (failures.Length < requiredFailureCapacity)
                throw new ArgumentException("Failure buffer is too small for the requested evaluation mode.", nameof(failures));

            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] == null) throw new ArgumentException("Placement rule list contains null.", nameof(rules));
                if (!rules[i].Id.IsValid) throw new ArgumentException("Placement rule ID must be valid.", nameof(rules));
            }

            double score = 0d;
            int failureCount = 0;
            for (int i = 0; i < rules.Length; i++)
            {
                IPlacementRule<TContext> rule = rules[i];
                PlacementRuleEvaluation evaluation = rule.Evaluate(in request, in context);
                if (evaluation.Allowed)
                {
                    score += evaluation.ScoreDelta;
                    if (double.IsNaN(score) || double.IsInfinity(score))
                        throw new OverflowException("Placement suitability score overflowed.");
                    continue;
                }

                failures[failureCount++] = new PlacementFailureRecord(rule.Id, evaluation.FailureId);
                if (!collectAllFailures) break;
            }

            return new PlacementEvaluationResult(failureCount == 0, score, failureCount);
        }
    }
}
