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
        /// <summary>读取到的原始文本；失败时通常为 null。</summary>
        public readonly string Text;
        /// <summary>是否来自 PersistentDataPath 用户覆盖文件。</summary>
        public readonly bool IsUserSave;
        /// <summary>失败原因；成功时为 null/empty。</summary>
        public readonly string Error;

        /// <summary>
        /// 文本非空且没有错误时视为成功。
        /// </summary>
        public bool IsSuccess => !string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Error);

        /// <summary>
        /// 创建读取结果。
        /// </summary>
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
        /// <summary>
        /// 异步读取相对路径对应的原始配置文本。
        /// </summary>
        /// <param name="relativePath">相对于数据源根目录的路径。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        UniTask<ConfigTextLoadResult> LoadAsync(string relativePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ConfigKit.Core 的可配置文本来源注册表。
    /// </summary>
    public static class ConfigTextSources
    {
        private static IConfigTextSource _default = new StreamingAssetsConfigTextSource();

        /// <summary>
        /// 当前默认文本来源。初始为 <see cref="StreamingAssetsConfigTextSource"/>。
        /// </summary>
        public static IConfigTextSource Default => _default;

        /// <summary>
        /// 替换默认文本来源；传入 null 时恢复 StreamingAssets 默认实现。
        /// </summary>
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
        /// <summary>
        /// 优先从 PersistentDataPath 读取用户覆盖；不存在时回退 StreamingAssets。
        /// </summary>
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

    /// <summary>
    /// ConfigKit 的跨平台路径规范化辅助。
    /// </summary>
    public static class ConfigPathUtility
    {
        /// <summary>
        /// 将反斜杠转换为 '/' 并移除开头的 '/'，确保路径保持相对形式。
        /// </summary>
        public static string NormalizeRelativePath(string relativePath)
        {
            return string.IsNullOrWhiteSpace(relativePath)
                ? string.Empty
                : relativePath.Replace("\\", "/").TrimStart('/');
        }

        /// <summary>
        /// 生成当前平台可被 UnityWebRequest 读取的 StreamingAssets URL。
        /// </summary>
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

        /// <summary>
        /// 生成 PersistentDataPath 下的绝对路径。
        /// </summary>
        public static string GetPersistentPath(string relativePath)
        {
            return Path.Combine(Application.persistentDataPath, NormalizeRelativePath(relativePath));
        }
    }
}
