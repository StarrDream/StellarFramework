using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace StellarFramework
{
    public static class HttpImageDownload
    {
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        // 新增 Sprite 缓存字典，彻底解决每次请求都 new Sprite 导致的内存泄漏
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        private static readonly Dictionary<string, UniTask<Texture2D>> _ongoingTasks =
            new Dictionary<string, UniTask<Texture2D>>();

        public static async UniTask<Texture2D> DownloadTextureAsync(string url,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            if (_textureCache.TryGetValue(url, out var cachedTex))
            {
                if (cachedTex != null) return cachedTex;

                // 缓存失效，清理脏数据
                _textureCache.Remove(url);
                _spriteCache.Remove(url);
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
                        LogKit.LogError($"[HttpImageDownload] 请求图片失败 | URL: {url} | Error: {request.error}");
                        return null;
                    }

                    var texture = DownloadHandlerTexture.GetContent(request);
                    if (texture != null)
                    {
                        texture.name = url;
                    }

                    return texture;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不输出 Error
                    return null;
                }
                catch (Exception e)
                {
                    LogKit.LogError($"[HttpImageDownload] 下载图片异常 | URL: {url} | 异常: {e.Message}");
                    return null;
                }
            }
        }

        public static async UniTaskVoid DownloadToRawImageAsync(string url, RawImage rawImage)
        {
            if (rawImage == null || string.IsNullOrEmpty(url))
            {
                return;
            }

            var texture = await DownloadTextureAsync(url, rawImage.GetCancellationTokenOnDestroy());

            // 异步返回后再次进行判空检查，防止组件在等待期间被销毁
            if (rawImage != null && texture != null)
            {
                rawImage.texture = texture;
                rawImage.color = Color.white;
            }
        }

        public static async UniTaskVoid DownloadToImageAsync(string url, Image image)
        {
            if (image == null || string.IsNullOrEmpty(url))
            {
                return;
            }

            var texture = await DownloadTextureAsync(url, image.GetCancellationTokenOnDestroy());

            if (image == null || texture == null)
            {
                return;
            }

            // 从缓存获取或创建新的 Sprite，避免重复创建导致 OOM
            if (!_spriteCache.TryGetValue(url, out var sprite) || sprite == null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = url;
                _spriteCache[url] = sprite;
            }

            image.sprite = sprite;
            image.color = Color.white;
        }

        public static void ClearCache()
        {
            // 必须显式销毁 Sprite 和 Texture2D，彻底释放显存
            foreach (var sprite in _spriteCache.Values)
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            }

            _spriteCache.Clear();

            foreach (var tex in _textureCache.Values)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }

            _textureCache.Clear();

            _ongoingTasks.Clear();
            LogKit.Log("[HttpImageDownload] 图片与Sprite缓存已清理，显存已释放");
        }
    }
}