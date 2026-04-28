using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 配置工具包门面 (Facade / Registry)
    /// 职责: 统一调度加载流程，维护所有 NormalConfig 和 NetConfig 实例的生命周期
    /// </summary>
    public static class ConfigKit
    {
        private static readonly Dictionary<string, NormalConfig> _normalConfigs =
            new Dictionary<string, NormalConfig>();

        private static readonly Dictionary<string, NetConfig> _netConfigs =
            new Dictionary<string, NetConfig>();

        private static readonly Dictionary<string, UniTaskCompletionSource<NormalConfig>> _normalLoadingTasks =
            new Dictionary<string, UniTaskCompletionSource<NormalConfig>>();

        private static readonly Dictionary<string, UniTaskCompletionSource<NetConfig>> _netLoadingTasks =
            new Dictionary<string, UniTaskCompletionSource<NetConfig>>();

        private static readonly Dictionary<string, string> _normalConfigPaths =
            new Dictionary<string, string>();

        private static readonly Dictionary<string, string> _netConfigPaths =
            new Dictionary<string, string>();

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
            yield return UniTask.ToCoroutine(async () =>
            {
                NormalConfig config = await LoadNormalConfigAsync(configName, relativePath);
                onComplete?.Invoke(config);
            });
        }

        public static async UniTask<NormalConfig> LoadNormalConfigAsync(string configName, string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(configName))
            {
                LogKit.LogError("[ConfigKit] 加载失败: configName 不能为空");
                return null;
            }

            if (!TryNormalizeAndValidatePath(relativePath, configName, "NormalConfig", out string normalizedPath))
            {
                return null;
            }

            if (!EnsureConfigNameMatchesPath(_normalConfigPaths, configName, normalizedPath, "NormalConfig"))
            {
                return null;
            }

            if (_normalConfigs.TryGetValue(configName, out NormalConfig existingConfig))
            {
                return existingConfig;
            }

            if (_normalLoadingTasks.TryGetValue(configName, out UniTaskCompletionSource<NormalConfig> existingTask))
            {
                return await existingTask.Task.AttachExternalCancellation(cancellationToken);
            }

            UniTaskCompletionSource<NormalConfig> loadingSource = new UniTaskCompletionSource<NormalConfig>();
            _normalLoadingTasks[configName] = loadingSource;
            RunNormalLoadTask(configName, normalizedPath, loadingSource).Forget();

            return await loadingSource.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// 获取已加载的普通配置实例
        /// </summary>
        public static NormalConfig GetNormalConfig(string configName)
        {
            if (_normalConfigs.TryGetValue(configName, out NormalConfig config))
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
            yield return UniTask.ToCoroutine(async () =>
            {
                NetConfig config = await LoadNetConfigAsync(configName, relativePath);
                onComplete?.Invoke(config);
            });
        }

        public static async UniTask<NetConfig> LoadNetConfigAsync(string configName, string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(configName))
            {
                LogKit.LogError("[ConfigKit] 加载失败: configName 不能为空");
                return null;
            }

            if (!TryNormalizeAndValidatePath(relativePath, configName, "NetConfig", out string normalizedPath))
            {
                return null;
            }

            if (!EnsureConfigNameMatchesPath(_netConfigPaths, configName, normalizedPath, "NetConfig"))
            {
                return null;
            }

            if (_netConfigs.TryGetValue(configName, out NetConfig existingConfig))
            {
                return existingConfig;
            }

            if (_netLoadingTasks.TryGetValue(configName, out UniTaskCompletionSource<NetConfig> existingTask))
            {
                return await existingTask.Task.AttachExternalCancellation(cancellationToken);
            }

            UniTaskCompletionSource<NetConfig> loadingSource = new UniTaskCompletionSource<NetConfig>();
            _netLoadingTasks[configName] = loadingSource;
            RunNetLoadTask(configName, normalizedPath, loadingSource).Forget();

            return await loadingSource.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// 获取已加载的网络配置实例
        /// </summary>
        public static NetConfig GetNetConfig(string configName)
        {
            if (_netConfigs.TryGetValue(configName, out NetConfig config))
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
            _normalLoadingTasks.Clear();
            _netLoadingTasks.Clear();
            _normalConfigPaths.Clear();
            _netConfigPaths.Clear();
        }

        private static async UniTask<NormalConfig> BuildNormalConfigAsync(string configName, string relativePath)
        {
            ConfigCore.ConfigLoadResult result = await ConfigCore.LoadConfigAsync(relativePath);
            if (result.Data == null)
            {
                LogKit.LogError($"[ConfigKit] NormalConfig '{configName}' 加载失败，路径: {relativePath}");
                return null;
            }

            var config = new NormalConfig(configName, relativePath, result.Data, result.IsUserSave);
            _normalConfigs[configName] = config;
            _normalConfigPaths[configName] = relativePath;
            return config;
        }

        private static async UniTask<NetConfig> BuildNetConfigAsync(string configName, string relativePath)
        {
            ConfigCore.ConfigLoadResult result = await ConfigCore.LoadConfigAsync(relativePath);
            if (result.Data == null)
            {
                LogKit.LogError($"[ConfigKit] NetConfig '{configName}' 加载失败，路径: {relativePath}");
                return null;
            }

            var config = new NetConfig(configName, result.Data);
            _netConfigs[configName] = config;
            _netConfigPaths[configName] = relativePath;
            return config;
        }

        private static async UniTaskVoid RunNormalLoadTask(string configName, string relativePath,
            UniTaskCompletionSource<NormalConfig> loadingSource)
        {
            try
            {
                NormalConfig config = await BuildNormalConfigAsync(configName, relativePath);
                loadingSource.TrySetResult(config);
            }
            catch (OperationCanceledException)
            {
                loadingSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                loadingSource.TrySetException(ex);
                LogKit.LogError($"[ConfigKit] NormalConfig 跟踪任务异常: {configName}\n{ex.Message}");
            }
            finally
            {
                if (_normalLoadingTasks.TryGetValue(configName, out UniTaskCompletionSource<NormalConfig> current) &&
                    ReferenceEquals(current, loadingSource))
                {
                    _normalLoadingTasks.Remove(configName);
                }
            }
        }

        private static async UniTaskVoid RunNetLoadTask(string configName, string relativePath,
            UniTaskCompletionSource<NetConfig> loadingSource)
        {
            try
            {
                NetConfig config = await BuildNetConfigAsync(configName, relativePath);
                loadingSource.TrySetResult(config);
            }
            catch (OperationCanceledException)
            {
                loadingSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                loadingSource.TrySetException(ex);
                LogKit.LogError($"[ConfigKit] NetConfig 跟踪任务异常: {configName}\n{ex.Message}");
            }
            finally
            {
                if (_netLoadingTasks.TryGetValue(configName, out UniTaskCompletionSource<NetConfig> current) &&
                    ReferenceEquals(current, loadingSource))
                {
                    _netLoadingTasks.Remove(configName);
                }
            }
        }

        private static bool TryNormalizeAndValidatePath(string relativePath, string configName, string configKind,
            out string normalizedPath)
        {
            normalizedPath = NormalizeRelativePath(relativePath);
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                return true;
            }

            LogKit.LogError(
                $"[ConfigKit] 加载失败: relativePath 不能为空, ConfigType={configKind}, ConfigName={configName}");
            return false;
        }

        private static bool EnsureConfigNameMatchesPath(Dictionary<string, string> pathMap, string configName,
            string relativePath, string configKind)
        {
            if (!pathMap.TryGetValue(configName, out string existingPath))
            {
                return true;
            }

            if (string.Equals(existingPath, relativePath, StringComparison.Ordinal))
            {
                return true;
            }

            LogKit.LogError(
                $"[ConfigKit] 加载失败: 同名配置映射到不同路径, ConfigType={configKind}, ConfigName={configName}, ExistingPath={existingPath}, RequestedPath={relativePath}");
            return false;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            return relativePath.Replace("\\", "/").TrimStart('/');
        }
    }
}
