using System;

namespace StellarFramework.PlacementKit
{
    public static class PlacementBuiltInFailureIds
    {
        public static readonly PlacementFailureId Slope = PlacementFailureId.From("placement.failure.slope");
        public static readonly PlacementFailureId WaterDepth = PlacementFailureId.From("placement.failure.water_depth");
        public static readonly PlacementFailureId Zone = PlacementFailureId.From("placement.failure.zone");
        public static readonly PlacementFailureId Conflict = PlacementFailureId.From("placement.failure.conflict");
        public static readonly PlacementFailureId Connection = PlacementFailureId.From("placement.failure.connection");
    }

    public sealed class PlacementSlopeRule : IPlacementRule<PlacementSiteFacts>
    {
        public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.slope");
        public double MaxSlopeDegrees { get; }

        public PlacementSlopeRule(double maxSlopeDegrees)
        {
            if (double.IsNaN(maxSlopeDegrees) || double.IsInfinity(maxSlopeDegrees) || maxSlopeDegrees < 0d)
                throw new ArgumentOutOfRangeException(nameof(maxSlopeDegrees));
            MaxSlopeDegrees = maxSlopeDegrees;
        }

        public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context) =>
            context.MaxSlopeDegrees <= MaxSlopeDegrees
                ? PlacementRuleEvaluation.Pass()
                : PlacementRuleEvaluation.Fail(PlacementBuiltInFailureIds.Slope);
    }

    public sealed class PlacementWaterDepthRule : IPlacementRule<PlacementSiteFacts>
    {
        public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.water_depth");
        public double MinDepth { get; }
        public double MaxDepth { get; }

        public PlacementWaterDepthRule(double minDepth, double maxDepth)
        {
            if (double.IsNaN(minDepth) || double.IsInfinity(minDepth)) throw new ArgumentOutOfRangeException(nameof(minDepth));
            if (double.IsNaN(maxDepth) || double.IsInfinity(maxDepth) || maxDepth < minDepth)
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            MinDepth = minDepth;
            MaxDepth = maxDepth;
        }

        public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context) =>
            context.MinWaterDepth >= MinDepth && context.MaxWaterDepth <= MaxDepth
                ? PlacementRuleEvaluation.Pass()
                : PlacementRuleEvaluation.Fail(PlacementBuiltInFailureIds.WaterDepth);
    }

    public sealed class PlacementRequiredZoneRule : IPlacementRule<PlacementSiteFacts>
    {
        public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.zone");
        public ulong RequiredMask { get; }
        public bool RequireAll { get; }

        public PlacementRequiredZoneRule(ulong requiredMask, bool requireAll = true)
        {
            if (requiredMask == 0UL) throw new ArgumentOutOfRangeException(nameof(requiredMask));
            RequiredMask = requiredMask;
            RequireAll = requireAll;
        }

        public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context)
        {
            ulong present = context.ZoneMask & RequiredMask;
            bool allowed = RequireAll ? present == RequiredMask : present != 0UL;
            return allowed ? PlacementRuleEvaluation.Pass() : PlacementRuleEvaluation.Fail(PlacementBuiltInFailureIds.Zone);
        }
    }

    public sealed class PlacementConflictRule : IPlacementRule<PlacementSiteFacts>
    {
        public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.conflict");
        public ulong ForbiddenMask { get; }

        public PlacementConflictRule(ulong forbiddenMask)
        {
            if (forbiddenMask == 0UL) throw new ArgumentOutOfRangeException(nameof(forbiddenMask));
            ForbiddenMask = forbiddenMask;
        }

        public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context) =>
            (context.ConflictMask & ForbiddenMask) == 0UL
                ? PlacementRuleEvaluation.Pass()
                : PlacementRuleEvaluation.Fail(PlacementBuiltInFailureIds.Conflict);
    }

    public sealed class PlacementConnectionRule : IPlacementRule<PlacementSiteFacts>
    {
        public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.connection");
        public ulong RequiredMask { get; }
        public bool RequireAll { get; }

        public PlacementConnectionRule(ulong requiredMask, bool requireAll = true)
        {
            if (requiredMask == 0UL) throw new ArgumentOutOfRangeException(nameof(requiredMask));
            RequiredMask = requiredMask;
            RequireAll = requireAll;
        }

        public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context)
        {
            ulong present = context.ConnectionMask & RequiredMask;
            bool allowed = RequireAll ? present == RequiredMask : present != 0UL;
            return allowed ? PlacementRuleEvaluation.Pass() : PlacementRuleEvaluation.Fail(PlacementBuiltInFailureIds.Connection);
        }
    }

    public sealed class PlacementBaseSuitabilityRule : IPlacementRule<PlacementSiteFacts>
    {
        public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.base_suitability");

        public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context) =>
            PlacementRuleEvaluation.Pass(context.BaseSuitability);
    }
}
