// ==================================================================================
// ResLoader - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：具体的资源加载器基类，负责与业务层对接。
// 改造说明：
// 1. 引入唯一的 LoaderId，在向 ResMgr 注册和注销时提供身份凭证，实现精准溯源。
// 2. 强化并发拦截，同步加载遇到异步加载时直接 Fail-Fast 阻断，防止死锁。
// ==================================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public abstract class ResLoader : IResLoader, Pool.IPoolable
    {
        private readonly HashSet<string> _loadedRecord = new HashSet<string>();
        private readonly Dictionary<string, UniTaskCompletionSource<ResData>> _loadingRecord =
            new Dictionary<string, UniTaskCompletionSource<ResData>>();
        private int _loaderVersion = 0;

        // 审计追踪：为每个加载器实例分配唯一标识 (例如: "UI_LoginPanel_Loader_abc123")
        private string _loaderId;

        public abstract string LoaderName { get; }

        protected abstract ResData LoadRealSync(string path);
        protected abstract UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken);
        protected abstract void UnloadReal(ResData data);

        public ResLoader()
        {
            GenerateNewLoaderId();
        }

        private void GenerateNewLoaderId()
        {
            _loaderId = $"{LoaderName}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        /// <summary>
        /// 允许业务层（如 UIPanel）注入更具可读性的 Owner 名字，方便快照排查
        /// </summary>
        public void SetOwnerName(string ownerName)
        {
            string safeOwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Anonymous" : ownerName.Trim();
            _loaderId = $"{LoaderName}_{safeOwnerName}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (_loadedRecord.Contains(path))
            {
                ResData cachedData = ResMgr.GetCache(path, LoaderName);
                if (cachedData != null && cachedData.Asset != null)
                {
                    return cachedData.Asset as T;
                }

                _loadedRecord.Remove(path);
            }

            ResData cache = ResMgr.GetCache(path, LoaderName);
            if (cache != null)
            {
                ResMgr.AddRef(path, LoaderName, _loaderId);
                _loadedRecord.Add(path);
                return cache.Asset as T;
            }

            if (ResMgr.IsLoadingAsync(path, LoaderName))
            {
                LogKit.LogError(
                    $"[ResLoader] 致命冲突: 资源 {path} 正在后台异步加载中，严禁此时发起同步加载请求。请统一业务层的加载链路。");
                return null;
            }

            ResData data = LoadRealSync(path);
            if (data != null && data.Asset != null)
            {
                data.Path = path;
                data.LoaderName = LoaderName;
                data.UnloadAction = UnloadReal;

                ResMgr.AddSync(data, _loaderId);
                _loadedRecord.Add(path);
                return data.Asset as T;
            }

            return null;
        }

        public async UniTask<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (_loadedRecord.Contains(path))
            {
                ResData cachedData = ResMgr.GetCache(path, LoaderName);
                if (cachedData != null && cachedData.Asset != null)
                {
                    return cachedData.Asset as T;
                }

                _loadedRecord.Remove(path);
            }

            if (_loadingRecord.TryGetValue(path, out UniTaskCompletionSource<ResData> loadingTask))
            {
                ResData pendingData = await loadingTask.Task;
                return pendingData?.Asset as T;
            }

            int currentVersion = _loaderVersion;
            string ownerIdSnapshot = _loaderId;
            UniTaskCompletionSource<ResData> loadingSource = new UniTaskCompletionSource<ResData>();
            _loadingRecord[path] = loadingSource;

            try
            {
                ResData data = await ResMgr.LoadSharedAsync(path, LoaderName, ownerIdSnapshot, async token =>
                {
                    ResData loaded = await LoadRealAsync(path, token);
                    if (loaded != null)
                    {
                        loaded.Path = path;
                        loaded.LoaderName = LoaderName;
                        loaded.UnloadAction = UnloadReal;
                    }

                    return loaded;
                }, cancellationToken);

                if (currentVersion != _loaderVersion)
                {
                    if (data != null)
                    {
                        ResMgr.RemoveRef(path, LoaderName, ownerIdSnapshot);
                    }

                    loadingSource.TrySetResult(null);
                    return null;
                }

                if (data == null || data.Asset == null)
                {
                    loadingSource.TrySetResult(null);
                    return null;
                }

                _loadedRecord.Add(path);
                loadingSource.TrySetResult(data);
                return data.Asset as T;
            }
            catch (OperationCanceledException)
            {
                loadingSource.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                loadingSource.TrySetException(ex);
                throw;
            }
            finally
            {
                _loadingRecord.Remove(path);
            }
        }

        public async UniTask PreloadAsync(IList<string> paths, Action<float> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (paths == null || paths.Count == 0)
            {
                onProgress?.Invoke(1.0f);
                return;
            }

            int total = paths.Count;
            int current = 0;
            const int BatchSize = 5;

            for (int i = 0; i < total; i += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tasks = new List<UniTask>();
                for (int j = 0; j < BatchSize && (i + j) < total; j++)
                {
                    string path = paths[i + j];
                    tasks.Add(LoadAsync<Object>(path, cancellationToken).ContinueWith(_ => { current++; }));
                }

                await UniTask.WhenAll(tasks);
                onProgress?.Invoke((float)current / total);
                await UniTask.Yield(cancellationToken);
            }
        }

        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path) || !_loadedRecord.Contains(path))
            {
                return;
            }

            _loadedRecord.Remove(path);
            ResMgr.RemoveRef(path, LoaderName, _loaderId);
        }

        public void ReleaseAll()
        {
            foreach (string path in _loadedRecord)
            {
                ResMgr.RemoveRef(path, LoaderName, _loaderId);
            }

            _loadedRecord.Clear();
            _loadingRecord.Clear();
            _loaderVersion++;
        }

        #region IPoolable 接口实现

        public virtual void OnAllocated()
        {
            _loadedRecord.Clear();
            _loadingRecord.Clear();
            _loaderVersion++;
            GenerateNewLoaderId();
        }

        public virtual void OnRecycled()
        {
            ReleaseAll();
        }

        public virtual void RecycleToPool()
        {
            LogKit.LogError(
                $"[ResLoader] RecycleToPool 未覆写: LoaderType={GetType().Name}. 请在自定义 Loader 中显式调用对应的 PoolKit.Recycle<具体类型>(this)。");
        }

        #endregion
    }
}
