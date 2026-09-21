using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldScatterResolveResult
    {
        public int AcceptedCount { get; }
        public int RejectedOccupancyCount { get; }
        public int RejectedBudgetCount { get; }
        public int RejectedSpacingCount { get; }

        internal WorldScatterResolveResult(int acceptedCount, int rejectedOccupancyCount)
            : this(acceptedCount, rejectedOccupancyCount, 0, 0)
        {
        }

        internal WorldScatterResolveResult(
            int acceptedCount,
            int rejectedOccupancyCount,
            int rejectedBudgetCount,
            int rejectedSpacingCount)
        {
            AcceptedCount = acceptedCount;
            RejectedOccupancyCount = rejectedOccupancyCount;
            RejectedBudgetCount = rejectedBudgetCount;
            RejectedSpacingCount = rejectedSpacingCount;
        }
    }

    public static class WorldResourceScatterResolver
    {
        public static WorldScatterResolveResult Resolve(
            ReadOnlySpan<WorldSpawnCandidate> candidates,
            WorldResourceCatalog catalog,
            Span<WorldOccupancyCellState> occupancyCells,
            Span<int> orderScratch,
            Span<WorldSpawnRecord> output)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (orderScratch.Length < candidates.Length)
                throw new ArgumentException("Order scratch must be at least candidate count.", nameof(orderScratch));
            if (output.Length < candidates.Length)
                throw new ArgumentException("Output must be at least candidate count so resolution cannot fail after mutating occupancy.", nameof(output));

            for (int i = 0; i < candidates.Length; i++)
            {
                WorldSpawnCandidate candidate = candidates[i];
                if ((uint)candidate.ResourceIndex >= (uint)catalog.Count)
                    throw new ArgumentOutOfRangeException(nameof(candidates), "Candidate resource index is not present in catalog.");
                if (catalog.GetDefinition(candidate.ResourceIndex).Distribution.MinSpacing > 0d)
                    throw new InvalidOperationException("Resources with MinSpacing > 0 require the full Resolve overload with planar spacing scratch.");
                if ((uint)candidate.SampleIndex >= (uint)occupancyCells.Length)
                    throw new ArgumentOutOfRangeException(nameof(candidates), "Candidate sample index exceeds occupancy buffer.");
                orderScratch[i] = i;
            }

            BuildHeap(candidates, catalog, orderScratch.Slice(0, candidates.Length));

            int heapCount = candidates.Length;
            int accepted = 0;
            int rejectedOccupancy = 0;
            while (heapCount > 0)
            {
                int candidateIndex = PopBest(candidates, catalog, orderScratch, ref heapCount);
                WorldSpawnCandidate candidate = candidates[candidateIndex];
                WorldResourceDefinition definition = catalog.GetDefinition(candidate.ResourceIndex);
                ref WorldOccupancyCellState cell = ref occupancyCells[candidate.SampleIndex];
                if (!cell.TryOccupy(definition.Occupies, definition.Excludes))
                {
                    rejectedOccupancy++;
                    continue;
                }

                output[accepted++] = new WorldSpawnRecord(in candidate);
            }

            return new WorldScatterResolveResult(accepted, rejectedOccupancy);
        }

        public static WorldScatterResolveResult Resolve(
            ReadOnlySpan<WorldSpawnCandidate> candidates,
            WorldResourceCatalog catalog,
            in WorldResourcePlanarDomain domain,
            WorldCompiledResourceBudget budget,
            Span<WorldOccupancyCellState> occupancyCells,
            Span<int> orderScratch,
            Span<int> resourceAcceptedScratch,
            Span<int> categoryAcceptedScratch,
            Span<int> spacingHeadsScratch,
            Span<WorldScatterSpacingNode> spacingNodesScratch,
            Span<WorldSpawnRecord> output)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (budget == null) throw new ArgumentNullException(nameof(budget));
            if (budget.ResourceCount != catalog.Count || budget.CategoryCount != catalog.CategoryCount)
                throw new ArgumentException("Compiled budget does not match the supplied resource catalog.", nameof(budget));
            if (occupancyCells.Length < domain.Count)
                throw new ArgumentException("Occupancy cells must cover the full planar layout.", nameof(occupancyCells));
            if (orderScratch.Length < candidates.Length)
                throw new ArgumentException("Order scratch must be at least candidate count.", nameof(orderScratch));
            if (resourceAcceptedScratch.Length < catalog.Count)
                throw new ArgumentException("Resource count scratch must cover every catalog resource.", nameof(resourceAcceptedScratch));
            if (categoryAcceptedScratch.Length < catalog.CategoryCount)
                throw new ArgumentException("Category count scratch must cover every catalog category.", nameof(categoryAcceptedScratch));
            if (spacingHeadsScratch.Length < domain.Count)
                throw new ArgumentException("Spacing head scratch must cover every layout sample.", nameof(spacingHeadsScratch));
            if (spacingNodesScratch.Length < candidates.Length)
                throw new ArgumentException("Spacing node scratch must be at least candidate count.", nameof(spacingNodesScratch));
            if (output.Length < candidates.Length)
                throw new ArgumentException("Output must be at least candidate count so resolution cannot fail after mutating occupancy.", nameof(output));

            for (int i = 0; i < catalog.Count; i++) resourceAcceptedScratch[i] = 0;
            for (int i = 0; i < catalog.CategoryCount; i++) categoryAcceptedScratch[i] = 0;
            for (int i = 0; i < domain.Count; i++) spacingHeadsScratch[i] = -1;

            for (int i = 0; i < candidates.Length; i++)
            {
                WorldSpawnCandidate candidate = candidates[i];
                if ((uint)candidate.ResourceIndex >= (uint)catalog.Count)
                    throw new ArgumentOutOfRangeException(nameof(candidates), "Candidate resource index is not present in catalog.");
                if ((uint)candidate.SampleIndex >= (uint)domain.Count)
                    throw new ArgumentOutOfRangeException(nameof(candidates), "Candidate sample index exceeds planar layout.");
                orderScratch[i] = i;
            }

            BuildHeap(candidates, catalog, orderScratch.Slice(0, candidates.Length));

            int heapCount = candidates.Length;
            int accepted = 0;
            int rejectedOccupancy = 0;
            int rejectedBudget = 0;
            int rejectedSpacing = 0;
            int spacingNodeCount = 0;

            while (heapCount > 0)
            {
                int candidateIndex = PopBest(candidates, catalog, orderScratch, ref heapCount);
                WorldSpawnCandidate candidate = candidates[candidateIndex];
                WorldResourceDefinition definition = catalog.GetDefinition(candidate.ResourceIndex);
                int categoryIndex = catalog.GetCategoryIndexForResource(candidate.ResourceIndex);

                if (!CanAcceptBudget(
                        budget,
                        candidate.ResourceIndex,
                        categoryIndex,
                        accepted,
                        resourceAcceptedScratch,
                        categoryAcceptedScratch))
                {
                    rejectedBudget++;
                    continue;
                }

                if (!PassesMinSpacing(
                        in candidate,
                        definition.Distribution.MinSpacing,
                        in domain,
                        spacingHeadsScratch,
                        spacingNodesScratch,
                        spacingNodeCount))
                {
                    rejectedSpacing++;
                    continue;
                }

                ref WorldOccupancyCellState cell = ref occupancyCells[candidate.SampleIndex];
                if (!cell.TryOccupy(definition.Occupies, definition.Excludes))
                {
                    rejectedOccupancy++;
                    continue;
                }

                output[accepted++] = new WorldSpawnRecord(in candidate);
                resourceAcceptedScratch[candidate.ResourceIndex]++;
                categoryAcceptedScratch[categoryIndex]++;

                if (definition.Distribution.MinSpacing > 0d)
                {
                    AddSpacingNode(
                        in candidate,
                        spacingHeadsScratch,
                        spacingNodesScratch,
                        ref spacingNodeCount);
                }
            }

            return new WorldScatterResolveResult(
                accepted,
                rejectedOccupancy,
                rejectedBudget,
                rejectedSpacing);
        }

        private static bool CanAcceptBudget(
            WorldCompiledResourceBudget budget,
            int resourceIndex,
            int categoryIndex,
            int acceptedCount,
            Span<int> resourceAccepted,
            Span<int> categoryAccepted)
        {
            if (budget.GlobalMaxAccepted >= 0 && acceptedCount >= budget.GlobalMaxAccepted) return false;
            int resourceMax = budget.GetResourceMax(resourceIndex);
            if (resourceMax >= 0 && resourceAccepted[resourceIndex] >= resourceMax) return false;
            int categoryMax = budget.GetCategoryMax(categoryIndex);
            return categoryMax < 0 || categoryAccepted[categoryIndex] < categoryMax;
        }

        private static bool PassesMinSpacing(
            in WorldSpawnCandidate candidate,
            double minSpacing,
            in WorldResourcePlanarDomain domain,
            Span<int> spacingHeads,
            Span<WorldScatterSpacingNode> spacingNodes,
            int spacingNodeCount)
        {
            if (minSpacing <= 0d || spacingNodeCount == 0) return true;

            int localX = candidate.SampleIndex % domain.Width;
            int localY = candidate.SampleIndex / domain.Width;
            int radiusSamples = (int)Math.Ceiling(minSpacing / domain.SampleStep);
            int minX = Math.Max(0, localX - radiusSamples);
            int maxX = Math.Min(domain.Width - 1, localX + radiusSamples);
            int minY = Math.Max(0, localY - radiusSamples);
            int maxY = Math.Min(domain.Height - 1, localY + radiusSamples);
            double minSpacingSquared = minSpacing * minSpacing;

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * domain.Width;
                for (int x = minX; x <= maxX; x++)
                {
                    int nodeIndex = spacingHeads[row + x];
                    while (nodeIndex >= 0)
                    {
                        WorldScatterSpacingNode node = spacingNodes[nodeIndex];
                        if (node.ResourceIndex == candidate.ResourceIndex)
                        {
                            double dx = (double)candidate.X - node.X;
                            double dy = (double)candidate.Y - node.Y;
                            if ((dx * dx) + (dy * dy) < minSpacingSquared) return false;
                        }
                        nodeIndex = node.Next;
                    }
                }
            }

            return true;
        }

        private static void AddSpacingNode(
            in WorldSpawnCandidate candidate,
            Span<int> spacingHeads,
            Span<WorldScatterSpacingNode> spacingNodes,
            ref int spacingNodeCount)
        {
            int nodeIndex = spacingNodeCount++;
            spacingNodes[nodeIndex] = new WorldScatterSpacingNode
            {
                ResourceIndex = candidate.ResourceIndex,
                X = candidate.X,
                Y = candidate.Y,
                Next = spacingHeads[candidate.SampleIndex]
            };
            spacingHeads[candidate.SampleIndex] = nodeIndex;
        }

        private static void BuildHeap(
            ReadOnlySpan<WorldSpawnCandidate> candidates,
            WorldResourceCatalog catalog,
            Span<int> heap)
        {
            for (int parent = (heap.Length / 2) - 1; parent >= 0; parent--)
                SiftDown(candidates, catalog, heap, parent, heap.Length);
        }

        private static int PopBest(
            ReadOnlySpan<WorldSpawnCandidate> candidates,
            WorldResourceCatalog catalog,
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
            ReadOnlySpan<WorldSpawnCandidate> candidates,
            WorldResourceCatalog catalog,
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
            in WorldSpawnCandidate left,
            in WorldSpawnCandidate right,
            WorldResourceCatalog catalog)
        {
            WorldResourceDefinition leftDefinition = catalog.GetDefinition(left.ResourceIndex);
            WorldResourceDefinition rightDefinition = catalog.GetDefinition(right.ResourceIndex);
            if (leftDefinition.Priority != rightDefinition.Priority)
                return leftDefinition.Priority > rightDefinition.Priority;
            if (!left.Score.Equals(right.Score)) return left.Score > right.Score;
            if (left.DeterministicKey != right.DeterministicKey)
                return left.DeterministicKey < right.DeterministicKey;

            int leftRank = catalog.GetStableTieRank(left.ResourceIndex);
            int rightRank = catalog.GetStableTieRank(right.ResourceIndex);
            if (leftRank != rightRank) return leftRank < rightRank;
            if (left.X != right.X) return left.X < right.X;
            if (left.Y != right.Y) return left.Y < right.Y;
            return left.SampleIndex < right.SampleIndex;
        }
    }
}
