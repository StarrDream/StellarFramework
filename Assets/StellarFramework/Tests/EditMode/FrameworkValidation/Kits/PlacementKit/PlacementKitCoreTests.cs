using System;
using NUnit.Framework;
using StellarFramework.PlacementKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class PlacementKitCoreTests
    {
        [Test]
        public void DefaultFootprintIsInvalidAndRequestRejectsIt()
        {
            PlacementFootprint footprint = default(PlacementFootprint);
            Assert.That(footprint.IsValid, Is.False);
            Assert.That(
                () => new PlacementRequest(
                    PlacementTypeId.From("placement.test"),
                    0d,
                    0d,
                    0d,
                    footprint),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void RotatedRectangleAndCircleProduceDeterministicBounds()
        {
            PlacementFootprint rectangle = PlacementFootprint.Rectangle(10d, 4d);
            PlacementBounds2D rotated = rectangle.GetAxisAlignedBounds(100d, 200d, 90d);
            Assert.That(rotated.Width, Is.EqualTo(4d).Within(0.0000001d));
            Assert.That(rotated.Height, Is.EqualTo(10d).Within(0.0000001d));

            PlacementFootprint circle = PlacementFootprint.Circle(3d);
            PlacementBounds2D circleBounds = circle.GetAxisAlignedBounds(-5d, 8d, 45d);
            Assert.That(circleBounds.MinX, Is.EqualTo(-8d));
            Assert.That(circleBounds.MaxX, Is.EqualTo(-2d));
            Assert.That(circleBounds.MinY, Is.EqualTo(5d));
            Assert.That(circleBounds.MaxY, Is.EqualTo(11d));
        }

        [Test]
        public void BuiltInRulesPassValidSiteAndAccumulateSuitability()
        {
            PlacementRequest request = Request();
            PlacementSiteFacts facts = new PlacementSiteFacts(
                maxSlopeDegrees: 12d,
                minWaterDepth: 0d,
                maxWaterDepth: 0.5d,
                zoneMask: 0b0011UL,
                conflictMask: 0UL,
                connectionMask: 0b0101UL,
                baseSuitability: 3.5d);
            IPlacementRule<PlacementSiteFacts>[] rules =
            {
                new PlacementSlopeRule(20d),
                new PlacementWaterDepthRule(0d, 1d),
                new PlacementRequiredZoneRule(0b0010UL),
                new PlacementConflictRule(0b1000UL),
                new PlacementConnectionRule(0b0100UL),
                new PlacementBaseSuitabilityRule()
            };
            PlacementFailureRecord[] failures = new PlacementFailureRecord[rules.Length];

            PlacementEvaluationResult result = PlacementEvaluator.Evaluate(
                in request,
                in facts,
                rules.AsSpan(),
                failures.AsSpan());

            Assert.That(result.Allowed, Is.True);
            Assert.That(result.FailureCount, Is.EqualTo(0));
            Assert.That(result.Score, Is.EqualTo(3.5d));
        }

        [Test]
        public void CollectAllFailuresReportsSlopeWaterZoneConflictAndConnection()
        {
            PlacementRequest request = Request();
            PlacementSiteFacts facts = new PlacementSiteFacts(
                maxSlopeDegrees: 35d,
                minWaterDepth: 2d,
                maxWaterDepth: 4d,
                zoneMask: 0b0001UL,
                conflictMask: 0b1000UL,
                connectionMask: 0b0001UL);
            IPlacementRule<PlacementSiteFacts>[] rules =
            {
                new PlacementSlopeRule(15d),
                new PlacementWaterDepthRule(0d, 1d),
                new PlacementRequiredZoneRule(0b0010UL),
                new PlacementConflictRule(0b1000UL),
                new PlacementConnectionRule(0b0100UL)
            };
            PlacementFailureRecord[] failures = new PlacementFailureRecord[rules.Length];

            PlacementEvaluationResult result = PlacementEvaluator.Evaluate(
                in request,
                in facts,
                rules.AsSpan(),
                failures.AsSpan(),
                collectAllFailures: true);

            Assert.That(result.Allowed, Is.False);
            Assert.That(result.FailureCount, Is.EqualTo(5));
            Assert.That(failures[0].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.Slope));
            Assert.That(failures[1].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.WaterDepth));
            Assert.That(failures[2].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.Zone));
            Assert.That(failures[3].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.Conflict));
            Assert.That(failures[4].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.Connection));
        }

        [Test]
        public void FirstFailureModeStopsImmediatelyAndNeedsSingleFailureSlot()
        {
            PlacementRequest request = Request();
            PlacementSiteFacts facts = new PlacementSiteFacts(
                maxSlopeDegrees: 60d,
                minWaterDepth: 0d,
                maxWaterDepth: 0d,
                zoneMask: 0UL,
                conflictMask: 0UL,
                connectionMask: 0UL);
            CountingRule secondRule = new CountingRule();
            IPlacementRule<PlacementSiteFacts>[] rules =
            {
                new PlacementSlopeRule(5d),
                secondRule
            };
            PlacementFailureRecord[] failures = new PlacementFailureRecord[1];

            PlacementEvaluationResult result = PlacementEvaluator.Evaluate(
                in request,
                in facts,
                rules.AsSpan(),
                failures.AsSpan(),
                collectAllFailures: false);

            Assert.That(result.Allowed, Is.False);
            Assert.That(result.FailureCount, Is.EqualTo(1));
            Assert.That(failures[0].FailureId, Is.EqualTo(PlacementBuiltInFailureIds.Slope));
            Assert.That(secondRule.EvaluationCount, Is.EqualTo(0));
        }

        [Test]
        public void RequiredZoneAndConnectionSupportAnyOrAllSemantics()
        {
            PlacementRequest request = Request();
            PlacementSiteFacts facts = new PlacementSiteFacts(
                0d,
                0d,
                0d,
                zoneMask: 0b0010UL,
                conflictMask: 0UL,
                connectionMask: 0b0100UL);

            PlacementRuleEvaluation zoneAny = new PlacementRequiredZoneRule(0b0110UL, requireAll: false).Evaluate(in request, in facts);
            PlacementRuleEvaluation zoneAll = new PlacementRequiredZoneRule(0b0110UL, requireAll: true).Evaluate(in request, in facts);
            PlacementRuleEvaluation connectionAny = new PlacementConnectionRule(0b1100UL, requireAll: false).Evaluate(in request, in facts);
            PlacementRuleEvaluation connectionAll = new PlacementConnectionRule(0b1100UL, requireAll: true).Evaluate(in request, in facts);

            Assert.That(zoneAny.Allowed, Is.True);
            Assert.That(zoneAll.Allowed, Is.False);
            Assert.That(connectionAny.Allowed, Is.True);
            Assert.That(connectionAll.Allowed, Is.False);
        }

        [Test]
        public void CustomRuleCanExtendCoreWithoutChangingEvaluator()
        {
            PlacementRequest request = Request();
            PlacementSiteFacts facts = new PlacementSiteFacts(0d, 0d, 0d, 0UL, 0UL, 0UL);
            CustomScoreRule rule = new CustomScoreRule();
            IPlacementRule<PlacementSiteFacts>[] rules = { rule };
            PlacementFailureRecord[] failures = new PlacementFailureRecord[1];

            PlacementEvaluationResult result = PlacementEvaluator.Evaluate(
                in request,
                in facts,
                rules.AsSpan(),
                failures.AsSpan());

            Assert.That(result.Allowed, Is.True);
            Assert.That(result.Score, Is.EqualTo(12.5d));
            Assert.That(rule.EvaluationCount, Is.EqualTo(1));
        }

        [Test]
        public void EvaluatorPrevalidatesNullRulesBeforeEvaluatingAnyRule()
        {
            PlacementRequest request = Request();
            PlacementSiteFacts facts = new PlacementSiteFacts(0d, 0d, 0d, 0UL, 0UL, 0UL);
            CountingRule first = new CountingRule();
            IPlacementRule<PlacementSiteFacts>[] rules = { first, null };
            PlacementFailureRecord[] failures = new PlacementFailureRecord[rules.Length];

            Assert.That(
                () => PlacementEvaluator.Evaluate(
                    in request,
                    in facts,
                    rules.AsSpan(),
                    failures.AsSpan()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(first.EvaluationCount, Is.EqualTo(0));
        }

        private static PlacementRequest Request() =>
            new PlacementRequest(
                PlacementTypeId.From("placement.building.house"),
                10d,
                20d,
                30d,
                PlacementFootprint.Rectangle(4d, 6d));

        private sealed class CountingRule : IPlacementRule<PlacementSiteFacts>
        {
            public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.counting");
            public int EvaluationCount { get; private set; }

            public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context)
            {
                EvaluationCount++;
                return PlacementRuleEvaluation.Pass();
            }
        }

        private sealed class CustomScoreRule : IPlacementRule<PlacementSiteFacts>
        {
            public PlacementRuleId Id { get; } = PlacementRuleId.From("placement.rule.custom_score");
            public int EvaluationCount { get; private set; }

            public PlacementRuleEvaluation Evaluate(in PlacementRequest request, in PlacementSiteFacts context)
            {
                EvaluationCount++;
                return PlacementRuleEvaluation.Pass(12.5d);
            }
        }
    }
}
