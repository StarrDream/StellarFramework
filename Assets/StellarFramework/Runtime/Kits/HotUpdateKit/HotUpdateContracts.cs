using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StellarFramework.HotUpdate
{
    public enum HybridCLRAAHotUpdateRunnerState
    {
        None,
        InitializingAddressables,
        CheckingCatalogs,
        DownloadingDependencies,
        LoadingBytes,
        LoadingMetadata,
        LoadingAssembly,
        EnteredHotUpdate,
        Failed
    }

    public struct HybridCLRAAHotUpdateResult
    {
        public bool Success;
        public HybridCLRAAHotUpdateRunnerState State;
        public string Error;
        public string LoadedAssemblyFullName;
        public HotUpdateManifest Manifest;
        public string ManifestSource;
    }

    public enum HotUpdateOperationStatus
    {
        None = 0,
        Success = 1,
        Unavailable = 2,
        InitializationFailed = 3,
        InvalidKeys = 4,
        CheckFailed = 5,
        UpdateFailed = 6,
        DownloadSizeFailed = 7,
        DownloadFailed = 8,
        CacheClearFailed = 9,
        Cancelled = 10,
        Exception = 11
    }

    public struct HotUpdateOperationResult
    {
        public bool Success;
        public HotUpdateOperationStatus Status;
        public string Error;
        public long ElapsedMilliseconds;
        public List<string> Keys;

        public static HotUpdateOperationResult Ok(long elapsedMilliseconds = 0, List<string> keys = null)
        {
            return new HotUpdateOperationResult
            {
                Success = true,
                Status = HotUpdateOperationStatus.Success,
                ElapsedMilliseconds = elapsedMilliseconds,
                Keys = keys
            };
        }

        public static HotUpdateOperationResult Fail(string error,
            HotUpdateOperationStatus status = HotUpdateOperationStatus.Exception,
            long elapsedMilliseconds = 0, List<string> keys = null)
        {
            return new HotUpdateOperationResult
            {
                Success = false,
                Status = status,
                Error = error,
                ElapsedMilliseconds = elapsedMilliseconds,
                Keys = keys
            };
        }
    }

    public struct HotUpdateDownloadProgress
    {
        public float Percent;
        public long DownloadedBytes;
        public long TotalBytes;
    }

    public struct HotUpdateDownloadResult
    {
        public bool Success;
        public HotUpdateOperationStatus Status;
        public long TotalBytes;
        public long DownloadedBytes;
        public string Error;
        public long ElapsedMilliseconds;
        public List<string> Keys;
    }

    public struct HotUpdateCheckResult
    {
        public bool Success;
        public HotUpdateOperationStatus Status;
        public bool HasUpdate;
        public long TotalDownloadSize;
        public List<string> UpdatedCatalogs;
        public List<string> Keys;
        public string Error;
        public long ElapsedMilliseconds;
    }

    public interface IResourceHotUpdateStrategy
    {
        UniTask<HotUpdateOperationResult> InitializeAsync(CancellationToken cancellationToken = default);
        UniTask<HotUpdateCheckResult> CheckResourceUpdatesAsync(IEnumerable<object> keys = null,
            bool updateCatalogs = true, CancellationToken cancellationToken = default);
        UniTask<HotUpdateDownloadResult> DownloadResourceUpdatesAsync(IEnumerable<object> keys,
            Action<HotUpdateDownloadProgress> onProgress = null, CancellationToken cancellationToken = default);
        UniTask<HotUpdateOperationResult> ClearResourceCacheAsync(IEnumerable<object> keys,
            CancellationToken cancellationToken = default);
    }

    public interface ICodeHotUpdateStrategy
    {
        UniTask<HybridCLRAAHotUpdateResult> RunCodeHotUpdateAsync(HotUpdateSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default);
    }

    public sealed class AddressablesHotUpdateStrategy : IResourceHotUpdateStrategy
    {
        private const string UnavailableMessage =
            "Addressables hot update is unavailable. Install StellarFramework.HotUpdateKit.Addressables.";

        public UniTask<HotUpdateOperationResult> InitializeAsync(CancellationToken cancellationToken = default) =>
            UniTask.FromResult(HotUpdateOperationResult.Fail(UnavailableMessage, HotUpdateOperationStatus.Unavailable));

        public UniTask<HotUpdateCheckResult> CheckResourceUpdatesAsync(IEnumerable<object> keys = null,
            bool updateCatalogs = true, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(new HotUpdateCheckResult
            {
                Success = false, Status = HotUpdateOperationStatus.Unavailable, Error = UnavailableMessage
            });

        public UniTask<HotUpdateDownloadResult> DownloadResourceUpdatesAsync(IEnumerable<object> keys,
            Action<HotUpdateDownloadProgress> onProgress = null, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(new HotUpdateDownloadResult
            {
                Success = false, Status = HotUpdateOperationStatus.Unavailable, Error = UnavailableMessage
            });

        public UniTask<HotUpdateOperationResult> ClearResourceCacheAsync(IEnumerable<object> keys,
            CancellationToken cancellationToken = default) =>
            UniTask.FromResult(HotUpdateOperationResult.Fail(UnavailableMessage, HotUpdateOperationStatus.Unavailable));
    }

    public sealed class UnavailableCodeHotUpdateStrategy : ICodeHotUpdateStrategy
    {
        private const string UnavailableMessage =
            "Code hot update is unavailable. Install StellarFramework.HotUpdateKit.HybridCLR.";

        public UniTask<HybridCLRAAHotUpdateResult> RunCodeHotUpdateAsync(HotUpdateSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(new HybridCLRAAHotUpdateResult
            {
                Success = false,
                State = HybridCLRAAHotUpdateRunnerState.Failed,
                Error = UnavailableMessage
            });
    }

    public static class HotUpdateKit
    {
        private static IResourceHotUpdateStrategy _resourceStrategy = new AddressablesHotUpdateStrategy();
        private static ICodeHotUpdateStrategy _codeStrategy = new UnavailableCodeHotUpdateStrategy();
        private static HotUpdateSettings _settings;

        public static IResourceHotUpdateStrategy ResourceStrategy => _resourceStrategy;
        public static ICodeHotUpdateStrategy CodeStrategy => _codeStrategy;
        public static HotUpdateSettings Settings => _settings ?? HotUpdateSettings.LoadOrCreateDefault();

        public static void Configure(IResourceHotUpdateStrategy resourceStrategy = null,
            ICodeHotUpdateStrategy codeStrategy = null, HotUpdateSettings settings = null)
        {
            if (resourceStrategy != null) _resourceStrategy = resourceStrategy;
            if (codeStrategy != null) _codeStrategy = codeStrategy;
            if (settings != null) _settings = settings;
        }

        public static void SetResourceStrategy(IResourceHotUpdateStrategy strategy)
        {
            if (strategy == null)
            {
                LogKit.LogError("[HotUpdateKit] SetResourceStrategy failed: strategy is null.");
                return;
            }

            _resourceStrategy = strategy;
        }

        public static void SetCodeHotUpdateStrategy(ICodeHotUpdateStrategy strategy)
        {
            if (strategy == null)
            {
                LogKit.LogError("[HotUpdateKit] SetCodeHotUpdateStrategy failed: strategy is null.");
                return;
            }

            _codeStrategy = strategy;
        }

        public static UniTask<HotUpdateOperationResult> InitializeAsync(CancellationToken cancellationToken = default) =>
            _resourceStrategy == null
                ? UniTask.FromResult(HotUpdateOperationResult.Fail("Resource hot update strategy is null."))
                : _resourceStrategy.InitializeAsync(cancellationToken);

        public static UniTask<HotUpdateCheckResult> CheckResourceUpdatesAsync(IEnumerable<object> keys = null,
            bool updateCatalogs = true, CancellationToken cancellationToken = default)
        {
            if (_resourceStrategy == null)
                return UniTask.FromResult(FailCheck("Resource hot update strategy is null."));

            List<object> resolvedKeys = keys != null ? new List<object>(keys) : Settings.BuildAddressablesDefaultUpdateKeys();
            return resolvedKeys.Count == 0
                ? UniTask.FromResult(FailCheck("Resource hot update keys are empty."))
                : _resourceStrategy.CheckResourceUpdatesAsync(resolvedKeys, updateCatalogs, cancellationToken);
        }

        public static UniTask<HotUpdateDownloadResult> DownloadResourceUpdatesAsync(IEnumerable<object> keys = null,
            Action<HotUpdateDownloadProgress> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (_resourceStrategy == null)
                return UniTask.FromResult(FailDownload("Resource hot update strategy is null."));

            List<object> resolvedKeys = keys != null ? new List<object>(keys) : Settings.BuildAddressablesDefaultUpdateKeys();
            return resolvedKeys.Count == 0
                ? UniTask.FromResult(FailDownload("Resource hot update keys are empty."))
                : _resourceStrategy.DownloadResourceUpdatesAsync(resolvedKeys, onProgress, cancellationToken);
        }

        public static UniTask<HotUpdateOperationResult> ClearResourceCacheAsync(IEnumerable<object> keys,
            CancellationToken cancellationToken = default) =>
            _resourceStrategy == null
                ? UniTask.FromResult(HotUpdateOperationResult.Fail("Resource hot update strategy is null."))
                : _resourceStrategy.ClearResourceCacheAsync(keys, cancellationToken);

        public static UniTask<HybridCLRAAHotUpdateResult> RunCodeHotUpdateAsync(
            HotUpdateSettings settings = null, IProgress<float> progress = null,
            CancellationToken cancellationToken = default) =>
            _codeStrategy == null
                ? UniTask.FromResult(new HybridCLRAAHotUpdateResult
                {
                    Success = false,
                    State = HybridCLRAAHotUpdateRunnerState.Failed,
                    Error = "Code hot update strategy is null."
                })
                : _codeStrategy.RunCodeHotUpdateAsync(settings ?? Settings, progress, cancellationToken);

        public static UniTask<HybridCLRAAHotUpdateResult> RunStartupHotUpdateAsync(
            HotUpdateSettings settings = null, IProgress<float> progress = null,
            CancellationToken cancellationToken = default) =>
            RunCodeHotUpdateAsync(settings, progress, cancellationToken);

        private static HotUpdateCheckResult FailCheck(string error) => new HotUpdateCheckResult
        {
            Success = false, Status = HotUpdateOperationStatus.Exception, Error = error
        };

        private static HotUpdateDownloadResult FailDownload(string error) => new HotUpdateDownloadResult
        {
            Success = false, Status = HotUpdateOperationStatus.Exception, Error = error
        };
    }
}
