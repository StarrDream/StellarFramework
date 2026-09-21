using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace StellarFramework
{
    /// <summary>
    /// 框架统一日志门面。
    /// 默认输出到 Unity Console，也可通过 <see cref="SetLogger"/> 接入项目自己的日志系统。
    /// </summary>
    /// <remarks>
    /// 普通 Log/Warning 受 ENABLE_LOG 条件编译控制；LogError 不依赖 ENABLE_LOG，
    /// 以便 Release 中仍可保留关键错误路径。是否真正输出错误由 <see cref="LogErrorEnabled"/> 控制。
    /// </remarks>
    public static class LogKit
    {
        private static ILogger _logger = new UnityLogger();

        private static bool _logErrorEnabled = true;

        /// <summary>
        /// 错误日志总开关。
        /// LogError 不随 ENABLE_LOG 宏裁剪（框架内部大量用于"非致命错误返回路径"），
        /// 需要关闭时（如正式发布包）显式置为 false。
        /// </summary>
        public static bool LogErrorEnabled
        {
            get => _logErrorEnabled;
            set => _logErrorEnabled = value;
        }

        /// <summary>
        /// 注入自定义日志处理器。传入 null 时保持当前 Logger 不变。
        /// </summary>
        public static void SetLogger(ILogger logger)
        {
            if (logger == null)
            {
                return;
            }

            _logger = logger;
        }

        /// <summary>
        /// 输出普通日志。未定义 ENABLE_LOG 时该调用会被编译器移除。
        /// </summary>
        [Conditional("ENABLE_LOG")]
        public static void Log(object msg)
        {
            _logger.Log(msg?.ToString());
        }

        /// <summary>
        /// 输出带调用对象类型前缀的普通日志。
        /// </summary>
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

        /// <summary>
        /// 输出警告日志。未定义 ENABLE_LOG 时该调用会被编译器移除。
        /// </summary>
        [Conditional("ENABLE_LOG")]
        public static void LogWarning(object msg)
        {
            _logger.LogWarning(msg?.ToString());
        }

        /// <summary>
        /// 输出带调用对象类型前缀的警告日志。
        /// </summary>
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
        /// 输出错误日志。
        /// </summary>
        /// <remarks>
        /// 本方法只负责记录，不会自动抛异常或中断流程。
        /// 若错误意味着当前操作不可继续，调用方仍应显式 return/throw。
        /// </remarks>
        public static void LogError(object msg)
        {
            if (!_logErrorEnabled)
            {
                return;
            }

            _logger.LogError(msg?.ToString());
        }

        /// <summary>
        /// 输出带调用对象类型前缀的错误日志。
        /// </summary>
        public static void LogError(object script, object msg)
        {
            if (!_logErrorEnabled)
            {
                return;
            }

            if (script == null)
            {
                _logger.LogError($"[NullScript] {msg}");
                return;
            }

            _logger.LogError($"[{script.GetType().Name}] {msg}");
        }

        /// <summary>
        /// 记录异常及其堆栈。
        /// </summary>
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

        /// <summary>
        /// 断言对象不为 null。
        /// 仅 Editor / Development Build 生效，Release 中该调用会被条件编译移除。
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void AssertNotNull(object obj, string errorMsg)
        {
            Assert(obj != null, errorMsg);
        }

        #endregion

        #region 轻量辅助工具

        /// <summary>
        /// 执行运行时校验并返回结果。
        /// 失败时始终记录 LogError；开发构建额外触发 Assert。
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
        /// 记录错误并返回 false，用于简化 Try/Validate 风格的错误分支。
        /// </summary>
        public static bool ErrorAndReturnFalse(string errorMsg)
        {
            LogError(errorMsg);
            return false;
        }

        #endregion
    }
}
