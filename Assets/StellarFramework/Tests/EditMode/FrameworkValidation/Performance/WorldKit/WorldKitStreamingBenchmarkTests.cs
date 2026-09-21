using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldKitStreamingBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void StreamingChurnBenchmark()
        {
            const int measuredIterations = 5;
            const int movementSteps = 200;
            WorldStreamingPolicy policy = new WorldStreamingPolicy(24, 16, 10, 6);
            WorldExtent extent = WorldExtent.Infinite;
            WorldChunkCoord initialFocus = new WorldChunkCoord(-1000, 2000);
            int demandCapacity = WorldStreamingPlanner.GetRequiredDemandCount(initialFocus, extent, in policy);

            WorldChunkStreamingRegistry registry = new WorldChunkStreamingRegistry(demandCapacity);
            WorldChunkStreamingState[] residentScratch = new WorldChunkStreamingState[demandCapacity];
            WorldChunkDemand[] demandScratch = new WorldChunkDemand[demandCapacity];
            WorldStreamingTransition[] transitionScratch = new WorldStreamingTransition[demandCapacity * 2];

            InitializeRegistry(
                registry,
                initialFocus,
                extent,
                in policy,
                demandScratch);
            RunMovement(
                registry,
                initialFocus,
                extent,
                in policy,
                movementSteps,
                residentScratch,
                demandScratch,
                transitionScratch);

            double[] elapsedMs = new double[measuredIterations];
            long transitionChecksum = 0L;
            long exactHotPathAllocatedBytes = 0L;
            long heapBefore = GC.GetTotalMemory(false);
            for (int iteration = 0; iteration < measuredIterations; iteration++)
            {
                registry.Clear();
                InitializeRegistry(
                    registry,
                    initialFocus,
                    extent,
                    in policy,
                    demandScratch);

                long start = Stopwatch.GetTimestamp();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                transitionChecksum += RunMovement(
                    registry,
                    initialFocus,
                    extent,
                    in policy,
                    movementSteps,
                    residentScratch,
                    demandScratch,
                    transitionScratch);
                exactHotPathAllocatedBytes +=
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                elapsedMs[iteration] =
                    (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            }
            long heapDelta = GC.GetTotalMemory(false) - heapBefore;

            Array.Sort(elapsedMs);
            string message = string.Format(
                "Streaming churn benchmark env={0} metadataRadius={1} residentTarget={2} movementSteps={3} minMedianMs={4:F3}/{5:F3} transitionChecksum={6} finalResident={7} exactHotPathAllocatedBytes={8} coarseHeapDelta={9}",
                Application.unityVersion,
                policy.MetadataRadius,
                demandCapacity,
                movementSteps,
                elapsedMs[0],
                elapsedMs[measuredIterations / 2],
                transitionChecksum,
                registry.Count,
                exactHotPathAllocatedBytes,
                heapDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(transitionChecksum, Is.GreaterThan(0L));
            Assert.That(registry.Count, Is.EqualTo(demandCapacity));
            Assert.That(exactHotPathAllocatedBytes, Is.EqualTo(0L),
                "Streaming churn hot path must remain allocation-free after warmup and buffer reuse.");
        }

        private static void InitializeRegistry(
            WorldChunkStreamingRegistry registry,
            WorldChunkCoord focus,
            WorldExtent extent,
            in WorldStreamingPolicy policy,
            WorldChunkDemand[] demandScratch)
        {
            int count = WorldStreamingPlanner.CollectDesired(
                focus,
                extent,
                in policy,
                demandScratch);
            for (int i = 0; i < count; i++)
            {
                WorldChunkDemand demand = demandScratch[i];
                for (int tier = 1; tier <= (int)demand.Tier; tier++)
                {
                    WorldStreamingTransitionResult result = registry.TryTransition(
                        demand.Coord,
                        (WorldStreamingTier)tier);
                    if (!result.Success)
                        throw new InvalidOperationException("Failed to initialize streaming registry.");
                }
            }
        }

        private static long RunMovement(
            WorldChunkStreamingRegistry registry,
            WorldChunkCoord initialFocus,
            WorldExtent extent,
            in WorldStreamingPolicy policy,
            int movementSteps,
            WorldChunkStreamingState[] residentScratch,
            WorldChunkDemand[] demandScratch,
            WorldStreamingTransition[] transitionScratch)
        {
            long checksum = 0L;
            for (int step = 1; step <= movementSteps; step++)
            {
                WorldChunkCoord focus = new WorldChunkCoord(
                    initialFocus.X + step,
                    initialFocus.Y + (step / 50));
                int transitionCount = WorldStreamingReconciler.CollectTransitions(
                    focus,
                    extent,
                    in policy,
                    registry,
                    residentScratch,
                    demandScratch,
                    transitionScratch);

                for (int i = 0; i < transitionCount; i++)
                {
                    WorldStreamingTransition transition = transitionScratch[i];
                    WorldStreamingTransitionResult result = registry.TryTransition(
                        transition.Coord,
                        transition.To);
                    if (!result.Success)
                        throw new InvalidOperationException("Streaming reconciliation emitted an invalid transition.");
                    checksum += ((long)transition.To + 1L) * 17L +
                                (transition.Coord.X & 0xFFL) +
                                ((transition.Coord.Y & 0xFFL) << 1);
                }
            }

            return checksum;
        }
    }
}
