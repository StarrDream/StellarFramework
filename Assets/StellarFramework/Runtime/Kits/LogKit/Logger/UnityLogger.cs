using System;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 默认的 Unity 原生日志实现
    /// </summary>
    public class UnityLogger : ILogger
    {
        /// <inheritdoc />
        public void Log(string message) => Debug.Log(message);
        /// <inheritdoc />
        public void LogWarning(string message) => Debug.LogWarning(message);
        /// <inheritdoc />
        public void LogError(string message) => Debug.LogError(message);
        /// <inheritdoc />
        public void LogException(Exception e) => Debug.LogException(e);
    }
}