using System;

namespace StellarFramework
{
    /// <summary>
    /// 日志处理器接口
    /// 允许外部项目在仅引用单一模块时，通过注入此接口对接自有日志系统。
    /// </summary>
    public interface ILogger
    {
        /// <summary>输出普通日志。</summary>
        void Log(string message);
        /// <summary>输出警告日志。</summary>
        void LogWarning(string message);
        /// <summary>输出错误日志。</summary>
        void LogError(string message);
        /// <summary>输出异常和堆栈。</summary>
        void LogException(Exception e);
    }
}