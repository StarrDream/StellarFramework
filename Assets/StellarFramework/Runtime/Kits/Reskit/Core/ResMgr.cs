using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using StellarFramework.Res.AB;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace StellarFramework.Res
{
    internal static class ResMgr
    {
        private static readonly Dictionary<string, ResData> _sharedCache = new Dictionary<string, ResData>();
        private static readonly Dictionary<string, UniTask<ResData>> _loadingTasks = new Dictionary<string, UniTask<ResData>>();

        public static string GetCacheKey(string path, ResLoaderType type)
        {
            return $"{type}:{path}";
        }

        // 核心异步加载逻辑（含任务去重）
        public static async UniTask<ResData> LoadSharedAsync(string path, ResLoaderType type, Func<UniTask<ResData>> loadFunc)
        {
            string key = GetCacheKey(path, type);

            // 1. 检查缓存
            if (_sharedCache.TryGetValue(key, out var cachedData))
            {
                if (cachedData.Asset != null)
                {
                    cachedData.RefCount++;
                    return cachedData;
                }

                _sharedCache.Remove(key);
            }

            // 2. 检查是否有正在进行的任务 (并发去重)
            if (_loadingTasks.TryGetValue(key, out var existingTask))
            {
                var res = await existingTask;
                if (res != null && res.Asset != null)
                {
                    res.RefCount++;
                    return res;
                }

                _loadingTasks.Remove(key);
            }

            // 3. 发起新任务
            var newTask = LoadInternalAsync(key, loadFunc);
            _loadingTasks[key] = newTask;

            try
            {
                return await newTask;
            }
            finally
            {
                if (_loadingTasks.ContainsKey(key)) _loadingTasks.Remove(key);
            }
        }

        private static async UniTask<ResData> LoadInternalAsync(string key, Func<UniTask<ResData>> loadFunc)
        {
            var data = await loadFunc.Invoke();
            if (data != null && data.Asset != null)
            {
                if (!_sharedCache.ContainsKey(key))
                {
                    data.RefCount = 1;
                    _sharedCache.Add(key, data);
                }
                else
                {
                    _sharedCache[key].RefCount++;
                    data = _sharedCache[key];
                }

                return data;
            }

            return null;
        }

        // 同步加载结果的缓存录入
        public static void AddSync(ResData data)
        {
            if (data == null) return;
            string key = GetCacheKey(data.Path, data.Type);

            if (!_sharedCache.ContainsKey(key))
            {
                data.RefCount = 1;
                _sharedCache.Add(key, data);
            }
            else
            {
                _sharedCache[key].RefCount++;
            }
        }

        public static ResData GetCache(string path, ResLoaderType type)
        {
            string key = GetCacheKey(path, type);
            if (_sharedCache.TryGetValue(key, out var data))
            {
                if (data.Asset != null) return data;
                _sharedCache.Remove(key);
            }

            return null;
        }

        // 检查是否有正在进行的异步任务 (防死锁用)
        public static bool IsLoadingAsync(string path, ResLoaderType type)
        {
            string key = GetCacheKey(path, type);
            return _loadingTasks.ContainsKey(key);
        }

        // 尝试获取已完成但未回调的异步任务结果 (防死锁用)
        public static ResData TryGetPendingResultSync(string path, ResLoaderType type)
        {
            string key = GetCacheKey(path, type);
            if (_loadingTasks.TryGetValue(key, out var task))
            {
                if (task.Status == UniTaskStatus.Succeeded)
                {
                    var res = task.GetAwaiter().GetResult();
                    if (res != null) return res;
                }
            }

            return null;
        }

        public static void AddRef(string path, ResLoaderType type)
        {
            string key = GetCacheKey(path, type);
            if (_sharedCache.TryGetValue(key, out var data))
            {
                data.RefCount++;
            }
        }

        public static void RemoveRef(string path, ResLoaderType type)
        {
            string key = GetCacheKey(path, type);
            if (!_sharedCache.TryGetValue(key, out var data)) return;

            data.RefCount--;
            if (data.RefCount <= 0)
            {
                _sharedCache.Remove(key);
                RealUnload(data);
            }
        }

        // [新增] 强力内存清理接口
        // 建议在场景切换或内存警告时调用
        public static void GarbageCollect()
        {
            LogKit.LogWarning("[ResMgr] 触发强力 GC (GC.Collect + UnloadUnusedAssets)...");
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        private static void RealUnload(ResData data)
        {
            if (data.Asset == null) return;

            switch (data.Type)
            {
                case ResLoaderType.Resources:
                    // Resources.UnloadAsset 只能卸载非 GameObject 资源 (如 Texture, Mesh)
                    // GameObject 类型的资源必须等待 Resources.UnloadUnusedAssets() 才能真正释放
                    if (!(data.Asset is GameObject) && !(data.Asset is Component))
                    {
                        Resources.UnloadAsset(data.Asset);
                    }

                    break;

                case ResLoaderType.Addressable:
#if UNITY_ADDRESSABLES
                    // [修复] 统一使用 AsyncOperationHandle (非泛型) 进行判断和释放
                    // AddressableLoader 中已确保 Data 字段存储的是非泛型 Handle
                    if (data.Data is AsyncOperationHandle handle)
                    {
                        if (handle.IsValid()) Addressables.Release(handle);
                    }
                    else
                    {
                        // 兜底保护：如果数据异常，尝试直接释放 Asset
                        Addressables.Release(data.Asset);
                    }
#endif
                    break;

                case ResLoaderType.AssetBundle:
                    AssetBundleManager.Instance?.UnloadAsset(data.Path);
                    break;
            }
        }
    }
}