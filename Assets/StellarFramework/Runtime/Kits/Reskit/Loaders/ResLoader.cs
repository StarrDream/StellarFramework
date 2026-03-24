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

        // 架构重构：子类提供字符串标识，代替 Enum
        public abstract string LoaderName { get; }

        protected abstract ResData LoadRealSync(string path);
        protected abstract UniTask<ResData> LoadRealAsync(string path);

        // 架构重构：子类必须实现自己的卸载逻辑
        protected abstract void UnloadReal(ResData data);

        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_loadedRecord.Contains(path))
            {
                ResData cachedData = ResMgr.GetCache(path, LoaderName);
                if (cachedData != null && cachedData.Asset != null) return cachedData.Asset as T;
                _loadedRecord.Remove(path);
            }

            ResData cache = ResMgr.GetCache(path, LoaderName);
            if (cache != null)
            {
                ResMgr.AddRef(path, LoaderName);
                _loadedRecord.Add(path);
                return cache.Asset as T;
            }

            if (ResMgr.IsLoadingAsync(path, LoaderName))
            {
                ResData pendingRes = ResMgr.TryGetPendingResultSync(path, LoaderName);
                if (pendingRes != null && pendingRes.Asset != null)
                {
                    ResMgr.AddSync(pendingRes);
                    _loadedRecord.Add(path);
                    return pendingRes.Asset as T;
                }

                LogKit.LogError($"[ResLoader] 同步加载失败: 资源正在后台异步加载中，Path={path}");
                return null;
            }

            ResData data = LoadRealSync(path);
            if (data != null && data.Asset != null)
            {
                data.Path = path;
                data.LoaderName = LoaderName;
                data.UnloadAction = UnloadReal; // 注入卸载委托
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
                ResData cachedData = ResMgr.GetCache(path, LoaderName);
                if (cachedData != null && cachedData.Asset != null) return cachedData.Asset as T;
                _loadedRecord.Remove(path);
            }

            int currentVersion = _loaderVersion;

            ResData data = await ResMgr.LoadSharedAsync(path, LoaderName, async () =>
            {
                var d = await LoadRealAsync(path);
                if (d != null)
                {
                    d.Path = path;
                    d.LoaderName = LoaderName;
                    d.UnloadAction = UnloadReal; // 注入卸载委托
                }

                return d;
            });

            if (currentVersion != _loaderVersion)
            {
                if (data != null) ResMgr.RemoveRef(path, LoaderName);
                return null;
            }

            if (data == null || data.Asset == null) return null;

            if (_loadedRecord.Contains(path))
            {
                ResMgr.RemoveRef(path, LoaderName);
                return data.Asset as T;
            }

            _loadedRecord.Add(path);
            return data.Asset as T;
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
            if (!_loadedRecord.Contains(path)) return;

            _loadedRecord.Remove(path);
            ResMgr.RemoveRef(path, LoaderName);
        }

        public void ReleaseAll()
        {
            foreach (string path in _loadedRecord)
            {
                ResMgr.RemoveRef(path, LoaderName);
            }

            _loadedRecord.Clear();
            _loaderVersion++;
        }

        public virtual void OnAllocated()
        {
            _loadedRecord.Clear();
            _loaderVersion++;
        }

        public virtual void OnRecycled()
        {
            ReleaseAll();
        }
    }
}