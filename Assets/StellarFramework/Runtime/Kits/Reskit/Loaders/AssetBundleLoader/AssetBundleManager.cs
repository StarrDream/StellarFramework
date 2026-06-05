using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework.Res.AB
{
    public enum AssetBundleManagerState
    {
        Uninitialized,
        Initializing,
        Initialized,
        Failed
    }

    internal enum AssetBundleLoadState
    {
        Unloaded,
        Loading,
        Loaded,
        Failed
    }

    [Singleton]
    public class AssetBundleManager : Singleton<AssetBundleManager>
    {
        private const string SHADER_BUNDLE_NAME = "shaders";

        private sealed class BundleRecord
        {
            public string BundleName;
            public AssetBundle Bundle;
            public int RefCount;
            public AssetBundleLoadState State;
            public UniTaskCompletionSource<AssetBundle> LoadingSource;
            public string LastError;
            public string[] Dependencies = Array.Empty<string>();
        }

        private readonly Dictionary<string, BundleRecord> _bundleRecords =
            new Dictionary<string, BundleRecord>(StringComparer.Ordinal);

        private readonly Dictionary<string, string[]> _dependenciesCache =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        private AssetBundleManifest _manifest;
        private Dictionary<string, string> _assetPathToBundleMap;
        private UniTaskCompletionSource<bool> _initCompletionSource;

        private AssetBundleManagerState _state = AssetBundleManagerState.Uninitialized;
        private string _lastError;
        private string _basePath = Application.streamingAssetsPath.Replace('\\', '/') + "/AssetBundles";
        private AssetBundleUnloadMode _unloadMode = AssetBundleUnloadMode.PreserveLoadedAssets;

        public AssetBundleManagerState State => _state;
        public string LastError => _lastError;
        public AssetBundleUnloadMode UnloadMode => _unloadMode;

        private string BasePath => _basePath;

        private string PlatformName
        {
            get
            {
#if UNITY_EDITOR
                return GetPlatformName(UnityEditor.EditorUserBuildSettings.activeBuildTarget);
#else
                return GetPlatformName(Application.platform);
#endif
            }
        }

        public override void OnSingletonInit()
        {
            Configure(ResKitRuntimeSettings.LoadOrCreateDefault());
            EnsureAssetMap();

#if UNITY_WEBGL && !UNITY_EDITOR
            LogKit.LogWarning("[AssetBundleManager] WebGL 环境跳过同步初始化，请在启动阶段调用 InitAsync。");
#else
            InitSync();
#endif
        }

        public void Configure(ResKitRuntimeSettings settings)
        {
            if (settings == null)
            {
                settings = ResKitRuntimeSettings.LoadOrCreateDefault();
            }

            _basePath = ResolveBasePath(settings);
            Configure(settings.AssetBundleUnloadMode);
        }

        private static string ResolveBasePath(ResKitRuntimeSettings settings)
        {
            string defaultPath = (Application.streamingAssetsPath + "/AssetBundles").Replace('\\', '/');
            if (settings == null)
            {
                return defaultPath;
            }

            string configuredPath = settings.AssetBundleRootPath;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return defaultPath;
            }

            string normalizedPath = configuredPath.Trim().Replace('\\', '/').TrimEnd('/');
            if (Path.IsPathRooted(normalizedPath))
            {
                return normalizedPath;
            }

            return (Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + normalizedPath).Replace('\\', '/');
        }

        public void Configure(AssetBundleUnloadMode unloadMode)
        {
            if (_state == AssetBundleManagerState.Initializing)
            {
                LogKit.LogError($"[AssetBundleManager] Configure 失败: 初始化中禁止修改卸载策略, Mode={unloadMode}");
                return;
            }

            _unloadMode = unloadMode;
        }

        public async UniTask<bool> InitAsync(CancellationToken cancellationToken = default)
        {
            if (_state == AssetBundleManagerState.Initialized)
            {
                return true;
            }

            if (_state == AssetBundleManagerState.Initializing && _initCompletionSource != null)
            {
                return await _initCompletionSource.Task.AttachExternalCancellation(cancellationToken);
            }

            _state = AssetBundleManagerState.Initializing;
            _lastError = null;
            _initCompletionSource = new UniTaskCompletionSource<bool>();

            try
            {
                EnsureAssetMap();

                bool manifestLoaded = await LoadManifestAsync(cancellationToken);
                if (!manifestLoaded)
                {
                    SetFailed("[AssetBundleManager] 初始化失败: Manifest 加载失败");
                    _initCompletionSource.TrySetResult(false);
                    return false;
                }

                await LoadGlobalShadersAsync(cancellationToken);

                _state = AssetBundleManagerState.Initialized;
                _initCompletionSource.TrySetResult(true);
                LogKit.Log("[AssetBundleManager] 异步初始化完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                _state = AssetBundleManagerState.Uninitialized;
                _lastError = "[AssetBundleManager] 初始化已取消";
                _initCompletionSource.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                SetFailed($"[AssetBundleManager] 初始化异常: {ex.Message}");
                _initCompletionSource.TrySetResult(false);
                return false;
            }
            finally
            {
                _initCompletionSource = null;
            }
        }

        public UnityEngine.Object LoadAssetSync(string assetPath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LogKit.LogError($"[AssetBundleManager] WebGL 不支持同步加载资源: {assetPath}。请改用 LoadAssetAsync。");
            return null;
#else
            if (string.IsNullOrEmpty(assetPath))
            {
                LogKit.LogError("[AssetBundleManager] 同步加载失败: assetPath 为空");
                return null;
            }

            if (!EnsureInitializedForSync())
            {
                return null;
            }

            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName))
            {
                LogKit.LogError($"[AssetBundleManager] 未注册资源: {assetPath}");
                return null;
            }

            List<string> acquiredBundles = new List<string>(8);
            if (!LoadBundleRecursiveSync(bundleName, acquiredBundles))
            {
                ReleaseAcquiredBundles(acquiredBundles);
                return null;
            }

            BundleRecord record = GetRecord(bundleName);
            UnityEngine.Object asset = record.Bundle != null ? record.Bundle.LoadAsset(assetPath) : null;
            if (asset == null)
            {
                LogKit.LogError($"[AssetBundleManager] 资源加载空: {assetPath}");
                ReleaseAcquiredBundles(acquiredBundles);
            }

            return asset;
#endif
        }

        public async UniTask<UnityEngine.Object> LoadAssetAsync(string assetPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                LogKit.LogError("[AssetBundleManager] 异步加载失败: assetPath 为空");
                return null;
            }

            if (!await InitAsync(cancellationToken))
            {
                return null;
            }

            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName))
            {
                LogKit.LogError($"[AssetBundleManager] 未注册资源: {assetPath}");
                return null;
            }

            List<string> acquiredBundles = new List<string>(8);
            try
            {
                if (!await LoadBundleRecursiveAsync(bundleName, acquiredBundles, cancellationToken))
                {
                    ReleaseAcquiredBundles(acquiredBundles);
                    return null;
                }

                BundleRecord record = GetRecord(bundleName);
                if (record.Bundle == null)
                {
                    ReleaseAcquiredBundles(acquiredBundles);
                    return null;
                }

                AssetBundleRequest request = record.Bundle.LoadAssetAsync(assetPath);
                await request.ToUniTask(cancellationToken: cancellationToken);

                if (request.asset == null)
                {
                    LogKit.LogError($"[AssetBundleManager] 资源加载空: {assetPath}");
                    ReleaseAcquiredBundles(acquiredBundles);
                }

                return request.asset;
            }
            catch (OperationCanceledException)
            {
                ReleaseAcquiredBundles(acquiredBundles);
                throw;
            }
        }

        public void UnloadAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (_assetPathToBundleMap == null || !_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName))
            {
                return;
            }

            UnloadBundleRecursive(bundleName);
        }

        public string TakeSnapshot()
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("========== [AssetBundleManager] Snapshot ==========");
            sb.AppendLine($"State={_state}, UnloadMode={_unloadMode}, LastError={_lastError ?? "None"}");
            sb.AppendLine($"ManifestLoaded={_manifest != null}, RegisteredAssets={_assetPathToBundleMap?.Count ?? 0}");

            foreach (KeyValuePair<string, BundleRecord> pair in _bundleRecords)
            {
                BundleRecord record = pair.Value;
                sb.AppendLine(
                    $"Bundle={record.BundleName}, State={record.State}, RefCount={record.RefCount}, Loaded={record.Bundle != null}, LastError={record.LastError ?? "None"}");
            }

            sb.AppendLine("==================================================");
            string snapshot = sb.ToString();
            LogKit.Log(snapshot);
            return snapshot;
        }

        private void EnsureAssetMap()
        {
            if (_assetPathToBundleMap != null)
            {
                return;
            }

            _assetPathToBundleMap = AssetMap.GetMap();
            if (_assetPathToBundleMap == null)
            {
                LogKit.LogError("[AssetBundleManager] AssetMap 未初始化，请先生成代码。");
                _assetPathToBundleMap = new Dictionary<string, string>();
            }
        }

        private bool EnsureInitializedForSync()
        {
            if (_state == AssetBundleManagerState.Initialized)
            {
                return true;
            }

            if (_state == AssetBundleManagerState.Initializing)
            {
                LogKit.LogError("[AssetBundleManager] 同步加载失败: 当前正在异步初始化中，请统一使用异步链路。");
                return false;
            }

            return InitSync();
        }

        private bool InitSync()
        {
            if (_state == AssetBundleManagerState.Initialized)
            {
                return true;
            }

            _state = AssetBundleManagerState.Initializing;
            _lastError = null;
            EnsureAssetMap();

            if (!LoadManifestSync())
            {
                SetFailed("[AssetBundleManager] 同步初始化失败: Manifest 加载失败");
                return false;
            }

            LoadGlobalShadersSync();
            _state = AssetBundleManagerState.Initialized;
            LogKit.Log("[AssetBundleManager] 同步初始化完成");
            return true;
        }

        private bool LoadManifestSync()
        {
            if (_manifest != null)
            {
                return true;
            }

            string platform = PlatformName;
            string manifestPath = $"{BasePath}/{platform}/{platform}";

            AssetBundle bundle = AssetBundle.LoadFromFile(manifestPath);
            if (bundle == null)
            {
                string altPath = $"{BasePath}/{platform}/AssetBundleManifest";
                bundle = AssetBundle.LoadFromFile(altPath);
            }

            if (bundle == null)
            {
                _lastError = $"Manifest 加载失败，请检查路径: {manifestPath}";
                LogKit.LogError($"[AssetBundleManager] {_lastError}");
                return false;
            }

            _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            bundle.Unload(false);

            if (_manifest != null)
            {
                return true;
            }

            _lastError = $"Manifest Bundle 中缺少 AssetBundleManifest: {manifestPath}";
            LogKit.LogError($"[AssetBundleManager] {_lastError}");
            return false;
        }

        private async UniTask<bool> LoadManifestAsync(CancellationToken cancellationToken)
        {
            if (_manifest != null)
            {
                return true;
            }

            string platform = PlatformName;
            string manifestPath = $"{BasePath}/{platform}/{platform}";

            AssetBundle bundle = await LoadBundlePlatformSafeAsync(manifestPath, cancellationToken);
            if (bundle == null)
            {
                string altPath = $"{BasePath}/{platform}/AssetBundleManifest";
                bundle = await LoadBundlePlatformSafeAsync(altPath, cancellationToken);
            }

            if (bundle == null)
            {
                _lastError = $"Manifest 异步加载失败: {manifestPath}";
                LogKit.LogError($"[AssetBundleManager] {_lastError}");
                return false;
            }

            _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            bundle.Unload(false);

            if (_manifest != null)
            {
                return true;
            }

            _lastError = $"Manifest Bundle 中缺少 AssetBundleManifest: {manifestPath}";
            LogKit.LogError($"[AssetBundleManager] {_lastError}");
            return false;
        }

        private void LoadGlobalShadersSync()
        {
            if (GetRecord(SHADER_BUNDLE_NAME).State == AssetBundleLoadState.Loaded)
            {
                return;
            }

            if (!HasBundleInManifest(SHADER_BUNDLE_NAME))
            {
                LogKit.Log("[AssetBundleManager] 当前构建产物中不包含 shaders 包，跳过预热。");
                return;
            }

            string path = $"{BasePath}/{PlatformName}/{SHADER_BUNDLE_NAME}";
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            ProcessShaderBundle(bundle);
        }

        private async UniTask LoadGlobalShadersAsync(CancellationToken cancellationToken)
        {
            if (GetRecord(SHADER_BUNDLE_NAME).State == AssetBundleLoadState.Loaded)
            {
                return;
            }

            if (!HasBundleInManifest(SHADER_BUNDLE_NAME))
            {
                LogKit.Log("[AssetBundleManager] 当前构建产物中不包含 shaders 包，跳过预热。");
                return;
            }

            string path = $"{BasePath}/{PlatformName}/{SHADER_BUNDLE_NAME}";
            AssetBundle bundle = await LoadBundlePlatformSafeAsync(path, cancellationToken);
            ProcessShaderBundle(bundle);
        }

        private void ProcessShaderBundle(AssetBundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            bundle.LoadAllAssets();
            ShaderVariantCollection[] svcs = bundle.LoadAllAssets<ShaderVariantCollection>();
            for (int i = 0; i < svcs.Length; i++)
            {
                if (!svcs[i].isWarmedUp)
                {
                    svcs[i].WarmUp();
                }
            }

            BundleRecord record = GetRecord(SHADER_BUNDLE_NAME);
            if (record.Bundle != null && record.Bundle != bundle)
            {
                bundle.Unload(false);
                return;
            }

            record.Bundle = bundle;
            record.RefCount = int.MaxValue;
            record.State = AssetBundleLoadState.Loaded;
            record.LastError = null;
            LogKit.Log("[AssetBundleManager] Shader 预热完成");
        }

        private bool LoadBundleRecursiveSync(string bundleName, List<string> acquiredBundles)
        {
            string[] deps = GetCachedDependencies(bundleName);
            for (int i = 0; i < deps.Length; i++)
            {
                if (!LoadBundleRecursiveSync(deps[i], acquiredBundles))
                {
                    return false;
                }
            }

            return LoadBundleSyncInternal(bundleName, acquiredBundles);
        }

        private bool LoadBundleSyncInternal(string bundleName, List<string> acquiredBundles)
        {
            BundleRecord record = GetRecord(bundleName);
            if (record.State == AssetBundleLoadState.Loaded && record.Bundle != null)
            {
                IncreaseRefCount(record);
                acquiredBundles?.Add(bundleName);
                return true;
            }

            if (record.State == AssetBundleLoadState.Loading)
            {
                record.LastError =
                    $"Bundle '{bundleName}' 正在异步加载中，严禁在此时发起同步加载请求。请统一业务层加载链路。";
                LogKit.LogError($"[AssetBundleManager] 致命并发冲突: {record.LastError}");
                return false;
            }

            string path = GetBundlePath(bundleName);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                MarkBundleFailed(record, $"同步加载 Bundle 失败: {path}");
                return false;
            }

            record.Bundle = bundle;
            record.RefCount = 1;
            record.State = AssetBundleLoadState.Loaded;
            record.LastError = null;
            record.Dependencies = GetCachedDependencies(bundleName);
            acquiredBundles?.Add(bundleName);
            return true;
        }

        private async UniTask<bool> LoadBundleRecursiveAsync(string bundleName, List<string> acquiredBundles,
            CancellationToken cancellationToken)
        {
            string[] deps = GetCachedDependencies(bundleName);
            for (int i = 0; i < deps.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await LoadBundleRecursiveAsync(deps[i], acquiredBundles, cancellationToken))
                {
                    return false;
                }
            }

            return await LoadBundleAsyncInternal(bundleName, acquiredBundles, cancellationToken);
        }

        private async UniTask<bool> LoadBundleAsyncInternal(string bundleName, List<string> acquiredBundles,
            CancellationToken cancellationToken)
        {
            BundleRecord record = GetRecord(bundleName);
            if (record.State == AssetBundleLoadState.Loaded && record.Bundle != null)
            {
                IncreaseRefCount(record);
                acquiredBundles?.Add(bundleName);
                return true;
            }

            if (record.State == AssetBundleLoadState.Loading && record.LoadingSource != null)
            {
                AssetBundle existingBundle = await record.LoadingSource.Task.AttachExternalCancellation(cancellationToken);
                if (existingBundle == null)
                {
                    return false;
                }

                IncreaseRefCount(record);
                acquiredBundles?.Add(bundleName);
                return true;
            }

            record.State = AssetBundleLoadState.Loading;
            record.LastError = null;
            record.LoadingSource = new UniTaskCompletionSource<AssetBundle>();

            try
            {
                AssetBundle bundle = await LoadBundlePlatformSafeAsync(GetBundlePath(bundleName), cancellationToken);
                if (bundle == null)
                {
                    MarkBundleFailed(record, $"异步加载 Bundle 失败: {GetBundlePath(bundleName)}");
                    record.LoadingSource.TrySetResult(null);
                    return false;
                }

                record.Bundle = bundle;
                record.RefCount = 1;
                record.State = AssetBundleLoadState.Loaded;
                record.Dependencies = GetCachedDependencies(bundleName);
                record.LastError = null;
                record.LoadingSource.TrySetResult(bundle);
                acquiredBundles?.Add(bundleName);
                return true;
            }
            catch (OperationCanceledException)
            {
                record.State = AssetBundleLoadState.Unloaded;
                record.LastError = "异步加载 Bundle 已取消";
                record.LoadingSource.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                MarkBundleFailed(record, $"异步加载异常: {bundleName}\n{ex.Message}");
                record.LoadingSource.TrySetResult(null);
                return false;
            }
            finally
            {
                record.LoadingSource = null;
            }
        }

        private async UniTask<AssetBundle> LoadBundlePlatformSafeAsync(string pathOrUrl,
            CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            using (UnityWebRequest uwr = UnityWebRequestAssetBundle.GetAssetBundle(pathOrUrl))
            {
                await uwr.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    LogKit.LogError($"[AssetBundleManager] WebGL 网络加载失败: {pathOrUrl}\n{uwr.error}");
                    return null;
                }

                return DownloadHandlerAssetBundle.GetContent(uwr);
            }
#else
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(pathOrUrl);
            if (request == null)
            {
                return null;
            }

            await request.ToUniTask(cancellationToken: cancellationToken);
            return request.assetBundle;
#endif
        }

        private void UnloadBundleRecursive(string bundleName)
        {
            UnloadBundleSingle(bundleName);

            string[] deps = GetCachedDependencies(bundleName);
            for (int i = 0; i < deps.Length; i++)
            {
                UnloadBundleRecursive(deps[i]);
            }
        }

        private void UnloadBundleSingle(string bundleName)
        {
            if (string.Equals(bundleName, SHADER_BUNDLE_NAME, StringComparison.Ordinal))
            {
                return;
            }

            if (!_bundleRecords.TryGetValue(bundleName, out BundleRecord record) ||
                record.State != AssetBundleLoadState.Loaded)
            {
                return;
            }

            record.RefCount--;
            if (record.RefCount > 0)
            {
                return;
            }

            if (record.RefCount < 0)
            {
                LogKit.LogError($"[AssetBundleManager] Bundle 引用计数为负，已强制归零: {bundleName}");
            }

            record.RefCount = 0;
            if (record.Bundle != null)
            {
                record.Bundle.Unload(_unloadMode == AssetBundleUnloadMode.DestroyLoadedAssets);
            }

            record.Bundle = null;
            record.State = AssetBundleLoadState.Unloaded;
            record.LastError = null;
        }

        private void ReleaseAcquiredBundles(List<string> acquiredBundles)
        {
            if (acquiredBundles == null)
            {
                return;
            }

            for (int i = acquiredBundles.Count - 1; i >= 0; i--)
            {
                UnloadBundleSingle(acquiredBundles[i]);
            }

            acquiredBundles.Clear();
        }

        private BundleRecord GetRecord(string bundleName)
        {
            if (!_bundleRecords.TryGetValue(bundleName, out BundleRecord record))
            {
                record = new BundleRecord
                {
                    BundleName = bundleName,
                    State = AssetBundleLoadState.Unloaded,
                    Dependencies = Array.Empty<string>()
                };
                _bundleRecords.Add(bundleName, record);
            }

            return record;
        }

        private void IncreaseRefCount(BundleRecord record)
        {
            if (record.RefCount < int.MaxValue)
            {
                record.RefCount++;
            }
        }

        private string[] GetCachedDependencies(string bundleName)
        {
            if (_dependenciesCache.TryGetValue(bundleName, out string[] deps))
            {
                return deps;
            }

            if (_manifest == null)
            {
                deps = Array.Empty<string>();
            }
            else
            {
                deps = _manifest.GetAllDependencies(bundleName) ?? Array.Empty<string>();
            }

            _dependenciesCache[bundleName] = deps;
            return deps;
        }

        private bool HasBundleInManifest(string bundleName)
        {
            if (_manifest == null)
            {
                return false;
            }

            string[] allBundles = _manifest.GetAllAssetBundles();
            return Array.IndexOf(allBundles, bundleName) >= 0;
        }

        private string GetBundlePath(string bundleName)
        {
            string lowerBundleName = bundleName.ToLowerInvariant();
            return $"{BasePath}/{PlatformName}/{lowerBundleName}";
        }

        private void MarkBundleFailed(BundleRecord record, string error)
        {
            record.State = AssetBundleLoadState.Failed;
            record.LastError = error;
            record.RefCount = 0;
            record.Bundle = null;
            LogKit.LogError($"[AssetBundleManager] {error}");
        }

        private void SetFailed(string error)
        {
            _state = AssetBundleManagerState.Failed;
            _lastError = error;
            LogKit.LogError(error);
        }

#if UNITY_EDITOR
        private string GetPlatformName(UnityEditor.BuildTarget target)
        {
            switch (target)
            {
                case UnityEditor.BuildTarget.Android: return "Android";
                case UnityEditor.BuildTarget.iOS: return "iOS";
                case UnityEditor.BuildTarget.StandaloneWindows:
                case UnityEditor.BuildTarget.StandaloneWindows64: return "Windows";
                case UnityEditor.BuildTarget.StandaloneOSX: return "OSX";
                case UnityEditor.BuildTarget.WebGL: return "WebGL";
                default: return "Unknown";
            }
        }
#else
        private string GetPlatformName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android: return "Android";
                case RuntimePlatform.IPhonePlayer: return "iOS";
                case RuntimePlatform.WindowsPlayer: return "Windows";
                case RuntimePlatform.OSXPlayer: return "OSX";
                case RuntimePlatform.WebGLPlayer: return "WebGL";
                default: return "Unknown";
            }
        }
#endif
    }
}
