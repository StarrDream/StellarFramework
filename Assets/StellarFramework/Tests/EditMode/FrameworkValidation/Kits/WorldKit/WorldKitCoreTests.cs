using System;
using NUnit.Framework;
using StellarFramework.WorldKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldKitCoreTests
    {
        [Test]
        public void WorldIdRequiresCanonicalStableFormat()
        {
            WorldId id = WorldId.From("world.main_01");
            Assert.That(id.IsValid, Is.True);
            Assert.That(id.Value, Is.EqualTo("world.main_01"));
            Assert.That(id, Is.EqualTo(WorldId.From("world.main_01")));

            string[] invalid = { null, "", " world.main", "world.main ", "World.Main", ".world", "world.", "world..main", "world-main" };
            for (int i = 0; i < invalid.Length; i++)
            {
                Assert.That(WorldId.TryCreate(invalid[i], out _, out string error), Is.False, invalid[i]);
                Assert.That(error, Is.Not.Null.And.Not.Empty, invalid[i]);
            }

            Assert.That(() => WorldId.From("World.Main"), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ChunkAndRegionCoordinatesSupportLongNegativeSpace()
        {
            WorldChunkCoord chunk = new WorldChunkCoord(long.MinValue + 1, long.MaxValue - 1);
            WorldRegionCoord region = new WorldRegionCoord(-1234567890123L, 9876543210123L);

            Assert.That(chunk, Is.EqualTo(new WorldChunkCoord(long.MinValue + 1, long.MaxValue - 1)));
            Assert.That(region, Is.EqualTo(new WorldRegionCoord(-1234567890123L, 9876543210123L)));
            Assert.That(new WorldChunkCoord(0, 0), Is.EqualTo(default(WorldChunkCoord)));
        }

        [Test]
        public void WorldPointRejectsNonFiniteValues()
        {
            Assert.That(new WorldPoint2D(-1000000000000.25, 999999999999.5).X,
                Is.EqualTo(-1000000000000.25));
            Assert.That(() => new WorldPoint2D(double.NaN, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new WorldPoint2D(0, double.PositiveInfinity), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ChunkBoundsAreHalfOpenAndAreaOverflowIsExplicit()
        {
            WorldChunkBounds bounds = new WorldChunkBounds(
                new WorldChunkCoord(-10, -20),
                new WorldChunkCoord(30, 40));

            Assert.That(bounds.Width, Is.EqualTo(40UL));
            Assert.That(bounds.Height, Is.EqualTo(60UL));
            Assert.That(bounds.Contains(new WorldChunkCoord(-10, -20)), Is.True);
            Assert.That(bounds.Contains(new WorldChunkCoord(29, 39)), Is.True);
            Assert.That(bounds.Contains(new WorldChunkCoord(30, 39)), Is.False);
            Assert.That(bounds.TryGetArea(out ulong area), Is.True);
            Assert.That(area, Is.EqualTo(2400UL));

            Assert.That(() => new WorldChunkBounds(new WorldChunkCoord(1, 0), new WorldChunkCoord(0, 1)),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            WorldChunkBounds enormous = new WorldChunkBounds(
                new WorldChunkCoord(long.MinValue, long.MinValue),
                new WorldChunkCoord(long.MaxValue, long.MaxValue));
            Assert.That(enormous.TryGetArea(out _), Is.False);
        }

        [Test]
        public void WorldExtentDistinguishesFiniteInfiniteAndInvalidDefault()
        {
            WorldExtent invalid = default(WorldExtent);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Contains(default(WorldChunkCoord)), Is.False);

            WorldExtent infinite = WorldExtent.Infinite;
            Assert.That(infinite.IsValid, Is.True);
            Assert.That(infinite.IsInfinite, Is.True);
            Assert.That(infinite.Contains(new WorldChunkCoord(long.MinValue, long.MaxValue)), Is.True);
            Assert.That(infinite.TryGetFiniteBounds(out _), Is.False);

            WorldChunkBounds bounds = new WorldChunkBounds(new WorldChunkCoord(-2, -2), new WorldChunkCoord(3, 4));
            WorldExtent finite = WorldExtent.Finite(bounds);
            Assert.That(finite.IsFinite, Is.True);
            Assert.That(finite.Contains(new WorldChunkCoord(-2, -2)), Is.True);
            Assert.That(finite.Contains(new WorldChunkCoord(3, 0)), Is.False);
            Assert.That(finite.TryGetFiniteBounds(out WorldChunkBounds roundTrip), Is.True);
            Assert.That(roundTrip, Is.EqualTo(bounds));

            Assert.That(() => WorldExtent.Finite(default(WorldChunkBounds)), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ChunkLifecycleOnlyAllowsAdjacentExplicitTransitions()
        {
            WorldChunkLifecycle lifecycle = new WorldChunkLifecycle();
            Assert.That(lifecycle.State, Is.EqualTo(WorldChunkState.Unloaded));

            WorldChunkTransitionResult skipped = lifecycle.TryTransition(WorldChunkState.DataReady);
            Assert.That(skipped.Success, Is.False);
            Assert.That(skipped.Error, Is.EqualTo(WorldChunkTransitionError.InvalidTransition));
            Assert.That(lifecycle.State, Is.EqualTo(WorldChunkState.Unloaded));

            AssertTransition(lifecycle, WorldChunkState.Metadata, WorldChunkState.Unloaded);
            AssertTransition(lifecycle, WorldChunkState.DataReady, WorldChunkState.Metadata);
            AssertTransition(lifecycle, WorldChunkState.Active, WorldChunkState.DataReady);

            WorldChunkTransitionResult same = lifecycle.TryTransition(WorldChunkState.Active);
            Assert.That(same.Success, Is.False);
            Assert.That(same.Error, Is.EqualTo(WorldChunkTransitionError.AlreadyInState));

            AssertTransition(lifecycle, WorldChunkState.DataReady, WorldChunkState.Active);
            AssertTransition(lifecycle, WorldChunkState.Metadata, WorldChunkState.DataReady);
            AssertTransition(lifecycle, WorldChunkState.Unloaded, WorldChunkState.Metadata);
        }

        [Test]
        public void ChunkLifecycleRejectsUnknownEnumWithoutMutatingState()
        {
            Assert.That(() => new WorldChunkLifecycle((WorldChunkState)999),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            WorldChunkLifecycle lifecycle = new WorldChunkLifecycle(WorldChunkState.Metadata);
            WorldChunkTransitionResult result = lifecycle.TryTransition((WorldChunkState)999);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(WorldChunkTransitionError.InvalidState));
            Assert.That(lifecycle.State, Is.EqualTo(WorldChunkState.Metadata));
        }

        private static void AssertTransition(
            WorldChunkLifecycle lifecycle,
            WorldChunkState target,
            WorldChunkState expectedPrevious)
        {
            WorldChunkTransitionResult result = lifecycle.TryTransition(target);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.EqualTo(WorldChunkTransitionError.None));
            Assert.That(result.PreviousState, Is.EqualTo(expectedPrevious));
            Assert.That(result.CurrentState, Is.EqualTo(target));
            Assert.That(lifecycle.State, Is.EqualTo(target));
        }
    }
}
