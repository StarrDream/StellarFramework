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

        public static async UniTask<Texture2D> DownloadTextureAsync(string imageUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                LogKit.LogError("[HttpImageDownload] 下载失败: imageUrl 为空");
                return null;
            }

            if (TryGetCachedTexture(imageUrl, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            OngoingDownload ongoing;

            if (OngoingTasks.TryGetValue(imageUrl, out ongoing))
            {
                ongoing.WaiterCount++;
            }
            else
            {
                ongoing = new OngoingDownload
                {
                    WaiterCount = 1
                };
                OngoingTasks[imageUrl] = ongoing;
                RunOngoingDownloadAsync(imageUrl, ongoing).Forget();
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

        public static async UniTask<Sprite> DownloadSpriteAsync(string imageUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                LogKit.LogError("[HttpImageDownload] 下载失败: imageUrl 为空");
                return null;
            }

            if (TryGetCachedSprite(imageUrl, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Texture2D texture = await DownloadTextureAsync(imageUrl, cancellationToken);
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            AddSpriteCache(imageUrl, sprite);
            return sprite;
        }

        public static async UniTask<bool> DownloadToImageAsync(Image targetImage, string imageUrl,
            bool setNativeSize = false)
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
            Sprite sprite = await DownloadSpriteAsync(imageUrl, token);

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
        /// 兼容旧调用顺序
        /// </summary>
        public static UniTask<bool> DownloadToImageAsync(string imageUrl, Image targetImage, bool setNativeSize = false)
        {
            return DownloadToImageAsync(targetImage, imageUrl, setNativeSize);
        }

        public static async UniTask<bool> DownloadToRawImageAsync(RawImage targetRawImage, string imageUrl)
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
            Texture2D texture = await DownloadTextureAsync(imageUrl, token);

            if (targetRawImage == null || texture == null)
            {
                return false;
            }

            targetRawImage.texture = texture;
            return true;
        }

        /// <summary>
        /// 兼容旧调用顺序
        /// </summary>
        public static UniTask<bool> DownloadToRawImageAsync(string imageUrl, RawImage targetRawImage)
        {
            return DownloadToRawImageAsync(targetRawImage, imageUrl);
        }

        public static void ClearCache()
        {
            foreach (KeyValuePair<string, OngoingDownload> pair in OngoingTasks)
            {
                pair.Value?.SharedCts.Cancel();
            }

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
            OngoingTasks.Clear();
        }

        public static void ClearCache(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            if (OngoingTasks.TryGetValue(imageUrl, out OngoingDownload ongoing))
            {
                ongoing.SharedCts.Cancel();
                OngoingTasks.Remove(imageUrl);
            }

            if (SpriteCache.TryGetValue(imageUrl, out CacheEntry<Sprite> spriteEntry))
            {
                if (spriteEntry?.Asset != null)
                {
                    UnityEngine.Object.Destroy(spriteEntry.Asset);
                }

                SpriteCache.Remove(imageUrl);
            }

            if (TextureCache.TryGetValue(imageUrl, out CacheEntry<Texture2D> textureEntry))
            {
                if (textureEntry?.Asset != null)
                {
                    UnityEngine.Object.Destroy(textureEntry.Asset);
                }

                TextureCache.Remove(imageUrl);
            }
        }

        private static async UniTask<Texture2D> DownloadTextureInternalAsync(string imageUrl,
            CancellationToken cancellationToken)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);

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

                AddTextureCache(imageUrl, texture);
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

        private static async UniTaskVoid RunOngoingDownloadAsync(string imageUrl, OngoingDownload ongoing)
        {
            try
            {
                Texture2D texture = await DownloadTextureInternalAsync(imageUrl, ongoing.SharedCts.Token);
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

                if (OngoingTasks.TryGetValue(imageUrl, out OngoingDownload current) && ReferenceEquals(current, ongoing))
                {
                    OngoingTasks.Remove(imageUrl);
                }

                ongoing.SharedCts.Dispose();
            }
        }
    }
}
