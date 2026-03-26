using System;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 默认的 Unity 原生日志实现
    /// </summary>
    public class UnityLogger : ILogger
    {
        public void Log(string message) => Debug.Log(message);
        public void LogWarning(string message) => Debug.LogWarning(message);
        public void LogError(string message) => Debug.LogError(message);
        public void LogException(Exception e) => Debug.LogException(e);
    }
}