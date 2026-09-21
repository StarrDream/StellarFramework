// ==================================================================================
// PerformanceUtil
// ----------------------------------------------------------------------------------
// 仅负责 CPU 耗时测量、内存快照与显式 GC 控制。
// 性能测量不会捕获业务异常，避免改变被测代码的错误语义。
// ==================================================================================

using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

namespace StellarFramework
{
    /// <summary>
    /// 开发期性能诊断辅助。
    /// 不替代 Unity Profiler，也不应放在高频生产逻辑中。
    /// </summary>
    public static class PerformanceUtil
    {
        /// <summary>
        /// 测量同步代码块的执行耗时。
        /// 仅在 Editor 或 Development Build 中生效；Release 中调用点会被条件编译移除。
        /// </summary>
        /// <remarks>不会捕获 action 抛出的异常，异常保持原始传播语义。</remarks>
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
        /// 打印 Unity Reserved、Unity Allocated 与托管堆的大致内存快照。
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
        /// 强制执行完整 GC，并请求 Resources.UnloadUnusedAssets。
        /// </summary>
        /// <remarks>
        /// 会造成明显主线程停顿。只应在加载界面、场景切换等允许卡顿的明确节点调用。
        /// </remarks>
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