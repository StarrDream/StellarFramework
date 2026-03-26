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
using UnityEngine;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public abstract class ResLoader : IResLoader, Pool.IPoolable
    {
        private readonly HashSet<string> _loadedRecord = new HashSet<string>();
        private int _loaderVersion = 0;

        // 审计追踪：为每个加载器实例分配唯一标识 (例如: "UI_LoginPanel_Loader_abc123")
        private string _loaderId;

        public abstract string LoaderName { get; }

        protected abstract ResData LoadRealSync(string path);
        protected abstract UniTask<ResData> LoadRealAsync(string path);
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
            _loaderId = $"{LoaderName}_{ownerName}";
        }

        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_loadedRecord.Contains(path))
            {
                ResData cachedData = ResMgr.GetCache(path, LoaderName);
                if (cachedData != null && cachedData.Asset != null) return cachedData.Asset as T;

                // 脏数据清理
                _loadedRecord.Remove(path);
            }

            ResData cache = ResMgr.GetCache(path, LoaderName);
            if (cache != null)
            {
                ResMgr.AddRef(path, LoaderName, _loaderId);
                _loadedRecord.Add(path);
                return cache.Asset as T;
            }

            // Fail-Fast：严禁在异步加载途中插入同步加载请求
            if (ResMgr.IsLoadingAsync(path, LoaderName))
            {
                LogKit.LogError($"[ResLoader] 致命冲突: 资源 {path} 正在后台异步加载中，严禁此时发起同步加载请求。请统一业务层的加载链路。");
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

            ResData data = await ResMgr.LoadSharedAsync(path, LoaderName, _loaderId, async () =>
            {
                var d = await LoadRealAsync(path);
                if (d != null)
                {
                    d.Path = path;
                    d.LoaderName = LoaderName;
                    d.UnloadAction = UnloadReal;
                }

                return d;
            });

            // 如果在 await 期间加载器被回收 (Version 变更)，必须立即释放刚加载好的资源，防止泄漏
            if (currentVersion != _loaderVersion)
            {
                if (data != null) ResMgr.RemoveRef(path, LoaderName, _loaderId);
                return null;
            }

            if (data == null || data.Asset == null) return null;

            if (_loadedRecord.Contains(path))
            {
                // 极端竞态处理：如果记录中已有，说明并发流中已经被添加过，撤销本次的 Ref 增加
                ResMgr.RemoveRef(path, LoaderName, _loaderId);
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
            ResMgr.RemoveRef(path, LoaderName, _loaderId);
        }

        public void ReleaseAll()
        {
            foreach (string path in _loadedRecord)
            {
                ResMgr.RemoveRef(path, LoaderName, _loaderId);
            }

            _loadedRecord.Clear();
            _loaderVersion++;
        }

        #region IPoolable 接口实现

        public virtual void OnAllocated()
        {
            _loadedRecord.Clear();
            _loaderVersion++;
            GenerateNewLoaderId(); // 每次出池分配新的身份，防止历史遗留问题
        }

        public virtual void OnRecycled()
        {
            ReleaseAll();
        }

        #endregion
    }
}