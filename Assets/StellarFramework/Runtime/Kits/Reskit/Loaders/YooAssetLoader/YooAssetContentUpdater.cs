using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace StellarFramework.Res
{
    /// <summary>
    /// YooAsset 启动期内容更新阶段。
    /// 仅描述 YooAsset 官方 HostPlayMode 更新流程，不包含 HybridCLR 代码热更职责。
    /// </summary>
    public enum YooAssetContentUpdateStage
    {
        None,
        InitializingPackage,
        RequestingVersion,
        UpdatingManifest,
        CreatingDownloader,
        Downloading,
        InstallingResKit,
        Completed,
        Failed
    }

    /// <summary>
    /// Stable failure categories for startup/update UI and telemetry.
    /// Do not parse Error strings in business code.
    /// </summary>
    public enum YooAssetContentUpdateErrorCode
    {
        None = 0,
        InvalidOptions,
        PackageInitializationFailed,
        PackageStateInvalid,
        VersionRequestFailed,
        EmptyPackageVersion,
        ManifestUpdateFailed,
        DownloadFailed,
        ResKitInstallFailed,
        Unexpected
    }

    public readonly struct YooAssetContentUpdateFailure
    {
        public readonly YooAssetContentUpdateErrorCode ErrorCode;
        public readonly YooAssetContentUpdateStage Stage;
        public readonly string Message;

        public YooAssetContentUpdateFailure(
            YooAssetContentUpdateErrorCode errorCode,
            YooAssetContentUpdateStage stage,
            string message)
        {
            ErrorCode = errorCode;
            Stage = stage;
            Message = message;
        }
    }

    /// <summary>
    /// Optional retry policy for short startup control-plane operations.
    /// Resource file retries remain owned by YooAsset's ResourceDownloaderOperation.
    /// </summary>
    public interface IYooAssetContentUpdateRetryPolicy
    {
        bool ShouldRetry(YooAssetContentUpdateFailure failure, int failedAttempt, out int delayMilliseconds);
    }

    /// <summary>
    /// Conservative default: retry version/manifest requests only.
    /// Package initialization has stateful recovery semantics and downloader retries are already handled by YooAsset.
    /// </summary>
    public sealed class YooAssetContentUpdateRetryPolicy : IYooAssetContentUpdateRetryPolicy
    {
        public static readonly YooAssetContentUpdateRetryPolicy Default = new YooAssetContentUpdateRetryPolicy(2, 500);

        public int MaxRetryCount { get; }
        public int DelayMilliseconds { get; }

        public YooAssetContentUpdateRetryPolicy(int maxRetryCount, int delayMilliseconds)
        {
            MaxRetryCount = Math.Max(0, maxRetryCount);
            DelayMilliseconds = Math.Max(0, delayMilliseconds);
        }

        public bool ShouldRetry(YooAssetContentUpdateFailure failure, int failedAttempt, out int delayMilliseconds)
        {
            delayMilliseconds = DelayMilliseconds;
            if (failedAttempt > MaxRetryCount) return false;

            return failure.ErrorCode == YooAssetContentUpdateErrorCode.VersionRequestFailed ||
                   failure.ErrorCode == YooAssetContentUpdateErrorCode.ManifestUpdateFailed;
        }
    }

    /// <summary>
    /// YooAsset HostPlayMode 内容更新配置。
    /// </summary>
    public sealed class YooAssetContentUpdateOptions
    {
        public string PackageName = YooAssetResKitInstaller.DefaultPackageName;
        public string MainHostServer;
        public string FallbackHostServer;
        public string BuildinPackageRoot;
        public string CachePackageRoot;
        public int OperationTimeoutSeconds = 60;
        public int DownloadWatchDogSeconds = 60;
        public int DownloadingMaxNumber = 8;
        public int FailedTryAgain = 3;
        public long ResumeDownloadMinimumSize = 1024L * 1024L;
        public bool AppendTimeTicks = true;
        public bool InstallResKitOnSuccess = true;
        public IYooAssetContentUpdateRetryPolicy RetryPolicy = YooAssetContentUpdateRetryPolicy.Default;

        /// <summary>
        /// 这些 HTTP 状态码代表已有临时文件不应继续保留。
        /// YooAsset 2.3.x 在断点下载器中遇到匹配状态码时会删除 temp 文件，
        /// 其中 416（Range Not Satisfiable）是最重要的保护项。
        /// </summary>
        public List<long> ResumeDownloadResponseCodes = new List<long> { 416L };
    }

    public readonly struct YooAssetContentUpdateProgress
    {
        public readonly YooAssetContentUpdateStage Stage;
        public readonly float Progress;
        public readonly int CurrentDownloadCount;
        public readonly int TotalDownloadCount;
        public readonly long CurrentDownloadBytes;
        public readonly long TotalDownloadBytes;
        public readonly int Attempt;

        public YooAssetContentUpdateProgress(
            YooAssetContentUpdateStage stage,
            float progress,
            int currentDownloadCount = 0,
            int totalDownloadCount = 0,
            long currentDownloadBytes = 0,
            long totalDownloadBytes = 0,
            int attempt = 1)
        {
            Stage = stage;
            Progress = progress;
            CurrentDownloadCount = currentDownloadCount;
            TotalDownloadCount = totalDownloadCount;
            CurrentDownloadBytes = currentDownloadBytes;
            TotalDownloadBytes = totalDownloadBytes;
            Attempt = Math.Max(1, attempt);
        }
    }

    public readonly struct YooAssetContentUpdateResult
    {
        public readonly bool Success;
        public readonly YooAssetContentUpdateStage Stage;
        public readonly string PackageName;
        public readonly string PackageVersion;
        public readonly string Error;
        public readonly YooAssetContentUpdateErrorCode ErrorCode;
        public readonly YooAssetContentUpdateStage FailureStage;
        public readonly int RetryCount;
        public readonly int DownloadedFileCount;
        public readonly long DownloadedBytes;

        internal YooAssetContentUpdateResult(
            bool success,
            YooAssetContentUpdateStage stage,
            string packageName,
            string packageVersion,
            string error,
            YooAssetContentUpdateErrorCode errorCode,
            YooAssetContentUpdateStage failureStage,
            int retryCount,
            int downloadedFileCount,
            long downloadedBytes)
        {
            Success = success;
            Stage = stage;
            PackageName = packageName;
            PackageVersion = packageVersion;
            Error = error;
            ErrorCode = errorCode;
            FailureStage = failureStage;
            RetryCount = retryCount;
            DownloadedFileCount = downloadedFileCount;
            DownloadedBytes = downloadedBytes;
        }
    }

    /// <summary>
    /// 一个很薄的 YooAsset HostPlayMode 启动更新辅助器。
    /// </summary>
    /// <remarks>
    /// 固定流程：Initialize -> RequestVersion -> Unload -> UpdateManifest -> Download -> Install ResKit。
    /// 断点续传完全使用 YooAsset DefaultCacheFileSystem 官方实现：临时文件保留 + HTTP Range。
    /// 本类不引用 HybridCLRKit；内容更新成功后，项目可独立调用 HybridCLRKit.RunAsync()。
    /// </remarks>
    public static class YooAssetContentUpdater
    {
        public static async UniTask<YooAssetContentUpdateResult> UpdateHostPackageAsync(
            YooAssetContentUpdateOptions options,
            IProgress<YooAssetContentUpdateProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateOptions(options, out string validationError))
            {
                return Fail(
                    options?.PackageName,
                    null,
                    YooAssetContentUpdateErrorCode.InvalidOptions,
                    YooAssetContentUpdateStage.None,
                    validationError,
                    0,
                    null);
            }

            string packageName = options.PackageName.Trim();
            string packageVersion = null;
            ResourceDownloaderOperation downloader = null;
            YooAssetContentUpdateStage currentStage = YooAssetContentUpdateStage.None;
            int retryCount = 0;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!YooAssets.Initialized)
                {
                    YooAssets.Initialize();
                }

                ResourcePackage package = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);
                if (package.InitializeStatus == EOperationStatus.None)
                {
                    currentStage = YooAssetContentUpdateStage.InitializingPackage;
                    Report(progress, YooAssetContentUpdateStage.InitializingPackage, 0f);
                    var remoteServices = new HostRemoteServices(options.MainHostServer, options.FallbackHostServer);
                    var parameters = new HostPlayModeParameters
                    {
                        BuildinFileSystemParameters = CreateOptionalBuildinFileSystem(options.BuildinPackageRoot),
                        CacheFileSystemParameters =
                            FileSystemParameters.CreateDefaultCacheFileSystemParameters(
                                remoteServices,
                                packageRoot: NullIfWhiteSpace(options.CachePackageRoot))
                    };

                    ConfigureCacheFileSystem(parameters.CacheFileSystemParameters, options);
                    InitializationOperation initOperation = package.InitializeAsync(parameters);
                    await AwaitOperationAsync(initOperation, cancellationToken);
                    if (initOperation.Status != EOperationStatus.Succeed)
                    {
                        return Fail(
                            packageName,
                            packageVersion,
                            YooAssetContentUpdateErrorCode.PackageInitializationFailed,
                            currentStage,
                            "YooAsset package initialization failed: " + initOperation.Error,
                            retryCount,
                            downloader);
                    }
                }
                else if (package.InitializeStatus != EOperationStatus.Succeed)
                {
                    return Fail(
                        packageName,
                        packageVersion,
                        YooAssetContentUpdateErrorCode.PackageStateInvalid,
                        YooAssetContentUpdateStage.InitializingPackage,
                        $"YooAsset package is in a non-reusable initialization state: {package.InitializeStatus}",
                        retryCount,
                        downloader);
                }

                currentStage = YooAssetContentUpdateStage.RequestingVersion;
                int versionAttempt = 0;
                while (true)
                {
                    versionAttempt++;
                    Report(progress, currentStage, 0.1f, versionAttempt);
                    RequestPackageVersionOperation versionOperation = package.RequestPackageVersionAsync(
                        options.AppendTimeTicks,
                        ClampPositive(options.OperationTimeoutSeconds));
                    await AwaitOperationAsync(versionOperation, cancellationToken);
                    if (versionOperation.Status == EOperationStatus.Succeed)
                    {
                        packageVersion = versionOperation.PackageVersion;
                        break;
                    }

                    var failure = new YooAssetContentUpdateFailure(
                        YooAssetContentUpdateErrorCode.VersionRequestFailed,
                        currentStage,
                        "Request package version failed: " + versionOperation.Error);
                    if (!TryGetRetryDelay(options.RetryPolicy, failure, versionAttempt, out int delayMs))
                    {
                        return Fail(
                            packageName,
                            packageVersion,
                            failure.ErrorCode,
                            failure.Stage,
                            failure.Message,
                            retryCount,
                            downloader);
                    }

                    retryCount++;
                    await DelayRetryAsync(delayMs, cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(packageVersion))
                {
                    return Fail(
                        packageName,
                        packageVersion,
                        YooAssetContentUpdateErrorCode.EmptyPackageVersion,
                        currentStage,
                        "Remote package version is empty.",
                        retryCount,
                        downloader);
                }

                currentStage = YooAssetContentUpdateStage.UpdatingManifest;
                Report(progress, currentStage, 0.2f);
                if (package.PackageValid)
                {
                    AsyncOperationBase unloadOperation = package.UnloadAllAssetsAsync();
                    await AwaitOperationAsync(unloadOperation, cancellationToken);
                }

                int manifestAttempt = 0;
                while (true)
                {
                    manifestAttempt++;
                    Report(progress, currentStage, 0.2f, manifestAttempt);
                    UpdatePackageManifestOperation manifestOperation = package.UpdatePackageManifestAsync(
                        packageVersion,
                        ClampPositive(options.OperationTimeoutSeconds));
                    await AwaitOperationAsync(manifestOperation, cancellationToken);
                    if (manifestOperation.Status == EOperationStatus.Succeed) break;

                    var failure = new YooAssetContentUpdateFailure(
                        YooAssetContentUpdateErrorCode.ManifestUpdateFailed,
                        currentStage,
                        "Update package manifest failed: " + manifestOperation.Error);
                    if (!TryGetRetryDelay(options.RetryPolicy, failure, manifestAttempt, out int delayMs))
                    {
                        return Fail(
                            packageName,
                            packageVersion,
                            failure.ErrorCode,
                            failure.Stage,
                            failure.Message,
                            retryCount,
                            downloader);
                    }

                    retryCount++;
                    await DelayRetryAsync(delayMs, cancellationToken);
                }

                currentStage = YooAssetContentUpdateStage.CreatingDownloader;
                Report(progress, currentStage, 0.3f);
                downloader = package.CreateResourceDownloader(
                    ClampDownloadConcurrency(options.DownloadingMaxNumber),
                    Math.Max(0, options.FailedTryAgain));

                if (downloader.TotalDownloadCount > 0)
                {
                    downloader.DownloadUpdateCallback = data =>
                    {
                        float normalized = 0.3f + data.Progress * 0.65f;
                        progress?.Report(new YooAssetContentUpdateProgress(
                            YooAssetContentUpdateStage.Downloading,
                            normalized,
                            data.CurrentDownloadCount,
                            data.TotalDownloadCount,
                            data.CurrentDownloadBytes,
                            data.TotalDownloadBytes));
                    };

                    downloader.BeginDownload();
                    using (cancellationToken.Register(downloader.CancelDownload))
                    {
                        await AwaitOperationAsync(downloader, cancellationToken);
                    }

                    if (downloader.Status != EOperationStatus.Succeed)
                    {
                        return Fail(
                            packageName,
                            packageVersion,
                            YooAssetContentUpdateErrorCode.DownloadFailed,
                            YooAssetContentUpdateStage.Downloading,
                            "Resource download failed: " + downloader.Error,
                            retryCount,
                            downloader);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                currentStage = YooAssetContentUpdateStage.InstallingResKit;
                Report(progress, currentStage, 0.97f);
                if (options.InstallResKitOnSuccess)
                {
                    try
                    {
                        YooAssetResKitInstaller.Install(packageName);
                    }
                    catch (Exception ex)
                    {
                        return Fail(
                            packageName,
                            packageVersion,
                            YooAssetContentUpdateErrorCode.ResKitInstallFailed,
                            currentStage,
                            "Install YooAsset backend into ResKit failed: " + ex.Message,
                            retryCount,
                            downloader);
                    }
                }

                currentStage = YooAssetContentUpdateStage.Completed;
                Report(progress, YooAssetContentUpdateStage.Completed, 1f);
                return new YooAssetContentUpdateResult(
                    true,
                    YooAssetContentUpdateStage.Completed,
                    packageName,
                    packageVersion,
                    null,
                    YooAssetContentUpdateErrorCode.None,
                    YooAssetContentUpdateStage.None,
                    retryCount,
                    downloader?.CurrentDownloadCount ?? 0,
                    downloader?.CurrentDownloadBytes ?? 0L);
            }
            catch (OperationCanceledException)
            {
                downloader?.CancelDownload();
                throw;
            }
            catch (Exception ex)
            {
                return Fail(
                    packageName,
                    packageVersion,
                    YooAssetContentUpdateErrorCode.Unexpected,
                    currentStage,
                    ex.Message,
                    retryCount,
                    downloader);
            }
        }

        private static async UniTask AwaitOperationAsync(
            AsyncOperationBase operation,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            await operation.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        private static void ConfigureCacheFileSystem(
            FileSystemParameters parameters,
            YooAssetContentUpdateOptions options)
        {
            parameters.AddParameter(
                FileSystemParametersDefine.DOWNLOAD_WATCH_DOG_TIME,
                ClampPositive(options.DownloadWatchDogSeconds));
            parameters.AddParameter(
                FileSystemParametersDefine.RESUME_DOWNLOAD_MINMUM_SIZE,
                Math.Max(0L, options.ResumeDownloadMinimumSize));

            if (options.ResumeDownloadResponseCodes != null)
            {
                parameters.AddParameter(
                    FileSystemParametersDefine.RESUME_DOWNLOAD_RESPONSE_CODES,
                    new List<long>(options.ResumeDownloadResponseCodes));
            }
        }

        private static bool TryValidateOptions(
            YooAssetContentUpdateOptions options,
            out string error)
        {
            if (options == null)
            {
                error = "YooAssetContentUpdateOptions is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.PackageName))
            {
                error = "PackageName is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.MainHostServer))
            {
                error = "MainHostServer is empty.";
                return false;
            }

            error = null;
            return true;
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static FileSystemParameters CreateOptionalBuildinFileSystem(string packageRoot)
        {
            string resolvedRoot = NullIfWhiteSpace(packageRoot);
            return resolvedRoot == null
                ? null
                : FileSystemParameters.CreateDefaultBuildinFileSystemParameters(packageRoot: resolvedRoot);
        }

        private static int ClampPositive(int value)
        {
            return Math.Max(1, value);
        }

        private static int ClampDownloadConcurrency(int value)
        {
            if (value < 1) return 1;
            return value > 64 ? 64 : value;
        }

        private static bool TryGetRetryDelay(
            IYooAssetContentUpdateRetryPolicy retryPolicy,
            YooAssetContentUpdateFailure failure,
            int failedAttempt,
            out int delayMilliseconds)
        {
            delayMilliseconds = 0;
            return retryPolicy != null &&
                   retryPolicy.ShouldRetry(failure, failedAttempt, out delayMilliseconds);
        }

        private static UniTask DelayRetryAsync(int delayMilliseconds, CancellationToken cancellationToken)
        {
            return delayMilliseconds <= 0
                ? UniTask.Yield(cancellationToken)
                : UniTask.Delay(delayMilliseconds, cancellationToken: cancellationToken);
        }

        private static void Report(
            IProgress<YooAssetContentUpdateProgress> progress,
            YooAssetContentUpdateStage stage,
            float value,
            int attempt = 1)
        {
            progress?.Report(new YooAssetContentUpdateProgress(stage, value, attempt: attempt));
        }

        private static YooAssetContentUpdateResult Fail(
            string packageName,
            string packageVersion,
            YooAssetContentUpdateErrorCode errorCode,
            YooAssetContentUpdateStage failureStage,
            string error,
            int retryCount,
            ResourceDownloaderOperation downloader)
        {
            string resolvedError = string.IsNullOrWhiteSpace(error)
                ? "Unknown YooAsset content update error."
                : error;
            return new YooAssetContentUpdateResult(
                false,
                YooAssetContentUpdateStage.Failed,
                packageName,
                packageVersion,
                resolvedError,
                errorCode,
                failureStage,
                retryCount,
                downloader?.CurrentDownloadCount ?? 0,
                downloader?.CurrentDownloadBytes ?? 0L);
        }

        private sealed class HostRemoteServices : IRemoteServices
        {
            private readonly string _main;
            private readonly string _fallback;

            public HostRemoteServices(string main, string fallback)
            {
                _main = TrimTrailingSlash(main);
                _fallback = string.IsNullOrWhiteSpace(fallback)
                    ? _main
                    : TrimTrailingSlash(fallback);
            }

            public string GetRemoteMainURL(string fileName)
            {
                return _main + "/" + fileName;
            }

            public string GetRemoteFallbackURL(string fileName)
            {
                return _fallback + "/" + fileName;
            }

            private static string TrimTrailingSlash(string value)
            {
                return value.Trim().TrimEnd('/');
            }
        }
    }
}
