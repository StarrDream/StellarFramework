using UnityEngine;

namespace StellarFramework.Settings
{
    /// <summary>
    /// SettingsKit 的内部日志边界，避免基础包强制依赖 LogKit。
    /// </summary>
    internal static class SettingsKitDiagnostics
    {
        public static void Log(string message) => Debug.Log(message);
        public static void LogWarning(string message) => Debug.LogWarning(message);
        public static void LogError(string message) => Debug.LogError(message);
    }
}
