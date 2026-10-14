using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if STELLARFRAMEWORK_UGUI
using UnityEngine.UI;
#endif

namespace StellarFramework
{
    /// <summary>
    /// HTTP 图片下载与小型内存缓存工具。
    /// </summary>
    /// <remarks>
    /// 同一作用域、URL 和有效请求头的并发下载会合并；每个等待者可以独立取消自己的等待。
    /// Texture/Sprite 各维护最多 128 项的近似 LRU 缓存。
    /// 缓存对象由本类创建和销毁，调用方不要主动 Destroy 缓存返回的资源。
    /// </remarks>
    public static class HttpImageDownload
    {
        private const int MaxTextureCacheCount = 128;
        private const int MaxSpriteCacheCount = 128;

        private sealed class CacheEntry<T> where T : UnityEngine.Object
        {
            public string Url;
            public T Asset;
            public long LastAccessTick;
        }

        private sealed class OngoingDownload
        {
            public CancellationTokenSource SharedCts = new CancellationTokenSource();
            public UniTaskCompletionSource<Texture2D> CompletionSource = new UniTaskCompletionSource<Texture2D>();
            public int WaiterCount;
            public bool IsCompleted;
        }

        private static readonly Dictionary<string, CacheEntry<Texture2D>> TextureCache =
            new Dictionary<string, CacheEntry<Texture2D>>(128);

        private static readonly Dictionary<string, CacheEntry<Sprite>> SpriteCache =
            new Dictionary<string, CacheEntry<Sprite>>(128);

        private static readonly Dictionary<string, OngoingDownload> OngoingTasks =
            new Dictionary<string, OngoingDownload>(64);

        private static long _accessTick;

        /// <summary>
        /// 下载 Texture2D；缓存命中时直接返回缓存对象。
        /// </summary>
        public static UniTask<Texture2D> DownloadTextureAsync(string imageUrl,
            CancellationToken cancellationToken = default)
        {
            return DownloadTextureAsync(imageUrl, cancellationToken, null);
        }

        public static async UniTask<Texture2D> DownloadTextureAsync(string imageUrl,
            CancellationToken cancellationToken, RequestConfig config)
        {
            // Public images keep their original unauthenticated behavior. Supplying a config
            // explicitly opts into its authentication policy; scope isolation always applies.
            RequestConfig snapshot = HttpKit.SnapshotRequestConfig(
                config ?? new RequestConfig { autoInjectToken = false }, false);
            if (snapshot == null) return null;
            return await DownloadTextureWithSnapshotAsync(imageUrl, cancellationToken, snapshot);
        }

        private static async UniTask<Texture2D> DownloadTextureWithSnapshotAsync(string imageUrl,
            CancellationToken cancellationToken, RequestConfig snapshot)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                LogKit.LogError("[HttpImageDownload] 下载失败: imageUrl 为空");
                return null;
            }

            string cacheKey = HttpKit.GenerateRequestKey("GET", imageUrl, null, snapshot);
            if (TryGetCachedTexture(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            OngoingDownload ongoing;

            if (OngoingTasks.TryGetValue(cacheKey, out ongoing) && !ongoing.SharedCts.IsCancellationRequested)
            {
                ongoing.WaiterCount++;
            }
            else
            {
                ongoing = new OngoingDownload
                {
                    WaiterCount = 1
                };
                OngoingTasks[cacheKey] = ongoing;
                RunOngoingDownloadAsync(imageUrl, cacheKey, snapshot, ongoing).Forget();
            }

            try
            {
                return await ongoing.CompletionSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                ongoing.WaiterCount = Math.Max(0, ongoing.WaiterCount - 1);
                if (ongoing.WaiterCount == 0 && !ongoing.IsCompleted && !ongoing.SharedCts.IsCancellationRequested)
                {
                    ongoing.SharedCts.Cancel();
                }
            }
        }

        /// <summary>
        /// 下载图片并创建中心 Pivot 的 Sprite；结果会进入 Sprite 缓存。
        /// </summary>
        public static UniTask<Sprite> DownloadSpriteAsync(string imageUrl,
            CancellationToken cancellationToken = default)
        {
            return DownloadSpriteAsync(imageUrl, cancellationToken, null);
        }

        public static async UniTask<Sprite> DownloadSpriteAsync(string imageUrl,
            CancellationToken cancellationToken, RequestConfig config)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                LogKit.LogError("[HttpImageDownload] 下载失败: imageUrl 为空");
                return null;
            }

            RequestConfig snapshot = HttpKit.SnapshotRequestConfig(
                config ?? new RequestConfig { autoInjectToken = false }, false);
            if (snapshot == null) return null;
            string cacheKey = HttpKit.GenerateRequestKey("GET", imageUrl, null, snapshot);
            if (TryGetCachedSprite(cacheKey, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Texture2D texture = await DownloadTextureWithSnapshotAsync(imageUrl, cancellationToken, snapshot);
            if (texture == null)
            {
                return null;
            }

            // Another waiter may have completed while this one awaited the shared texture.
            if (TryGetCachedSprite(cacheKey, out cachedSprite)) return cachedSprite;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            AddSpriteCache(cacheKey, sprite);
            return sprite;
        }

#if STELLARFRAMEWORK_UGUI
        /// <summary>
        /// 下载图片并赋给 Image。目标销毁时会自动取消等待。
        /// </summary>
        /// <returns>成功设置 Sprite 时返回 true。</returns>
        public static UniTask<bool> DownloadToImageAsync(Image targetImage, string imageUrl,
            bool setNativeSize = false)
        {
            return DownloadToImageAsync(targetImage, imageUrl, setNativeSize, null);
        }

        public static async UniTask<bool> DownloadToImageAsync(Image targetImage, string imageUrl,
            bool setNativeSize, RequestConfig config)
        {
            if (targetImage == null)
            {
                LogKit.LogError($"[HttpImageDownload] 设置图片失败: targetImage 为空, Url={imageUrl}");
                return false;
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                LogKit.LogError(
                    $"[HttpImageDownload] 设置图片失败: imageUrl 为空, TriggerObject={targetImage.gameObject.name}");
                return false;
            }

            CancellationToken token = targetImage.GetCancellationTokenOnDestroy();
            Sprite sprite = await DownloadSpriteAsync(imageUrl, token, config);

            if (targetImage == null || sprite == null)
            {
                return false;
            }

            targetImage.sprite = sprite;
            if (setNativeSize)
            {
                targetImage.SetNativeSize();
            }

            return true;
        }

        /// <summary>
        /// 兼容旧参数顺序的 Image 重载。
        /// </summary>
        public static UniTask<bool> DownloadToImageAsync(string imageUrl, Image targetImage, bool setNativeSize = false)
        {
            return DownloadToImageAsync(targetImage, imageUrl, setNativeSize);
        }

        public static UniTask<bool> DownloadToImageAsync(string imageUrl, Image targetImage, bool setNativeSize,
            RequestConfig config)
        {
            return DownloadToImageAsync(targetImage, imageUrl, setNativeSize, config);
        }

        /// <summary>
        /// 下载图片并赋给 RawImage。目标销毁时会自动取消等待。
        /// </summary>
        public static UniTask<bool> DownloadToRawImageAsync(RawImage targetRawImage, string imageUrl)
        {
            return DownloadToRawImageAsync(targetRawImage, imageUrl, null);
        }

        public static async UniTask<bool> DownloadToRawImageAsync(RawImage targetRawImage, string imageUrl,
            RequestConfig config)
        {
            if (targetRawImage == null)
            {
                LogKit.LogError($"[HttpImageDownload] 设置 RawImage 失败: targetRawImage 为空, Url={imageUrl}");
                return false;
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                LogKit.LogError(
                    $"[HttpImageDownload] 设置 RawImage 失败: imageUrl 为空, TriggerObject={targetRawImage.gameObject.name}");
                return false;
            }

            CancellationToken token = targetRawImage.GetCancellationTokenOnDestroy();
            Texture2D texture = await DownloadTextureAsync(imageUrl, token, config);

            if (targetRawImage == null || texture == null)
            {
                return false;
            }

            targetRawImage.texture = texture;
            return true;
        }

        /// <summary>
        /// 兼容旧参数顺序的 RawImage 重载。
        /// </summary>
        public static UniTask<bool> DownloadToRawImageAsync(string imageUrl, RawImage targetRawImage)
        {
            return DownloadToRawImageAsync(targetRawImage, imageUrl);
        }

        public static UniTask<bool> DownloadToRawImageAsync(string imageUrl, RawImage targetRawImage,
            RequestConfig config)
        {
            return DownloadToRawImageAsync(targetRawImage, imageUrl, config);
        }
#endif

        /// <summary>
        /// 取消全部进行中的共享下载，并销毁全部 Sprite/Texture 缓存。
        /// </summary>
        public static void ClearCache()
        {
            OngoingDownload[] pending = OngoingTasks.Values.ToArray();
            OngoingTasks.Clear();

            foreach (KeyValuePair<string, CacheEntry<Sprite>> pair in SpriteCache)
            {
                if (pair.Value?.Asset != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.Asset);
                }
            }

            SpriteCache.Clear();

            foreach (KeyValuePair<string, CacheEntry<Texture2D>> pair in TextureCache)
            {
                if (pair.Value?.Asset != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.Asset);
                }
            }

            TextureCache.Clear();
            // Cancellation may run user continuations synchronously. Clear the old maps
            // first, so a new request started by a continuation remains tracked.
            foreach (OngoingDownload download in pending)
            {
                try { download.SharedCts.Cancel(); }
                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// 清除指定 URL 的进行中下载和缓存资源。
        /// </summary>
        public static void ClearCache(string imageUrl)
        {
            ClearCache(imageUrl, null);
        }

        public static void ClearCache(string imageUrl, RequestConfig config)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            RequestConfig snapshot = HttpKit.SnapshotRequestConfig(
                config ?? new RequestConfig { autoInjectToken = false }, false);
            if (snapshot == null) return;
            string cacheKey = HttpKit.GenerateRequestKey("GET", imageUrl, null, snapshot);
            if (OngoingTasks.TryGetValue(cacheKey, out OngoingDownload ongoing))
            {
                OngoingTasks.Remove(cacheKey);
                ongoing.SharedCts.Cancel();
            }

            if (SpriteCache.TryGetValue(cacheKey, out CacheEntry<Sprite> spriteEntry))
            {
                if (spriteEntry?.Asset != null)
                {
                    UnityEngine.Object.Destroy(spriteEntry.Asset);
                }

                SpriteCache.Remove(cacheKey);
            }

            if (TextureCache.TryGetValue(cacheKey, out CacheEntry<Texture2D> textureEntry))
            {
                if (textureEntry?.Asset != null)
                {
                    UnityEngine.Object.Destroy(textureEntry.Asset);
                }

                TextureCache.Remove(cacheKey);
            }
        }

        private static async UniTask<Texture2D> DownloadTextureInternalAsync(string imageUrl,
            string cacheKey, RequestConfig snapshot, CancellationToken cancellationToken)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
            foreach (KeyValuePair<string, string> header in snapshot.headers)
                request.SetRequestHeader(header.Key, header.Value);
            request.timeout = snapshot.timeout;

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogKit.LogError($"[HttpImageDownload] 下载图片失败: Url={imageUrl}, Error={request.error}");
                    return null;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    LogKit.LogError($"[HttpImageDownload] 下载图片失败: 纹理内容为空, Url={imageUrl}");
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();
                AddTextureCache(cacheKey, texture);
                return texture;
            }
            catch (OperationCanceledException)
            {
                LogKit.Log($"[HttpImageDownload] 下载已取消: Url={imageUrl}");
                return null;
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[HttpImageDownload] 下载异常: Url={imageUrl}, Exception={ex.Message}");
                return null;
            }
        }

        private static bool TryGetCachedTexture(string imageUrl, out Texture2D texture)
        {
            texture = null;

            if (!TextureCache.TryGetValue(imageUrl, out CacheEntry<Texture2D> entry))
            {
                return false;
            }

            if (entry == null || entry.Asset == null)
            {
                TextureCache.Remove(imageUrl);
                return false;
            }

            entry.LastAccessTick = ++_accessTick;
            texture = entry.Asset;
            return true;
        }

        private static bool TryGetCachedSprite(string imageUrl, out Sprite sprite)
        {
            sprite = null;

            if (!SpriteCache.TryGetValue(imageUrl, out CacheEntry<Sprite> entry))
            {
                return false;
            }

            if (entry == null || entry.Asset == null)
            {
                SpriteCache.Remove(imageUrl);
                return false;
            }

            entry.LastAccessTick = ++_accessTick;
            sprite = entry.Asset;
            return true;
        }

        private static void AddTextureCache(string imageUrl, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (TextureCache.TryGetValue(imageUrl, out CacheEntry<Texture2D> existed))
            {
                if (existed.Asset != null && existed.Asset != texture)
                {
                    UnityEngine.Object.Destroy(existed.Asset);
                }

                existed.Asset = texture;
                existed.LastAccessTick = ++_accessTick;
            }
            else
            {
                TextureCache.Add(imageUrl, new CacheEntry<Texture2D>
                {
                    Url = imageUrl,
                    Asset = texture,
                    LastAccessTick = ++_accessTick
                });
            }

            TrimTextureCacheIfNeeded();
        }

        private static void AddSpriteCache(string imageUrl, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (SpriteCache.TryGetValue(imageUrl, out CacheEntry<Sprite> existed))
            {
                if (existed.Asset != null && existed.Asset != sprite)
                {
                    UnityEngine.Object.Destroy(existed.Asset);
                }

                existed.Asset = sprite;
                existed.LastAccessTick = ++_accessTick;
            }
            else
            {
                SpriteCache.Add(imageUrl, new CacheEntry<Sprite>
                {
                    Url = imageUrl,
                    Asset = sprite,
                    LastAccessTick = ++_accessTick
                });
            }

            TrimSpriteCacheIfNeeded();
        }

        private static void TrimTextureCacheIfNeeded()
        {
            if (TextureCache.Count <= MaxTextureCacheCount)
            {
                return;
            }

            string lruKey = null;
            long minTick = long.MaxValue;

            foreach (KeyValuePair<string, CacheEntry<Texture2D>> pair in TextureCache)
            {
                if (pair.Value == null)
                {
                    lruKey = pair.Key;
                    break;
                }

                // 被 SpriteCache 引用的纹理不参与淘汰，
                // 否则 Sprite 会持有已销毁的纹理导致显示白屏（双缓存互相销毁）。
                if (IsTextureReferencedBySprite(pair.Value.Asset))
                {
                    continue;
                }

                if (pair.Value.LastAccessTick < minTick)
                {
                    minTick = pair.Value.LastAccessTick;
                    lruKey = pair.Key;
                }
            }

            if (string.IsNullOrEmpty(lruKey))
            {
                return;
            }

            if (TextureCache.TryGetValue(lruKey, out CacheEntry<Texture2D> entry))
            {
                if (entry?.Asset != null)
                {
                    UnityEngine.Object.Destroy(entry.Asset);
                }
            }

            TextureCache.Remove(lruKey);
        }

        /// <summary>
        /// 判断纹理是否仍被某个缓存的 Sprite 引用。
        /// 仅在需要淘汰纹理时调用，缓存规模上限（128）内遍历可接受。
        /// </summary>
        private static bool IsTextureReferencedBySprite(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, CacheEntry<Sprite>> pair in SpriteCache)
            {
                if (pair.Value?.Asset != null && pair.Value.Asset.texture == texture)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TrimSpriteCacheIfNeeded()
        {
            if (SpriteCache.Count <= MaxSpriteCacheCount)
            {
                return;
            }

            string lruKey = null;
            long minTick = long.MaxValue;

            foreach (KeyValuePair<string, CacheEntry<Sprite>> pair in SpriteCache)
            {
                if (pair.Value == null)
                {
                    lruKey = pair.Key;
                    break;
                }

                if (pair.Value.LastAccessTick < minTick)
                {
                    minTick = pair.Value.LastAccessTick;
                    lruKey = pair.Key;
                }
            }

            if (string.IsNullOrEmpty(lruKey))
            {
                return;
            }

            if (SpriteCache.TryGetValue(lruKey, out CacheEntry<Sprite> entry))
            {
                if (entry?.Asset != null)
                {
                    UnityEngine.Object.Destroy(entry.Asset);
                }
            }

            SpriteCache.Remove(lruKey);
        }

        private static async UniTaskVoid RunOngoingDownloadAsync(string imageUrl, string cacheKey,
            RequestConfig snapshot, OngoingDownload ongoing)
        {
            try
            {
                Texture2D texture = await DownloadTextureInternalAsync(imageUrl, cacheKey, snapshot, ongoing.SharedCts.Token);
                ongoing.CompletionSource.TrySetResult(texture);
            }
            catch (OperationCanceledException)
            {
                ongoing.CompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                ongoing.CompletionSource.TrySetException(ex);
                LogKit.LogError($"[HttpImageDownload] 跟踪下载任务异常: Url={imageUrl}, Exception={ex.Message}");
            }
            finally
            {
                ongoing.IsCompleted = true;

                if (OngoingTasks.TryGetValue(cacheKey, out OngoingDownload current) && ReferenceEquals(current, ongoing))
                {
                    OngoingTasks.Remove(cacheKey);
                }

                ongoing.SharedCts.Dispose();
            }
        }
    }
}
