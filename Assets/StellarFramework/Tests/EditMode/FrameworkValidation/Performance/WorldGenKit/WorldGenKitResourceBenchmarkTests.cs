using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Resources;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitResourceBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void WorldGenKitResourcesBenchmark_512x512GenerateBudgetSpacingResolve()
        {
            WorldResourcePlanarDomain layout = new WorldResourcePlanarDomain(
                512,
                512,
                originX: -16384L,
                originY: 8192L);
            WorldGenerationSeed seed = new WorldGenerationSeed(0xC0FFEEUL);

            WorldOccupancyRegistryBuilder occupancyBuilder = new WorldOccupancyRegistryBuilder();
            WorldOccupancyTypeId vegetationId = WorldOccupancyTypeId.From("occupancy.vegetation");
            WorldOccupancyTypeId buildingId = WorldOccupancyTypeId.From("occupancy.building");
            occupancyBuilder.Register(vegetationId);
            occupancyBuilder.Register(buildingId);
            WorldOccupancyRegistry occupancyRegistry = occupancyBuilder.Build();

            WorldResourceDefinition tree = new WorldResourceDefinition(
                WorldResourceId.From("resource.benchmark_tree"),
                WorldResourceCategoryId.From("resource_category.vegetation"),
                new WorldResourceDistributionDefinition(
                    WorldResourceDistributionMode.Density,
                    occurrence: 0.04d,
                    clusterSize: 2,
                    richness: 1d,
                    minSpacing: 2d),
                occupancyRegistry.CreateMask(vegetationId),
                occupancyRegistry.CreateMask(buildingId),
                priority: 10);
            WorldResourceCatalog catalog = new WorldResourceCatalog(new[] { tree }.AsSpan());
            catalog.TryGetIndex(tree.Id, out int treeIndex);
            WorldResolvedResourceGenerationSettings settings =
                new WorldResourceGenerationSettings(WorldResourceGenerationMultiplier.Identity).Resolve(tree);
            WorldCompiledResourceBudget budget = new WorldResourceBudget(globalMaxAccepted: 10000).Compile(catalog);

            byte[] eligibility = new byte[layout.Count];
            float[] suitability = new float[layout.Count];
            for (int i = 0; i < layout.Count; i++)
            {
                eligibility[i] = 1;
                suitability[i] = (i % 97) / 96f;
            }

            WorldSpawnCandidate[] candidates = new WorldSpawnCandidate[layout.Count];
            WorldCoverageSampleRank[] coverageScratch = new WorldCoverageSampleRank[layout.Count];
            WorldOccupancyCellState[] occupancyCells = new WorldOccupancyCellState[layout.Count];
            int[] orderScratch = new int[layout.Count];
            int[] resourceCounts = new int[catalog.Count];
            int[] categoryCounts = new int[catalog.CategoryCount];
            int[] spacingHeads = new int[layout.Count];
            WorldScatterSpacingNode[] spacingNodes = new WorldScatterSpacingNode[layout.Count];
            WorldSpawnRecord[] output = new WorldSpawnRecord[layout.Count];

            // Warm once so the reported samples are less dominated by editor/JIT cold-start noise.
            WorldResourceCandidateGenerationResult generation = WorldResourceCandidateGenerator.Generate(
                treeIndex,
                catalog,
                in layout,
                seed,
                in settings,
                eligibility.AsSpan(),
                suitability.AsSpan(),
                candidates.AsSpan(),
                coverageScratch.AsSpan());
            WorldScatterResolveResult resolve = WorldResourceScatterResolver.Resolve(
                candidates.AsSpan(0, generation.GeneratedCount),
                catalog,
                in layout,
                budget,
                occupancyCells.AsSpan(),
                orderScratch.AsSpan(),
                resourceCounts.AsSpan(),
                categoryCounts.AsSpan(),
                spacingHeads.AsSpan(),
                spacingNodes.AsSpan(),
                output.AsSpan());

            const int measuredIterations = 5;
            double[] generationMs = new double[measuredIterations];
            double[] resolveMs = new double[measuredIterations];
            long heapBefore = GC.GetTotalMemory(false);

            for (int iteration = 0; iteration < measuredIterations; iteration++)
            {
                long generationStart = Stopwatch.GetTimestamp();
                generation = WorldResourceCandidateGenerator.Generate(
                    treeIndex,
                    catalog,
                    in layout,
                    seed,
                    in settings,
                    eligibility.AsSpan(),
                    suitability.AsSpan(),
                    candidates.AsSpan(),
                    coverageScratch.AsSpan());
                long generationEnd = Stopwatch.GetTimestamp();
                generationMs[iteration] =
                    (generationEnd - generationStart) * 1000d / Stopwatch.Frequency;

                Array.Clear(occupancyCells, 0, occupancyCells.Length);
                long resolveStart = Stopwatch.GetTimestamp();
                resolve = WorldResourceScatterResolver.Resolve(
                    candidates.AsSpan(0, generation.GeneratedCount),
                    catalog,
                    in layout,
                    budget,
                    occupancyCells.AsSpan(),
                    orderScratch.AsSpan(),
                    resourceCounts.AsSpan(),
                    categoryCounts.AsSpan(),
                    spacingHeads.AsSpan(),
                    spacingNodes.AsSpan(),
                    output.AsSpan());
                long resolveEnd = Stopwatch.GetTimestamp();
                resolveMs[iteration] =
                    (resolveEnd - resolveStart) * 1000d / Stopwatch.Frequency;
            }

            long heapDelta = GC.GetTotalMemory(false) - heapBefore;

            Array.Sort(generationMs);
            Array.Sort(resolveMs);
            double generationMinMs = generationMs[0];
            double generationMedianMs = generationMs[measuredIterations / 2];
            double resolveMinMs = resolveMs[0];
            double resolveMedianMs = resolveMs[measuredIterations / 2];

            long checksum = 0L;
            for (int i = 0; i < resolve.AcceptedCount; i++)
                checksum += output[i].SampleIndex * 31L + output[i].X * 17L + output[i].Y;

            string message = string.Format(
                "WorldGenKit Resources benchmark env={0} samples={1} generated={2} accepted={3} rejectedBudget={4} rejectedSpacing={5} rejectedOccupancy={6} measuredIterations={7} generateMinMs={8:F3} generateMedianMs={9:F3} resolveMinMs={10:F3} resolveMedianMs={11:F3} checksum={12} allocationDelta={13}",
                Application.unityVersion,
                layout.Count,
                generation.GeneratedCount,
                resolve.AcceptedCount,
                resolve.RejectedBudgetCount,
                resolve.RejectedSpacingCount,
                resolve.RejectedOccupancyCount,
                measuredIterations,
                generationMinMs,
                generationMedianMs,
                resolveMinMs,
                resolveMedianMs,
                checksum,
                heapDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(generation.GeneratedCount, Is.GreaterThan(0));
            Assert.That(resolve.AcceptedCount, Is.GreaterThan(0).And.LessThanOrEqualTo(10000));
            Assert.That(resolve.RejectedSpacingCount + resolve.RejectedBudgetCount, Is.GreaterThan(0));
            Assert.That(checksum, Is.Not.EqualTo(0L));
        }
    }
}
