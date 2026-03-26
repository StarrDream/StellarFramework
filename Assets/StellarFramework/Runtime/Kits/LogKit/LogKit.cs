// ==================================================================================
// LogKit - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：统一 Runtime Core 的日志输出与断言入口。
// 改造说明：
// 1. 引入 Assert 断言层，用于在 Editor/Dev 环境下强制执行契约校验（Fail-Fast）。
// 2. 规范化日志级别，禁止业务层绕过 LogKit 直接调用 Debug.Log。
// ==================================================================================

using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace StellarFramework
{
    public static class LogKit
    {
        private static ILogger _logger = new UnityLogger();

        /// <summary>
        /// 注入自定义日志处理器
        /// </summary>
        public static void SetLogger(ILogger logger)
        {
            if (logger != null) _logger = logger;
        }

        [Conditional("ENABLE_LOG")]
        public static void Log(object msg) => _logger.Log(msg?.ToString());

        [Conditional("ENABLE_LOG")]
        public static void Log(object script, object msg)
            => _logger.Log($"[{script.GetType().Name}] {msg}");

        [Conditional("ENABLE_LOG")]
        public static void LogWarning(object msg) => _logger.LogWarning(msg?.ToString());

        [Conditional("ENABLE_LOG")]
        public static void LogWarning(object script, object msg)
            => _logger.LogWarning($"[{script.GetType().Name}] {msg}");

        /// <summary>
        /// 错误日志输出
        /// 规范：调用此方法后，业务线必须紧跟 return 阻断逻辑，防止脏数据扩散。
        /// </summary>
        [Conditional("ENABLE_LOG")]
        public static void LogError(object msg) => _logger.LogError(msg?.ToString());

        [Conditional("ENABLE_LOG")]
        public static void LogError(object script, object msg)
            => _logger.LogError($"[{script.GetType().Name}] {msg}");

        [Conditional("ENABLE_LOG")]
        public static void LogException(Exception e) => _logger.LogException(e);

        #region 断言层 (Assertions)

        /// <summary>
        /// 状态断言
        /// 行为：在 Editor 和 Dev Build 下，若条件不满足将抛出异常强制中断运行；Release 包下自动剔除。
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Assert(bool condition, string errorMsg)
        {
            if (!condition)
            {
                _logger.LogError($"[Assert Failed] {errorMsg}");
#if UNITY_EDITOR
                throw new InvalidOperationException($"[Assert Failed] {errorMsg}");
#endif
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void AssertNotNull(object obj, string errorMsg) => Assert(obj != null, errorMsg);

        #endregion
    }
}