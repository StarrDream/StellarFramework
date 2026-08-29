using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework
{
    /// <summary>
    /// 纯文本配置读取结果。ConfigKit.Core 不预设 JSON、XML 或二进制序列化方案。
    /// </summary>
    public readonly struct ConfigTextLoadResult
    {
        public readonly string Text;
        public readonly bool IsUserSave;
        public readonly string Error;

        public bool IsSuccess => !string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Error);

        public ConfigTextLoadResult(string text, bool isUserSave, string error = null)
        {
            Text = text;
            IsUserSave = isUserSave;
            Error = error;
        }
    }

    /// <summary>
    /// 配置文本来源。项目可接入 ResKit、Addressables、远端服务或自定义加密存储。
    /// </summary>
    public interface IConfigTextSource
    {
        UniTask<ConfigTextLoadResult> LoadAsync(string relativePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ConfigKit.Core 的可配置文本来源注册表。
    /// </summary>
    public static class ConfigTextSources
    {
        private static IConfigTextSource _default = new StreamingAssetsConfigTextSource();

        public static IConfigTextSource Default => _default;

        public static void ConfigureDefault(IConfigTextSource source)
        {
            _default = source ?? new StreamingAssetsConfigTextSource();
        }
    }

    /// <summary>
    /// 默认文本来源：PersistentDataPath 覆盖 StreamingAssets。
    /// </summary>
    public sealed class StreamingAssetsConfigTextSource : IConfigTextSource
    {
        public async UniTask<ConfigTextLoadResult> LoadAsync(string relativePath,
            CancellationToken cancellationToken = default)
        {
            string normalizedPath = ConfigPathUtility.NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return new ConfigTextLoadResult(null, false, "relativePath is empty.");
            }

            string persistentPath = ConfigPathUtility.GetPersistentPath(normalizedPath);
            bool isUserSave = File.Exists(persistentPath);
            string loadUrl = isUserSave
                ? "file://" + persistentPath.Replace("\\", "/")
                : ConfigPathUtility.GetStreamingAssetsUrl(normalizedPath);

            using UnityWebRequest request = UnityWebRequest.Get(loadUrl);
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new ConfigTextLoadResult(null, isUserSave, "Loading was cancelled.");
            }
            catch (UnityWebRequestException exception)
            {
                return new ConfigTextLoadResult(null, isUserSave,
                    exception.UnityWebRequest?.error ?? exception.Message);
            }
            catch (Exception exception)
            {
                return new ConfigTextLoadResult(null, isUserSave, exception.Message);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return new ConfigTextLoadResult(null, isUserSave, request.error);
            }

            string text = request.downloadHandler.text;
            if (!string.IsNullOrEmpty(text) && text[0] == '\ufeff')
            {
                text = text.Substring(1);
            }

            return new ConfigTextLoadResult(text, isUserSave);
        }
    }

    public static class ConfigPathUtility
    {
        public static string NormalizeRelativePath(string relativePath)
        {
            return string.IsNullOrWhiteSpace(relativePath)
                ? string.Empty
                : relativePath.Replace("\\", "/").TrimStart('/');
        }

        public static string GetStreamingAssetsUrl(string relativePath)
        {
            string normalizedPath = NormalizeRelativePath(relativePath);
#if UNITY_EDITOR
            return "file://" + Path.Combine(Application.streamingAssetsPath, normalizedPath).Replace("\\", "/");
#elif UNITY_ANDROID || UNITY_WEBGL || UNITY_OPENHARMONY
            return Application.streamingAssetsPath + "/" + normalizedPath;
#else
            return "file://" + Path.Combine(Application.streamingAssetsPath, normalizedPath).Replace("\\", "/");
#endif
        }

        public static string GetPersistentPath(string relativePath)
        {
            return Path.Combine(Application.persistentDataPath, NormalizeRelativePath(relativePath));
        }
    }
}
