using System;
using System.Diagnostics;

namespace StellarFramework
{
    /// <summary>
    /// SingletonKit 的最小诊断边界，使其可脱离 LogKit 单独导入。
    /// </summary>
    public static class SingletonKitDiagnostics
    {
        public static void Log(object message)
        {
            UnityEngine.Debug.Log(message);
        }

        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

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
