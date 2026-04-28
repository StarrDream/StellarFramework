// ==================================================================================
// ResMgr - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：全局资源缓存与引用计数调度中心。
// 改造说明：
// 1. 引入 OwnerId 机制，每次 AddRef/RemoveRef 必须提供身份证明。
// 2. 增加 Fail-Fast 断言，严禁 RefCount 出现负数或重复卸载。
// 3. 新增 TakeSnapshot 审计接口，一键打印所有驻留资源及其持有者。
// ==================================================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Res
{
    internal static class ResMgr
    {
        private sealed class OngoingLoadEntry
        {
            public CancellationTokenSource SharedCts = new CancellationTokenSource();
            public UniTaskCompletionSource<ResData> CompletionSource = new UniTaskCompletionSource<ResData>();
            public int WaiterCount;
            public bool IsCompleted;
        }

        private static readonly Dictionary<string, ResData> _sharedCache = new Dictionary<string, ResData>();

        private static readonly Dictionary<string, OngoingLoadEntry> _loadingTasks =
            new Dictionary<string, OngoingLoadEntry>();

        private static int _pendingResourcesUnloadCount = 0;
        private const int RESOURCES_UNLOAD_THRESHOLD = 10;

        public static string GetCacheKey(string path, string loaderName)
        {
            return $"{loaderName}:{path}";
        }

        public static async UniTask<ResData> LoadSharedAsync(string path, string loaderName, string ownerId,
            Func<CancellationToken, UniTask<ResData>> loadFunc, CancellationToken cancellationToken = default)
        {
            LogKit.AssertNotNull(path, "[ResMgr] LoadSharedAsync 失败: path 为空");

            string key = GetCacheKey(path, loaderName);

            if (_sharedCache.TryGetValue(key, out ResData cachedData))
            {
                if (cachedData.Asset != null)
                {
                    AddRefInternal(cachedData, ownerId);
                    return cachedData;
                }

                _sharedCache.Remove(key);
            }

            OngoingLoadEntry entry;
            if (_loadingTasks.TryGetValue(key, out entry))
            {
                entry.WaiterCount++;
            }
            else
            {
                entry = new OngoingLoadEntry
                {
                    WaiterCount = 1
                };
                _loadingTasks[key] = entry;
                RunSharedLoadAsync(key, path, loaderName, loadFunc, entry).Forget();
            }

            try
            {
                ResData loadedData = await entry.CompletionSource.Task.AttachExternalCancellation(cancellationToken);
                if (loadedData != null && loadedData.Asset != null)
                {
                    AddRefInternal(loadedData, ownerId);
                }

                return loadedData;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                entry.WaiterCount = Math.Max(0, entry.WaiterCount - 1);
                if (entry.WaiterCount == 0 && !entry.IsCompleted && !entry.SharedCts.IsCancellationRequested)
                {
                    entry.SharedCts.Cancel();
                }
            }
        }

        private static async UniTask<ResData> LoadInternalAsync(string key, string path, string loaderName,
            Func<CancellationToken, UniTask<ResData>> loadFunc, CancellationToken cancellationToken)
        {
            ResData data;
            try
            {
                data = await loadFunc.Invoke(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            if (data == null || data.Asset == null)
            {
                LogKit.LogError($"[ResMgr] 物理加载失败: 资源为空，Path={path}，Loader={loaderName}");
                return null;
            }

            if (!_sharedCache.ContainsKey(key))
            {
                data.RefCount = 0;
                _sharedCache.Add(key, data);
            }

            return _sharedCache[key];
        }

        public static void AddSync(ResData data, string ownerId)
        {
            if (data == null || string.IsNullOrEmpty(data.Path) || data.Asset == null)
            {
                return;
            }

            string key = GetCacheKey(data.Path, data.LoaderName);
            if (!_sharedCache.ContainsKey(key))
            {
                data.RefCount = 0;
                _sharedCache.Add(key, data);
            }

            AddRefInternal(_sharedCache[key], ownerId);
        }

        public static ResData GetCache(string path, string loaderName)
        {
            string key = GetCacheKey(path, loaderName);
            if (_sharedCache.TryGetValue(key, out ResData data))
            {
                if (data.Asset != null)
                {
                    return data;
                }

                _sharedCache.Remove(key);
            }

            return null;
        }

        public static bool IsLoadingAsync(string path, string loaderName)
        {
            return _loadingTasks.ContainsKey(GetCacheKey(path, loaderName));
        }

        public static void AddRef(string path, string loaderName, string ownerId)
        {
            string key = GetCacheKey(path, loaderName);
            if (_sharedCache.TryGetValue(key, out ResData data))
            {
                AddRefInternal(data, ownerId);
            }
        }

        public static void RemoveRef(string path, string loaderName, string ownerId)
        {
            string key = GetCacheKey(path, loaderName);
            if (!_sharedCache.TryGetValue(key, out ResData data))
            {
                return;
            }

            data.RefCount--;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            data.RemoveOwner(ownerId);
#endif

            LogKit.Assert(data.RefCount >= 0,
                $"[ResMgr] 致命错误：资源 {path} 的引用计数出现负数 ({data.RefCount})！存在重复卸载或越权释放。");

            if (data.RefCount > 0)
            {
                return;
            }

            _sharedCache.Remove(key);
            RealUnload(data);
        }

        private static void AddRefInternal(ResData data, string ownerId)
        {
            data.RefCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            data.AddOwner(ownerId);
#endif
        }

        public static void GarbageCollect()
        {
            LogKit.LogWarning("[ResMgr] 触发强力 GC (GC.Collect + UnloadUnusedAssets)...");
            _pendingResourcesUnloadCount = 0;
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

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
            if (data == null || data.Asset == null)
            {
                return;
            }

            if (data.UnloadAction != null)
            {
                data.UnloadAction.Invoke(data);
            }
            else
            {
                LogKit.LogWarning($"[ResMgr] 资源缺少卸载委托，执行默认 Destroy: Path={data.Path}");
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(data.Asset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(data.Asset);
                }
            }
        }

        private static async UniTaskVoid RunSharedLoadAsync(string key, string path, string loaderName,
            Func<CancellationToken, UniTask<ResData>> loadFunc, OngoingLoadEntry entry)
        {
            ResData data = null;

            try
            {
                data = await LoadInternalAsync(key, path, loaderName, loadFunc, entry.SharedCts.Token);
                entry.CompletionSource.TrySetResult(data);
            }
            catch (OperationCanceledException)
            {
                entry.CompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                entry.CompletionSource.TrySetException(ex);
                LogKit.LogError($"[ResMgr] 跟踪共享加载任务异常: Key={key}\n{ex.Message}");
            }
            finally
            {
                entry.IsCompleted = true;

                if (_loadingTasks.TryGetValue(key, out OngoingLoadEntry current) && ReferenceEquals(current, entry))
                {
                    _loadingTasks.Remove(key);
                }

                if (entry.WaiterCount == 0 && data != null && data.Asset != null && data.RefCount == 0)
                {
                    _sharedCache.Remove(key);
                    RealUnload(data);
                }

                entry.SharedCts.Dispose();
            }
        }

        #region 审计与诊断 (Audit & Diagnostics)

        /// <summary>
        /// 打印当前内存中所有受管资源的快照
        /// 商业化项目排查内存泄漏的利器
        /// </summary>
        public static void TakeSnapshot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("========== [ResMgr] 资源驻留内存快照 ==========");
            sb.AppendLine($"当前总计驻留资源数: {_sharedCache.Count}");

            foreach (KeyValuePair<string, ResData> kvp in _sharedCache)
            {
                ResData data = kvp.Value;
                sb.AppendLine($"\n[资源] {data.Path} (Loader: {data.LoaderName})");
                sb.AppendLine($" - 引用计数 (RefCount): {data.RefCount}");
                sb.AppendLine($" - 持有者 (Owners):");
                if (data.Owners.Count == 0)
                {
                    sb.AppendLine("    <无明确持有者，可能存在泄漏>");
                }
                else
                {
                    foreach (string owner in data.Owners)
                    {
                        sb.AppendLine($"    -> {owner}");
                    }
                }
            }

            sb.AppendLine("===============================================");
            LogKit.Log(sb.ToString());
#else
            LogKit.LogWarning("[ResMgr] 内存快照功能仅在 Editor 或 Development Build 下可用。");
#endif
        }

        #endregion
    }
}
