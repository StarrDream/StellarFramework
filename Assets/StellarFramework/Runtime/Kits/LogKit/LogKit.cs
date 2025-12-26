using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace StellarFramework
{
    /// <summary>
    /// 高性能日志工具
    /// 特性：
    /// 1. 使用 [Conditional] 特性，Release 包自动剔除调用，零开销。
    /// 2. 统一前缀，方便过滤。
    /// 
    /// 使用方法：
    /// 在 Player Settings -> Scripting Define Symbols 中添加 "ENABLE_LOG" 开启日志。
    /// </summary>
    public static class LogKit
    {
        [Conditional("ENABLE_LOG")]
        public static void Log(object msg)
        {
            //  直接调用，避免 $"..." 产生的 GC
            Debug.Log(msg);
        }

        [Conditional("ENABLE_LOG")]
        public static void LogFormat(string format, params object[] args)
        {
            //  使用原生 Format，性能优于 C# 字符串插值
            Debug.LogFormat(format, args);
        }

        [Conditional("ENABLE_LOG")]
        public static void LogWarning(object msg)
        {
            Debug.LogWarning(msg);
        }

        [Conditional("ENABLE_LOG")]
        public static void LogError(object msg)
        {
            Debug.LogError(msg);
        }

        [Conditional("ENABLE_LOG")]
        public static void LogException(System.Exception e)
        {
            Debug.LogException(e);
        }
    }
}