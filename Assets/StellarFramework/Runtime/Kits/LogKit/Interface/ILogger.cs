using System;

namespace StellarFramework
{
    /// <summary>
    /// 日志处理器接口
    /// 允许外部项目在仅引用单一模块时，通过注入此接口对接自有日志系统。
    /// </summary>
    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogException(Exception e);
    }
}