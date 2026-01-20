using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Res.AB
{
    public class AssetBundleManager : Singleton<AssetBundleManager>
    {
        private const string SHADER_BUNDLE_NAME = "shaders";
        private AssetBundleManifest _manifest;
        private Dictionary<string, string> _assetPathToBundleMap;

        // 资源缓存
        private readonly Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();

        // 引用计数
        private readonly Dictionary<string, int> _bundleRefCounts = new Dictionary<string, int>();

        // 依赖缓存
        private readonly Dictionary<string, string[]> _dependenciesCache = new Dictionary<string, string[]>();

        // 异步任务去重
        private readonly Dictionary<string, UniTask<AssetBundle>> _loadingBundles = new Dictionary<string, UniTask<AssetBundle>>();

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

        public override void OnSingletonInit()
        {
            _assetPathToBundleMap = AssetMap.GetMap();
            if (_assetPathToBundleMap == null)
            {
                LogKit.LogError("[AssetBundleManager] AssetMap 未初始化，请先生成代码。");
                _assetPathToBundleMap = new Dictionary<string, string>();
            }

            LoadManifestSync();
            LoadGlobalShadersSync();
        }

        private void LoadManifestSync()
        {
            if (_manifest != null) return;
            string platform = PlatformName;
            string manifestPath = $"{BasePath}/{platform}/{platform}";

            AssetBundle bundle = AssetBundle.LoadFromFile(manifestPath);
            if (bundle == null)
            {
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

        private void LoadGlobalShadersSync()
        {
            if (_loadedBundles.ContainsKey(SHADER_BUNDLE_NAME)) return;
            string path = $"{BasePath}/{PlatformName}/{SHADER_BUNDLE_NAME}";

            // 优化：真机环境减少 IO Check，直接尝试加载
#if UNITY_EDITOR
            if (!File.Exists(path)) return;
#endif

            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle != null)
            {
                bundle.LoadAllAssets();
                var svcs = bundle.LoadAllAssets<ShaderVariantCollection>();
                foreach (var svc in svcs)
                {
                    if (!svc.isWarmedUp) svc.WarmUp();
                }

                _loadedBundles.Add(SHADER_BUNDLE_NAME, bundle);
                _bundleRefCounts.Add(SHADER_BUNDLE_NAME, int.MaxValue); // 永不卸载
                LogKit.Log($"[AssetBundleManager] Shader 预热完成");
            }
        }

        #region API - Sync Load

        public UnityEngine.Object LoadAssetSync(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName))
            {
                LogKit.LogError($"[AssetBundleManager] 未注册资源: {assetPath}");
                return null;
            }

            // [Fix] 移除 visited 参数，允许菱形依赖正确计数
            // 信任 Unity BuildPipeline 保证无环依赖
            LoadBundleRecursiveSync(bundleName);

            if (_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                var obj = bundle.LoadAsset(assetPath);
                if (obj == null) LogKit.LogError($"[AssetBundleManager] 资源加载空: {assetPath}");
                return obj;
            }

            return null;
        }

        private void LoadBundleRecursiveSync(string bundleName)
        {
            // 1. 先处理依赖 (深度优先)
            var deps = GetCachedDependencies(bundleName);
            if (deps != null)
            {
                foreach (var dep in deps)
                {
                    // 递归调用，确保每个路径的依赖都被计数
                    LoadBundleRecursiveSync(dep);
                }
            }

            // 2. 加载主包
            LoadBundleSyncInternal(bundleName);
        }

        private void LoadBundleSyncInternal(string bundleName)
        {
            // 情况 A: 已经加载过 -> 引用计数 +1
            if (_loadedBundles.ContainsKey(bundleName))
            {
                IncreaseRefCount(bundleName);
                return;
            }

            // 情况 B: 正在异步加载 -> 死锁保护
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

            // 情况 C: 首次加载
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

            // [Fix] 移除 visited 参数
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
            // 1. 并行加载所有依赖
            var deps = GetCachedDependencies(bundleName);
            if (deps != null && deps.Length > 0)
            {
                var tasks = new List<UniTask>(deps.Length);
                foreach (var dep in deps)
                {
                    // 递归调用
                    tasks.Add(LoadBundleRecursiveAsync(dep));
                }

                await UniTask.WhenAll(tasks);
            }

            // 2. 加载主包
            await LoadBundleAsyncInternal(bundleName);
        }

        private async UniTask LoadBundleAsyncInternal(string bundleName)
        {
            if (_loadedBundles.ContainsKey(bundleName))
            {
                IncreaseRefCount(bundleName);
                return;
            }

            // 任务去重：如果已经在加载，直接 await 那个任务
            if (_loadingBundles.TryGetValue(bundleName, out var loadingTask))
            {
                await loadingTask;
                // 任务完成后，再次检查并增加引用计数
                // 必须增加，因为当前请求也需要持有引用
                if (_loadedBundles.ContainsKey(bundleName))
                {
                    IncreaseRefCount(bundleName);
                }

                return;
            }

            string lowerBundleName = bundleName.ToLowerInvariant();
            string path = $"{BasePath}/{PlatformName}/{lowerBundleName}";

            var newTask = AssetBundle.LoadFromFileAsync(path).ToUniTask().Preserve();
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

        #endregion

        #region Unload Logic (Recursive)

        public void UnloadAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (!_assetPathToBundleMap.TryGetValue(assetPath, out string bundleName)) return;

            // [Fix] 移除 visited，确保菱形依赖能被正确减计数
            UnloadBundleRecursive(bundleName);
        }

        private void UnloadBundleRecursive(string bundleName)
        {
            // 1. 卸载自身
            UnloadBundleSingle(bundleName);

            // 2. 递归卸载依赖
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
                        bundle.Unload(false); // 彻底卸载，包括从该 Bundle 加载出的 Asset
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