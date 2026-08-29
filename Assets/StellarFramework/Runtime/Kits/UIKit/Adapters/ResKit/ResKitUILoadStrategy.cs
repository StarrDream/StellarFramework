using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// Default UIKit loading strategy backed by ResKit.
    /// Paths and backend can come from UIKitSettings/ResKitRuntimeSettings instead of hard-coded Resources paths.
    /// </summary>
    public class ResKitUILoadStrategy : IUILoadStrategy
    {
        private readonly IResLoader _loader;
        private readonly bool _ownsLoader;
        private readonly bool _supportSyncLoad;
        private readonly string _uiRootPath;
        private readonly string _panelPathFormat;
        private bool _isReleased;

        public bool SupportSyncLoad => _loader != null && !_isReleased && _supportSyncLoad;

        public ResKitUILoadStrategy(IResLoader loader)
            : this(loader, UIKitSettings.LoadOrCreateDefault(), false)
        {
        }

        public ResKitUILoadStrategy(IResLoader loader, UIKitSettings settings)
            : this(loader, settings, false)
        {
        }

        public ResKitUILoadStrategy(UIKitSettings settings)
        {
            settings = settings != null ? settings : UIKitSettings.LoadOrCreateDefault();

            ResLoaderRequest request = ResolveUIRequest(settings);

            _loader = ResKit.Allocate(request);
            _ownsLoader = true;
            _supportSyncLoad = settings.AllowSyncLoad && !IsAddressablesLoader(_loader);
            _uiRootPath = ResolveUIRootPath(settings);
            _panelPathFormat = ResolvePanelPathFormat(settings);

            if (_loader == null)
            {
                Debug.LogError(
                    $"[ResKitUILoadStrategy] Initialize failed: backend allocation returned null. Backend={request.Backend}, CustomKey={request.CustomKey ?? "null"}");
            }
        }

        public ResKitUILoadStrategy()
            : this(UIKitSettings.LoadOrCreateDefault())
        {
        }

        private static bool IsAddressablesLoader(IResLoader loader)
        {
            return loader is ResLoader resLoader
                   && string.Equals(resLoader.LoaderName, "Addressables", StringComparison.Ordinal);
        }

        private ResKitUILoadStrategy(IResLoader loader, UIKitSettings settings, bool ownsLoader)
        {
            settings = settings != null ? settings : UIKitSettings.LoadOrCreateDefault();
            _loader = loader;
            _ownsLoader = ownsLoader;
            _supportSyncLoad = settings.AllowSyncLoad && !IsAddressablesLoader(loader);
            _uiRootPath = ResolveUIRootPath(settings);
            _panelPathFormat = ResolvePanelPathFormat(settings);

            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] Initialize failed: loader is null.");
            }
        }

        public GameObject LoadUIRoot()
        {
            if (!EnsureLoaderAvailable(nameof(LoadUIRoot), _uiRootPath))
            {
                return null;
            }

            if (!SupportSyncLoad)
            {
                Debug.LogError("[ResKitUILoadStrategy] LoadUIRoot failed: current strategy does not support sync load.");
                return null;
            }

            return _loader.Load<GameObject>(_uiRootPath);
        }

        public async UniTask<GameObject> LoadUIRootAsync(CancellationToken cancellationToken = default)
        {
            if (!EnsureLoaderAvailable(nameof(LoadUIRootAsync), _uiRootPath))
            {
                return null;
            }

            return await _loader.LoadAsync<GameObject>(_uiRootPath, cancellationToken);
        }

        public GameObject LoadPanelPrefab(string panelName)
        {
            string path = BuildPanelPath(panelName);
            if (!EnsurePanelRequest(nameof(LoadPanelPrefab), panelName, path))
            {
                return null;
            }

            if (!SupportSyncLoad)
            {
                Debug.LogError(
                    $"[ResKitUILoadStrategy] LoadPanelPrefab failed: current strategy does not support sync load. Panel={panelName}");
                return null;
            }

            return _loader.Load<GameObject>(path);
        }

        public async UniTask<GameObject> LoadPanelPrefabAsync(string panelName,
            CancellationToken cancellationToken = default)
        {
            string path = BuildPanelPath(panelName);
            if (!EnsurePanelRequest(nameof(LoadPanelPrefabAsync), panelName, path))
            {
                return null;
            }

            return await _loader.LoadAsync<GameObject>(path, cancellationToken);
        }

        public void UnloadPanelPrefab(string panelName)
        {
            string path = BuildPanelPath(panelName);
            if (!EnsurePanelRequest(nameof(UnloadPanelPrefab), panelName, path))
            {
                return;
            }

            _loader.Unload(path);
        }

        public void ReleaseAll()
        {
            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] ReleaseAll failed: loader is null.");
                return;
            }

            if (_isReleased)
            {
                return;
            }

            _loader.ReleaseAll();

            if (_ownsLoader)
            {
                ResKit.Recycle(_loader);
            }

            _isReleased = true;
        }

        private static ResLoaderRequest ResolveUIRequest(UIKitSettings settings)
        {
            ResLoadBackend backend = settings != null
                ? (ResLoadBackend)settings.LegacyResLoadBackendValue
                : ResLoadBackend.Default;
            if (backend != ResLoadBackend.Default)
            {
                return backend == ResLoadBackend.Custom
                    ? ResLoaderRequest.Custom(settings != null ? settings.CustomLoaderKey : null, "UIKit")
                    : ResLoaderRequest.For(backend, "UIKit");
            }

            ResKitRuntimeSettings resSettings = ResKitRuntimeSettings.LoadOrCreateDefault();
            if (resSettings != null && resSettings.DefaultUILoadBackend != ResLoadBackend.Default)
            {
                return resSettings.DefaultUILoadBackend == ResLoadBackend.Custom
                    ? ResLoaderRequest.Custom(resSettings.DefaultUICustomLoaderKey, "UIKit")
                    : ResLoaderRequest.For(resSettings.DefaultUILoadBackend, "UIKit");
            }

            return ResLoaderRequest.For(ResLoadBackend.Resources, "UIKit");
        }

        private static string ResolveUIRootPath(UIKitSettings settings)
        {
            if (settings != null && !string.IsNullOrWhiteSpace(settings.UIRootPath))
            {
                return settings.UIRootPath.Trim();
            }

            ResKitRuntimeSettings resSettings = ResKitRuntimeSettings.LoadOrCreateDefault();
            if (resSettings != null && !string.IsNullOrWhiteSpace(resSettings.UIRootPath))
            {
                return resSettings.UIRootPath.Trim();
            }

            return "UIPanel/UIRoot";
        }

        private static string ResolvePanelPathFormat(UIKitSettings settings)
        {
            if (settings != null && !string.IsNullOrWhiteSpace(settings.PanelPathFormat) &&
                settings.PanelPathFormat.Contains("{0}"))
            {
                return settings.PanelPathFormat.Trim();
            }

            ResKitRuntimeSettings resSettings = ResKitRuntimeSettings.LoadOrCreateDefault();
            if (resSettings != null && !string.IsNullOrWhiteSpace(resSettings.UIPanelPathFormat) &&
                resSettings.UIPanelPathFormat.Contains("{0}"))
            {
                return resSettings.UIPanelPathFormat.Trim();
            }

            return "UIPanel/{0}";
        }

        private string BuildPanelPath(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
            {
                return string.Empty;
            }

            return string.Format(_panelPathFormat, panelName);
        }

        private bool EnsurePanelRequest(string apiName, string panelName, string path)
        {
            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError($"[ResKitUILoadStrategy] {apiName} failed: panelName is null or empty.");
                return false;
            }

            return EnsureLoaderAvailable(apiName, path);
        }

        private bool EnsureLoaderAvailable(string apiName, string target)
        {
            if (_loader == null)
            {
                Debug.LogError($"[ResKitUILoadStrategy] {apiName} failed: loader is null. Target={target}");
                return false;
            }

            if (_isReleased)
            {
                Debug.LogError(
                    $"[ResKitUILoadStrategy] {apiName} failed: strategy has already been released. Target={target}");
                return false;
            }

            if (string.IsNullOrEmpty(target))
            {
                Debug.LogError($"[ResKitUILoadStrategy] {apiName} failed: target path is empty.");
                return false;
            }

            return true;
        }
    }
}
