using System;
using NUnit.Framework;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldKitStreamingCoreTests
    {
        [Test]
        public void RegionLayoutUsesFloorDivisionAcrossNegativeChunks()
        {
            WorldRegionLayout layout = new WorldRegionLayout(16, 8);

            Assert.That(layout.GetRegion(new WorldChunkCoord(0, 0)), Is.EqualTo(new WorldRegionCoord(0, 0)));
            Assert.That(layout.GetRegion(new WorldChunkCoord(15, 7)), Is.EqualTo(new WorldRegionCoord(0, 0)));
            Assert.That(layout.GetRegion(new WorldChunkCoord(16, 8)), Is.EqualTo(new WorldRegionCoord(1, 1)));
            Assert.That(layout.GetRegion(new WorldChunkCoord(-1, -1)), Is.EqualTo(new WorldRegionCoord(-1, -1)));
            Assert.That(layout.GetRegion(new WorldChunkCoord(-16, -8)), Is.EqualTo(new WorldRegionCoord(-1, -1)));
            Assert.That(layout.GetRegion(new WorldChunkCoord(-17, -9)), Is.EqualTo(new WorldRegionCoord(-2, -2)));
        }

        [Test]
        public void RegionBoundsAreHalfOpenAndOverflowIsExplicit()
        {
            WorldRegionLayout layout = new WorldRegionLayout(16, 8);
            Assert.That(layout.TryGetChunkBounds(new WorldRegionCoord(-2, 3), out WorldChunkBounds bounds), Is.True);
            Assert.That(bounds.Min, Is.EqualTo(new WorldChunkCoord(-32, 24)));
            Assert.That(bounds.MaxExclusive, Is.EqualTo(new WorldChunkCoord(-16, 32)));
            Assert.That(bounds.Contains(new WorldChunkCoord(-17, 31)), Is.True);
            Assert.That(bounds.Contains(new WorldChunkCoord(-16, 31)), Is.False);

            Assert.That(layout.TryGetChunkBounds(new WorldRegionCoord(long.MaxValue, 0), out _), Is.False);
            Assert.That(default(WorldRegionLayout).IsValid, Is.False);
        }

        [Test]
        public void StreamingPolicyRequiresMonotonicResidencyRadii()
        {
            WorldStreamingPolicy policy = new WorldStreamingPolicy(4, 3, 2, 1);
            Assert.That(policy.GetTier(0), Is.EqualTo(WorldStreamingTier.Presentation));
            Assert.That(policy.GetTier(1), Is.EqualTo(WorldStreamingTier.Presentation));
            Assert.That(policy.GetTier(2), Is.EqualTo(WorldStreamingTier.Simulation));
            Assert.That(policy.GetTier(3), Is.EqualTo(WorldStreamingTier.Data));
            Assert.That(policy.GetTier(4), Is.EqualTo(WorldStreamingTier.Metadata));
            Assert.That(policy.GetTier(5), Is.EqualTo(WorldStreamingTier.None));

            Assert.That(() => new WorldStreamingPolicy(1, 2, 1, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(default(WorldStreamingPolicy).IsValid, Is.False);
        }

        [Test]
        public void DemandPlannerIsDeterministicAcrossPositiveAndNegativeSpace()
        {
            WorldStreamingPolicy policy = new WorldStreamingPolicy(2, 1, 1, 0);
            WorldChunkCoord focus = new WorldChunkCoord(-10, 7);
            int count = WorldStreamingPlanner.GetRequiredDemandCount(focus, WorldExtent.Infinite, in policy);
            WorldChunkDemand[] first = new WorldChunkDemand[count];
            WorldChunkDemand[] second = new WorldChunkDemand[count];

            Assert.That(WorldStreamingPlanner.CollectDesired(focus, WorldExtent.Infinite, in policy, first), Is.EqualTo(25));
            Assert.That(WorldStreamingPlanner.CollectDesired(focus, WorldExtent.Infinite, in policy, second), Is.EqualTo(25));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first[0].Coord, Is.EqualTo(new WorldChunkCoord(-12, 5)));
            Assert.That(first[0].Tier, Is.EqualTo(WorldStreamingTier.Metadata));
            Assert.That(first[12].Coord, Is.EqualTo(focus));
            Assert.That(first[12].Tier, Is.EqualTo(WorldStreamingTier.Presentation));
            Assert.That(first[24].Coord, Is.EqualTo(new WorldChunkCoord(-8, 9)));
        }

        [Test]
        public void FiniteExtentClipsDemandWithoutChangingRowMajorOrder()
        {
            WorldStreamingPolicy policy = new WorldStreamingPolicy(2, 2, 1, 0);
            WorldExtent extent = WorldExtent.Finite(new WorldChunkBounds(
                new WorldChunkCoord(-1, -1),
                new WorldChunkCoord(2, 2)));
            WorldChunkCoord focus = new WorldChunkCoord(0, 0);
            int required = WorldStreamingPlanner.GetRequiredDemandCount(focus, extent, in policy);
            WorldChunkDemand[] output = new WorldChunkDemand[required];

            Assert.That(required, Is.EqualTo(9));
            Assert.That(WorldStreamingPlanner.CollectDesired(focus, extent, in policy, output), Is.EqualTo(9));
            Assert.That(output[0].Coord, Is.EqualTo(new WorldChunkCoord(-1, -1)));
            Assert.That(output[8].Coord, Is.EqualTo(new WorldChunkCoord(1, 1)));
        }

        [Test]
        public void DemandPlannerPreflightsDestinationBeforeWriting()
        {
            WorldStreamingPolicy policy = new WorldStreamingPolicy(1, 1, 0, 0);
            WorldChunkDemand sentinel = new WorldChunkDemand(
                new WorldChunkCoord(99, 88),
                WorldStreamingTier.Metadata);
            WorldChunkDemand[] output = { sentinel, sentinel, sentinel };

            Assert.Throws<ArgumentException>(() =>
                WorldStreamingPlanner.CollectDesired(
                    new WorldChunkCoord(0, 0),
                    WorldExtent.Infinite,
                    in policy,
                    output));
            Assert.That(output[0], Is.EqualTo(sentinel));
            Assert.That(output[1], Is.EqualTo(sentinel));
            Assert.That(output[2], Is.EqualTo(sentinel));
        }

        [Test]
        public void DemandPlannerRejectsCoordinateOverflowBeforeWriting()
        {
            WorldStreamingPolicy policy = new WorldStreamingPolicy(1, 1, 1, 1);
            WorldChunkDemand sentinel = new WorldChunkDemand(
                new WorldChunkCoord(5, 6),
                WorldStreamingTier.Metadata);
            WorldChunkDemand[] output = { sentinel, sentinel, sentinel, sentinel, sentinel, sentinel, sentinel, sentinel, sentinel };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldStreamingPlanner.CollectDesired(
                    new WorldChunkCoord(long.MaxValue, 0),
                    WorldExtent.Infinite,
                    in policy,
                    output));
            Assert.That(output[0], Is.EqualTo(sentinel));
        }

        [Test]
        public void StreamingRegistryKeepsSimulationAndPresentationAsDistinctAdjacentTiers()
        {
            WorldChunkStreamingRegistry registry = new WorldChunkStreamingRegistry();
            WorldChunkCoord chunk = new WorldChunkCoord(-3, 9);

            Assert.That(registry.GetTier(chunk), Is.EqualTo(WorldStreamingTier.None));
            Assert.That(registry.TryTransition(chunk, WorldStreamingTier.Data).Error,
                Is.EqualTo(WorldStreamingTransitionError.InvalidTransition));
            Assert.That(registry.Count, Is.EqualTo(0));

            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Metadata), WorldStreamingTier.None, WorldStreamingTier.Metadata);
            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Data), WorldStreamingTier.Metadata, WorldStreamingTier.Data);
            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Simulation), WorldStreamingTier.Data, WorldStreamingTier.Simulation);
            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Presentation), WorldStreamingTier.Simulation, WorldStreamingTier.Presentation);
            Assert.That(registry.Count, Is.EqualTo(1));

            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Simulation), WorldStreamingTier.Presentation, WorldStreamingTier.Simulation);
            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Data), WorldStreamingTier.Simulation, WorldStreamingTier.Data);
            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.Metadata), WorldStreamingTier.Data, WorldStreamingTier.Metadata);
            AssertSuccess(registry.TryTransition(chunk, WorldStreamingTier.None), WorldStreamingTier.Metadata, WorldStreamingTier.None);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void StreamingRegistryWritesStableResidentOrderAndPreflightsBuffer()
        {
            WorldChunkStreamingRegistry registry = new WorldChunkStreamingRegistry();
            WorldChunkCoord a = new WorldChunkCoord(1, 2);
            WorldChunkCoord b = new WorldChunkCoord(-3, 4);
            AssertSuccess(registry.TryTransition(a, WorldStreamingTier.Metadata), WorldStreamingTier.None, WorldStreamingTier.Metadata);
            AssertSuccess(registry.TryTransition(b, WorldStreamingTier.Metadata), WorldStreamingTier.None, WorldStreamingTier.Metadata);
            AssertSuccess(registry.TryTransition(a, WorldStreamingTier.Data), WorldStreamingTier.Metadata, WorldStreamingTier.Data);

            WorldChunkStreamingState[] output = new WorldChunkStreamingState[2];
            Assert.That(registry.WriteStates(output), Is.EqualTo(2));
            Assert.That(output[0], Is.EqualTo(new WorldChunkStreamingState(a, WorldStreamingTier.Data)));
            Assert.That(output[1], Is.EqualTo(new WorldChunkStreamingState(b, WorldStreamingTier.Metadata)));

            WorldChunkStreamingState sentinel = new WorldChunkStreamingState(new WorldChunkCoord(8, 8), WorldStreamingTier.Metadata);
            WorldChunkStreamingState[] tooSmall = { sentinel };
            Assert.Throws<ArgumentException>(() => registry.WriteStates(tooSmall));
            Assert.That(tooSmall[0], Is.EqualTo(sentinel));
        }

        [Test]
        public void ReconcilerEmitsDowngradesBeforeUpgradesAndOnlyOneTierPerWave()
        {
            WorldChunkStreamingRegistry registry = new WorldChunkStreamingRegistry();
            WorldChunkCoord oldFocus = new WorldChunkCoord(0, 0);
            AssertSuccess(registry.TryTransition(oldFocus, WorldStreamingTier.Metadata), WorldStreamingTier.None, WorldStreamingTier.Metadata);
            AssertSuccess(registry.TryTransition(oldFocus, WorldStreamingTier.Data), WorldStreamingTier.Metadata, WorldStreamingTier.Data);
            AssertSuccess(registry.TryTransition(oldFocus, WorldStreamingTier.Simulation), WorldStreamingTier.Data, WorldStreamingTier.Simulation);
            AssertSuccess(registry.TryTransition(oldFocus, WorldStreamingTier.Presentation), WorldStreamingTier.Simulation, WorldStreamingTier.Presentation);

            WorldStreamingPolicy policy = new WorldStreamingPolicy(1, 1, 0, 0);
            WorldChunkCoord nextFocus = new WorldChunkCoord(3, 0);
            WorldChunkStreamingState[] resident = new WorldChunkStreamingState[registry.Count];
            WorldChunkDemand[] demand = new WorldChunkDemand[
                WorldStreamingPlanner.GetRequiredDemandCount(nextFocus, WorldExtent.Infinite, in policy)];
            WorldStreamingTransition[] transitions = new WorldStreamingTransition[1 + demand.Length];

            int written = WorldStreamingReconciler.CollectTransitions(
                nextFocus,
                WorldExtent.Infinite,
                in policy,
                registry,
                resident,
                demand,
                transitions);

            Assert.That(written, Is.EqualTo(10));
            Assert.That(transitions[0], Is.EqualTo(new WorldStreamingTransition(
                oldFocus, WorldStreamingTier.Presentation, WorldStreamingTier.Simulation)));
            for (int i = 1; i < written; i++)
            {
                Assert.That(transitions[i].From, Is.EqualTo(WorldStreamingTier.None));
                Assert.That(transitions[i].To, Is.EqualTo(WorldStreamingTier.Metadata));
            }
        }

        [Test]
        public void DesiredTierHandlesExtremeOppositeSignedCoordinatesWithoutOverflow()
        {
            WorldStreamingPolicy policy = new WorldStreamingPolicy(2, 1, 1, 0);
            Assert.That(WorldStreamingPlanner.GetDesiredTier(
                new WorldChunkCoord(long.MinValue, 0),
                new WorldChunkCoord(long.MaxValue, 0),
                WorldExtent.Infinite,
                in policy), Is.EqualTo(WorldStreamingTier.None));
        }

        private static void AssertSuccess(
            WorldStreamingTransitionResult result,
            WorldStreamingTier previous,
            WorldStreamingTier current)
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.PreviousTier, Is.EqualTo(previous));
            Assert.That(result.CurrentTier, Is.EqualTo(current));
        }
    }
}
