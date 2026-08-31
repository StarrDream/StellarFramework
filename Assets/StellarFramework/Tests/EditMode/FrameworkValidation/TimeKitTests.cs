using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class TimeKitTests
    {
        [SetUp]
        public void SetUp()
        {
            TimeKit.ClearAllTimers();
            TimeKit.Configure(new TimeKitSettings { InitialTimerCapacity = 8, MaxCallbacksPerUpdate = 2 });
            Assert.That(TimeKit.Reset(new GameDateTime(1, 1, 1)), Is.True);
        }

        [Test]
        public void CalendarRoundTripAndDayBoundaryAreStable()
        {
            var date = new GameDateTime(2, 3, 30, 23, 59, 59, 999);
            long tick = TimeKit.ToTick(date);
            Assert.That(TimeKit.ToDateTime(tick), Is.EqualTo(date));
            Assert.That(TimeKit.ToDateTime(TickMath.TicksPerDay), Is.EqualTo(new GameDateTime(1, 1, 2)));
        }

        [Test]
        public void ClockKeepsFractionalTicksAndIgnoresUnityScaleConceptually()
        {
            var clock = new TimeClock();
            clock.Reset(0, 1d, false);
            Assert.That(clock.Advance(0.0005d), Is.EqualTo(0));
            Assert.That(clock.Advance(0.0005d), Is.EqualTo(1));
            clock.SetTimeScale(2d);
            Assert.That(clock.Advance(0.5d), Is.EqualTo(1000));
            clock.Pause();
            Assert.That(clock.Advance(1d), Is.EqualTo(0));
        }

        [Test]
        public void SameTickTimersUseRegistrationOrderAndStaleHandleCannotCancelReuse()
        {
            string order = string.Empty;
            TimerHandle first = TimeKit.ScheduleAt(10, () => order += "A");
            TimeKit.ScheduleAt(10, () => order += "B");
            Assert.That(TimeKit.Cancel(first), Is.True);
            TimerHandle reused = TimeKit.ScheduleAt(10, () => order += "C");
            Assert.That(first.Cancel(), Is.False);
            Assert.That(reused.IsValid, Is.True);
            TimeKit.AddTime(GameDuration.FromTicks(10));
            Assert.That(order, Is.EqualTo("BC"));
            Assert.That(TimeKit.ValidateInvariantsForTests(), Is.True);
        }

        [Test]
        public void LatestCatchUpCompressesPeriodsAndBudgetLeavesBacklog()
        {
            int elapsed = 0;
            TimeKit.ScheduleEvery(GameDuration.Seconds(1), context => elapsed = context.ElapsedCount,
                TimerCatchUpPolicy.Latest);
            TimeKit.AddTime(GameDuration.Seconds(5));
            Assert.That(elapsed, Is.EqualTo(5));

            TimeKit.ClearAllTimers();
            int allCount = 0;
            TimeKit.ScheduleEvery(GameDuration.Seconds(1), () => allCount++, TimerCatchUpPolicy.All, 5);
            TimeKit.AddTime(GameDuration.Seconds(5));
            Assert.That(allCount, Is.EqualTo(2));
            Assert.That(TimeKit.GetDiagnostics().DueBacklogCount, Is.EqualTo(1));
            TimeKit.ProcessDueNow(8);
            Assert.That(allCount, Is.EqualTo(5));
        }

        [Test]
        public void CallbackReentrancyAndClearAreSafe()
        {
            int executed = 0;
            TimerHandle recurring = default;
            recurring = TimeKit.ScheduleEvery(GameDuration.Seconds(1), () =>
            {
                executed++;
                recurring.Cancel();
                TimeKit.ScheduleAt(TimeKit.Tick, () => executed++);
            });
            TimeKit.AddTime(GameDuration.Seconds(1));
            TimeKit.ProcessDueNow(4);
            Assert.That(executed, Is.EqualTo(2));
            Assert.That(recurring.IsValid, Is.False);

            TimeKit.ScheduleAt(TimeKit.Tick, () => TimeKit.ClearAllTimers());
            TimeKit.ScheduleAt(TimeKit.Tick, () => executed += 100);
            TimeKit.ProcessDueNow(4);
            Assert.That(executed, Is.EqualTo(2));
            Assert.That(TimeKit.ActiveTimerCount, Is.EqualTo(0));
            Assert.That(TimeKit.ValidateInvariantsForTests(), Is.True);
        }

        [Test]
        public void OverflowAndInvalidInputsDoNotRegisterTimers()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ScheduleAfter delay"));
            Assert.That(TimeKit.ScheduleAfter(GameDuration.FromTicks(-1), () => { }), Is.EqualTo(TimerHandle.Invalid));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ScheduleEvery 参数非法"));
            Assert.That(TimeKit.ScheduleEvery(GameDuration.FromTicks(0), () => { }), Is.EqualTo(TimerHandle.Invalid));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ScheduleAt 失败"));
            Assert.That(TimeKit.ScheduleAt(-1, () => { }), Is.EqualTo(TimerHandle.Invalid));
            Assert.That(TimeKit.ActiveTimerCount, Is.EqualTo(0));
        }
    }
}
