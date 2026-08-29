using UnityEngine;

namespace StellarFramework
{
    /// <summary>JSON 适配器内部日志边界，不强制项目安装 LogKit。</summary>
    internal static class ConfigKitJsonDiagnostics
    {
        public static void Log(string message) => Debug.Log(message);
        public static void LogWarning(string message) => Debug.LogWarning(message);
        public static void LogError(string message) => Debug.LogError(message);
    }
}
