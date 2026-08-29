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
        public readonly struct ConfigLoadResult
        {
            public readonly JObject Data;
            public readonly bool IsUserSave;

            public ConfigLoadResult(JObject data, bool isUserSave)
            {
                Data = data;
                IsUserSave = isUserSave;
            }
        }

        public static IEnumerator LoadConfigProcess(string relativePath, Action<JObject, bool> onComplete)
        {
            yield return UniTask.ToCoroutine(async () =>
            {
                ConfigLoadResult result = await LoadConfigAsync(relativePath);
                onComplete?.Invoke(result.Data, result.IsUserSave);
            });
        }

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

        public static string GetStreamingAssetsUrl(string relativePath)
        {
            return ConfigPathUtility.GetStreamingAssetsUrl(relativePath);
        }

        public static string GetPersistentPath(string relativePath)
        {
            return ConfigPathUtility.GetPersistentPath(relativePath);
        }
    }
}
