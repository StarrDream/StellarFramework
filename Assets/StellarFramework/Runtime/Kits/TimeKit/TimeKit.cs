using System;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 游戏世界时钟与高性能未来时刻调度入口。它与 Unity Time.timeScale 解耦，
    /// 默认基于 Time.unscaledDeltaTime 推进；它不替代 ActionKit 的流程 Delay。
    /// </summary>
    public static class TimeKit
    {
        private static TimeKitSettings _settings;
        private static TimeClock _clock;
        private static TimeKitDiagnostics _diagnostics;
        private static TimeScheduler _scheduler;
        private static TimeKitDriver _driver;

        /// <summary>当前游戏世界绝对 Tick；一个 Tick 等于一游戏毫秒。</summary>
        public static long Tick
        {
            get
            {
                EnsureInitialized();
                return _clock.Tick;
            }
        }

        /// <summary>由当前 Tick 和当前日历规则推导的日期时间视图。</summary>
        public static GameDateTime Now => ToDateTime(Tick);

        /// <summary>游戏世界时间倍率，不跟随 Unity Time.timeScale。</summary>
        public static double TimeScale
        {
            get
            {
                EnsureInitialized();
                return _clock.TimeScale;
            }
            set
            {
                EnsureInitialized();
                _clock.SetTimeScale(value);
            }
        }

        /// <summary>TimeKit 是否被显式暂停。</summary>
        public static bool IsPaused
        {
            get
            {
                EnsureInitialized();
                return _clock.IsPaused;
            }
        }

        /// <summary>当前活动 Timer 数。</summary>
        public static int ActiveTimerCount
        {
            get
            {
                EnsureInitialized();
                return _scheduler.ActiveTimerCount;
            }
        }

        /// <summary>当前日期时间转换所使用的游戏日历。</summary>
        public static GameCalendarSettings Calendar
        {
            get
            {
                EnsureInitialized();
                return _settings.Calendar;
            }
        }

        /// <summary>
        /// 在尚无活动 Timer 时覆盖默认配置。配置成功后会重置世界时钟到 Tick 0。
        /// </summary>
        public static bool Configure(TimeKitSettings settings)
        {
            EnsureInitialized();
            if (settings == null)
            {
                LogKit.LogError("[TimeKit] Configure 失败: settings 为空。");
                return false;
            }

            if (_scheduler.ActiveTimerCount != 0)
            {
                LogKit.LogError("[TimeKit] Configure 失败: 存在活动 Timer 时禁止替换运行时配置。");
                return false;
            }

            _scheduler.Reset();
            _settings = settings.CloneValidated();
            _scheduler.Reserve(_settings.InitialTimerCapacity);
            _clock.Reset(0L, _settings.DefaultTimeScale, false);
            return true;
        }

        /// <summary>显式暂停游戏世界时间；Unity unscaled 时间仍会流逝。</summary>
        public static void Pause()
        {
            EnsureInitialized();
            _clock.Pause();
        }

        /// <summary>恢复游戏世界时间推进。</summary>
        public static void Resume()
        {
            EnsureInitialized();
            _clock.Resume();
        }

        /// <summary>预留 Timer Slot 容量，适合进入大规模模拟场景前调用。</summary>
        public static void Reserve(int timerCapacity)
        {
            EnsureInitialized();
            if (timerCapacity < 0)
            {
                LogKit.LogError($"[TimeKit] Reserve 参数非法: {timerCapacity}");
                return;
            }

            _scheduler.Reserve(timerCapacity);
        }

        /// <summary>清空所有 Timer；正在执行的 callback 可结束，但不会再次被重新调度。</summary>
        public static void ClearAllTimers()
        {
            EnsureInitialized();
            _scheduler.ClearAll();
        }

        /// <summary>在当前 Tick 之后调度一次无上下文回调。</summary>
        public static TimerHandle ScheduleAfter(GameDuration delay, Action callback)
        {
            return ScheduleAfterInternal(delay, callback, null, null, 0);
        }

        /// <summary>在当前 Tick 之后调度一次带时间上下文的回调。</summary>
        public static TimerHandle ScheduleAfter(GameDuration delay, Action<TimeTriggerContext> callback)
        {
            return ScheduleAfterInternal(delay, null, callback, null, 0);
        }

        /// <summary>在当前 Tick 之后调度一次高性能 Receiver 事件。</summary>
        public static TimerHandle ScheduleAfter(GameDuration delay, ITimeEventReceiver receiver, int eventId)
        {
            return ScheduleAfterInternal(delay, null, null, receiver, eventId);
        }

        /// <summary>在指定 Tick 调度一次无上下文回调。过去或当前 Tick 的任务不会同步执行。</summary>
        public static TimerHandle ScheduleAt(long triggerTick, Action callback)
        {
            EnsureInitialized();
            return ValidateOneShot(triggerTick, callback) ?
                _scheduler.Schedule(triggerTick, 0L, 1, TimerCatchUpPolicy.Latest, callback) : TimerHandle.Invalid;
        }

        /// <summary>在指定 Tick 调度一次带时间上下文的回调。过去或当前 Tick 的任务不会同步执行。</summary>
        public static TimerHandle ScheduleAt(long triggerTick, Action<TimeTriggerContext> callback)
        {
            EnsureInitialized();
            return ValidateOneShot(triggerTick, callback) ?
                _scheduler.Schedule(triggerTick, 0L, 1, TimerCatchUpPolicy.Latest, callback) : TimerHandle.Invalid;
        }

        /// <summary>在指定游戏日期时间调度一次回调。</summary>
        public static TimerHandle ScheduleAt(GameDateTime dateTime, Action callback)
        {
            return TryToTick(dateTime, out long tick) ? ScheduleAt(tick, callback) : TimerHandle.Invalid;
        }

        /// <summary>按固定游戏世界周期调度无上下文回调。</summary>
        public static TimerHandle ScheduleEvery(GameDuration interval, Action callback,
            TimerCatchUpPolicy catchUpPolicy = TimerCatchUpPolicy.Latest, int repeatCount = -1)
        {
            EnsureInitialized();
            if (!ValidateRecurring(interval, callback, catchUpPolicy, repeatCount) ||
                !TickMath.TryAdd(_clock.Tick, interval.Ticks, out long triggerTick))
            {
                return TimerHandle.Invalid;
            }

            return _scheduler.Schedule(triggerTick, interval.Ticks, repeatCount, catchUpPolicy, callback);
        }

        /// <summary>按固定游戏世界周期调度带上下文回调。</summary>
        public static TimerHandle ScheduleEvery(GameDuration interval, Action<TimeTriggerContext> callback,
            TimerCatchUpPolicy catchUpPolicy = TimerCatchUpPolicy.Latest, int repeatCount = -1)
        {
            EnsureInitialized();
            if (!ValidateRecurring(interval, callback, catchUpPolicy, repeatCount) ||
                !TickMath.TryAdd(_clock.Tick, interval.Ticks, out long triggerTick))
            {
                return TimerHandle.Invalid;
            }

            return _scheduler.Schedule(triggerTick, interval.Ticks, repeatCount, catchUpPolicy, callback);
        }

        /// <summary>按固定游戏世界周期调度高性能 Receiver 事件。</summary>
        public static TimerHandle ScheduleEvery(GameDuration interval, ITimeEventReceiver receiver, int eventId,
            TimerCatchUpPolicy catchUpPolicy = TimerCatchUpPolicy.Latest, int repeatCount = -1)
        {
            EnsureInitialized();
            if (!ValidateRecurring(interval, receiver, catchUpPolicy, repeatCount) ||
                !TickMath.TryAdd(_clock.Tick, interval.Ticks, out long triggerTick))
            {
                return TimerHandle.Invalid;
            }

            return _scheduler.Schedule(triggerTick, interval.Ticks, repeatCount, catchUpPolicy, receiver, eventId);
        }

        /// <summary>取消一个有效 Timer；已取消、已完成或过期句柄返回 false。</summary>
        public static bool Cancel(TimerHandle handle)
        {
            EnsureInitialized();
            return _scheduler.Cancel(handle);
        }

        /// <summary>向未来推进游戏世界时间，并立即在当前调用中处理受预算限制的到期 Timer。</summary>
        public static bool AddTime(GameDuration duration)
        {
            EnsureInitialized();
            if (duration.Ticks < 0L || !_clock.AddTicks(duration.Ticks))
            {
                return false;
            }

            if (!_scheduler.IsProcessing)
            {
                _scheduler.ProcessDue(_clock.Tick, _settings.MaxCallbacksPerUpdate);
            }

            return true;
        }

        /// <summary>只允许向未来设置世界时间；回退请使用 Reset。</summary>
        public static bool SetTime(GameDateTime futureTime)
        {
            EnsureInitialized();
            if (!TryToTick(futureTime, out long futureTick) || futureTick < _clock.Tick)
            {
                LogKit.LogError("[TimeKit] SetTime 失败: 仅允许设置到当前 Tick 或未来 Tick。");
                return false;
            }

            return AddTime(GameDuration.FromTicks(futureTick - _clock.Tick));
        }

        /// <summary>重置世界时间并清空所有运行时 Timer，使现有 Handle 全部失效。</summary>
        public static bool Reset(GameDateTime time)
        {
            EnsureInitialized();
            if (!TryToTick(time, out long tick))
            {
                LogKit.LogError("[TimeKit] Reset 失败: 非法 GameDateTime。");
                return false;
            }

            _scheduler.Reset();
            _clock.Reset(tick, _settings.DefaultTimeScale, false);
            return true;
        }

        /// <summary>把 Tick 转换为当前游戏日历下的日期时间。</summary>
        public static GameDateTime ToDateTime(long tick)
        {
            EnsureInitialized();
            if (tick < 0L)
            {
                LogKit.LogError($"[TimeKit] ToDateTime 不接受负 Tick: {tick}");
                return default;
            }

            return GameCalendarConverter.ToDateTime(tick, _settings.Calendar);
        }

        /// <summary>把游戏日期时间转换为当前日历下的 Tick；非法输入返回 0 并记录错误。</summary>
        public static long ToTick(GameDateTime dateTime)
        {
            return TryToTick(dateTime, out long tick) ? tick : 0L;
        }

        /// <summary>获取低成本运行时诊断快照。</summary>
        public static TimeKitDiagnosticsSnapshot GetDiagnostics()
        {
            EnsureInitialized();
            return _scheduler.GetDiagnostics();
        }

        /// <summary>立即处理当前已到期任务，适用于加载结算或测试；仍受传入预算限制。</summary>
        public static int ProcessDueNow(int maxCallbacks)
        {
            EnsureInitialized();
            if (maxCallbacks < 1)
            {
                LogKit.LogError($"[TimeKit] ProcessDueNow 参数非法: {maxCallbacks}");
                return 0;
            }

            return _scheduler.ProcessDue(_clock.Tick, maxCallbacks);
        }

        internal static bool IsHandleValid(TimerHandle handle)
        {
            EnsureInitialized();
            return _scheduler.IsHandleValid(handle);
        }

        internal static void InternalUpdate(float unscaledDeltaSeconds)
        {
            EnsureInitialized();
            _clock.Advance(unscaledDeltaSeconds);
            _scheduler.ProcessDue(_clock.Tick, _settings.MaxCallbacksPerUpdate);
        }

        internal static void SetDriver(TimeKitDriver driver)
        {
            _driver = driver;
        }

        internal static bool ValidateInvariantsForTests()
        {
            EnsureInitialized();
            return _scheduler.ValidateInvariants();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_scheduler == null)
            {
                CreateRuntime(new TimeKitSettings().CloneValidated());
            }
            else
            {
                _scheduler.Reset();
                _clock.Reset(0L, _settings.DefaultTimeScale, false);
            }

            _driver = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDriverBeforeSceneLoad()
        {
            EnsureInitialized();
            TimeKitDriver.EnsureCreated();
        }

        private static void EnsureInitialized()
        {
            if (_scheduler == null)
            {
                CreateRuntime(new TimeKitSettings().CloneValidated());
            }
        }

        private static void CreateRuntime(TimeKitSettings settings)
        {
            _settings = settings;
            _diagnostics = new TimeKitDiagnostics();
            _clock = new TimeClock();
            _clock.Reset(0L, settings.DefaultTimeScale, false);
            _scheduler = new TimeScheduler(settings.InitialTimerCapacity, _diagnostics);
        }

        private static TimerHandle ScheduleAfterInternal(GameDuration delay, Action callback,
            Action<TimeTriggerContext> contextCallback, ITimeEventReceiver receiver, int eventId)
        {
            EnsureInitialized();
            if (delay.Ticks < 0L || !TickMath.TryAdd(_clock.Tick, delay.Ticks, out long triggerTick))
            {
                LogKit.LogError($"[TimeKit] ScheduleAfter delay 非法或溢出: {delay.Ticks}");
                return TimerHandle.Invalid;
            }

            if (callback != null)
            {
                return _scheduler.Schedule(triggerTick, 0L, 1, TimerCatchUpPolicy.Latest, callback);
            }

            if (contextCallback != null)
            {
                return _scheduler.Schedule(triggerTick, 0L, 1, TimerCatchUpPolicy.Latest, contextCallback);
            }

            if (receiver != null)
            {
                return _scheduler.Schedule(triggerTick, 0L, 1, TimerCatchUpPolicy.Latest, receiver, eventId);
            }

            LogKit.LogError("[TimeKit] ScheduleAfter 失败: callback 或 receiver 为空。");
            return TimerHandle.Invalid;
        }

        private static bool TryToTick(GameDateTime dateTime, out long tick)
        {
            EnsureInitialized();
            if (GameCalendarConverter.TryToTick(dateTime, _settings.Calendar, out tick))
            {
                return true;
            }

            LogKit.LogError($"[TimeKit] 非法 GameDateTime: {dateTime}");
            return false;
        }

        private static bool ValidateOneShot(long triggerTick, Delegate callback)
        {
            if (triggerTick < 0L || callback == null)
            {
                LogKit.LogError("[TimeKit] ScheduleAt 失败: triggerTick 或 callback 非法。");
                return false;
            }

            return true;
        }

        private static bool ValidateRecurring(GameDuration interval, Delegate callback,
            TimerCatchUpPolicy catchUpPolicy, int repeatCount)
        {
            return ValidateRecurringCore(interval, callback != null, catchUpPolicy, repeatCount);
        }

        private static bool ValidateRecurring(GameDuration interval, ITimeEventReceiver receiver,
            TimerCatchUpPolicy catchUpPolicy, int repeatCount)
        {
            return ValidateRecurringCore(interval, receiver != null, catchUpPolicy, repeatCount);
        }

        private static bool ValidateRecurringCore(GameDuration interval, bool hasCallback,
            TimerCatchUpPolicy catchUpPolicy, int repeatCount)
        {
            if (!hasCallback || interval.Ticks <= 0L || repeatCount == 0 || repeatCount < -1 ||
                catchUpPolicy < TimerCatchUpPolicy.All || catchUpPolicy > TimerCatchUpPolicy.Skip)
            {
                LogKit.LogError("[TimeKit] ScheduleEvery 参数非法。");
                return false;
            }

            return true;
        }
    }
}
