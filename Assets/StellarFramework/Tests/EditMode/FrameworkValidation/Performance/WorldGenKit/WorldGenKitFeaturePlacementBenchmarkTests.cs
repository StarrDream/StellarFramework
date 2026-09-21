using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.PlacementKit;
using StellarFramework.WorldGenKit.Feature;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitFeaturePlacementBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void FeatureResolverAndPlacementEvaluatorBenchmark()
        {
            const int featureCandidateCount = 4096;
            const int placementEvaluationCount = 100000;
            const int measuredIterations = 5;

            WorldFeatureDefinition landmark = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.benchmark_landmark"),
                WorldFeatureCategoryId.From("feature_category.benchmark"),
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(1d, 1d),
                WorldFeatureQuota.Unlimited(),
                priority: 10);
            WorldFeatureCatalog catalog =
                new WorldFeatureCatalog(new[] { landmark }.AsSpan());

            WorldFeatureCandidate[] candidates =
                new WorldFeatureCandidate[featureCandidateCount];
            for (int i = 0; i < candidates.Length; i++)
            {
                int x = i & 63;
                int y = i >> 6;
                candidates[i] = new WorldFeatureCandidate(
                    0,
                    x * 2d,
                    y * 2d,
                    0d,
                    1d - (i * 0.000001d),
                    (ulong)(i + 1));
            }

            int[] orderScratch = new int[featureCandidateCount];
            int[] acceptedCountScratch = new int[catalog.Count];
            WorldFeatureReservation[] acceptedReservations =
                new WorldFeatureReservation[featureCandidateCount];
            WorldFeatureInstanceData[] featureOutput =
                new WorldFeatureInstanceData[featureCandidateCount];

            PlacementRequest placementRequest = new PlacementRequest(
                PlacementTypeId.From("placement.benchmark"),
                0d,
                0d,
                0d,
                PlacementFootprint.Rectangle(4d, 4d));
            PlacementSiteFacts siteFacts = new PlacementSiteFacts(
                maxSlopeDegrees: 5d,
                minWaterDepth: 0d,
                maxWaterDepth: 0d,
                zoneMask: 0b0011UL,
                conflictMask: 0UL,
                connectionMask: 0b0101UL,
                baseSuitability: 0.75d);
            IPlacementRule<PlacementSiteFacts>[] rules =
            {
                new PlacementSlopeRule(10d),
                new PlacementWaterDepthRule(0d, 0d),
                new PlacementRequiredZoneRule(0b0010UL),
                new PlacementConflictRule(0b1000UL),
                new PlacementConnectionRule(0b0100UL),
                new PlacementBaseSuitabilityRule()
            };
            PlacementFailureRecord[] failureBuffer =
                new PlacementFailureRecord[rules.Length];

            WorldFeatureResolveResult featureWarmup = ResolveFeatures(
                candidates,
                catalog,
                orderScratch,
                acceptedCountScratch,
                acceptedReservations,
                featureOutput);
            PlacementEvaluationResult placementWarmup =
                PlacementEvaluator.Evaluate(
                    in placementRequest,
                    in siteFacts,
                    rules.AsSpan(),
                    failureBuffer.AsSpan());
            Assert.That(featureWarmup.AcceptedCount, Is.EqualTo(featureCandidateCount));
            Assert.That(placementWarmup.Allowed, Is.True);

            double[] featureMs = new double[measuredIterations];
            double[] placementMs = new double[measuredIterations];
            long checksum = 0L;
            long heapBefore = GC.GetTotalMemory(false);

            for (int iteration = 0; iteration < measuredIterations; iteration++)
            {
                long featureStart = Stopwatch.GetTimestamp();
                WorldFeatureResolveResult featureResult = ResolveFeatures(
                    candidates,
                    catalog,
                    orderScratch,
                    acceptedCountScratch,
                    acceptedReservations,
                    featureOutput);
                long featureEnd = Stopwatch.GetTimestamp();
                featureMs[iteration] =
                    (featureEnd - featureStart) * 1000d / Stopwatch.Frequency;
                checksum += featureResult.AcceptedCount;
                checksum += (long)featureOutput[featureResult.AcceptedCount - 1].X;

                long placementStart = Stopwatch.GetTimestamp();
                double scoreAccumulator = 0d;
                for (int i = 0; i < placementEvaluationCount; i++)
                {
                    PlacementEvaluationResult placementResult =
                        PlacementEvaluator.Evaluate(
                            in placementRequest,
                            in siteFacts,
                            rules.AsSpan(),
                            failureBuffer.AsSpan());
                    scoreAccumulator += placementResult.Score;
                    checksum += placementResult.FailureCount;
                }
                long placementEnd = Stopwatch.GetTimestamp();
                placementMs[iteration] =
                    (placementEnd - placementStart) * 1000d / Stopwatch.Frequency;
                checksum += (long)(scoreAccumulator * 1000d);
            }

            long heapDelta = GC.GetTotalMemory(false) - heapBefore;
            Array.Sort(featureMs);
            Array.Sort(placementMs);

            double featureMin = featureMs[0];
            double featureMedian = featureMs[measuredIterations / 2];
            double placementMin = placementMs[0];
            double placementMedian = placementMs[measuredIterations / 2];

            string message = string.Format(
                "Feature/Placement benchmark env={0} featureCandidates={1} featureMinMs={2:F3} featureMedianMs={3:F3} placementEvaluations={4} placementMinMs={5:F3} placementMedianMs={6:F3} checksum={7} allocationDelta={8}",
                Application.unityVersion,
                featureCandidateCount,
                featureMin,
                featureMedian,
                placementEvaluationCount,
                placementMin,
                placementMedian,
                checksum,
                heapDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(checksum, Is.Not.EqualTo(0L));
        }

        private static WorldFeatureResolveResult ResolveFeatures(
            WorldFeatureCandidate[] candidates,
            WorldFeatureCatalog catalog,
            int[] orderScratch,
            int[] acceptedCountScratch,
            WorldFeatureReservation[] acceptedReservations,
            WorldFeatureInstanceData[] featureOutput)
        {
            return WorldFeatureResolver.Resolve(
                candidates.AsSpan(),
                catalog,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<WorldFeatureReservation>.Empty,
                orderScratch.AsSpan(),
                acceptedCountScratch.AsSpan(),
                acceptedReservations.AsSpan(),
                featureOutput.AsSpan());
        }
    }
}
