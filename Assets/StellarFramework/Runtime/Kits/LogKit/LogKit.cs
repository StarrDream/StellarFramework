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
            if (logger == null)
            {
                return;
            }

            _logger = logger;
        }

        [Conditional("ENABLE_LOG")]
        public static void Log(object msg)
        {
            _logger.Log(msg?.ToString());
        }

        [Conditional("ENABLE_LOG")]
        public static void Log(object script, object msg)
        {
            if (script == null)
            {
                _logger.Log($"[NullScript] {msg}");
                return;
            }

            _logger.Log($"[{script.GetType().Name}] {msg}");
        }

        [Conditional("ENABLE_LOG")]
        public static void LogWarning(object msg)
        {
            _logger.LogWarning(msg?.ToString());
        }

        [Conditional("ENABLE_LOG")]
        public static void LogWarning(object script, object msg)
        {
            if (script == null)
            {
                _logger.LogWarning($"[NullScript] {msg}");
                return;
            }

            _logger.LogWarning($"[{script.GetType().Name}] {msg}");
        }

        /// <summary>
        /// 错误日志输出
        /// 规范：调用此方法后，业务线必须紧跟 return 阻断逻辑，防止脏数据扩散。
        /// </summary>
        [Conditional("ENABLE_LOG")]
        public static void LogError(object msg)
        {
            _logger.LogError(msg?.ToString());
        }

        [Conditional("ENABLE_LOG")]
        public static void LogError(object script, object msg)
        {
            if (script == null)
            {
                _logger.LogError($"[NullScript] {msg}");
                return;
            }

            _logger.LogError($"[{script.GetType().Name}] {msg}");
        }

        [Conditional("ENABLE_LOG")]
        public static void LogException(Exception e)
        {
            _logger.LogException(e);
        }

        #region 断言层 (Assertions)

        /// <summary>
        /// 状态断言
        /// 注意：这是开发期诊断工具，不应被当作 Release 环境下的真实阻断机制。
        /// 运行时真正的安全阻断，请显式使用 if + LogError + return。
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Assert(bool condition, string errorMsg)
        {
            if (condition)
            {
                return;
            }

            _logger.LogError($"[Assert Failed] {errorMsg}");
#if UNITY_EDITOR
            throw new InvalidOperationException($"[Assert Failed] {errorMsg}");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void AssertNotNull(object obj, string errorMsg)
        {
            Assert(obj != null, errorMsg);
        }

        #endregion

        #region 轻量辅助工具

        /// <summary>
        /// 我提供一个显式返回 bool 的校验辅助，避免业务误把 Assert 当成真正阻断。
        /// </summary>
        public static bool AssertAndLog(bool condition, string errorMsg)
        {
            if (condition)
            {
                return true;
            }

            LogError(errorMsg);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert(false, errorMsg);
#endif
            return false;
        }

        /// <summary>
        /// 我提供统一的错误后 false 返回助手，减少重复样板代码。
        /// </summary>
        public static bool ErrorAndReturnFalse(string errorMsg)
        {
            LogError(errorMsg);
            return false;
        }

        #endregion
    }
}