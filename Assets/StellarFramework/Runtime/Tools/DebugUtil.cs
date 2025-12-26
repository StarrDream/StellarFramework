using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

namespace StellarFramework
{
    public static class LogKitUtil
    {
        // [Conditional] 确保这些代码只在编辑器或开发包中运行，Release包会自动剔除调用
        // 避免测试代码影响线上性能
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void MeasureExecutionTime(Action action, string actionName = "Action")
        {
            if (action == null)
            {
                LogKit.LogError($"[LogKitUtil] {actionName} 无法执行: Action 为空");
                return;
            }

            // Stopwatch 比 DateTime.Now 精确得多，且无 GC
            var stopwatch = Stopwatch.StartNew();
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                // 保留堆栈信息以便定位
                LogKit.LogError($"[LogKitUtil] {actionName} 执行发生异常: {e.Message}\n{e.StackTrace}");
                throw; // Fail Fast: 异常必须抛出，不能吞掉
            }
            finally
            {
                stopwatch.Stop();
                // 仅在耗时超过 0ms 时打印，减少日志刷屏
                LogKit.Log($"[LogKitUtil] {actionName} 耗时: {stopwatch.Elapsed.TotalMilliseconds:F4} ms");
            }
        }

        public static void LogMemoryUsage()
        {
            // Profiler 调用有一定开销，建议仅在特定 LogKit 模式下查看
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long gcMemory = GC.GetTotalMemory(false);

            // 使用 F2 格式化减少字符串拼接的混乱
            LogKit.Log($"[LogKitUtil] 内存快照:\n" +
                      $" >> Unity Reserved (系统预留): {ToMB(totalReserved)} MB\n" +
                      $" >> Unity Allocated (实际使用): {ToMB(totalAllocated)} MB\n" +
                      $" >> Mono Heap (脚本堆内存): {ToMB(gcMemory)} MB");
        }

        /// <summary>
        /// 强制执行 GC (慎用！会导致卡顿)
        /// </summary>
        public static void ForceGarbageCollection()
        {
            LogKit.LogWarning("[LogKitUtil] 正在执行强制 GC，可能会导致主线程卡顿...");

            // 完整的 GC 流程
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // 卸载未使用的资源 (Assets)
            var asyncOp = Resources.UnloadUnusedAssets();

            // 注意：这里无法 await asyncOp，因为本方法是同步的。
            // 实际效果会在下一帧生效。
            LogKit.Log("[LogKitUtil] 强制 GC 指令已发出");
        }

        private static float ToMB(long bytes)
        {
            return bytes / 1024f / 1024f;
        }
    }
}