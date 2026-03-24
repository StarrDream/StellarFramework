using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// 基于 ResKit 的默认 UI 加载策略
    /// 我把路径规则和资源细节封装在策略内部，避免 UIKit 继续依赖具体资源实现
    /// </summary>
    public class ResKitUILoadStrategy : IUILoadStrategy
    {
        private const string UI_ROOT_PATH = "UIPanel/UIRoot";
        private const string PANEL_PREFIX = "UIPanel/";

        private readonly IResLoader _loader;
        private readonly bool _ownsLoader;

        public bool SupportSyncLoad => _loader != null;

        /// <summary>
        /// 我允许外部传入自己的 IResLoader，这样业务方可以自由接入自己体系里的加载器
        /// </summary>
        public ResKitUILoadStrategy(IResLoader loader)
        {
            _loader = loader;
            _ownsLoader = false;

            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] 初始化失败: 加载器为空，当前状态=loader:null");
            }
        }

        /// <summary>
        /// 我提供零配置默认构造，保持老项目最小接入成本
        /// </summary>
        public ResKitUILoadStrategy()
        {
            _loader = ResKit.Allocate<ResourceLoader>();
            _ownsLoader = true;

            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] 初始化失败: 默认 ResourceLoader 创建失败，当前状态=loader:null");
            }
        }

        public GameObject LoadUIRoot()
        {
            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] 同步加载 UIRoot 失败: 加载器为空，路径=UIPanel/UIRoot");
                return null;
            }

            return _loader.Load<GameObject>(UI_ROOT_PATH);
        }

        public async UniTask<GameObject> LoadUIRootAsync()
        {
            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] 异步加载 UIRoot 失败: 加载器为空，路径=UIPanel/UIRoot");
                return null;
            }

            return await _loader.LoadAsync<GameObject>(UI_ROOT_PATH);
        }

        public GameObject LoadPanelPrefab(string panelName)
        {
            if (_loader == null)
            {
                Debug.LogError($"[ResKitUILoadStrategy] 同步加载 Panel 失败: 加载器为空，PanelName={panelName}");
                return null;
            }

            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError($"[ResKitUILoadStrategy] 同步加载 Panel 失败: 参数非法，PanelName={panelName}");
                return null;
            }

            string path = PANEL_PREFIX + panelName;
            return _loader.Load<GameObject>(path);
        }

        public async UniTask<GameObject> LoadPanelPrefabAsync(string panelName)
        {
            if (_loader == null)
            {
                Debug.LogError($"[ResKitUILoadStrategy] 异步加载 Panel 失败: 加载器为空，PanelName={panelName}");
                return null;
            }

            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError($"[ResKitUILoadStrategy] 异步加载 Panel 失败: 参数非法，PanelName={panelName}");
                return null;
            }

            string path = PANEL_PREFIX + panelName;
            return await _loader.LoadAsync<GameObject>(path);
        }

        public void UnloadPanelPrefab(string panelName)
        {
            if (_loader == null)
            {
                Debug.LogError($"[ResKitUILoadStrategy] 卸载 Panel 失败: 加载器为空，PanelName={panelName}");
                return;
            }

            if (string.IsNullOrEmpty(panelName))
            {
                Debug.LogError($"[ResKitUILoadStrategy] 卸载 Panel 失败: 参数非法，PanelName={panelName}");
                return;
            }

            string path = PANEL_PREFIX + panelName;
            _loader.Unload(path);
        }

        public void ReleaseAll()
        {
            if (_loader == null)
            {
                Debug.LogError("[ResKitUILoadStrategy] ReleaseAll 失败: 加载器为空");
                return;
            }

            _loader.ReleaseAll();

            if (_ownsLoader)
            {
                ResKit.Recycle(_loader);
            }
        }
    }
}