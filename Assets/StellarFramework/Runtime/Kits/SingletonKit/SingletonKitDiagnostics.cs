using System;
using System.Diagnostics;

namespace StellarFramework
{
    /// <summary>
    /// SingletonKit 的最小诊断边界，使其可脱离 LogKit 单独导入。
    /// </summary>
    public static class SingletonKitDiagnostics
    {
        /// <summary>
        /// 输出普通诊断日志。保持对 LogKit 的零依赖以支持 SingletonKit 独立导出。
        /// </summary>
        public static void Log(object message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <summary>
        /// 输出错误诊断日志。
        /// </summary>
        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

        /// <summary>
        /// Editor/Development Build 下执行 Fail-Fast 断言；Release 中该调用会被条件编译移除。
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Assert(bool condition, string errorMessage)
        {
            if (condition)
            {
                return;
            }

            UnityEngine.Debug.LogError($"[Assert Failed] {errorMessage}");
#if UNITY_EDITOR
            throw new InvalidOperationException($"[Assert Failed] {errorMessage}");
#endif
        }
    }
}
