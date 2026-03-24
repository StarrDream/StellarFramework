using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

namespace StellarFramework
{
    /// <summary>
    /// 性能与内存诊断工具
    /// 职责单一：仅负责 CPU 耗时测量、内存快照与 GC 控制，与基础日志流转彻底解耦。
    /// </summary>
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

            // 使用 Stopwatch 规避 DateTime.Now 带来的装箱与 GC 开销，且精度更高
            var stopwatch = Stopwatch.StartNew();
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                // 精准报错：保留堆栈信息，随后 Fail Fast 抛出，绝不掩盖业务层异常
                LogKit.LogError($"[PerformanceUtil] {actionName} 执行期发生异常: {e.Message}\n{e.StackTrace}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LogKit.Log($"[PerformanceUtil] {actionName} 耗时: {stopwatch.Elapsed.TotalMilliseconds:F4} ms");
            }
        }

        /// <summary>
        /// 打印当前内存快照
        /// </summary>
        public static void LogMemoryUsage()
        {
            // Profiler API 调用存在底层开销，建议仅在排查内存瓶颈时调用
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

            // 触发完整的托管堆 GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // 触发非托管资源 (Unity Assets) 的卸载
            // 注意：UnloadUnusedAssets 是异步操作，实际内存在下一帧或更晚才会真正回落
            Resources.UnloadUnusedAssets();

            LogKit.Log("[PerformanceUtil] 强制 GC 与资源卸载指令已发出");
        }

        /// <summary>
        /// 字节转兆字节辅助计算
        /// </summary>
        private static float ToMB(long bytes)
        {
            return bytes / 1024f / 1024f;
        }
    }
}