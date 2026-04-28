using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// 基于 ResKit 的默认 UI 加载策略。
    /// </summary>
    public class ResKitUILoadStrategy : IUILoadStrategy
    {
        private const string UI_ROOT_PATH = "UIPanel/UIRoot";
        private const string PANEL_PREFIX = "UIPanel/";

        private readonly IResLoader _loader;
        private readonly bool _ownsLoader;
        private bool _isReleased;

        public bool SupportSyncLoad => _loader != null && !_isReleased;

        public ResKitUILoadStrategy(IResLoader loader)
        {
            _loader = loader;
            _ownsLoader = false;

            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] Initialize failed: loader is null");
            }
        }

        public ResKitUILoadStrategy()
        {
            _loader = ResKit.Allocate<ResourceLoader>();
            _ownsLoader = true;

            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] Initialize failed: default ResourceLoader allocation returned null");
            }
        }

        public GameObject LoadUIRoot()
        {
            if (!EnsureLoaderAvailable(nameof(LoadUIRoot), UI_ROOT_PATH))
            {
                return null;
            }

            return _loader.Load<GameObject>(UI_ROOT_PATH);
        }

        public async UniTask<GameObject> LoadUIRootAsync(CancellationToken cancellationToken = default)
        {
            if (!EnsureLoaderAvailable(nameof(LoadUIRootAsync), UI_ROOT_PATH))
            {
                return null;
            }

            return await _loader.LoadAsync<GameObject>(UI_ROOT_PATH, cancellationToken);
        }

        public GameObject LoadPanelPrefab(string panelName)
        {
            if (!EnsureLoaderAvailable(nameof(LoadPanelPrefab), panelName))
            {
                return null;
            }

            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError("[ResKitUILoadStrategy] LoadPanelPrefab failed: panelName is null or empty");
                return null;
            }

            return _loader.Load<GameObject>(PANEL_PREFIX + panelName);
        }

        public async UniTask<GameObject> LoadPanelPrefabAsync(string panelName,
            CancellationToken cancellationToken = default)
        {
            if (!EnsureLoaderAvailable(nameof(LoadPanelPrefabAsync), panelName))
            {
                return null;
            }

            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError("[ResKitUILoadStrategy] LoadPanelPrefabAsync failed: panelName is null or empty");
                return null;
            }

            return await _loader.LoadAsync<GameObject>(PANEL_PREFIX + panelName, cancellationToken);
        }

        public void UnloadPanelPrefab(string panelName)
        {
            if (!EnsureLoaderAvailable(nameof(UnloadPanelPrefab), panelName))
            {
                return;
            }

            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError("[ResKitUILoadStrategy] UnloadPanelPrefab failed: panelName is null or empty");
                return;
            }

            _loader.Unload(PANEL_PREFIX + panelName);
        }

        public void ReleaseAll()
        {
            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] ReleaseAll failed: loader is null");
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

        private bool EnsureLoaderAvailable(string apiName, string target)
        {
            if (_loader == null)
            {
                Debug.LogError($"[ResKitUILoadStrategy] {apiName} failed: loader is null, Target={target}");
                return false;
            }

            if (_isReleased)
            {
                Debug.LogError(
                    $"[ResKitUILoadStrategy] {apiName} failed: strategy has already been released, Target={target}");
                return false;
            }

            return true;
        }
    }
}
