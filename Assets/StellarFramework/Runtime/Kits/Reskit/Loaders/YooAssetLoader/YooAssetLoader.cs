using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Pool;
using UnityEngine;
using YooAsset;

namespace StellarFramework.Res
{
    /// <summary>
    /// ResKit 对 YooAsset 2.3.x 的资源加载适配器。
    /// </summary>
    /// <remarks>
    /// 本 Loader 只负责已经初始化完成的 ResourcePackage 的 Asset 加载与句柄释放。
    /// Package 创建、初始化、版本更新、Manifest 更新和下载流程仍由 YooAsset 启动/热更层负责。
    /// ResMgr 的最终引用释放会映射为 AssetHandle.Release()，不会建立第二套 YooAsset 引用计数。
    /// </remarks>
    public sealed class YooAssetLoader : ResLoader
    {
        private string _packageName = YooAssetResKitInstaller.DefaultPackageName;

        /// <inheritdoc />
        public override string LoaderName =>
            $"{YooAssetResKitInstaller.LoaderKey}:{_packageName}";

        /// <summary>
        /// 当前 Loader 使用的 YooAsset Package 名称。
        /// </summary>
        public string PackageName => _packageName;

        /// <summary>
        /// 配置需要使用的已初始化 ResourcePackage。
        /// 每次从 PoolKit 分配后由 Installer factory 调用。
        /// </summary>
        public void Configure(string packageName)
        {
            _packageName = string.IsNullOrWhiteSpace(packageName)
                ? YooAssetResKitInstaller.DefaultPackageName
                : packageName.Trim();
        }

        protected override ResData LoadRealSync(string path)
        {
            ResourcePackage package = GetReadyPackage();
            if (package == null)
            {
                return null;
            }

            AssetHandle handle = package.LoadAssetSync<UnityEngine.Object>(path);
            handle.WaitForAsyncComplete();
            return BuildResult(path, handle);
        }

        protected override async UniTask<ResData> LoadRealAsync(
            string path,
            CancellationToken cancellationToken)
        {
            return await LoadRealAsyncTyped<UnityEngine.Object>(path, cancellationToken);
        }

        protected override async UniTask<ResData> LoadRealAsyncTyped<T>(
            string path,
            CancellationToken cancellationToken)
        {
            ResourcePackage package = GetReadyPackage();
            if (package == null)
            {
                return null;
            }

            AssetHandle handle = package.LoadAssetAsync<T>(path);
            try
            {
                await handle.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
                return BuildResult(path, handle);
            }
            catch
            {
                handle.Release();
                throw;
            }
        }

        protected override void UnloadReal(ResData data)
        {
            if (data?.Data is AssetHandle handle)
            {
                handle.Release();
                data.Data = null;
            }
        }

        /// <inheritdoc />
        public override void OnAllocated()
        {
            base.OnAllocated();
            _packageName = YooAssetResKitInstaller.DefaultPackageName;
        }

        /// <inheritdoc />
        public override void OnRecycled()
        {
            base.OnRecycled();
            _packageName = YooAssetResKitInstaller.DefaultPackageName;
        }

        /// <inheritdoc />
        public override void RecycleToPool()
        {
            PoolKit.Recycle(this);
        }

        private ResourcePackage GetReadyPackage()
        {
            ResourcePackage package = YooAssets.TryGetPackage(_packageName);
            if (package == null)
            {
                LogKit.LogError(
                    $"[YooAssetLoader] Package is not created. Package={_packageName}. " +
                    "Create and initialize the YooAsset ResourcePackage before using ResKit.");
                return null;
            }

            if (package.InitializeStatus != EOperationStatus.Succeed)
            {
                LogKit.LogError(
                    $"[YooAssetLoader] Package is not initialized. Package={_packageName}, Status={package.InitializeStatus}");
                return null;
            }

            return package;
        }

        private ResData BuildResult(string path, AssetHandle handle)
        {
            if (handle == null)
            {
                return null;
            }

            if (handle.Status != EOperationStatus.Succeed)
            {
                string error = handle.LastError;
                handle.Release();
                LogKit.LogError(
                    $"[YooAssetLoader] Load failed. Package={_packageName}, Path={path}, Error={error}");
                return null;
            }

            UnityEngine.Object asset = handle.AssetObject;
            if (asset == null)
            {
                handle.Release();
                LogKit.LogError(
                    $"[YooAssetLoader] Load completed without asset. Package={_packageName}, Path={path}");
                return null;
            }

            return new ResData
            {
                Asset = asset,
                Data = handle
            };
        }
    }
}
