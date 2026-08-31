#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;

namespace StellarFramework
{
    /// <summary>仅供 Editor 或 Development Build 主线程使用的 TimeKit 基准入口。</summary>
    public static class TimeKitBenchmark
    {
        private static readonly Action NoOp = OnNoOp;

        /// <summary>执行 10 万注册、空闲更新、随机取消与批量到期基准。</summary>
        public static TimeKitBenchmarkResult Run100k()
        {
            const int count = 100000;
            TimeKit.ClearAllTimers();
            TimeKit.Reserve(count);
            var handles = new TimerHandle[count];
            long gcBefore = GC.GetTotalMemory(false);
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++) handles[i] = TimeKit.ScheduleAfter(GameDuration.Hours(1), NoOp);
            long registerMs = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            for (int i = 0; i < 1000; i++) TimeKit.ProcessDueNow(1);
            long idleMs = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            for (int i = 0; i < count; i += 2) handles[i].Cancel();
            long cancelMs = stopwatch.ElapsedMilliseconds;

            TimeKit.ClearAllTimers();
            for (int i = 0; i < count; i++) TimeKit.ScheduleAt(TimeKit.Tick, NoOp);
            stopwatch.Restart();
            while (TimeKit.ActiveTimerCount > 0) TimeKit.ProcessDueNow(count);
            long dueMs = stopwatch.ElapsedMilliseconds;
            return new TimeKitBenchmarkResult(registerMs, idleMs, cancelMs, dueMs, GC.GetTotalMemory(false) - gcBefore);
        }

        private static void OnNoOp() { }
    }

    /// <summary>TimeKit 基准结果；GC 数字包含基准方法自身及运行环境分配。</summary>
    public readonly struct TimeKitBenchmarkResult
    {
        /// <summary>10 万注册耗时毫秒。</summary>
        public long RegisterMilliseconds { get; }
        /// <summary>1000 次空闲处理耗时毫秒。</summary>
        public long IdleUpdateMilliseconds { get; }
        /// <summary>5 万随机取消耗时毫秒。</summary>
        public long CancelMilliseconds { get; }
        /// <summary>10 万同 Tick 回调处理耗时毫秒。</summary>
        public long DueMilliseconds { get; }
        /// <summary>基准期间托管内存差值。</summary>
        public long ManagedMemoryDeltaBytes { get; }

        internal TimeKitBenchmarkResult(long registerMilliseconds, long idleUpdateMilliseconds, long cancelMilliseconds,
            long dueMilliseconds, long managedMemoryDeltaBytes)
        {
            RegisterMilliseconds = registerMilliseconds;
            IdleUpdateMilliseconds = idleUpdateMilliseconds;
            CancelMilliseconds = cancelMilliseconds;
            DueMilliseconds = dueMilliseconds;
            ManagedMemoryDeltaBytes = managedMemoryDeltaBytes;
        }
    }
}
#endif
