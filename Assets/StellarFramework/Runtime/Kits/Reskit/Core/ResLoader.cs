using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public abstract class ResLoader : IResLoader, Pool.IPoolable
    {
        private readonly HashSet<string> _loadedRecord = new HashSet<string>();
        private int _loaderVersion = 0;

        public abstract ResLoaderType LoaderType { get; }
        protected abstract ResData LoadRealSync(string path);
        protected abstract UniTask<ResData> LoadRealAsync(string path);

        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;

            // 1. 检查是否本 Loader 已加载
            if (_loadedRecord.Contains(path))
            {
                var cachedData = ResMgr.GetCache(path, LoaderType);
                if (cachedData != null && cachedData.Asset != null)
                {
                    return cachedData.Asset as T;
                }

                _loadedRecord.Remove(path);
            }

            // 2. 检查全局缓存
            var cache = ResMgr.GetCache(path, LoaderType);
            if (cache != null)
            {
                ResMgr.AddRef(path, LoaderType);
                _loadedRecord.Add(path);
                return cache.Asset as T;
            }

            // 3. [并发保护] 检查是否有正在进行的异步任务
            if (ResMgr.IsLoadingAsync(path, LoaderType))
            {
                // 3.1 尝试捡漏：如果异步任务刚好完成了
                var pendingRes = ResMgr.TryGetPendingResultSync(path, LoaderType);
                if (pendingRes != null && pendingRes.Asset != null)
                {
                    ResMgr.AddSync(pendingRes); // 增加引用计数
                    _loadedRecord.Add(path);
                    return pendingRes.Asset as T;
                }

                // 3.2 无法挽回：异步任务正在运行且未完成
                LogKit.LogError($"[ResLoader] 同步加载失败: 资源 '{path}' 正在后台异步加载中。\n" +
                                "原因: Unity 禁止对同一资源同时进行 AssetBundle 异步和同步加载。\n" +
                                "解决: 请确保逻辑统一（全异步），或在调用同步加载前 await 异步任务。");
                return null;
            }

            // 4. 执行真实同步加载
            var data = LoadRealSync(path);
            if (data != null && data.Asset != null)
            {
                data.Path = path;
                data.Type = LoaderType;
                ResMgr.AddSync(data);
                _loadedRecord.Add(path);
                return data.Asset as T;
            }

            return null;
        }

        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_loadedRecord.Contains(path))
            {
                var cachedData = ResMgr.GetCache(path, LoaderType);
                if (cachedData != null && cachedData.Asset != null)
                {
                    return cachedData.Asset as T;
                }

                _loadedRecord.Remove(path);
            }

            int currentVersion = _loaderVersion;
            var data = await ResMgr.LoadSharedAsync(path, LoaderType, () => LoadRealAsync(path));

            // 版本检查：防止加载期间 Loader 被回收
            if (currentVersion != _loaderVersion)
            {
                if (data != null) ResMgr.RemoveRef(path, LoaderType);
                return null;
            }

            if (data != null && data.Asset != null)
            {
                // [并发修复] 再次检查是否已记录，防止并发调用导致重复 AddRef
                if (_loadedRecord.Contains(path))
                {
                    ResMgr.RemoveRef(path, LoaderType);
                    return data.Asset as T;
                }

                _loadedRecord.Add(path);
                return data.Asset as T;
            }

            return null;
        }

        public async UniTask PreloadAsync(IList<string> paths, Action<float> onProgress = null)
        {
            if (paths == null || paths.Count == 0)
            {
                onProgress?.Invoke(1.0f);
                return;
            }

            int total = paths.Count;
            int current = 0;
            const int BATCH_SIZE = 5;

            for (int i = 0; i < total; i += BATCH_SIZE)
            {
                var tasks = new List<UniTask>();
                for (int j = 0; j < BATCH_SIZE && (i + j) < total; j++)
                {
                    string path = paths[i + j];
                    tasks.Add(LoadAsync<Object>(path).ContinueWith(obj => { current++; }));
                }

                await UniTask.WhenAll(tasks);
                onProgress?.Invoke((float)current / total);
                await UniTask.Yield();
            }
        }

        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (_loadedRecord.Contains(path))
            {
                _loadedRecord.Remove(path);
                ResMgr.RemoveRef(path, LoaderType);
            }
        }

        public void ReleaseAll()
        {
            foreach (var path in _loadedRecord)
            {
                ResMgr.RemoveRef(path, LoaderType);
            }

            _loadedRecord.Clear();
            _loaderVersion++;
        }

        public virtual void OnAllocated()
        {
            _loadedRecord.Clear();
            _loaderVersion++;
        }

        public virtual void OnRecycled() => ReleaseAll();
    }
}