using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using UnityEngine;

namespace StellarFramework.Res
{
    public static class AddressablesResKitInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            RegisterAddressableHotUpdateSingleton();
            ResKit.RegisterCustomLoader("Addressables", request => ResKit.Allocate<AddressableLoader>());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            Install();
        }
#endif

        private static void RegisterAddressableHotUpdateSingleton()
        {
            SingletonFactory.RegisterMetadata(typeof(AddressableHotUpdateManager),
                new SingletonMetadata
                {
                    ResourcePath = "",
                    LifeCycle = SingletonLifeCycle.Global,
                    UseContainer = true
                });
            SingletonFactory.RegisterPureSingletonCreator(typeof(AddressableHotUpdateManager),
                static () => new AddressableHotUpdateManager());
        }
    }

    internal sealed class AddressablesPackageHotUpdateStrategy : IResourceHotUpdateStrategy
    {
        public async UniTask<HotUpdateOperationResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            AddressableOperationResult result =
                await AddressableHotUpdateManager.Instance.InitializeAsync(cancellationToken);
            return FromAddressableOperation(result);
        }

        public async UniTask<HotUpdateCheckResult> CheckResourceUpdatesAsync(IEnumerable<object> keys = null,
            bool updateCatalogs = true, CancellationToken cancellationToken = default)
        {
            UpdateCheckResult result = await AddressableHotUpdateManager.Instance.CheckCatalogUpdatesAsync(
                keys,
                updateCatalogs,
                cancellationToken);

            return new HotUpdateCheckResult
            {
                Success = result.IsSuccess,
                Status = MapStatus(result.Status),
                HasUpdate = result.HasUpdate,
                TotalDownloadSize = result.TotalDownloadSize,
                UpdatedCatalogs = result.UpdatedCatalogs,
                Keys = result.Keys,
                Error = result.Error,
                ElapsedMilliseconds = result.ElapsedMilliseconds
            };
        }

        public async UniTask<HotUpdateDownloadResult> DownloadResourceUpdatesAsync(IEnumerable<object> keys,
            Action<HotUpdateDownloadProgress> onProgress = null, CancellationToken cancellationToken = default)
        {
            AddressableDownloadResult result = await AddressableHotUpdateManager.Instance.DownloadDependenciesAsync(
                keys,
                progress => onProgress?.Invoke(new HotUpdateDownloadProgress
                {
                    Percent = progress.Percent,
                    DownloadedBytes = progress.DownloadedBytes,
                    TotalBytes = progress.TotalBytes
                }),
                cancellationToken);

            return new HotUpdateDownloadResult
            {
                Success = result.Success,
                Status = MapStatus(result.Status),
                TotalBytes = result.TotalBytes,
                DownloadedBytes = result.DownloadedBytes,
                Error = result.Error,
                ElapsedMilliseconds = result.ElapsedMilliseconds,
                Keys = result.Keys
            };
        }

        public async UniTask<HotUpdateOperationResult> ClearResourceCacheAsync(IEnumerable<object> keys,
            CancellationToken cancellationToken = default)
        {
            AddressableOperationResult result =
                await AddressableHotUpdateManager.Instance.ClearDependencyCacheAsync(keys, cancellationToken);
            return FromAddressableOperation(result);
        }

        private static HotUpdateOperationResult FromAddressableOperation(AddressableOperationResult result)
        {
            return new HotUpdateOperationResult
            {
                Success = result.Success,
                Status = MapStatus(result.Status),
                Error = result.Error,
                ElapsedMilliseconds = result.ElapsedMilliseconds,
                Keys = result.Keys
            };
        }

        private static HotUpdateOperationStatus MapStatus(AddressableHotUpdateStatus status)
        {
            switch (status)
            {
                case AddressableHotUpdateStatus.Success:
                    return HotUpdateOperationStatus.Success;
                case AddressableHotUpdateStatus.AddressablesUnavailable:
                    return HotUpdateOperationStatus.Unavailable;
                case AddressableHotUpdateStatus.InitializationFailed:
                    return HotUpdateOperationStatus.InitializationFailed;
                case AddressableHotUpdateStatus.InvalidKeys:
                    return HotUpdateOperationStatus.InvalidKeys;
                case AddressableHotUpdateStatus.CatalogCheckFailed:
                    return HotUpdateOperationStatus.CheckFailed;
                case AddressableHotUpdateStatus.CatalogUpdateFailed:
                    return HotUpdateOperationStatus.UpdateFailed;
                case AddressableHotUpdateStatus.DownloadSizeFailed:
                    return HotUpdateOperationStatus.DownloadSizeFailed;
                case AddressableHotUpdateStatus.DownloadFailed:
                    return HotUpdateOperationStatus.DownloadFailed;
                case AddressableHotUpdateStatus.CacheClearFailed:
                    return HotUpdateOperationStatus.CacheClearFailed;
                case AddressableHotUpdateStatus.Cancelled:
                    return HotUpdateOperationStatus.Cancelled;
                case AddressableHotUpdateStatus.None:
                    return HotUpdateOperationStatus.None;
                default:
                    return HotUpdateOperationStatus.Exception;
            }
        }
    }
}
