using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using LogKit = StellarFramework.ConfigKitJsonDiagnostics;

namespace StellarFramework
{
    /// <summary>
    /// ConfigKit.Json 的 JObject 加载桥接层。
    /// 文本获取由 ConfigKit.Core 的 IConfigTextSource 负责。
    /// </summary>
    public static class ConfigCore
    {
        /// <summary>
        /// JSON 配置加载结果。
        /// </summary>
        public readonly struct ConfigLoadResult
        {
            /// <summary>解析后的 JObject；失败时为 null。</summary>
            public readonly JObject Data;
            /// <summary>原始文本是否来自用户覆盖文件。</summary>
            public readonly bool IsUserSave;

            public ConfigLoadResult(JObject data, bool isUserSave)
            {
                Data = data;
                IsUserSave = isUserSave;
            }
        }

        /// <summary>
        /// Coroutine 兼容入口，内部复用异步加载流程。
        /// </summary>
        public static IEnumerator LoadConfigProcess(string relativePath, Action<JObject, bool> onComplete)
        {
            yield return UniTask.ToCoroutine(async () =>
            {
                ConfigLoadResult result = await LoadConfigAsync(relativePath);
                onComplete?.Invoke(result.Data, result.IsUserSave);
            });
        }

        /// <summary>
        /// 从当前 IConfigTextSource 读取文本并解析为 JObject。
        /// 加载或 JSON 解析失败时返回 Data=null，并记录错误。
        /// </summary>
        public static async UniTask<ConfigLoadResult> LoadConfigAsync(string relativePath,
            CancellationToken cancellationToken = default)
        {
            ConfigTextLoadResult sourceResult = await ConfigTextSources.Default.LoadAsync(relativePath, cancellationToken);
            if (!sourceResult.IsSuccess)
            {
                LogKit.LogError($"[ConfigKit.Json] 配置文本加载失败: {relativePath}, Error={sourceResult.Error}");
                return new ConfigLoadResult(null, sourceResult.IsUserSave);
            }

            try
            {
                return new ConfigLoadResult(JObject.Parse(sourceResult.Text), sourceResult.IsUserSave);
            }
            catch (Exception exception)
            {
                LogKit.LogError(
                    $"[ConfigKit.Json] JSON 解析异常: {relativePath}\n{exception.Message}\n原始内容: {sourceResult.Text}");
                return new ConfigLoadResult(null, sourceResult.IsUserSave);
            }
        }

        /// <summary>返回跨平台 StreamingAssets URL。</summary>
        public static string GetStreamingAssetsUrl(string relativePath)
        {
            return ConfigPathUtility.GetStreamingAssetsUrl(relativePath);
        }

        /// <summary>返回 PersistentDataPath 下的绝对路径。</summary>
        public static string GetPersistentPath(string relativePath)
        {
            return ConfigPathUtility.GetPersistentPath(relativePath);
        }
    }
}
