using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace StellarFramework.Res
{
    public enum AddressableHotUpdateStatus
    {
        None = 0,
        Success = 1,
        AddressablesUnavailable = 2,
        InitializationFailed = 3,
        InvalidKeys = 4,
        CatalogCheckFailed = 5,
        CatalogUpdateFailed = 6,
        DownloadSizeFailed = 7,
        DownloadFailed = 8,
        CacheClearFailed = 9,
        Cancelled = 10,
        Exception = 11
    }

    public struct AddressableOperationResult
    {
        public bool Success;
        public AddressableHotUpdateStatus Status;
        public string Error;
        public long ElapsedMilliseconds;
        public List<string> Keys;

        public static AddressableOperationResult Ok(long elapsedMilliseconds = 0)
        {
            return new AddressableOperationResult
            {
                Success = true,
                Status = AddressableHotUpdateStatus.Success,
                ElapsedMilliseconds = elapsedMilliseconds
            };
        }

        public static AddressableOperationResult Fail(string error,
            AddressableHotUpdateStatus status = AddressableHotUpdateStatus.Exception,
            long elapsedMilliseconds = 0)
        {
            return new AddressableOperationResult
            {
                Success = false,
                Status = status,
                Error = error,
                ElapsedMilliseconds = elapsedMilliseconds
            };
        }
    }

    public struct AddressableDownloadProgress
    {
        public float Percent;
        public long DownloadedBytes;
        public long TotalBytes;
    }

    public struct AddressableDownloadResult
    {
        public bool Success;
        public AddressableHotUpdateStatus Status;
        public long TotalBytes;
        public long DownloadedBytes;
        public string Error;
        public long ElapsedMilliseconds;
        public List<string> Keys;
    }

    public struct UpdateCheckResult
    {
        public bool IsSuccess;
        public AddressableHotUpdateStatus Status;
        public bool HasUpdate;
        public long TotalDownloadSize;
        public List<string> UpdatedCatalogs;
        public List<string> Keys;
        public string Error;
        public long ElapsedMilliseconds;
    }

    [Singleton]
    public class AddressableHotUpdateManager : Singleton<AddressableHotUpdateManager>
    {
        private readonly List<string> _lastReport = new List<string>();
        private bool _isInitialized;
        private bool _isInitializing;
        private UniTaskCompletionSource<bool> _initializeSource;
        private string _lastError;

        public bool IsInitialized => _isInitialized;
        public string LastError => _lastError;
        public IReadOnlyList<string> LastReport => _lastReport;

        public async UniTask<AddressableOperationResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BeginReport("Initialize");

#if UNITY_ADDRESSABLES
            if (_isInitialized)
            {
                Report("Addressables is already initialized.");
                stopwatch.Stop();
                return AddressableOperationResult.Ok(stopwatch.ElapsedMilliseconds);
            }

            if (_isInitializing && _initializeSource != null)
            {
                try
                {
                    bool existingResult = await _initializeSource.Task.AttachExternalCancellation(cancellationToken);
                    stopwatch.Stop();
                    return existingResult
                        ? AddressableOperationResult.Ok(stopwatch.ElapsedMilliseconds)
                        : AddressableOperationResult.Fail(_lastError, AddressableHotUpdateStatus.InitializationFailed,
                            stopwatch.ElapsedMilliseconds);
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    return FailOperation("Addressables initialization wait cancelled.",
                        AddressableHotUpdateStatus.Cancelled, stopwatch.ElapsedMilliseconds);
                }
            }

            _isInitializing = true;
            _lastError = null;
            _initializeSource = new UniTaskCompletionSource<bool>();

            AsyncOperationHandle<IResourceLocator> initHandle = default;
            try
            {
                initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask(cancellationToken: cancellationToken);

                if (initHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    _lastError = initHandle.OperationException?.Message ?? "Addressables.InitializeAsync failed.";
                    _initializeSource.TrySetResult(false);
                    stopwatch.Stop();
                    return FailOperation(_lastError, AddressableHotUpdateStatus.InitializationFailed,
                        stopwatch.ElapsedMilliseconds);
                }

                _isInitialized = true;
                _initializeSource.TrySetResult(true);
                Report("Addressables initialization succeeded.");
                stopwatch.Stop();
                return AddressableOperationResult.Ok(stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _lastError = "Addressables initialization cancelled.";
                _initializeSource.TrySetResult(false);
                stopwatch.Stop();
                return FailOperation(_lastError, AddressableHotUpdateStatus.Cancelled, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _initializeSource.TrySetResult(false);
                stopwatch.Stop();
                return FailOperation(_lastError, AddressableHotUpdateStatus.Exception, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                ReleaseIfValid(initHandle);
                _isInitializing = false;
                _initializeSource = null;
            }
#else
            await UniTask.CompletedTask;
            stopwatch.Stop();
            _lastError = "Addressables is unavailable. Install Addressables and enable UNITY_ADDRESSABLES.";
            return FailOperation(_lastError, AddressableHotUpdateStatus.AddressablesUnavailable,
                stopwatch.ElapsedMilliseconds);
#endif
        }

        public async UniTask<UpdateCheckResult> CheckCatalogUpdatesAsync(IEnumerable<object> keys = null,
            bool updateCatalogs = true, CancellationToken cancellationToken = default)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BeginReport("CheckCatalogUpdates");

            UpdateCheckResult result = new UpdateCheckResult
            {
                IsSuccess = false,
                Status = AddressableHotUpdateStatus.None,
                HasUpdate = false,
                TotalDownloadSize = 0,
                UpdatedCatalogs = new List<string>(),
                Keys = new List<string>()
            };

#if UNITY_ADDRESSABLES
            AddressableOperationResult initializeResult = await InitializeAsync(cancellationToken);
            if (!initializeResult.Success)
            {
                result.Status = initializeResult.Status;
                result.Error = initializeResult.Error;
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return result;
            }

            BeginReport("CheckCatalogUpdates");
            Report("Addressables initialization is ready.");

            AsyncOperationHandle<List<string>> checkHandle = default;
            AsyncOperationHandle<List<IResourceLocator>> updateHandle = default;
            AsyncOperationHandle<long> sizeHandle = default;

            try
            {
                List<object> downloadKeys = NormalizeKeys(keys);
                if (downloadKeys.Count == 0)
                {
                    downloadKeys = HotUpdateSettings.LoadOrCreateDefault().BuildAddressablesDefaultUpdateKeys();
                    Report("No explicit keys were passed. Runtime settings default update keys will be used.");
                }

                result.Keys = ToKeyStrings(downloadKeys);

                checkHandle = Addressables.CheckForCatalogUpdates(false);
                List<string> catalogsToUpdate = await checkHandle.ToUniTask(cancellationToken: cancellationToken);

                if (catalogsToUpdate != null && catalogsToUpdate.Count > 0)
                {
                    result.HasUpdate = true;
                    result.UpdatedCatalogs.AddRange(catalogsToUpdate);
                    Report($"Catalog updates found. Count={catalogsToUpdate.Count}");

                    if (updateCatalogs)
                    {
                        updateHandle = Addressables.UpdateCatalogs(catalogsToUpdate, false);
                        await updateHandle.ToUniTask(cancellationToken: cancellationToken);

                        if (updateHandle.Status != AsyncOperationStatus.Succeeded)
                        {
                            result.Status = AddressableHotUpdateStatus.CatalogUpdateFailed;
                            result.Error = updateHandle.OperationException?.Message ?? "Addressables.UpdateCatalogs failed.";
                            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                            ReportError(result.Error);
                            return result;
                        }

                        Report("Catalog update completed.");
                    }
                }
                else
                {
                    Report("No catalog update found.");
                }

                if (downloadKeys.Count > 0)
                {
                    sizeHandle = Addressables.GetDownloadSizeAsync((IEnumerable)downloadKeys);
                    result.TotalDownloadSize = await sizeHandle.ToUniTask(cancellationToken: cancellationToken);

                    if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
                    {
                        result.Status = AddressableHotUpdateStatus.DownloadSizeFailed;
                        result.Error = sizeHandle.OperationException?.Message ?? "Addressables.GetDownloadSizeAsync failed.";
                        result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        ReportError(result.Error);
                        return result;
                    }

                    result.HasUpdate |= result.TotalDownloadSize > 0;
                }

                result.IsSuccess = true;
                result.Status = AddressableHotUpdateStatus.Success;
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                Report(
                    $"Check complete. HasUpdate={result.HasUpdate}, DownloadSize={result.TotalDownloadSize / 1048576f:F2} MB, Keys={result.Keys.Count}, Elapsed={result.ElapsedMilliseconds}ms");
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = AddressableHotUpdateStatus.Cancelled;
                result.Error = "Addressables catalog check cancelled.";
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                ReportError(result.Error);
                return result;
            }
            catch (Exception ex)
            {
                result.Status = AddressableHotUpdateStatus.Exception;
                result.Error = ex.Message;
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                ReportError(result.Error);
                return result;
            }
            finally
            {
                ReleaseIfValid(checkHandle);
                ReleaseIfValid(updateHandle);
                ReleaseIfValid(sizeHandle);
            }
#else
            await UniTask.CompletedTask;
            result.Status = AddressableHotUpdateStatus.AddressablesUnavailable;
            result.Error = "Addressables is unavailable. Install Addressables and enable UNITY_ADDRESSABLES.";
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            ReportError(result.Error);
            return result;
#endif
        }

        public async UniTask<AddressableDownloadResult> DownloadDependenciesAsync(IEnumerable<object> keys,
            Action<float> onProgress = null, CancellationToken cancellationToken = default)
        {
            return await DownloadDependenciesAsync(keys,
                (AddressableDownloadProgress progress) => onProgress?.Invoke(progress.Percent),
                cancellationToken);
        }

        public async UniTask<AddressableDownloadResult> DownloadDependenciesAsync(IEnumerable<object> keys,
            Action<AddressableDownloadProgress> onProgress, CancellationToken cancellationToken = default)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BeginReport("DownloadDependencies");

            AddressableDownloadResult result = new AddressableDownloadResult
            {
                Success = false,
                Status = AddressableHotUpdateStatus.None,
                Keys = new List<string>()
            };

#if UNITY_ADDRESSABLES
            List<object> downloadKeys = NormalizeKeys(keys);
            result.Keys = ToKeyStrings(downloadKeys);
            if (downloadKeys.Count == 0)
            {
                result.Status = AddressableHotUpdateStatus.InvalidKeys;
                result.Error = "Download keys are empty.";
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                ReportError(result.Error);
                return result;
            }

            AddressableOperationResult initializeResult = await InitializeAsync(cancellationToken);
            if (!initializeResult.Success)
            {
                result.Status = initializeResult.Status;
                result.Error = initializeResult.Error;
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return result;
            }

            BeginReport("DownloadDependencies");
            Report("Addressables initialization is ready.");

            AsyncOperationHandle downloadHandle = default;
            try
            {
                downloadHandle = Addressables.DownloadDependenciesAsync(
                    (IEnumerable)downloadKeys,
                    Addressables.MergeMode.Union,
                    false);

                while (!downloadHandle.IsDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.Status = AddressableHotUpdateStatus.Cancelled;
                        result.Error = "Addressables dependency download cancelled.";
                        result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        ReportError(result.Error);
                        return result;
                    }

                    DownloadStatus status = downloadHandle.GetDownloadStatus();
                    result.TotalBytes = status.TotalBytes;
                    result.DownloadedBytes = status.DownloadedBytes;
                    onProgress?.Invoke(new AddressableDownloadProgress
                    {
                        Percent = status.TotalBytes > 0 ? status.Percent : 0f,
                        DownloadedBytes = status.DownloadedBytes,
                        TotalBytes = status.TotalBytes
                    });

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    DownloadStatus finalStatus = downloadHandle.GetDownloadStatus();
                    result.Success = true;
                    result.Status = AddressableHotUpdateStatus.Success;
                    result.TotalBytes = finalStatus.TotalBytes;
                    result.DownloadedBytes = finalStatus.DownloadedBytes;
                    result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                    onProgress?.Invoke(new AddressableDownloadProgress
                    {
                        Percent = 1f,
                        DownloadedBytes = finalStatus.DownloadedBytes,
                        TotalBytes = finalStatus.TotalBytes
                    });
                    Report(
                        $"Download complete. Size={result.TotalBytes / 1048576f:F2} MB, Keys={result.Keys.Count}, Elapsed={result.ElapsedMilliseconds}ms");
                }
                else
                {
                    result.Status = AddressableHotUpdateStatus.DownloadFailed;
                    result.Error = downloadHandle.OperationException?.Message ?? "Addressables.DownloadDependenciesAsync failed.";
                    result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                    ReportError(result.Error);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = AddressableHotUpdateStatus.Cancelled;
                result.Error = "Addressables dependency download cancelled.";
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                ReportError(result.Error);
                return result;
            }
            catch (Exception ex)
            {
                result.Status = AddressableHotUpdateStatus.Exception;
                result.Error = ex.Message;
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                ReportError(result.Error);
                return result;
            }
            finally
            {
                ReleaseIfValid(downloadHandle);
            }
#else
            await UniTask.CompletedTask;
            result.Status = AddressableHotUpdateStatus.AddressablesUnavailable;
            result.Error = "Addressables is unavailable. Install Addressables and enable UNITY_ADDRESSABLES.";
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            ReportError(result.Error);
            return result;
#endif
        }

        public async UniTask<AddressableOperationResult> ClearDependencyCacheAsync(IEnumerable<object> keys,
            CancellationToken cancellationToken = default)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BeginReport("ClearDependencyCache");

#if UNITY_ADDRESSABLES
            List<object> clearKeys = NormalizeKeys(keys);
            List<string> keyStrings = ToKeyStrings(clearKeys);
            if (clearKeys.Count == 0)
            {
                return FailOperation("Clear keys are empty.", AddressableHotUpdateStatus.InvalidKeys,
                    stopwatch.ElapsedMilliseconds, keyStrings);
            }

            AddressableOperationResult initializeResult = await InitializeAsync(cancellationToken);
            if (!initializeResult.Success)
            {
                return initializeResult;
            }

            BeginReport("ClearDependencyCache");
            Report("Addressables initialization is ready.");

            for (int i = 0; i < clearKeys.Count; i++)
            {
                AsyncOperationHandle<bool> handle = Addressables.ClearDependencyCacheAsync(clearKeys[i], false);
                try
                {
                    bool clearResult = await handle.ToUniTask(cancellationToken: cancellationToken);
                    if (handle.Status != AsyncOperationStatus.Succeeded || !clearResult)
                    {
                        string error = handle.OperationException?.Message ??
                                       $"Addressables.ClearDependencyCacheAsync failed. Key={clearKeys[i]}";
                        return FailOperation(error, AddressableHotUpdateStatus.CacheClearFailed,
                            stopwatch.ElapsedMilliseconds, keyStrings);
                    }
                }
                catch (OperationCanceledException)
                {
                    return FailOperation("Addressables dependency cache clear cancelled.",
                        AddressableHotUpdateStatus.Cancelled, stopwatch.ElapsedMilliseconds, keyStrings);
                }
                catch (Exception ex)
                {
                    return FailOperation(ex.Message, AddressableHotUpdateStatus.Exception,
                        stopwatch.ElapsedMilliseconds, keyStrings);
                }
                finally
                {
                    ReleaseIfValid(handle);
                }
            }

            stopwatch.Stop();
            Report($"Cleared dependency cache. Count={clearKeys.Count}, Elapsed={stopwatch.ElapsedMilliseconds}ms");
            AddressableOperationResult result = AddressableOperationResult.Ok(stopwatch.ElapsedMilliseconds);
            result.Keys = keyStrings;
            return result;
#else
            await UniTask.CompletedTask;
            return FailOperation("Addressables is unavailable. Install Addressables and enable UNITY_ADDRESSABLES.",
                AddressableHotUpdateStatus.AddressablesUnavailable, stopwatch.ElapsedMilliseconds);
#endif
        }

        public async UniTask<UpdateCheckResult> CheckUpdateAsync(IEnumerable<object> keys = null)
        {
            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            return await CheckCatalogUpdatesAsync(keys, settings.AddressablesUpdateCatalogsOnCheck);
        }

        public async UniTask<bool> DownloadUpdateAsync(IEnumerable<object> keys, Action<float> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            AddressableDownloadResult result = await DownloadDependenciesAsync(keys, onProgress, cancellationToken);
            return result.Success;
        }

        private AddressableOperationResult FailOperation(string error, AddressableHotUpdateStatus status,
            long elapsedMilliseconds, List<string> keys = null)
        {
            _lastError = error;
            ReportError(error);
            AddressableOperationResult result =
                AddressableOperationResult.Fail(error, status, elapsedMilliseconds);
            result.Keys = keys;
            return result;
        }

        private void BeginReport(string operation)
        {
            _lastReport.Clear();
            _lastError = null;
            Report($"Operation={operation}");
        }

        private void Report(string message)
        {
            _lastReport.Add(message);
            LogKit.Log($"[AAHotUpdate] {message}");
        }

        private void ReportError(string message)
        {
            _lastError = message;
            _lastReport.Add("ERROR: " + message);
            LogKit.LogError($"[AAHotUpdate] {message}");
        }

        private static List<object> NormalizeKeys(IEnumerable<object> keys)
        {
            List<object> result = new List<object>();
            if (keys == null)
            {
                return result;
            }

            foreach (object key in keys)
            {
                if (key == null)
                {
                    continue;
                }

                if (key is string stringKey)
                {
                    if (!string.IsNullOrWhiteSpace(stringKey))
                    {
                        result.Add(stringKey.Trim());
                    }

                    continue;
                }

                result.Add(key);
            }

            return result;
        }

        private static List<string> ToKeyStrings(IEnumerable<object> keys)
        {
            List<string> result = new List<string>();
            if (keys == null)
            {
                return result;
            }

            foreach (object key in keys)
            {
                if (key == null)
                {
                    continue;
                }

                string text = key.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(text.Trim());
                }
            }

            return result;
        }

#if UNITY_ADDRESSABLES
        private static void ReleaseIfValid(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
#endif
    }
}
