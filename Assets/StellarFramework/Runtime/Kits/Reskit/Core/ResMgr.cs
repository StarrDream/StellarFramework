using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace StellarFramework.Res
{
    /// <summary>
    /// [Internal] 资源管理器核心
    /// 职责：缓存管理、引用计数、并发加载控制 (Task Deduplication)
    /// </summary>
    internal static class ResMgr
    {
        // 全局已加载缓存
        private static readonly Dictionary<string, ResData> _sharedCache = new Dictionary<string, ResData>();

        // 正在加载中的任务 (用于并发去重)
        private static readonly Dictionary<string, UniTask<ResData>> _loadingTasks = new Dictionary<string, UniTask<ResData>>();

        //  生成带类型的唯一 Key (Type://Path)，解决同名资源冲突
        public static string GetCacheKey(string path, ResLoaderType type)
        {
            return $"{type}://{path}";
        }

        /// <summary>
        /// 核心加载入口 (支持并发去重)
        /// </summary>
        public static async UniTask<ResData> LoadSharedAsync(string path, ResLoaderType type, Func<UniTask<ResData>> loadFunc)
        {
            string key = GetCacheKey(path, type);

            // 1. 查缓存 (一级缓存)
            if (_sharedCache.TryGetValue(key, out var cachedData))
            {
                if (cachedData.Asset != null)
                {
                    cachedData.RefCount++;
                    return cachedData;
                }

                // 缓存已失效（对象被销毁），移除脏数据
                _sharedCache.Remove(key);
            }

            // 2. 查加载任务 (二级缓存 - 并发去重)
            if (_loadingTasks.TryGetValue(key, out var existingTask))
            {
                // 等待正在进行的加载任务完成
                var res = await existingTask;
                if (res != null && res.Asset != null)
                {
                    // 任务完成后，引用计数+1 (因为是新的请求者)
                    res.RefCount++;
                    return res;
                }

                // 如果之前的任务失败了，移除任务记录，允许重新尝试
                _loadingTasks.Remove(key);
            }

            // 3. 发起新任务
            // 使用 lazy task 确保异常捕获和字典清理
            var newTask = LoadInternalAsync(key, loadFunc);
            _loadingTasks[key] = newTask;

            try
            {
                var result = await newTask;
                return result;
            }
            finally
            {
                // 无论成功失败，移除加载中标记
                if (_loadingTasks.ContainsKey(key))
                {
                    _loadingTasks.Remove(key);
                }
            }
        }

        private static async UniTask<ResData> LoadInternalAsync(string key, Func<UniTask<ResData>> loadFunc)
        {
            // 执行具体的加载逻辑 (由 Loader 提供)
            var data = await loadFunc.Invoke();

            if (data != null && data.Asset != null)
            {
                // 加载成功，加入缓存
                if (!_sharedCache.ContainsKey(key))
                {
                    data.RefCount = 1; // 初始引用
                    _sharedCache.Add(key, data);
                }
                else
                {
                    // 极低概率：在 await 期间缓存被其他同步方式写入了
                    _sharedCache[key].RefCount++;
                    data = _sharedCache[key];
                }

                return data;
            }

            return null;
        }

        /// <summary>
        /// 同步添加 (仅用于 Resources.Load)
        /// </summary>
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
                // 引用归零，执行卸载
                _sharedCache.Remove(key);
                RealUnload(data);
            }
        }

        private static void RealUnload(ResData data)
        {
            if (data.Asset == null) return;

            switch (data.Type)
            {
                case ResLoaderType.Resources:
                    if (!(data.Asset is GameObject))
                    {
                        Resources.UnloadAsset(data.Asset);
                    }

                    break;

                case ResLoaderType.Addressable:
#if UNITY_ADDRESSABLES
                    // 生产级优先使用 Handle 释放，避免歧义
                    if (data.Data != null)
                    {
                        // 这是一个 struct，拆箱
                        var handle = (AsyncOperationHandle)data.Data;
                        if (handle.IsValid())
                        {
                            Addressables.Release(handle);
                        }
                    }
                    else
                    {
                        // 兜底：如果没有 Handle，才尝试用 Object 释放
                        Addressables.Release(data.Asset);
                    }
#endif
                    break;
            }
        }
    }
}