using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>SimulationKit V1 行为契约：调度器本身不依赖 Unity 时间或业务对象。</summary>
    public sealed class SimulationKitTests
    {
        [Test]
        public void SimulationIdRejectsNegativeAndKeepsZeroInvalid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationId(-1));
            SimulationId none = new SimulationId(0);
            SimulationId id = new SimulationId(7);
            Assert.That(none.IsInvalid, Is.True);
            Assert.That(none.IsValid, Is.False);
            Assert.That(id.IsValid, Is.True);
            Assert.That(id.Value, Is.EqualTo(7));
            Assert.That(id.ToString(), Is.EqualTo("7"));
            Assert.That(id, Is.EqualTo(new SimulationId(7)));
            Assert.That(id == new SimulationId(7), Is.True);
            Assert.That(id != new SimulationId(8), Is.True);
        }

        [Test]
        public void ConstructorRejectsNegativeCapacityAndStartsEmpty()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationScheduler(-1));
            SimulationScheduler scheduler = new SimulationScheduler(8);
            Assert.That(scheduler.Count, Is.EqualTo(0));
            Assert.That(scheduler.Contains(new SimulationId(1)), Is.False);
        }

        [Test]
        public void RegisterNormalAndExplicitDelayExposeState()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            Assert.That(scheduler.TryRegister(new SimulationId(1), 10, 5).Success, Is.True);
            Assert.That(scheduler.TryRegister(new SimulationId(2), 10, 7, 3).Success, Is.True);
            Assert.That(scheduler.Count, Is.EqualTo(2));
            Assert.That(scheduler.TryGetInterval(new SimulationId(1), out long interval1), Is.True);
            Assert.That(interval1, Is.EqualTo(5));
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(1), out long due1), Is.True);
            Assert.That(due1, Is.EqualTo(15));
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(2), out long due2), Is.True);
            Assert.That(due2, Is.EqualTo(13));
        }

        [Test]
        public void ExplicitZeroDelayIsImmediatelyDue()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            Assert.That(scheduler.TryRegister(new SimulationId(4), 100, 10, 0).Success, Is.True);
            SimulationId[] destination = new SimulationId[1];
            SimulationCollectResult result = scheduler.CollectDue(100, destination.AsSpan());
            Assert.That(result.WrittenCount, Is.EqualTo(1));
            Assert.That(destination[0], Is.EqualTo(new SimulationId(4)));
            Assert.That(result.HasBacklog, Is.False);
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(4), out long due), Is.True);
            Assert.That(due, Is.EqualTo(110));
        }

        [Test]
        public void RegisterFailuresAreAtomic()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            SimulationId id = new SimulationId(1);
            Assert.That(scheduler.TryRegister(id, 0, 10).Success, Is.True);
            Assert.That(scheduler.TryRegister(id, 0, 10).Error, Is.EqualTo(SimulationMutationError.DuplicateId));
            Assert.That(scheduler.TryRegister(new SimulationId(0), 0, 10).Error,
                Is.EqualTo(SimulationMutationError.InvalidId));
            Assert.That(scheduler.TryRegister(new SimulationId(2), 0, 0).Error,
                Is.EqualTo(SimulationMutationError.InvalidInterval));
            Assert.That(scheduler.TryRegister(new SimulationId(3), 0, 10, -1).Error,
                Is.EqualTo(SimulationMutationError.InvalidDelay));
            Assert.That(scheduler.Count, Is.EqualTo(1));
            Assert.That(scheduler.TryGetNextDueTick(id, out long due), Is.True);
            Assert.That(due, Is.EqualTo(10));
        }

        [Test]
        public void RegisterOverflowReturnsErrorWithoutAdding()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            SimulationMutationResult result = scheduler.TryRegister(new SimulationId(1), long.MaxValue, 1);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SimulationMutationError.TickOverflow));
            Assert.That(scheduler.Count, Is.EqualTo(0));
        }

        [Test]
        public void CollectDueHandlesNotDueAndActualDispatchTickWithoutCatchup()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 0, 10);
            SimulationId[] destination = new SimulationId[4];
            SimulationCollectResult notDue = scheduler.CollectDue(9, destination.AsSpan());
            Assert.That(notDue.WrittenCount, Is.EqualTo(0));
            Assert.That(notDue.HasBacklog, Is.False);

            SimulationCollectResult overdue = scheduler.CollectDue(100, destination.AsSpan());
            Assert.That(overdue.WrittenCount, Is.EqualTo(1));
            Assert.That(overdue.HasBacklog, Is.False);
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(1), out long next), Is.True);
            Assert.That(next, Is.EqualTo(110));

            SimulationCollectResult sameTick = scheduler.CollectDue(100, destination.AsSpan());
            Assert.That(sameTick.WrittenCount, Is.EqualTo(0));
            Assert.That(scheduler.CollectDue(110, destination.AsSpan()).WrittenCount, Is.EqualTo(1));
        }

        [Test]
        public void CollectDueUsesDeterministicIdTieBreak()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(9), 0, 5);
            scheduler.TryRegister(new SimulationId(3), 0, 5);
            scheduler.TryRegister(new SimulationId(7), 0, 5);
            scheduler.TryRegister(new SimulationId(1), 0, 5);
            SimulationId[] destination = new SimulationId[4];
            SimulationCollectResult result = scheduler.CollectDue(5, destination.AsSpan());
            Assert.That(result.WrittenCount, Is.EqualTo(4));
            Assert.That(destination[0].Value, Is.EqualTo(1));
            Assert.That(destination[1].Value, Is.EqualTo(3));
            Assert.That(destination[2].Value, Is.EqualTo(7));
            Assert.That(destination[3].Value, Is.EqualTo(9));
        }

        [Test]
        public void CollectDueBudgetDrainsBacklogOnSameTickWithoutDuplicates()
        {
            const int count = 101;
            SimulationScheduler scheduler = new SimulationScheduler(count);
            for (int i = 1; i <= count; i++)
            {
                Assert.That(scheduler.TryRegister(new SimulationId(i), 0, 20).Success, Is.True);
            }

            var seen = new HashSet<int>();
            SimulationId[] destination = new SimulationId[7];
            int calls = 0;
            bool backlog;
            do
            {
                SimulationCollectResult result = scheduler.CollectDue(20, destination.AsSpan());
                calls++;
                for (int i = 0; i < result.WrittenCount; i++)
                {
                    Assert.That(seen.Add(destination[i].Value), Is.True);
                }

                backlog = result.HasBacklog;
            } while (backlog);

            Assert.That(seen.Count, Is.EqualTo(count));
            Assert.That(calls, Is.EqualTo((count + destination.Length - 1) / destination.Length));
            Assert.That(scheduler.CollectDue(20, destination.AsSpan()).WrittenCount, Is.EqualTo(0));
        }

        [Test]
        public void EmptyDestinationReportsBacklogWithoutChangingEntries()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 0, 10);
            scheduler.TryRegister(new SimulationId(2), 0, 20);
            SimulationCollectResult result = scheduler.CollectDue(10, Span<SimulationId>.Empty);
            Assert.That(result.WrittenCount, Is.EqualTo(0));
            Assert.That(result.HasBacklog, Is.True);
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(1), out long due), Is.True);
            Assert.That(due, Is.EqualTo(10));
            Assert.That(scheduler.CollectDue(10, new SimulationId[2].AsSpan()).WrittenCount, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterRootMiddleAndLastKeepsHeapIndexIntegrity()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            for (int i = 1; i <= 8; i++) scheduler.TryRegister(new SimulationId(i), 0, i);
            Assert.That(scheduler.TryUnregister(new SimulationId(1)).Success, Is.True);
            Assert.That(scheduler.TryUnregister(new SimulationId(4)).Success, Is.True);
            Assert.That(scheduler.TryUnregister(new SimulationId(8)).Success, Is.True);
            Assert.That(scheduler.TryUnregister(new SimulationId(99)).Error, Is.EqualTo(SimulationMutationError.NotFound));
            Assert.That(scheduler.Count, Is.EqualTo(5));
            for (int i = 1; i <= 8; i++)
            {
                bool shouldExist = i != 1 && i != 4 && i != 8;
                Assert.That(scheduler.Contains(new SimulationId(i)), Is.EqualTo(shouldExist), i.ToString());
            }

            SimulationId[] destination = new SimulationId[8];
            SimulationCollectResult result = scheduler.CollectDue(8, destination.AsSpan());
            Assert.That(result.WrittenCount, Is.EqualTo(5));
            Assert.That(destination[0].Value, Is.EqualTo(2));
            Assert.That(destination[1].Value, Is.EqualTo(3));
            Assert.That(destination[2].Value, Is.EqualTo(5));
            Assert.That(destination[3].Value, Is.EqualTo(6));
            Assert.That(destination[4].Value, Is.EqualTo(7));
        }

        [Test]
        public void SetIntervalReordersBothDirectionsAndMissingIsReported()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 0, 10);
            scheduler.TryRegister(new SimulationId(2), 0, 20);
            scheduler.TryRegister(new SimulationId(3), 0, 30);
            Assert.That(scheduler.TrySetInterval(new SimulationId(3), 1, 1).Success, Is.True);
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(3), out long early), Is.True);
            Assert.That(early, Is.EqualTo(2));
            Assert.That(scheduler.CollectDue(2, new SimulationId[1].AsSpan()).WrittenCount, Is.EqualTo(1));
            Assert.That(scheduler.TrySetInterval(new SimulationId(1), 2, 100).Success, Is.True);
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(1), out long late), Is.True);
            Assert.That(late, Is.EqualTo(102));
            Assert.That(scheduler.TrySetInterval(new SimulationId(99), 2, 1).Error,
                Is.EqualTo(SimulationMutationError.NotFound));
            Assert.That(scheduler.TrySetInterval(new SimulationId(2), 2, 0).Error,
                Is.EqualTo(SimulationMutationError.InvalidInterval));
        }

        [Test]
        public void SetIntervalOverflowIsAtomic()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 0, 10);
            SimulationMutationResult result = scheduler.TrySetInterval(new SimulationId(1), long.MaxValue, 1);
            Assert.That(result.Error, Is.EqualTo(SimulationMutationError.TickOverflow));
            Assert.That(scheduler.TryGetInterval(new SimulationId(1), out long interval), Is.True);
            Assert.That(interval, Is.EqualTo(10));
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(1), out long due), Is.True);
            Assert.That(due, Is.EqualTo(10));
        }

        [Test]
        public void TimeRegressionIsRejectedAndFailedMutationStillObservesLaterTick()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 100, 10);
            Assert.That(scheduler.TryRegister(new SimulationId(2), 200, 0).Error,
                Is.EqualTo(SimulationMutationError.InvalidInterval));
            Assert.Throws<InvalidOperationException>(() => scheduler.CollectDue(199, new SimulationId[1].AsSpan()));
            Assert.Throws<InvalidOperationException>(() => scheduler.TrySetInterval(new SimulationId(1), 150, 1));
            Assert.Throws<InvalidOperationException>(() => scheduler.TryRegister(new SimulationId(3), 150, 1));
            Assert.That(scheduler.CollectDue(200, new SimulationId[1].AsSpan()).WrittenCount, Is.EqualTo(1));
        }

        [Test]
        public void SameTickIsAllowedAndClearResetsTimeline()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 5, 2);
            Assert.That(scheduler.TrySetInterval(new SimulationId(1), 5, 3).Success, Is.True);
            Assert.That(scheduler.CollectDue(5, new SimulationId[1].AsSpan()).WrittenCount, Is.EqualTo(0));
            scheduler.Clear();
            Assert.That(scheduler.Count, Is.EqualTo(0));
            Assert.That(scheduler.TryRegister(new SimulationId(2), 0, 1).Success, Is.True);
            Assert.That(scheduler.CollectDue(1, new SimulationId[1].AsSpan()).WrittenCount, Is.EqualTo(1));
        }

        [Test]
        public void CollectOverflowLeavesCurrentEntryAndEarlierDispatchesCommitted()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), long.MaxValue - 3, 1);
            scheduler.TryRegister(new SimulationId(2), long.MaxValue - 2, 2, 0);
            SimulationId[] destination = new SimulationId[2];
            Assert.Throws<OverflowException>(() => scheduler.CollectDue(long.MaxValue - 1, destination.AsSpan()));
            Assert.That(destination[0], Is.EqualTo(new SimulationId(1)));
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(2), out long due), Is.True);
            Assert.That(due, Is.EqualTo(long.MaxValue - 2));
            Assert.That(scheduler.TryGetNextDueTick(new SimulationId(1), out long earlierNext), Is.True);
            Assert.That(earlierNext, Is.EqualTo(long.MaxValue));
            Assert.That(scheduler.Count, Is.EqualTo(2));
        }

        [Test]
        public void HasBacklogDistinguishesExactFillAndOneExtra()
        {
            SimulationScheduler scheduler = new SimulationScheduler();
            scheduler.TryRegister(new SimulationId(1), 0, 10);
            scheduler.TryRegister(new SimulationId(2), 0, 10);
            scheduler.TryRegister(new SimulationId(3), 0, 10);
            SimulationCollectResult exact = scheduler.CollectDue(10, new SimulationId[3].AsSpan());
            Assert.That(exact.WrittenCount, Is.EqualTo(3));
            Assert.That(exact.HasBacklog, Is.False);

            scheduler.Clear();
            scheduler.TryRegister(new SimulationId(1), 0, 10);
            scheduler.TryRegister(new SimulationId(2), 0, 10);
            scheduler.TryRegister(new SimulationId(3), 0, 10);
            SimulationCollectResult oneExtra = scheduler.CollectDue(10, new SimulationId[2].AsSpan());
            Assert.That(oneExtra.WrittenCount, Is.EqualTo(2));
            Assert.That(oneExtra.HasBacklog, Is.True);
        }
    }
}
