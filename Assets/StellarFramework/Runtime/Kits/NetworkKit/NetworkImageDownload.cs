// ========== NetworkImageDownload.cs ==========
// Path: Assets/StellarFramework/Runtime/Kits/NetworkKit/NetworkImageDownload.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace StellarFramework
{
    public static class NetworkImageDownload
    {
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, UniTask<Texture2D>> _ongoingTasks = new Dictionary<string, UniTask<Texture2D>>();

        public static async UniTask<Texture2D> DownloadTextureAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url)) return null;

            if (_textureCache.TryGetValue(url, out var cachedTex))
            {
                if (cachedTex != null) return cachedTex;
                _textureCache.Remove(url);
            }

            if (_ongoingTasks.TryGetValue(url, out var existingTask))
            {
                return await existingTask;
            }

            var task = DownloadInternalAsync(url, cancellationToken);
            _ongoingTasks[url] = task;

            try
            {
                var texture = await task;
                if (texture != null)
                {
                    _textureCache[url] = texture;
                }

                return texture;
            }
            finally
            {
                _ongoingTasks.Remove(url);
            }
        }

        private static async UniTask<Texture2D> DownloadInternalAsync(string url, CancellationToken cancellationToken)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                try
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        LogKit.LogError($"[NetworkUtil] 请求失败: {url}\nError: {request.error}");
                        return null;
                    }

                    var texture = DownloadHandlerTexture.GetContent(request);
                    if (texture != null) texture.name = url;
                    return texture;
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception e)
                {
                    LogKit.LogError($"[NetworkUtil] 下载异常: {e.Message}");
                    return null;
                }
            }
        }

        public static async UniTaskVoid DownloadToRawImageAsync(string url, RawImage rawImage)
        {
            if (rawImage == null) return;
            var texture = await DownloadTextureAsync(url, rawImage.GetCancellationTokenOnDestroy());
            if (rawImage != null && texture != null)
            {
                rawImage.texture = texture;
                rawImage.color = Color.white;
            }
        }

        public static async UniTaskVoid DownloadToImageAsync(string url, Image image)
        {
            if (image == null) return;
            var texture = await DownloadTextureAsync(url, image.GetCancellationTokenOnDestroy());
            if (image != null && texture != null)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                image.sprite = sprite;
                image.color = Color.white;
            }
        }

        public static void ClearCache()
        {
            //  必须显式销毁 Texture2D，否则会造成显存泄漏
            foreach (var tex in _textureCache.Values)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }

            _textureCache.Clear();
            _ongoingTasks.Clear();
            LogKit.Log("[NetworkUtil] 图片缓存已清理 (显存已释放)");
        }
    }
}