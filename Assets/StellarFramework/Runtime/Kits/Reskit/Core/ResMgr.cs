using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Res
{
    internal static class ResMgr
    {
        private static readonly Dictionary<string, ResData> _sharedCache = new Dictionary<string, ResData>();

        private static readonly Dictionary<string, UniTask<ResData>> _loadingTasks =
            new Dictionary<string, UniTask<ResData>>();

        private static int _pendingResourcesUnloadCount = 0;
        private const int RESOURCES_UNLOAD_THRESHOLD = 10;

        public static string GetCacheKey(string path, string loaderName)
        {
            return $"{loaderName}:{path}";
        }

        public static async UniTask<ResData> LoadSharedAsync(string path, string loaderName,
            Func<UniTask<ResData>> loadFunc)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogKit.LogError($"[ResMgr] LoadSharedAsync 失败: path 为空，Loader={loaderName}");
                return null;
            }

            string key = GetCacheKey(path, loaderName);

            if (_sharedCache.TryGetValue(key, out var cachedData))
            {
                if (cachedData.Asset != null)
                {
                    cachedData.RefCount++;
                    return cachedData;
                }

                _sharedCache.Remove(key);
            }

            if (_loadingTasks.TryGetValue(key, out var existingTask))
            {
                ResData existingRes = await existingTask;
                if (existingRes != null && existingRes.Asset != null)
                {
                    existingRes.RefCount++;
                    return existingRes;
                }

                _loadingTasks.Remove(key);
            }

            UniTask<ResData> newTask = LoadInternalAsync(key, path, loaderName, loadFunc).Preserve();
            _loadingTasks[key] = newTask;

            try
            {
                return await newTask;
            }
            finally
            {
                if (_loadingTasks.ContainsKey(key))
                {
                    _loadingTasks.Remove(key);
                }
            }
        }

        private static async UniTask<ResData> LoadInternalAsync(string key, string path, string loaderName,
            Func<UniTask<ResData>> loadFunc)
        {
            ResData data = await loadFunc.Invoke();

            if (data == null || data.Asset == null)
            {
                LogKit.LogError($"[ResMgr] LoadInternalAsync 失败: 资源为空，Path={path}，Loader={loaderName}");
                return null;
            }

            if (!_sharedCache.ContainsKey(key))
            {
                data.RefCount = 1;
                _sharedCache.Add(key, data);
                return data;
            }

            _sharedCache[key].RefCount++;
            return _sharedCache[key];
        }

        public static void AddSync(ResData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Path) || data.Asset == null) return;

            string key = GetCacheKey(data.Path, data.LoaderName);

            if (!_sharedCache.ContainsKey(key))
            {
                data.RefCount = 1;
                _sharedCache.Add(key, data);
                return;
            }

            _sharedCache[key].RefCount++;
        }

        public static ResData GetCache(string path, string loaderName)
        {
            string key = GetCacheKey(path, loaderName);
            if (_sharedCache.TryGetValue(key, out var data))
            {
                if (data.Asset != null) return data;
                _sharedCache.Remove(key);
            }

            return null;
        }

        public static bool IsLoadingAsync(string path, string loaderName)
        {
            return _loadingTasks.ContainsKey(GetCacheKey(path, loaderName));
        }

        public static ResData TryGetPendingResultSync(string path, string loaderName)
        {
            string key = GetCacheKey(path, loaderName);
            if (_loadingTasks.TryGetValue(key, out var task))
            {
                if (task.Status == UniTaskStatus.Succeeded)
                {
                    ResData res = task.GetAwaiter().GetResult();
                    if (res != null) return res;
                }
            }

            return null;
        }

        public static void AddRef(string path, string loaderName)
        {
            string key = GetCacheKey(path, loaderName);
            if (_sharedCache.TryGetValue(key, out var data))
            {
                data.RefCount++;
            }
        }

        public static void RemoveRef(string path, string loaderName)
        {
            string key = GetCacheKey(path, loaderName);
            if (!_sharedCache.TryGetValue(key, out var data)) return;

            data.RefCount--;
            if (data.RefCount > 0) return;

            _sharedCache.Remove(key);
            RealUnload(data);
        }

        public static void GarbageCollect()
        {
            LogKit.LogWarning("[ResMgr] 触发强力 GC (GC.Collect + UnloadUnusedAssets)...");
            _pendingResourcesUnloadCount = 0;
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 供 ResourceLoader 调用的惰性卸载触发器
        /// </summary>
        public static void TriggerResourcesUnload()
        {
            _pendingResourcesUnloadCount++;
            if (_pendingResourcesUnloadCount >= RESOURCES_UNLOAD_THRESHOLD)
            {
                _pendingResourcesUnloadCount = 0;
                Resources.UnloadUnusedAssets();
                LogKit.Log("[ResMgr] 达到 Resources 卸载阈值，已触发后台 UnloadUnusedAssets");
            }
        }

        private static void RealUnload(ResData data)
        {
            if (data == null || data.Asset == null) return;

            // 架构重构：ResMgr 不再关心具体的卸载方式，直接调用注入的委托
            if (data.UnloadAction != null)
            {
                try
                {
                    data.UnloadAction.Invoke(data);
                }
                catch (Exception e)
                {
                    LogKit.LogError($"[ResMgr] 卸载资源时发生异常: Path={data.Path}, Loader={data.LoaderName}\n{e}");
                }
            }
            else
            {
                LogKit.LogWarning($"[ResMgr] 资源缺少卸载委托，执行默认 Destroy: Path={data.Path}");
                if (Application.isPlaying) UnityEngine.Object.Destroy(data.Asset);
                else UnityEngine.Object.DestroyImmediate(data.Asset);
            }
        }
    }
}