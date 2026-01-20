using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework.Res.AB
{
    public class AssetBundleManager : Singleton<AssetBundleManager>
    {
        private const string SHADER_BUNDLE_NAME = "shaders";

        private AssetBundleManifest _manifest;
        private Dictionary<string, string> _assetPathToBundleMap;

        // 缓存
        private readonly Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();
        private readonly Dictionary<string, int> _bundleRefCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, string[]> _dependenciesCache = new Dictionary<string, string[]>();
        private readonly Dictionary<string, UniTask<AssetBundle>> _loadingBundles = new Dictionary<string, UniTask<AssetBundle>>();

        // 路径处理：WebGL下 StreamingAssetsPath 是 URL，不需要额外处理，但 LoadFromFile 需要本地路径
        private string BasePath => Application.streamingAssetsPath + "/AssetBundles";

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

        /// <summary>
        /// 同步初始化 (注意：WebGL 平台下此方法不会加载 Manifest 和 Shader，必须调用 InitAsync)
        /// </summary>
        public override void OnSingletonInit()
        {
            _assetPathToBundleMap = AssetMap.GetMap();
            if (_assetPathToBundleMap == null)
            {
                LogKit.LogError("[AssetBundleManager] AssetMap 未初始化，请先生成代码。");
                _assetPathToBundleMap = new Dictionary<string, string>();
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            LogKit.LogWarning("[AssetBundleManager] WebGL 环境检测：跳过同步初始化。请务必在游戏启动时调用 'await AssetBundleManager.Instance.InitAsync();'");
#else
            LoadManifestSync();
            LoadGlobalShadersSync();
#endif
        }

        /// <summary>
        /// 异步初始化 (WebGL 必用，其他平台推荐使用)
        /// </summary>
        public async UniTask InitAsync()
        {
            if (_assetPathToBundleMap == null) OnSingletonInit();

            await LoadManifestAsync();
            await LoadGlobalShadersAsync();
            LogKit.Log("[AssetBundleManager] 异步初始化完成");
        }

        #region Internal Loaders (Manifest & Shaders)

        private void LoadManifestSync()
        {
            if (_manifest != null) return;
            string platform = PlatformName;
            string manifestPath = $"{BasePath}/{platform}/{platform}";

            AssetBundle bundle = AssetBundle.LoadFromFile(manifestPath);
            if (bundle == null)
            {
                // 尝试加载备用路径
                string altPath = $"{BasePath}/{platform}/AssetBundleManifest";
                bundle = AssetBundle.LoadFromFile(altPath);
            }

            if (bundle != null)
            {
                _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                bundle.Unload(false);
            }
            else
            {
                LogKit.LogError($"[AssetBundleManager] Manifest 加载失败，请检查路径: {manifestPath}");
            }
        }

        private async UniTask LoadManifestAsync()
        {
            if (_manifest != null) return;
            string platform = PlatformName;
            string manifestPath = $"{BasePath}/{platform}/{platform}";

            AssetBundle bundle = await LoadBundlePlatformSafeAsync(manifestPath);

            if (bundle != null)
            {
                _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                bundle.Unload(false);
            }
            else
            {
                LogKit.LogError($"[AssetBundleManager] Manifest 异步加载失败: {manifestPath}");
            }
        }

        private void LoadGlobalShadersSync()
        {
            if (_loadedBundles.ContainsKey(SHADER_BUNDLE_NAME)) return;
            string path = $"{BasePath}/{PlatformName}/{SHADER_BUNDLE_NAME}";

#if UNITY_EDITOR
            if (!File.Exists(path)) return;
#endif
            var bundle = AssetBundle.LoadFromFile(path);
            ProcessShaderBundle(bundle);
        }

        private async UniTask LoadGlobalShadersAsync()
        {
            if (_loadedBundles.ContainsKey(SHADER_BUNDLE_NAME)) return;
            string path = $"{BasePath}/{PlatformName}/{SHADER_BUNDLE_NAME}";

            // WebGL 无法使用 File.Exists 预检查，直接尝试加载
            var bundle = await LoadBundlePlatformSafeAsync(path);
            ProcessShaderBundle(bundle);
        }

        private void ProcessShaderBundle(AssetBundle bundle)
        {
            if (bundle != null)
            {
                bundle.LoadAllAssets();
                var svcs = bundle.LoadAllAssets<ShaderVariantCollection>();
                foreach (var svc in svcs)
                {
                    if (!svc.isWarmedUp) svc.WarmUp();
                }

                if (!_loadedBundles.ContainsKey(SHADER_BUNDLE_NAME))
                {
                    _loadedBundles.Add(SHADER_BUNDLE_NAME, bundle);
                    _bundleRefCounts.Add(SHADER_BUNDLE_NAME, int.MaxValue); // 永不卸载
                    LogKit.Log($"[AssetBundleManager] Shader 预热完成");
                }
            }
        }

        #endregion

        #region API - Sync Load

        public UnityEngine.Object LoadAssetSync(string assetPath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LogKit.LogError($"[AssetBundleManager] WebGL 不支持同步加载资源: {assetPath}。请改用 LoadAssetAsync。");
            return null;
#else
            if (string.IsNullOrEmpty(assetPath)) return null;

            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName))
            {
                LogKit.LogError($"[AssetBundleManager] 未注册资源: {assetPath}");
                return null;
            }

            LoadBundleRecursiveSync(bundleName);

            if (_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                var obj = bundle.LoadAsset(assetPath);
                if (obj == null) LogKit.LogError($"[AssetBundleManager] 资源加载空: {assetPath}");
                return obj;
            }

            return null;
#endif
        }

        private void LoadBundleRecursiveSync(string bundleName)
        {
            var deps = GetCachedDependencies(bundleName);
            if (deps != null)
            {
                foreach (var dep in deps)
                {
                    LoadBundleRecursiveSync(dep);
                }
            }

            LoadBundleSyncInternal(bundleName);
        }

        private void LoadBundleSyncInternal(string bundleName)
        {
            if (_loadedBundles.ContainsKey(bundleName))
            {
                IncreaseRefCount(bundleName);
                return;
            }

            // 检查异步冲突
            if (_loadingBundles.TryGetValue(bundleName, out var task))
            {
                if (task.Status == UniTaskStatus.Succeeded)
                {
                    var res = task.GetAwaiter().GetResult();
                    if (res != null)
                    {
                        if (!_loadedBundles.ContainsKey(bundleName))
                        {
                            _loadedBundles.Add(bundleName, res);
                            _bundleRefCounts.Add(bundleName, 1);
                        }
                        else
                        {
                            IncreaseRefCount(bundleName);
                        }

                        return;
                    }
                }

                LogKit.LogError($"[AssetBundleManager] 严重并发冲突: Bundle '{bundleName}' 正在异步加载中，无法执行同步加载。");
                return;
            }

            string lowerBundleName = bundleName.ToLowerInvariant();
            string path = $"{BasePath}/{PlatformName}/{lowerBundleName}";

            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle != null)
            {
                if (!_loadedBundles.ContainsKey(bundleName))
                {
                    _loadedBundles.Add(bundleName, bundle);
                    _bundleRefCounts.Add(bundleName, 1);
                }
                else
                {
                    bundle.Unload(false);
                    IncreaseRefCount(bundleName);
                }
            }
            else
            {
                LogKit.LogError($"[AssetBundleManager] 同步加载 Bundle 失败: {path}");
            }
        }

        #endregion

        #region API - Async Load

        public async UniTask<UnityEngine.Object> LoadAssetAsync(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;

            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName))
            {
                LogKit.LogError($"[AssetBundleManager] 未注册资源: {assetPath}");
                return null;
            }

            await LoadBundleRecursiveAsync(bundleName);

            if (_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                var req = bundle.LoadAssetAsync(assetPath);
                await req;
                if (req.asset == null) LogKit.LogError($"[AssetBundleManager] 资源加载空: {assetPath}");
                return req.asset;
            }

            return null;
        }

        private async UniTask LoadBundleRecursiveAsync(string bundleName)
        {
            var deps = GetCachedDependencies(bundleName);
            if (deps != null && deps.Length > 0)
            {
                var tasks = new List<UniTask>(deps.Length);
                foreach (var dep in deps)
                {
                    tasks.Add(LoadBundleRecursiveAsync(dep));
                }

                await UniTask.WhenAll(tasks);
            }

            await LoadBundleAsyncInternal(bundleName);
        }

        private async UniTask LoadBundleAsyncInternal(string bundleName)
        {
            // 1. 检查缓存
            if (_loadedBundles.ContainsKey(bundleName))
            {
                IncreaseRefCount(bundleName);
                return;
            }

            // 2. 检查是否正在加载中 (去重)
            if (_loadingBundles.TryGetValue(bundleName, out var loadingTask))
            {
                await loadingTask;
                if (_loadedBundles.ContainsKey(bundleName))
                {
                    IncreaseRefCount(bundleName);
                }

                return;
            }

            string lowerBundleName = bundleName.ToLowerInvariant();
            string path = $"{BasePath}/{PlatformName}/{lowerBundleName}";

            // 3. 核心修复：根据平台选择加载方式
            var newTask = LoadBundlePlatformSafeAsync(path).Preserve();

            _loadingBundles[bundleName] = newTask;

            AssetBundle bundle = null;
            try
            {
                bundle = await newTask;
            }
            catch (Exception e)
            {
                LogKit.LogError($"[AssetBundleManager] 异步加载异常: {bundleName}\n{e}");
            }
            finally
            {
                _loadingBundles.Remove(bundleName);
            }

            // 4. 缓存处理
            if (bundle != null)
            {
                if (!_loadedBundles.ContainsKey(bundleName))
                {
                    _loadedBundles.Add(bundleName, bundle);
                    _bundleRefCounts.Add(bundleName, 1);
                }
                else
                {
                    bundle.Unload(false);
                    IncreaseRefCount(bundleName);
                }
            }
            else
            {
                LogKit.LogError($"[AssetBundleManager] 异步加载 Bundle 失败: {path}");
            }
        }

        /// <summary>
        /// 平台安全的 Bundle 加载器
        /// </summary>
        private async UniTask<AssetBundle> LoadBundlePlatformSafeAsync(string pathOrUrl)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL 必须使用 UnityWebRequest
            using (UnityWebRequest uwr = UnityWebRequestAssetBundle.GetAssetBundle(pathOrUrl))
            {
                await uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    LogKit.LogError($"[AssetBundleManager] WebGL 网络加载失败: {pathOrUrl}\n{uwr.error}");
                    return null;
                }
                return DownloadHandlerAssetBundle.GetContent(uwr);
            }
#else
            // 其他平台 (PC/Android/iOS) 使用 LoadFromFileAsync 性能更高
            return await AssetBundle.LoadFromFileAsync(pathOrUrl);
#endif
        }

        #endregion

        #region Unload Logic (Recursive)

        public void UnloadAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName)) return;

            UnloadBundleRecursive(bundleName);
        }

        private void UnloadBundleRecursive(string bundleName)
        {
            UnloadBundleSingle(bundleName);
            var deps = GetCachedDependencies(bundleName);
            if (deps != null)
            {
                foreach (var dep in deps)
                {
                    UnloadBundleRecursive(dep);
                }
            }
        }

        private void UnloadBundleSingle(string bundleName)
        {
            if (bundleName == SHADER_BUNDLE_NAME) return;

            if (_bundleRefCounts.ContainsKey(bundleName))
            {
                _bundleRefCounts[bundleName]--;
                if (_bundleRefCounts[bundleName] <= 0)
                {
                    if (_loadedBundles.TryGetValue(bundleName, out var bundle))
                    {
                        bundle.Unload(false); // true 会卸载已加载的对象，这里用 false 配合引用计数
                        _loadedBundles.Remove(bundleName);
                    }

                    _bundleRefCounts.Remove(bundleName);
                }
            }
        }

        private void IncreaseRefCount(string bundleName)
        {
            if (_bundleRefCounts.ContainsKey(bundleName))
            {
                if (_bundleRefCounts[bundleName] < int.MaxValue)
                    _bundleRefCounts[bundleName]++;
            }
        }

        #endregion

        #region Helpers

        private string[] GetCachedDependencies(string bundleName)
        {
            if (_dependenciesCache.TryGetValue(bundleName, out var deps)) return deps;
            if (_manifest == null) return null;

            deps = _manifest.GetAllDependencies(bundleName);
            _dependenciesCache[bundleName] = deps;
            return deps;
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

        #endregion
    }
}