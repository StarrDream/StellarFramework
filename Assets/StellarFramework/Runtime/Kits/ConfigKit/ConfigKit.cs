using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 配置工具包门面 (Facade / Registry)
    /// 职责: 统一调度加载流程，维护所有 NormalConfig 和 NetConfig 实例的生命周期
    /// </summary>
    public static class ConfigKit
    {
        private static readonly Dictionary<string, NormalConfig>
            _normalConfigs = new Dictionary<string, NormalConfig>();

        private static readonly Dictionary<string, NetConfig> _netConfigs = new Dictionary<string, NetConfig>();

        #region NormalConfig 管理

        /// <summary>
        /// 异步加载普通配置
        /// </summary>
        /// <param name="configName">配置标识名 (如 ShopConfig)</param>
        /// <param name="relativePath">相对路径 (如 Normal/ShopConfig.json)</param>
        /// <param name="onComplete">加载完成回调</param>
        public static IEnumerator LoadNormalConfig(string configName, string relativePath,
            Action<NormalConfig> onComplete = null)
        {
            if (string.IsNullOrEmpty(configName))
            {
                LogKit.LogError("[ConfigKit] 加载失败: configName 不能为空");
                onComplete?.Invoke(null);
                yield break;
            }

            if (_normalConfigs.TryGetValue(configName, out var existingConfig))
            {
                onComplete?.Invoke(existingConfig);
                yield break;
            }

            yield return ConfigCore.LoadConfigProcess(relativePath, (data, isUserSave) =>
            {
                if (data != null)
                {
                    var config = new NormalConfig(configName, relativePath, data, isUserSave);
                    _normalConfigs[configName] = config;
                    onComplete?.Invoke(config);
                }
                else
                {
                    LogKit.LogError($"[ConfigKit] NormalConfig '{configName}' 加载失败，路径: {relativePath}");
                    onComplete?.Invoke(null);
                }
            });
        }

        /// <summary>
        /// 获取已加载的普通配置实例
        /// </summary>
        public static NormalConfig GetNormalConfig(string configName)
        {
            if (_normalConfigs.TryGetValue(configName, out var config))
            {
                return config;
            }

            LogKit.LogError($"[ConfigKit] 未找到 NormalConfig: '{configName}'，请确认是否已加载");
            return null;
        }

        #endregion

        #region NetConfig 管理

        /// <summary>
        /// 异步加载网络配置
        /// </summary>
        /// <param name="configName">配置标识名 (如 LoginNetConfig)</param>
        /// <param name="relativePath">相对路径 (如 Net/LoginNetConfig.json)</param>
        /// <param name="onComplete">加载完成回调</param>
        public static IEnumerator LoadNetConfig(string configName, string relativePath,
            Action<NetConfig> onComplete = null)
        {
            if (string.IsNullOrEmpty(configName))
            {
                LogKit.LogError("[ConfigKit] 加载失败: configName 不能为空");
                onComplete?.Invoke(null);
                yield break;
            }

            if (_netConfigs.TryGetValue(configName, out var existingConfig))
            {
                onComplete?.Invoke(existingConfig);
                yield break;
            }

            yield return ConfigCore.LoadConfigProcess(relativePath, (data, _) =>
            {
                if (data != null)
                {
                    var config = new NetConfig(configName, data);
                    _netConfigs[configName] = config;
                    onComplete?.Invoke(config);
                }
                else
                {
                    LogKit.LogError($"[ConfigKit] NetConfig '{configName}' 加载失败，路径: {relativePath}");
                    onComplete?.Invoke(null);
                }
            });
        }

        /// <summary>
        /// 获取已加载的网络配置实例
        /// </summary>
        public static NetConfig GetNetConfig(string configName)
        {
            if (_netConfigs.TryGetValue(configName, out var config))
            {
                return config;
            }

            LogKit.LogError($"[ConfigKit] 未找到 NetConfig: '{configName}'，请确认是否已加载");
            return null;
        }

        #endregion

        /// <summary>
        /// 清理所有缓存的配置实例
        /// </summary>
        public static void ClearAll()
        {
            _normalConfigs.Clear();
            _netConfigs.Clear();
        }
    }
}