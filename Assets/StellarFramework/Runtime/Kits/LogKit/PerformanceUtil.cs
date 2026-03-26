// ==================================================================================
// PerformanceUtil - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：仅负责 CPU 耗时测量、内存快照与 GC 控制。
// 改造说明：
// 1. 彻底移除 MeasureExecutionTime 内部的 try-catch，遵循 Fail-Fast 原则。
//    性能测试工具不应干涉或掩盖业务代码的异常抛出行为。
// ==================================================================================

using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

namespace StellarFramework
{
    public static class PerformanceUtil
    {
        /// <summary>
        /// 测量代码块的执行耗时
        /// 仅在 Editor 或 Development Build 中生效，Release 包自动剔除，零性能开销
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void MeasureExecutionTime(Action action, string actionName = "Action")
        {
            if (action == null)
            {
                LogKit.LogError($"[PerformanceUtil] {actionName} 无法执行: 传入的 Action 委托为空");
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            // 核心改造：移除 try-catch，让业务异常自然上抛。
            // 保证在性能测量的同时，不破坏业务层原有的错误阻断链路。
            action.Invoke();

            stopwatch.Stop();
            LogKit.Log($"[PerformanceUtil] {actionName} 耗时: {stopwatch.Elapsed.TotalMilliseconds:F4} ms");
        }

        /// <summary>
        /// 打印当前内存快照
        /// </summary>
        public static void LogMemoryUsage()
        {
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long gcMemory = GC.GetTotalMemory(false);

            LogKit.Log($"[PerformanceUtil] 内存快照:\n" +
                       $" >> Unity Reserved (系统预留): {ToMB(totalReserved):F2} MB\n" +
                       $" >> Unity Allocated (实际使用): {ToMB(totalAllocated):F2} MB\n" +
                       $" >> Mono Heap (脚本堆内存): {ToMB(gcMemory):F2} MB");
        }

        /// <summary>
        /// 强制执行完整的垃圾回收 (极度危险，仅限场景切换或明确的内存释放节点使用)
        /// </summary>
        public static void ForceGarbageCollection()
        {
            LogKit.LogWarning("[PerformanceUtil] 正在执行强制 GC，将引发主线程阻塞与帧率抖动...");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Resources.UnloadUnusedAssets();

            LogKit.Log("[PerformanceUtil] 强制 GC 与资源卸载指令已发出");
        }

        private static float ToMB(long bytes)
        {
            return bytes / 1024f / 1024f;
        }
    }
}