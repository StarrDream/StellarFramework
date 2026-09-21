using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldFeatureResolveResult
    {
        public int AcceptedCount { get; }
        public int RejectedQuotaCount { get; }
        public int RejectedReservationCount { get; }

        internal WorldFeatureResolveResult(
            int acceptedCount,
            int rejectedQuotaCount,
            int rejectedReservationCount)
        {
            AcceptedCount = acceptedCount;
            RejectedQuotaCount = rejectedQuotaCount;
            RejectedReservationCount = rejectedReservationCount;
        }
    }

    public static class WorldFeatureResolver
    {
        public static WorldFeatureResolveResult Resolve(
            ReadOnlySpan<WorldFeatureCandidate> candidates,
            WorldFeatureCatalog catalog,
            ReadOnlySpan<int> existingWorldCounts,
            ReadOnlySpan<int> existingRegionCounts,
            ReadOnlySpan<WorldFeatureReservation> existingReservations,
            Span<int> orderScratch,
            Span<int> acceptedPerFeatureScratch,
            Span<WorldFeatureReservation> acceptedReservations,
            Span<WorldFeatureInstanceData> output)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            ValidateOptionalCountSpan(existingWorldCounts, catalog.Count, nameof(existingWorldCounts));
            ValidateOptionalCountSpan(existingRegionCounts, catalog.Count, nameof(existingRegionCounts));
            if (orderScratch.Length < candidates.Length)
                throw new ArgumentException("Order scratch must be at least candidate count.", nameof(orderScratch));
            if (acceptedPerFeatureScratch.Length < catalog.Count)
                throw new ArgumentException("Accepted-count scratch must cover every catalog feature.", nameof(acceptedPerFeatureScratch));
            if (acceptedReservations.Length < candidates.Length)
                throw new ArgumentException("Accepted reservation output must be at least candidate count.", nameof(acceptedReservations));
            if (output.Length < candidates.Length)
                throw new ArgumentException("Feature output must be at least candidate count.", nameof(output));

            for (int i = 0; i < existingWorldCounts.Length; i++)
                if (existingWorldCounts[i] < 0) throw new ArgumentOutOfRangeException(nameof(existingWorldCounts));
            for (int i = 0; i < existingRegionCounts.Length; i++)
                if (existingRegionCounts[i] < 0) throw new ArgumentOutOfRangeException(nameof(existingRegionCounts));

            for (int i = 0; i < existingReservations.Length; i++)
            {
                WorldFeatureReservation reservation = existingReservations[i];
                if ((uint)reservation.FeatureIndex >= (uint)catalog.Count)
                    throw new ArgumentOutOfRangeException(nameof(existingReservations), "Existing reservation feature index is outside the catalog.");
                if (!reservation.Bounds.IsValid)
                    throw new ArgumentException("Existing reservation bounds must be valid.", nameof(existingReservations));
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if ((uint)candidates[i].FeatureIndex >= (uint)catalog.Count)
                    throw new ArgumentOutOfRangeException(nameof(candidates), "Candidate feature index is outside the catalog.");
                orderScratch[i] = i;
            }

            for (int i = 0; i < catalog.Count; i++) acceptedPerFeatureScratch[i] = 0;
            BuildHeap(candidates, catalog, orderScratch.Slice(0, candidates.Length));

            int heapCount = candidates.Length;
            int accepted = 0;
            int rejectedQuota = 0;
            int rejectedReservation = 0;
            while (heapCount > 0)
            {
                int candidateIndex = PopBest(candidates, catalog, orderScratch, ref heapCount);
                WorldFeatureCandidate candidate = candidates[candidateIndex];
                WorldFeatureDefinition definition = catalog.GetDefinition(candidate.FeatureIndex);

                if (!PassesQuota(
                        definition.Quota,
                        candidate.FeatureIndex,
                        existingWorldCounts,
                        existingRegionCounts,
                        acceptedPerFeatureScratch))
                {
                    rejectedQuota++;
                    continue;
                }

                WorldFeatureReservation reservation = WorldFeatureReservation.FromCandidate(in candidate, catalog);
                if (OverlapsAny(
                        in reservation,
                        existingReservations,
                        acceptedReservations.Slice(0, accepted)))
                {
                    rejectedReservation++;
                    continue;
                }

                acceptedReservations[accepted] = reservation;
                WorldFeatureBounds bounds = reservation.Bounds;
                output[accepted] = new WorldFeatureInstanceData(in candidate, in bounds);
                acceptedPerFeatureScratch[candidate.FeatureIndex]++;
                accepted++;
            }

            return new WorldFeatureResolveResult(accepted, rejectedQuota, rejectedReservation);
        }

        private static bool PassesQuota(
            WorldFeatureQuota quota,
            int featureIndex,
            ReadOnlySpan<int> existingWorldCounts,
            ReadOnlySpan<int> existingRegionCounts,
            Span<int> acceptedCounts)
        {
            int accepted = acceptedCounts[featureIndex];
            int worldCount = (existingWorldCounts.Length == 0 ? 0 : existingWorldCounts[featureIndex]) + accepted;
            int regionCount = (existingRegionCounts.Length == 0 ? 0 : existingRegionCounts[featureIndex]) + accepted;
            if (quota.MaxPerWorld >= 0 && worldCount >= quota.MaxPerWorld) return false;
            if (quota.MaxPerRegion >= 0 && regionCount >= quota.MaxPerRegion) return false;
            return true;
        }

        private static bool OverlapsAny(
            in WorldFeatureReservation candidate,
            ReadOnlySpan<WorldFeatureReservation> existing,
            ReadOnlySpan<WorldFeatureReservation> accepted)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                WorldFeatureBounds bounds = existing[i].Bounds;
                if (candidate.Bounds.Overlaps(in bounds)) return true;
            }
            for (int i = 0; i < accepted.Length; i++)
            {
                WorldFeatureBounds bounds = accepted[i].Bounds;
                if (candidate.Bounds.Overlaps(in bounds)) return true;
            }
            return false;
        }

        private static void ValidateOptionalCountSpan(ReadOnlySpan<int> counts, int expected, string parameterName)
        {
            if (counts.Length != 0 && counts.Length != expected)
                throw new ArgumentException("Optional count span must be empty or match catalog count.", parameterName);
        }

        private static void BuildHeap(
            ReadOnlySpan<WorldFeatureCandidate> candidates,
            WorldFeatureCatalog catalog,
            Span<int> heap)
        {
            for (int parent = (heap.Length / 2) - 1; parent >= 0; parent--)
                SiftDown(candidates, catalog, heap, parent, heap.Length);
        }

        private static int PopBest(
            ReadOnlySpan<WorldFeatureCandidate> candidates,
            WorldFeatureCatalog catalog,
            Span<int> heap,
            ref int heapCount)
        {
            int best = heap[0];
            heapCount--;
            if (heapCount > 0)
            {
                heap[0] = heap[heapCount];
                SiftDown(candidates, catalog, heap, 0, heapCount);
            }
            return best;
        }

        private static void SiftDown(
            ReadOnlySpan<WorldFeatureCandidate> candidates,
            WorldFeatureCatalog catalog,
            Span<int> heap,
            int parent,
            int count)
        {
            while (true)
            {
                int left = (parent * 2) + 1;
                if (left >= count) return;
                int right = left + 1;
                int bestChild = right < count && IsBetter(candidates[heap[right]], candidates[heap[left]], catalog)
                    ? right
                    : left;
                if (!IsBetter(candidates[heap[bestChild]], candidates[heap[parent]], catalog)) return;
                int swap = heap[parent];
                heap[parent] = heap[bestChild];
                heap[bestChild] = swap;
                parent = bestChild;
            }
        }

        private static bool IsBetter(
            in WorldFeatureCandidate left,
            in WorldFeatureCandidate right,
            WorldFeatureCatalog catalog)
        {
            WorldFeatureDefinition leftDefinition = catalog.GetDefinition(left.FeatureIndex);
            WorldFeatureDefinition rightDefinition = catalog.GetDefinition(right.FeatureIndex);
            if (leftDefinition.Priority != rightDefinition.Priority)
                return leftDefinition.Priority > rightDefinition.Priority;
            if (!left.Score.Equals(right.Score)) return left.Score > right.Score;
            if (left.DeterministicKey != right.DeterministicKey)
                return left.DeterministicKey < right.DeterministicKey;
            int leftRank = catalog.GetStableTieRank(left.FeatureIndex);
            int rightRank = catalog.GetStableTieRank(right.FeatureIndex);
            if (leftRank != rightRank) return leftRank < rightRank;
            if (!left.X.Equals(right.X)) return left.X < right.X;
            if (!left.Y.Equals(right.Y)) return left.Y < right.Y;
            return left.RotationDegrees < right.RotationDegrees;
        }
    }
}
