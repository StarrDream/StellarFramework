using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
#endif

namespace StellarFramework.Res
{
    /// <summary>
    /// 热更检查结果结构体
    /// </summary>
    public struct UpdateCheckResult
    {
        public bool HasUpdate;
        public long TotalDownloadSize; // 单位：字节 (Bytes)
    }

    /// <summary>
    /// Addressables 商业级热更管理器
    /// 职责：提供 Catalog 更新检查、下载体积计算、依赖预下载与进度回调的完整工作流。
    /// </summary>
    [Singleton]
    public class AddressableHotUpdateManager : Singleton<AddressableHotUpdateManager>
    {
        /// <summary>
        /// 检查远端是否有资源更新，并计算需要下载的总字节数。
        /// </summary>
        /// <param name="keys">需要检查的资源标签或键值（如 "default", "level1"）。若为空，则检查所有已加载的 Catalog。</param>
        public async UniTask<UpdateCheckResult> CheckUpdateAsync(IEnumerable<object> keys = null)
        {
            UpdateCheckResult result = new UpdateCheckResult { HasUpdate = false, TotalDownloadSize = 0 };

#if UNITY_ADDRESSABLES
            try
            {
                // 1. 初始化 Addressables (如果尚未初始化)
                await Addressables.InitializeAsync().Task;

                // 2. 检查 Catalog 更新
                var checkHandle = Addressables.CheckForCatalogUpdates(false);
                List<string> catalogsToUpdate = await checkHandle.Task;
                Addressables.Release(checkHandle);

                if (catalogsToUpdate != null && catalogsToUpdate.Count > 0)
                {
                    LogKit.Log($"[AAHotUpdate] 发现 {catalogsToUpdate.Count} 个 Catalog 更新，正在拉取最新 Catalog...");
                    
                    // 3. 更新 Catalog
                    var updateHandle = Addressables.UpdateCatalogs(catalogsToUpdate, false);
                    await updateHandle.Task;
                    Addressables.Release(updateHandle);
                }
                else
                {
                    LogKit.Log("[AAHotUpdate] 本地 Catalog 已是最新。");
                }

                // 4. 计算下载体积
                AsyncOperationHandle<long> sizeHandle;
                if (keys != null)
                {
                    sizeHandle = Addressables.GetDownloadSizeAsync(keys);
                }
                else
                {
                    // 如果没有指定 key，默认检查所有资源（通常传入一个包含所有需要热更资源的 Label）
                    // 注意：直接传 null 会报错，这里使用默认的 default 标签作为兜底示范，实际项目建议传入具体的 Label
                    LogKit.LogWarning("[AAHotUpdate] 未指定 Keys，将默认检查 'default' 标签的下载体积。");
                    sizeHandle = Addressables.GetDownloadSizeAsync("default");
                }

                long totalSize = await sizeHandle.Task;
                Addressables.Release(sizeHandle);

                result.TotalDownloadSize = totalSize;
                result.HasUpdate = totalSize > 0;

                LogKit.Log($"[AAHotUpdate] 检查完成。是否有更新: {result.HasUpdate}, 需下载体积: {totalSize / 1048576f:F2} MB");
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[AAHotUpdate] 检查更新时发生异常: {ex.Message}\n{ex.StackTrace}");
            }
#else
            await UniTask.CompletedTask;
            LogKit.LogError("[AAHotUpdate] 请先安装 Addressables 包并定义 UNITY_ADDRESSABLES 宏");
#endif
            return result;
        }

        /// <summary>
        /// 执行热更下载，包含进度回调。
        /// </summary>
        /// <param name="keys">需要下载的资源标签或键值</param>
        /// <param name="onProgress">进度回调 (0.0f ~ 1.0f)</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask<bool> DownloadUpdateAsync(IEnumerable<object> keys, Action<float> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (keys == null)
            {
                LogKit.LogError("[AAHotUpdate] 下载失败: keys 参数不能为空。");
                return false;
            }

#if UNITY_ADDRESSABLES
            AsyncOperationHandle downloadHandle = default;
            try
            {
                downloadHandle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union);

                // 轮询进度
                while (!downloadHandle.IsDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LogKit.LogWarning("[AAHotUpdate] 下载被手动取消。");
                        Addressables.Release(downloadHandle);
                        return false;
                    }

                    if (downloadHandle.IsValid())
                    {
                        var status = downloadHandle.GetDownloadStatus();
                        if (status.TotalBytes > 0)
                        {
                            onProgress?.Invoke(status.Percent);
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    onProgress?.Invoke(1.0f);
                    LogKit.Log("[AAHotUpdate] 资源下载完成！");
                    Addressables.Release(downloadHandle);
                    return true;
                }
                else
                {
                    LogKit.LogError($"[AAHotUpdate] 下载失败: {downloadHandle.OperationException?.Message}");
                    Addressables.Release(downloadHandle);
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                LogKit.LogWarning("[AAHotUpdate] 下载任务已取消。");
                if (downloadHandle.IsValid()) Addressables.Release(downloadHandle);
                return false;
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[AAHotUpdate] 下载过程发生异常: {ex.Message}\n{ex.StackTrace}");
                if (downloadHandle.IsValid()) Addressables.Release(downloadHandle);
                return false;
            }
#else
            await UniTask.CompletedTask;
            LogKit.LogError("[AAHotUpdate] 请先安装 Addressables 包并定义 UNITY_ADDRESSABLES 宏");
            return false;
#endif
        }

        /// <summary>
        /// 清理本地缓存的 Bundle 文件（通常用于修复损坏的本地包或释放空间）
        /// </summary>
        public void ClearDependencyCacheAsync(IEnumerable<object> keys)
        {
            if (keys == null) return;

#if UNITY_ADDRESSABLES
            Addressables.ClearDependencyCacheAsync(keys);
            LogKit.Log("[AAHotUpdate] 已触发清理本地依赖缓存指令。");
#else
            LogKit.LogError("[AAHotUpdate] 请先安装 Addressables 包并定义 UNITY_ADDRESSABLES 宏");
#endif
        }
    }
}
