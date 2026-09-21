using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceCandidateGenerationResult
    {
        public int GeneratedCount { get; }
        public int EligibleCount { get; }
        public int TargetCount { get; }

        internal WorldResourceCandidateGenerationResult(int generatedCount, int eligibleCount, int targetCount)
        {
            GeneratedCount = generatedCount;
            EligibleCount = eligibleCount;
            TargetCount = targetCount;
        }
    }

    public static class WorldResourceCandidateGenerator
    {
        private const ulong DensityLocalKey = 0x44454E53495459UL;
        private const ulong CandidateLocalKey = 0x43414E444944UL;
        private const ulong ClusterLocalKey = 0x434C5553544552UL;

        public static WorldResourceCandidateGenerationResult Generate(
            int resourceIndex,
            WorldResourceCatalog catalog,
            in WorldResourcePlanarDomain domain,
            WorldGenerationSeed worldSeed,
            in WorldResolvedResourceGenerationSettings settings,
            ReadOnlySpan<byte> eligibilityMask,
            ReadOnlySpan<float> suitabilityScores,
            Span<WorldSpawnCandidate> output,
            Span<WorldCoverageSampleRank> coverageScratch)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if ((uint)resourceIndex >= (uint)catalog.Count) throw new ArgumentOutOfRangeException(nameof(resourceIndex));
            ValidateInputLength(eligibilityMask, domain.Count, nameof(eligibilityMask));
            ValidateInputLength(suitabilityScores, domain.Count, nameof(suitabilityScores));
            ValidateSuitabilityScores(suitabilityScores);

            WorldResourceDefinition definition = catalog.GetDefinition(resourceIndex);
            WorldRuleId ruleId = WorldRuleId.From(definition.Id.Value);
            WorldNoiseKey noiseKey = WorldNoiseRule.Compile(ruleId);
            return settings.Mode == WorldResourceDistributionMode.Coverage
                ? GenerateCoverage(
                    resourceIndex, in domain, worldSeed, noiseKey, in settings,
                    eligibilityMask, suitabilityScores, output, coverageScratch)
                : GenerateDensity(
                    resourceIndex, in domain, worldSeed, noiseKey, in settings,
                    eligibilityMask, suitabilityScores, output);
        }

        private static WorldResourceCandidateGenerationResult GenerateDensity(
            int resourceIndex,
            in WorldResourcePlanarDomain domain,
            WorldGenerationSeed seed,
            WorldNoiseKey noiseKey,
            in WorldResolvedResourceGenerationSettings settings,
            ReadOnlySpan<byte> eligibilityMask,
            ReadOnlySpan<float> suitabilityScores,
            Span<WorldSpawnCandidate> output)
        {
            int eligibleCount = 0;
            int required = 0;
            for (int y = 0; y < domain.Height; y++)
            {
                for (int x = 0; x < domain.Width; x++)
                {
                    int seedIndex = domain.GetIndex(x, y);
                    if (!IsEligible(eligibilityMask, seedIndex)) continue;
                    eligibleCount++;
                    long absoluteX = domain.GetAbsoluteX(x);
                    long absoluteY = domain.GetAbsoluteY(y);
                    double occurrence = WorldNoiseRule.Sample01(seed, absoluteX, absoluteY, noiseKey, DensityLocalKey);
                    if (occurrence >= settings.Occurrence) continue;
                    required += CountClusterCandidates(
                        x, y, settings.ClusterSize, in domain, eligibilityMask,
                        ToKey(WorldNoiseRule.Sample01(seed, absoluteX, absoluteY, noiseKey, ClusterLocalKey)));
                }
            }

            if (output.Length < required)
                throw new ArgumentException("Output span is smaller than deterministic density candidate count: " + required, nameof(output));

            int written = 0;
            for (int y = 0; y < domain.Height; y++)
            {
                for (int x = 0; x < domain.Width; x++)
                {
                    int seedIndex = domain.GetIndex(x, y);
                    if (!IsEligible(eligibilityMask, seedIndex)) continue;
                    long absoluteX = domain.GetAbsoluteX(x);
                    long absoluteY = domain.GetAbsoluteY(y);
                    double occurrence = WorldNoiseRule.Sample01(seed, absoluteX, absoluteY, noiseKey, DensityLocalKey);
                    if (occurrence >= settings.Occurrence) continue;
                    ulong clusterKey = ToKey(WorldNoiseRule.Sample01(seed, absoluteX, absoluteY, noiseKey, ClusterLocalKey));
                    written += WriteCluster(
                        resourceIndex, x, y, settings.ClusterSize, in domain, seed, noiseKey,
                        clusterKey, settings.Richness, eligibilityMask, suitabilityScores, output.Slice(written));
                }
            }

            return new WorldResourceCandidateGenerationResult(written, eligibleCount, required);
        }

        private static WorldResourceCandidateGenerationResult GenerateCoverage(
            int resourceIndex,
            in WorldResourcePlanarDomain domain,
            WorldGenerationSeed seed,
            WorldNoiseKey noiseKey,
            in WorldResolvedResourceGenerationSettings settings,
            ReadOnlySpan<byte> eligibilityMask,
            ReadOnlySpan<float> suitabilityScores,
            Span<WorldSpawnCandidate> output,
            Span<WorldCoverageSampleRank> scratch)
        {
            int eligibleCount = 0;
            int clusterSpan = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(settings.ClusterSize)));
            for (int y = 0; y < domain.Height; y++)
            {
                long absoluteY = domain.GetAbsoluteY(y);
                for (int x = 0; x < domain.Width; x++)
                {
                    int sampleIndex = domain.GetIndex(x, y);
                    if (!IsEligible(eligibilityMask, sampleIndex)) continue;
                    if (eligibleCount >= scratch.Length)
                        throw new ArgumentException("Coverage scratch must hold every eligible sample.", nameof(scratch));

                    long absoluteX = domain.GetAbsoluteX(x);
                    long clusterX = FloorDiv(absoluteX, clusterSpan);
                    long clusterY = FloorDiv(absoluteY, clusterSpan);
                    scratch[eligibleCount++] = new WorldCoverageSampleRank
                    {
                        SampleIndex = sampleIndex,
                        Score = GetSuitability(suitabilityScores, sampleIndex),
                        ClusterKey = ToKey(WorldNoiseRule.Sample01(seed, clusterX, clusterY, noiseKey, ClusterLocalKey)),
                        LocalKey = ToKey(WorldNoiseRule.Sample01(seed, absoluteX, absoluteY, noiseKey, CandidateLocalKey))
                    };
                }
            }

            int target = (int)Math.Round(eligibleCount * settings.Occurrence, MidpointRounding.AwayFromZero);
            if (target > eligibleCount) target = eligibleCount;
            if (output.Length < target)
                throw new ArgumentException("Output span is smaller than coverage target candidate count: " + target, nameof(output));
            if (target == 0) return new WorldResourceCandidateGenerationResult(0, eligibleCount, 0);

            Span<WorldCoverageSampleRank> heap = scratch.Slice(0, eligibleCount);
            BuildCoverageHeap(heap);
            int heapCount = eligibleCount;
            for (int i = 0; i < target; i++)
            {
                WorldCoverageSampleRank rank = PopCoverageBest(heap, ref heapCount);
                int x = rank.SampleIndex % domain.Width;
                int y = rank.SampleIndex / domain.Width;
                long absoluteX = domain.GetAbsoluteX(x);
                long absoluteY = domain.GetAbsoluteY(y);
                output[i] = new WorldSpawnCandidate(
                    resourceIndex,
                    rank.SampleIndex,
                    absoluteX,
                    absoluteY,
                    rank.Score,
                    rank.LocalKey,
                    settings.Richness);
            }

            return new WorldResourceCandidateGenerationResult(target, eligibleCount, target);
        }

        private static int CountClusterCandidates(
            int seedX,
            int seedY,
            int clusterSize,
            in WorldResourcePlanarDomain domain,
            ReadOnlySpan<byte> eligibilityMask,
            ulong clusterKey)
        {
            int count = 0;
            for (int member = 0; member < clusterSize; member++)
            {
                GetTransformedSpiralOffset(member, clusterKey, out int dx, out int dy);
                int x = seedX + dx;
                int y = seedY + dy;
                if ((uint)x >= (uint)domain.Width || (uint)y >= (uint)domain.Height) continue;
                int index = domain.GetIndex(x, y);
                if (!IsEligible(eligibilityMask, index)) continue;
                count++;
            }
            return count;
        }

        private static int WriteCluster(
            int resourceIndex,
            int seedX,
            int seedY,
            int clusterSize,
            in WorldResourcePlanarDomain domain,
            WorldGenerationSeed seed,
            WorldNoiseKey noiseKey,
            ulong clusterKey,
            double richness,
            ReadOnlySpan<byte> eligibilityMask,
            ReadOnlySpan<float> suitabilityScores,
            Span<WorldSpawnCandidate> output)
        {
            int written = 0;
            for (int member = 0; member < clusterSize; member++)
            {
                GetTransformedSpiralOffset(member, clusterKey, out int dx, out int dy);
                int x = seedX + dx;
                int y = seedY + dy;
                if ((uint)x >= (uint)domain.Width || (uint)y >= (uint)domain.Height) continue;
                int sampleIndex = domain.GetIndex(x, y);
                if (!IsEligible(eligibilityMask, sampleIndex)) continue;
                long absoluteX = domain.GetAbsoluteX(x);
                long absoluteY = domain.GetAbsoluteY(y);
                ulong key = ToKey(WorldNoiseRule.Sample01(
                    seed, absoluteX, absoluteY, noiseKey, CandidateLocalKey + (ulong)member));
                output[written++] = new WorldSpawnCandidate(
                    resourceIndex,
                    sampleIndex,
                    absoluteX,
                    absoluteY,
                    GetSuitability(suitabilityScores, sampleIndex),
                    key,
                    richness);
            }
            return written;
        }

        private static void GetTransformedSpiralOffset(int member, ulong key, out int x, out int y)
        {
            GetSpiralOffset(member, out int sx, out int sy);
            int variant = (int)(key & 7UL);
            switch (variant)
            {
                case 0: x = sx; y = sy; break;
                case 1: x = -sy; y = sx; break;
                case 2: x = -sx; y = -sy; break;
                case 3: x = sy; y = -sx; break;
                case 4: x = -sx; y = sy; break;
                case 5: x = sx; y = -sy; break;
                case 6: x = sy; y = sx; break;
                default: x = -sy; y = -sx; break;
            }
        }

        private static void GetSpiralOffset(int index, out int x, out int y)
        {
            if (index == 0) { x = 0; y = 0; return; }
            int layer = (int)Math.Ceiling((Math.Sqrt(index + 1d) - 1d) / 2d);
            int legLength = layer * 2;
            int maxValue = (2 * layer + 1) * (2 * layer + 1) - 1;
            int delta = maxValue - index;
            int leg = delta / legLength;
            int offset = delta % legLength;
            switch (leg)
            {
                case 0: x = layer - offset; y = -layer; break;
                case 1: x = -layer; y = -layer + offset; break;
                case 2: x = -layer + offset; y = layer; break;
                default: x = layer; y = layer - offset; break;
            }
        }

        private static bool IsEligible(ReadOnlySpan<byte> mask, int index) => mask.Length == 0 || mask[index] != 0;
        private static double GetSuitability(ReadOnlySpan<float> scores, int index) => scores.Length == 0 ? 0d : scores[index];

        private static void ValidateInputLength<T>(ReadOnlySpan<T> values, int expected, string parameterName)
        {
            if (values.Length != 0 && values.Length != expected)
                throw new ArgumentException("Optional input span must be empty or match layout sample count.", parameterName);
        }

        private static void ValidateSuitabilityScores(ReadOnlySpan<float> scores)
        {
            for (int i = 0; i < scores.Length; i++)
            {
                if (float.IsNaN(scores[i]) || float.IsInfinity(scores[i]))
                    throw new ArgumentOutOfRangeException(nameof(scores), "Suitability scores must be finite.");
            }
        }

        private static ulong ToKey(double sample)
        {
            const double max53 = 9007199254740991d;
            return (ulong)(sample * max53);
        }

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            return remainder < 0L ? quotient - 1L : quotient;
        }

        private static void BuildCoverageHeap(Span<WorldCoverageSampleRank> heap)
        {
            for (int parent = (heap.Length / 2) - 1; parent >= 0; parent--)
                SiftCoverageDown(heap, parent, heap.Length);
        }

        private static WorldCoverageSampleRank PopCoverageBest(Span<WorldCoverageSampleRank> heap, ref int count)
        {
            WorldCoverageSampleRank best = heap[0];
            count--;
            if (count > 0)
            {
                heap[0] = heap[count];
                SiftCoverageDown(heap, 0, count);
            }
            return best;
        }

        private static void SiftCoverageDown(Span<WorldCoverageSampleRank> heap, int parent, int count)
        {
            while (true)
            {
                int left = (parent * 2) + 1;
                if (left >= count) return;
                int right = left + 1;
                int bestChild = right < count && IsCoverageBetter(in heap[right], in heap[left]) ? right : left;
                if (!IsCoverageBetter(in heap[bestChild], in heap[parent])) return;
                WorldCoverageSampleRank swap = heap[parent];
                heap[parent] = heap[bestChild];
                heap[bestChild] = swap;
                parent = bestChild;
            }
        }

        private static bool IsCoverageBetter(in WorldCoverageSampleRank left, in WorldCoverageSampleRank right)
        {
            if (!left.Score.Equals(right.Score)) return left.Score > right.Score;
            if (left.ClusterKey != right.ClusterKey) return left.ClusterKey < right.ClusterKey;
            if (left.LocalKey != right.LocalKey) return left.LocalKey < right.LocalKey;
            return left.SampleIndex < right.SampleIndex;
        }
    }
}
