using System;
using System.Diagnostics;
using UnityEngine;

namespace StellarFramework.Pool
{
    /// <summary>
    /// PoolKit 的最小诊断边界。独立导入时不需要 LogKit。
    /// </summary>
    internal static class PoolKitDiagnostics
    {
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

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void AssertNotNull(object value, string errorMessage)
        {
            Assert(value != null, errorMessage);
        }
    }
}
