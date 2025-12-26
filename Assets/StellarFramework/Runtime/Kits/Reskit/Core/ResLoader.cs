using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public abstract class ResLoader : IResLoader, Pool.IPoolable
    {
        //  使用 HashSet 优化查找性能 O(N) -> O(1)
        private readonly HashSet<string> _loadedRecord = new HashSet<string>();

        // 版本号机制：防止异步任务在 ReleaseAll 之后回来污染数据
        private int _loaderVersion = 0;

        public abstract ResLoaderType LoaderType { get; }
        protected abstract ResData LoadRealSync(string path);
        protected abstract UniTask<ResData> LoadRealAsync(string path);

        public T Load<T>(string path) where T : Object
        {
            // 1. 优先查缓存
            var cache = ResMgr.GetCache(path, LoaderType);
            if (cache != null)
            {
                ResMgr.AddRef(path, LoaderType);
                _loadedRecord.Add(path);
                return cache.Asset as T;
            }

            // 2. 同步加载新资源
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
            // 记录当前版本号
            int currentVersion = _loaderVersion;

            // 异步加载  传入 LoaderType 生成唯一 Key
            var data = await ResMgr.LoadSharedAsync(path, LoaderType, () => LoadRealAsync(path));

            // 竞态条件检查
            // 如果 await 期间调用了 ReleaseAll/Recycle，版本号会变化
            if (currentVersion != _loaderVersion)
            {
                // 此时 Loader 已经重置，这个迟到的资源不应该被记录
                // 并且因为 ResMgr.LoadSharedAsync 内部已经 +1 了引用计数，我们需要手动 -1 抵消掉
                if (data != null)
                {
                    ResMgr.RemoveRef(path, LoaderType);
                    LogKit.LogWarning($"[ResLoader] 丢弃过期的异步加载结果: {path}");
                }

                return null;
            }

            if (data != null && data.Asset != null)
            {
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

        // 卸载单个资源
        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 检查记录中是否有该资源
            if (_loadedRecord.Contains(path))
            {
                _loadedRecord.Remove(path);
                ResMgr.RemoveRef(path, LoaderType); // 引用计数 -1
            }
        }

        public void ReleaseAll()
        {
            // 释放所有持有的资源
            foreach (var path in _loadedRecord)
            {
                ResMgr.RemoveRef(path, LoaderType);
            }

            _loadedRecord.Clear();

            // 版本号递增，立即使所有在途的异步任务失效
            _loaderVersion++;
        }

        public virtual void OnAllocated()
        {
            _loadedRecord.Clear();
            _loaderVersion++; // 分配时也更新版本号，双重保险
        }

        public virtual void OnRecycled() => ReleaseAll();
    }
}