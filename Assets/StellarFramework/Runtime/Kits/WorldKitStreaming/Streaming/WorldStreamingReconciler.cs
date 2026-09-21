using System;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming
{
    public readonly struct WorldStreamingTransition : IEquatable<WorldStreamingTransition>
    {
        public WorldChunkCoord Coord { get; }
        public WorldStreamingTier From { get; }
        public WorldStreamingTier To { get; }

        public WorldStreamingTransition(
            WorldChunkCoord coord,
            WorldStreamingTier from,
            WorldStreamingTier to)
        {
            if (from < WorldStreamingTier.None || from > WorldStreamingTier.Presentation)
                throw new ArgumentOutOfRangeException(nameof(from));
            if (to < WorldStreamingTier.None || to > WorldStreamingTier.Presentation)
                throw new ArgumentOutOfRangeException(nameof(to));
            if (Math.Abs((int)to - (int)from) != 1)
                throw new ArgumentException("Streaming transition must move exactly one adjacent tier.");

            Coord = coord;
            From = from;
            To = to;
        }

        public bool Equals(WorldStreamingTransition other) =>
            Coord == other.Coord && From == other.From && To == other.To;
        public override bool Equals(object obj) => obj is WorldStreamingTransition other && Equals(other);
        public override int GetHashCode() => unchecked(((Coord.GetHashCode() * 397) ^ (int)From) * 397 ^ (int)To);
        public static bool operator ==(WorldStreamingTransition left, WorldStreamingTransition right) => left.Equals(right);
        public static bool operator !=(WorldStreamingTransition left, WorldStreamingTransition right) => !left.Equals(right);
    }

    /// <summary>
    /// Creates one reconciliation wave. Downgrades are emitted first in stable residency order,
    /// then upgrades in deterministic row-major demand order. The registry is never mutated here.
    /// </summary>
    public static class WorldStreamingReconciler
    {
        public static int CollectTransitions(
            WorldChunkCoord focus,
            WorldExtent extent,
            in WorldStreamingPolicy policy,
            WorldChunkStreamingRegistry registry,
            Span<WorldChunkStreamingState> residentScratch,
            Span<WorldChunkDemand> demandScratch,
            Span<WorldStreamingTransition> destination)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (residentScratch.Length < registry.Count)
                throw new ArgumentException("Resident scratch is smaller than registry Count.", nameof(residentScratch));

            int demandCount = WorldStreamingPlanner.GetRequiredDemandCount(focus, extent, in policy);
            if (demandScratch.Length < demandCount)
                throw new ArgumentException("Demand scratch is smaller than required demand count.", nameof(demandScratch));

            int residentCount = registry.WriteStates(residentScratch);
            WorldStreamingPlanner.CollectDesired(
                focus,
                extent,
                in policy,
                demandScratch.Slice(0, demandCount));

            int required = CountDowngrades(
                focus,
                extent,
                in policy,
                residentScratch.Slice(0, residentCount));
            required = checked(required + CountUpgrades(
                registry,
                demandScratch.Slice(0, demandCount)));
            if (destination.Length < required)
                throw new ArgumentException("Transition destination is smaller than the required reconciliation wave.", nameof(destination));

            int written = 0;
            for (int i = 0; i < residentCount; i++)
            {
                WorldChunkStreamingState current = residentScratch[i];
                WorldStreamingTier desired = WorldStreamingPlanner.GetDesiredTier(
                    focus, current.Coord, extent, in policy);
                if (current.Tier <= desired) continue;
                destination[written++] = new WorldStreamingTransition(
                    current.Coord,
                    current.Tier,
                    (WorldStreamingTier)((int)current.Tier - 1));
            }

            for (int i = 0; i < demandCount; i++)
            {
                WorldChunkDemand desired = demandScratch[i];
                WorldStreamingTier current = registry.GetTier(desired.Coord);
                if (current >= desired.Tier) continue;
                destination[written++] = new WorldStreamingTransition(
                    desired.Coord,
                    current,
                    (WorldStreamingTier)((int)current + 1));
            }

            if (written != required)
                throw new InvalidOperationException("Streaming reconciliation preflight count does not match emitted output.");
            return written;
        }

        private static int CountDowngrades(
            WorldChunkCoord focus,
            WorldExtent extent,
            in WorldStreamingPolicy policy,
            ReadOnlySpan<WorldChunkStreamingState> current)
        {
            int count = 0;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].Tier > WorldStreamingPlanner.GetDesiredTier(
                        focus, current[i].Coord, extent, in policy))
                    count++;
            }
            return count;
        }

        private static int CountUpgrades(
            WorldChunkStreamingRegistry registry,
            ReadOnlySpan<WorldChunkDemand> desired)
        {
            int count = 0;
            for (int i = 0; i < desired.Length; i++)
            {
                if (registry.GetTier(desired[i].Coord) < desired[i].Tier)
                    count++;
            }
            return count;
        }
    }
}
