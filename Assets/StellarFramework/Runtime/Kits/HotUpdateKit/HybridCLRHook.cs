using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.HotUpdate
{
    /// <summary>
    /// HybridCLR 热更生命周期钩子
    /// 职责：提供标准化的代码热更装载与跳转流程，彻底解耦 AOT 环境与 HotUpdate 环境。
    /// </summary>
    public static class HybridCLRHook
    {
        public enum HotUpdateState
        {
            None,
            LoadingMetadata,
            MetadataLoaded,
            LoadingHotUpdateAssembly,
            LoadedHotUpdateAssembly,
            EnteringHotUpdate,
            EnteredHotUpdate,
            Failed
        }

        [Header("热更配置规范")] public static string HotUpdateAssemblyName = "HotUpdate.dll";

        public static string HotUpdateEntryClass = "HotUpdate.HotUpdateMain";
        public static string HotUpdateEntryMethod = "Main";

        /// <summary>
        /// 补充元数据 DLL 列表
        /// </summary>
        public static List<string> AOTMetaAssemblyFiles = new List<string>
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll"
        };

        public static HotUpdateState State { get; private set; } = HotUpdateState.None;
        public static string LastError { get; private set; }
        public static string LoadedAssemblyFullName { get; private set; }

        /// <summary>
        /// 步骤 1：加载 AOT 补充元数据
        /// </summary>
        public static async UniTask<bool> LoadMetadataForAOTAssembliesAsync(
            Func<string, UniTask<byte[]>> dllBytesProvider)
        {
#if !HYBRIDCLR_ENABLE
            State = HotUpdateState.LoadingMetadata;
            LastError = null;
            SetFailed("[HybridCLRHook] HYBRIDCLR_ENABLE is not enabled. AOT metadata loading is unavailable.");
            await UniTask.CompletedTask;
            return false;
#else
            if (dllBytesProvider == null)
            {
                SetFailed("[HybridCLRHook] 加载 AOT 元数据失败: dllBytesProvider 为空");
                return false;
            }

            State = HotUpdateState.LoadingMetadata;
            LastError = null;

            for (int i = 0; i < AOTMetaAssemblyFiles.Count; i++)
            {
                string aotDllName = AOTMetaAssemblyFiles[i];
                if (string.IsNullOrEmpty(aotDllName))
                {
                    SetFailed("[HybridCLRHook] 加载 AOT 元数据失败: 检测到空 DLL 名称");
                    return false;
                }

                byte[] dllBytes = await dllBytesProvider.Invoke(aotDllName);
                if (dllBytes == null || dllBytes.Length == 0)
                {
                    SetFailed($"[HybridCLRHook] 无法获取 AOT 元数据 DLL 字节流: {aotDllName}");
                    return false;
                }

                HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HybridCLR.HomologousImageMode.SuperSet);
                LogKit.Log($"[HybridCLRHook] 成功加载 AOT 补充元数据: {aotDllName}");
            }

            State = HotUpdateState.MetadataLoaded;
            return true;
#endif
        }

        /// <summary>
        /// 步骤 2：加载热更程序集并执行跳转
        /// </summary>
        public static bool LoadAndStartHotUpdateAssembly(byte[] hotUpdateDllBytes)
        {
            if (hotUpdateDllBytes == null || hotUpdateDllBytes.Length == 0)
            {
                SetFailed("[HybridCLRHook] 启动热更失败: 热更 DLL 字节流为空");
                return false;
            }

            State = HotUpdateState.LoadingHotUpdateAssembly;
            LastError = null;
            LoadedAssemblyFullName = null;

            Assembly hotUpdateAssembly = null;
            try
            {
                hotUpdateAssembly = Assembly.Load(hotUpdateDllBytes);
            }
            catch (Exception e)
            {
                SetFailed($"[HybridCLRHook] 加载热更程序集失败: Exception={e.Message}");
                return false;
            }

            if (hotUpdateAssembly == null)
            {
                SetFailed("[HybridCLRHook] 加载热更程序集失败: Assembly.Load 返回为空");
                return false;
            }

            LoadedAssemblyFullName = hotUpdateAssembly.FullName;
            State = HotUpdateState.LoadedHotUpdateAssembly;
            LogKit.Log($"[HybridCLRHook] 成功加载热更程序集: {LoadedAssemblyFullName}");

            Type entryType = hotUpdateAssembly.GetType(HotUpdateEntryClass);
            if (entryType == null)
            {
                SetFailed($"[HybridCLRHook] 找不到热更入口类: {HotUpdateEntryClass}");
                return false;
            }

            MethodInfo method = entryType.GetMethod(HotUpdateEntryMethod, BindingFlags.Static | BindingFlags.Public);
            if (method == null)
            {
                SetFailed($"[HybridCLRHook] 找不到热更入口方法: {HotUpdateEntryMethod}, EntryClass={HotUpdateEntryClass}");
                return false;
            }

            State = HotUpdateState.EnteringHotUpdate;
            LogKit.Log("[HybridCLRHook] 正在跨域跳转至热更逻辑...");

            try
            {
                method.Invoke(null, null);
            }
            catch (Exception e)
            {
                SetFailed($"[HybridCLRHook] 执行热更入口失败: Exception={e.Message}\nStackTrace={e.StackTrace}");
                return false;
            }

            State = HotUpdateState.EnteredHotUpdate;
            LogKit.Log("[HybridCLRHook] 热更入口执行完成");
            return true;
        }

        private static void SetFailed(string error)
        {
            State = HotUpdateState.Failed;
            LastError = error;
            LogKit.LogError(error);
        }
    }

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
        UniTask<HybridCLRAAHotUpdateResult> RunCodeHotUpdateAsync(ResKitRuntimeSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default);
    }

    public sealed class AddressablesHotUpdateStrategy : IResourceHotUpdateStrategy
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
            UpdateCheckResult result = await AddressableHotUpdateManager.Instance.CheckCatalogUpdatesAsync(keys,
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

    public sealed class HybridCLRCodeHotUpdateStrategy : ICodeHotUpdateStrategy
    {
        public UniTask<HybridCLRAAHotUpdateResult> RunCodeHotUpdateAsync(ResKitRuntimeSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            return HybridCLRAAHotUpdateRunner.RunAsync(settings, progress, cancellationToken);
        }
    }

    public static class HotUpdateKit
    {
        private static IResourceHotUpdateStrategy _resourceStrategy = new AddressablesHotUpdateStrategy();
        private static ICodeHotUpdateStrategy _codeStrategy = new HybridCLRCodeHotUpdateStrategy();
        private static ResKitRuntimeSettings _settings;

        public static IResourceHotUpdateStrategy ResourceStrategy => _resourceStrategy;
        public static ICodeHotUpdateStrategy CodeStrategy => _codeStrategy;
        public static ResKitRuntimeSettings Settings => _settings ?? ResKitRuntimeSettings.LoadOrCreateDefault();

        public static void Configure(IResourceHotUpdateStrategy resourceStrategy = null,
            ICodeHotUpdateStrategy codeStrategy = null, ResKitRuntimeSettings settings = null)
        {
            if (resourceStrategy != null)
            {
                _resourceStrategy = resourceStrategy;
            }

            if (codeStrategy != null)
            {
                _codeStrategy = codeStrategy;
            }

            if (settings != null)
            {
                _settings = settings;
            }
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

        public static UniTask<HotUpdateOperationResult> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            if (_resourceStrategy == null)
            {
                return UniTask.FromResult(HotUpdateOperationResult.Fail(
                    "Resource hot update strategy is null.",
                    HotUpdateOperationStatus.Exception));
            }

            return _resourceStrategy.InitializeAsync(cancellationToken);
        }

        public static UniTask<HotUpdateCheckResult> CheckResourceUpdatesAsync(IEnumerable<object> keys = null,
            bool updateCatalogs = true, CancellationToken cancellationToken = default)
        {
            if (_resourceStrategy == null)
            {
                return UniTask.FromResult(new HotUpdateCheckResult
                {
                    Success = false,
                    Status = HotUpdateOperationStatus.Exception,
                    Error = "Resource hot update strategy is null."
                });
            }

            List<object> resolvedKeys = keys != null ? new List<object>(keys) : Settings.BuildAddressablesDefaultUpdateKeys();
            if (resolvedKeys.Count == 0)
            {
                return UniTask.FromResult(new HotUpdateCheckResult
                {
                    Success = false,
                    Status = HotUpdateOperationStatus.InvalidKeys,
                    Error = "Resource hot update keys are empty. Configure Addressables labels/keys in ResKitRuntimeSettings or pass explicit keys."
                });
            }

            return _resourceStrategy.CheckResourceUpdatesAsync(resolvedKeys,
                updateCatalogs, cancellationToken);
        }

        public static UniTask<HotUpdateDownloadResult> DownloadResourceUpdatesAsync(IEnumerable<object> keys = null,
            Action<HotUpdateDownloadProgress> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (_resourceStrategy == null)
            {
                return UniTask.FromResult(new HotUpdateDownloadResult
                {
                    Success = false,
                    Status = HotUpdateOperationStatus.Exception,
                    Error = "Resource hot update strategy is null."
                });
            }

            List<object> resolvedKeys = keys != null ? new List<object>(keys) : Settings.BuildAddressablesDefaultUpdateKeys();
            if (resolvedKeys.Count == 0)
            {
                return UniTask.FromResult(new HotUpdateDownloadResult
                {
                    Success = false,
                    Status = HotUpdateOperationStatus.InvalidKeys,
                    Error = "Resource hot update keys are empty. Configure Addressables labels/keys in ResKitRuntimeSettings or pass explicit keys."
                });
            }

            return _resourceStrategy.DownloadResourceUpdatesAsync(resolvedKeys,
                onProgress, cancellationToken);
        }

        public static UniTask<HotUpdateOperationResult> ClearResourceCacheAsync(IEnumerable<object> keys,
            CancellationToken cancellationToken = default)
        {
            if (_resourceStrategy == null)
            {
                return UniTask.FromResult(HotUpdateOperationResult.Fail(
                    "Resource hot update strategy is null.",
                    HotUpdateOperationStatus.Exception));
            }

            return _resourceStrategy.ClearResourceCacheAsync(keys, cancellationToken);
        }

        public static UniTask<HybridCLRAAHotUpdateResult> RunCodeHotUpdateAsync(
            ResKitRuntimeSettings settings = null, IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_codeStrategy == null)
            {
                return UniTask.FromResult(FailCodeResult("Code hot update strategy is null."));
            }

            ResKitRuntimeSettings resolvedSettings = settings ?? Settings;
            if (resolvedSettings == null)
            {
                return UniTask.FromResult(FailCodeResult("ResKitRuntimeSettings is null."));
            }

            ResKitRuntimeSettingsValidationReport validation = resolvedSettings.Validate(true);
            if (!validation.IsValid)
            {
                return UniTask.FromResult(FailCodeResult(
                    "ResKitRuntimeSettings validation failed: " + string.Join(" | ", validation.Errors)));
            }

            return _codeStrategy.RunCodeHotUpdateAsync(resolvedSettings, progress, cancellationToken);
        }

        public static UniTask<HybridCLRAAHotUpdateResult> RunStartupHotUpdateAsync(
            ResKitRuntimeSettings settings = null, IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return RunCodeHotUpdateAsync(settings, progress, cancellationToken);
        }

        private static HybridCLRAAHotUpdateResult FailCodeResult(string error)
        {
            LogKit.LogError($"[HotUpdateKit] {error}");
            return new HybridCLRAAHotUpdateResult
            {
                Success = false,
                State = HybridCLRAAHotUpdateRunnerState.Failed,
                Error = error
            };
        }
    }

    /// <summary>
    /// Startup-only HybridCLR hot update runner backed by Addressables.
    /// It downloads dll.bytes assets, verifies the hot-update assembly hash, loads AOT metadata,
    /// and then jumps to the configured static hot-update entry.
    /// </summary>
    public static class HybridCLRAAHotUpdateRunner
    {
        public static HybridCLRAAHotUpdateRunnerState State { get; private set; } =
            HybridCLRAAHotUpdateRunnerState.None;

        public static string LastError { get; private set; }

        public static UniTask<HybridCLRAAHotUpdateResult> RunAsync(ResKitRuntimeSettings settings,
            Action<float> onProgress, CancellationToken cancellationToken = default)
        {
            IProgress<float> progress = onProgress != null ? Progress.Create(onProgress) : null;
            return RunAsync(settings, progress, cancellationToken);
        }

        public static async UniTask<HybridCLRAAHotUpdateResult> RunAsync(ResKitRuntimeSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            LastError = null;
            State = HybridCLRAAHotUpdateRunnerState.None;
            progress?.Report(0f);

#if !HYBRIDCLR_ENABLE
            await UniTask.CompletedTask;
            return Fail("HYBRIDCLR_ENABLE is not enabled. HybridCLR AA hot update is unavailable.");
#else
            if (settings == null)
            {
                settings = ResKitRuntimeSettings.LoadOrCreateDefault();
            }

            if (settings == null)
            {
                return Fail("ResKitRuntimeSettings is null.");
            }

            HotUpdateManifestLoadResult manifestLoadResult = await HotUpdateManifestSourceChain.LoadAsync(
                HotUpdateManifestSourceChain.BuildDefaultSources(settings),
                cancellationToken);

            if (!manifestLoadResult.Success || manifestLoadResult.Manifest == null)
            {
                return Fail(manifestLoadResult.Error);
            }

            HotUpdateManifest manifest = manifestLoadResult.Manifest;
            HotUpdateManifestValidationReport manifestValidation = manifest.Validate();
            if (!manifestValidation.IsValid)
            {
                return Fail("HotUpdateManifest validation failed: " + string.Join(" | ", manifestValidation.Errors));
            }

            LogKit.Log($"[HybridCLRAAHotUpdateRunner] Manifest loaded from {manifestLoadResult.Source}");

            if (string.IsNullOrWhiteSpace(manifest.hotUpdateAssemblyKey))
            {
                return Fail("HotUpdateAssemblyKey is empty.");
            }

            AddressableHotUpdateManager aaManager = AddressableHotUpdateManager.Instance;

            State = HybridCLRAAHotUpdateRunnerState.InitializingAddressables;
            AddressableOperationResult initResult = await aaManager.InitializeAsync(cancellationToken);
            if (!initResult.Success)
            {
                return Fail(initResult.Error);
            }

            progress?.Report(0.1f);

            List<object> hotUpdateKeys = manifest.BuildDownloadKeys();

            State = HybridCLRAAHotUpdateRunnerState.CheckingCatalogs;
            UpdateCheckResult checkResult = await aaManager.CheckCatalogUpdatesAsync(
                hotUpdateKeys,
                settings.AddressablesUpdateCatalogsOnCheck,
                cancellationToken);

            if (!checkResult.IsSuccess)
            {
                return Fail(checkResult.Error);
            }

            progress?.Report(0.25f);

            State = HybridCLRAAHotUpdateRunnerState.DownloadingDependencies;
            AddressableDownloadResult downloadResult = await aaManager.DownloadDependenciesAsync(
                hotUpdateKeys,
                downloadProgress => progress?.Report(0.25f + downloadProgress.Percent * 0.2f),
                cancellationToken);

            if (!downloadResult.Success)
            {
                return Fail(downloadResult.Error);
            }

            progress?.Report(0.45f);

            AddressableLoader loader = ResKit.Allocate<AddressableLoader>();
            loader.SetOwnerName("HybridCLRAAHotUpdateRunner");

            try
            {
                State = HybridCLRAAHotUpdateRunnerState.LoadingBytes;
                Dictionary<string, byte[]> metadataBytes = await LoadMetadataBytesAsync(
                    loader,
                    manifest.aotMetadataKeys,
                    cancellationToken);

                TextAsset hotUpdateAsset = await loader.LoadAsync<TextAsset>(
                    manifest.hotUpdateAssemblyKey,
                    cancellationToken);

                if (hotUpdateAsset == null || hotUpdateAsset.bytes == null || hotUpdateAsset.bytes.Length == 0)
                {
                    return Fail($"Hot update dll.bytes load failed: {manifest.hotUpdateAssemblyKey}");
                }

                byte[] hotUpdateBytes = hotUpdateAsset.bytes;
                string actualSha256;
                if (!VerifySha256(hotUpdateBytes, manifest.hotUpdateAssemblySha256, out actualSha256))
                {
                    return Fail(
                        $"Hot update dll SHA256 mismatch. Key={manifest.hotUpdateAssemblyKey}, Expected={manifest.hotUpdateAssemblySha256}, Actual={actualSha256}");
                }

                progress?.Report(0.65f);

                State = HybridCLRAAHotUpdateRunnerState.LoadingMetadata;
                HybridCLRHook.AOTMetaAssemblyFiles = new List<string>(metadataBytes.Keys);
                bool metadataLoaded = await HybridCLRHook.LoadMetadataForAOTAssembliesAsync(key =>
                {
                    byte[] bytes;
                    metadataBytes.TryGetValue(key, out bytes);
                    return UniTask.FromResult(bytes);
                });

                if (!metadataLoaded)
                {
                    return Fail(HybridCLRHook.LastError);
                }

                progress?.Report(0.82f);

                State = HybridCLRAAHotUpdateRunnerState.LoadingAssembly;
                if (!string.IsNullOrWhiteSpace(manifest.hotUpdateEntryClass))
                {
                    HybridCLRHook.HotUpdateEntryClass = manifest.hotUpdateEntryClass.Trim();
                }

                if (!string.IsNullOrWhiteSpace(manifest.hotUpdateEntryMethod))
                {
                    HybridCLRHook.HotUpdateEntryMethod = manifest.hotUpdateEntryMethod.Trim();
                }

                HybridCLRHook.HotUpdateAssemblyName = manifest.hotUpdateAssemblyKey.Trim();

                bool assemblyLoaded = HybridCLRHook.LoadAndStartHotUpdateAssembly(hotUpdateBytes);
                if (!assemblyLoaded)
                {
                    return Fail(HybridCLRHook.LastError);
                }

                State = HybridCLRAAHotUpdateRunnerState.EnteredHotUpdate;
                progress?.Report(1f);
                return new HybridCLRAAHotUpdateResult
                {
                    Success = true,
                    State = State,
                    LoadedAssemblyFullName = HybridCLRHook.LoadedAssemblyFullName,
                    Manifest = manifest,
                    ManifestSource = manifestLoadResult.Source
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
            finally
            {
                ResKit.Recycle(loader);
            }
#endif
        }

        private static async UniTask<Dictionary<string, byte[]>> LoadMetadataBytesAsync(AddressableLoader loader,
            IReadOnlyList<string> metadataKeys, CancellationToken cancellationToken)
        {
            Dictionary<string, byte[]> result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (metadataKeys == null)
            {
                return result;
            }

            for (int i = 0; i < metadataKeys.Count; i++)
            {
                string key = metadataKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                key = key.Trim();
                TextAsset metadataAsset = await loader.LoadAsync<TextAsset>(key, cancellationToken);
                if (metadataAsset == null || metadataAsset.bytes == null || metadataAsset.bytes.Length == 0)
                {
                    throw new InvalidOperationException($"AOT metadata dll.bytes load failed: {key}");
                }

                result[key] = metadataAsset.bytes;
            }

            return result;
        }

        private static bool VerifySha256(byte[] bytes, string expectedSha256, out string actualSha256)
        {
            actualSha256 = ComputeSha256(bytes);
            if (string.IsNullOrWhiteSpace(expectedSha256))
            {
                return true;
            }

            string normalizedExpected = expectedSha256.Trim().Replace("-", string.Empty);
            return string.Equals(normalizedExpected, actualSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private static HybridCLRAAHotUpdateResult Fail(string error)
        {
            State = HybridCLRAAHotUpdateRunnerState.Failed;
            LastError = string.IsNullOrWhiteSpace(error) ? "Unknown HybridCLR AA hot update error." : error;
            LogKit.LogError($"[HybridCLRAAHotUpdateRunner] {LastError}");
            return new HybridCLRAAHotUpdateResult
            {
                Success = false,
                State = State,
                Error = LastError,
                LoadedAssemblyFullName = HybridCLRHook.LoadedAssemblyFullName
            };
        }
    }
}
