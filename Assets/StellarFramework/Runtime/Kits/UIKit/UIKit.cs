using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.UI
{
    [Singleton("Managers/UIKit", SingletonLifeCycle.Global, false)]
    public class UIKit : MonoSingleton<UIKit>
    {
        private const string UI_ROOT_NAME = "UIRoot";

        private IUILoadStrategy _loadStrategy;
        private bool _isInitialized;

        public Canvas RootCanvas { get; private set; }
        public CanvasScaler RootScaler { get; private set; }
        public Camera UICamera { get; private set; }

        private readonly Dictionary<UIPanelBase.PanelLayer, Transform> _layers =
            new Dictionary<UIPanelBase.PanelLayer, Transform>();

        private readonly Dictionary<Type, UIPanelBase> _panelCache =
            new Dictionary<Type, UIPanelBase>();

        private readonly Dictionary<Type, string> _panelNames =
            new Dictionary<Type, string>();

        private readonly Dictionary<Type, UniTask<UIPanelBase>> _panelLoadingTasks =
            new Dictionary<Type, UniTask<UIPanelBase>>();

        private CancellationTokenSource _destroyCts = new CancellationTokenSource();

        #region 配置与初始化

        /// <summary>
        /// 我允许在初始化前注入自定义 UI 加载策略。
        /// 初始化后切换策略会破坏已缓存面板与资源引用边界，所以这里直接阻断。
        /// </summary>
        public void Configure(IUILoadStrategy loadStrategy)
        {
            if (_isInitialized)
            {
                Debug.LogError(
                    $"[UIKit] Configure 失败: UIKit 已初始化，当前策略={_loadStrategy?.GetType().Name ?? "null"}，新策略={loadStrategy?.GetType().Name ?? "null"}");
                return;
            }

            if (loadStrategy == null)
            {
                Debug.LogError("[UIKit] Configure 失败: 传入的加载策略为空");
                return;
            }

            _loadStrategy = loadStrategy;
        }

        public void Init()
        {
            if (_isInitialized)
            {
                return;
            }

            EnsureDefaultStrategy();
            if (_loadStrategy == null)
            {
                Debug.LogError("[UIKit] Init 失败: 加载策略为空");
                return;
            }

            GameObject rootPrefab = _loadStrategy.LoadUIRoot();
            if (rootPrefab == null)
            {
                Debug.LogError($"[UIKit] Init 失败: UIRoot 加载为空，策略={_loadStrategy.GetType().Name}");
                return;
            }

            if (!SetupUIRoot(rootPrefab))
            {
                Debug.LogError(
                    $"[UIKit] Init 失败: UIRoot 结构非法，策略={_loadStrategy.GetType().Name}，Prefab={rootPrefab.name}");
                return;
            }

            _isInitialized = true;
            LogKit.Log($"[UIKit] 同步初始化完成，策略={_loadStrategy.GetType().Name}");
        }

        public async UniTask InitAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            EnsureDefaultStrategy();
            if (_loadStrategy == null)
            {
                Debug.LogError("[UIKit] InitAsync 失败: 加载策略为空");
                return;
            }

            GameObject rootPrefab = await _loadStrategy.LoadUIRootAsync();
            if (rootPrefab == null)
            {
                Debug.LogError($"[UIKit] InitAsync 失败: UIRoot 加载为空，策略={_loadStrategy.GetType().Name}");
                return;
            }

            if (!SetupUIRoot(rootPrefab))
            {
                Debug.LogError(
                    $"[UIKit] InitAsync 失败: UIRoot 结构非法，策略={_loadStrategy.GetType().Name}，Prefab={rootPrefab.name}");
                return;
            }

            _isInitialized = true;
            LogKit.Log($"[UIKit] 异步初始化完成，策略={_loadStrategy.GetType().Name}");
        }

        private void EnsureDefaultStrategy()
        {
            if (_loadStrategy != null)
            {
                return;
            }

            _loadStrategy = new ResKitUILoadStrategy();
        }

        private bool SetupUIRoot(GameObject rootPrefab)
        {
            if (rootPrefab == null)
            {
                Debug.LogError("[UIKit] SetupUIRoot 失败: rootPrefab 为空");
                return false;
            }

            if (RootCanvas != null)
            {
                Debug.LogError(
                    $"[UIKit] SetupUIRoot 失败: 已存在 UIRoot，当前 Canvas 物体名={RootCanvas.gameObject.name}，新 Prefab={rootPrefab.name}");
                return false;
            }

            GameObject rootGo = Instantiate(rootPrefab);
            rootGo.name = UI_ROOT_NAME;

            Canvas rootCanvas = rootGo.GetComponent<Canvas>();
            CanvasScaler rootScaler = rootGo.GetComponent<CanvasScaler>();
            Camera uiCamera = rootGo.GetComponentInChildren<Camera>(true);

            if (rootCanvas == null)
            {
                Debug.LogError(
                    $"[UIKit] SetupUIRoot 失败: UIRoot 缺少 Canvas，物体名={rootGo.name}，Prefab={rootPrefab.name}");
                Destroy(rootGo);
                return false;
            }

            var newLayers = new Dictionary<UIPanelBase.PanelLayer, Transform>();
            foreach (UIPanelBase.PanelLayer layer in Enum.GetValues(typeof(UIPanelBase.PanelLayer)))
            {
                string layerName = layer.ToString();
                Transform layerTrans = rootGo.transform.Find(layerName);
                if (layerTrans == null)
                {
                    Debug.LogError(
                        $"[UIKit] SetupUIRoot 失败: UIRoot 缺少层级节点，物体名={rootGo.name}，缺失层={layerName}，Prefab={rootPrefab.name}");
                    Destroy(rootGo);
                    return false;
                }

                newLayers[layer] = layerTrans;
            }

            rootGo.transform.SetParent(null, false);
            DontDestroyOnLoad(rootGo);

            RootCanvas = rootCanvas;
            RootScaler = rootScaler;
            UICamera = uiCamera;

            _layers.Clear();
            foreach (var pair in newLayers)
            {
                _layers[pair.Key] = pair.Value;
            }

            return true;
        }

        public void SetResolution(Vector2 resolution, float matchWidthOrHeight)
        {
            if (RootScaler == null)
            {
                Debug.LogError(
                    $"[UIKit] SetResolution 失败: RootScaler 为空，Resolution={resolution}，Match={matchWidthOrHeight}");
                return;
            }

            RootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            RootScaler.referenceResolution = resolution;
            RootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            RootScaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        #endregion

        #region 静态公开 API - 打开

        public static TPanel OpenPanel<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            return Instance.OpenPanelInternalSync<TPanel>(data);
        }

        public static async UniTask<TPanel> OpenPanelAsync<TPanel>(UIPanelDataBase data = null)
            where TPanel : UIPanelBase
        {
            return await Instance.OpenPanelInternalAsync<TPanel>(data);
        }

        #endregion

        #region 静态公开 API - 预加载

        public static TPanel PreloadPanel<TPanel>() where TPanel : UIPanelBase
        {
            return Instance.GetOrLoadPanelInternalSync<TPanel>();
        }

        public static async UniTask<TPanel> PreloadPanelAsync<TPanel>() where TPanel : UIPanelBase
        {
            return await Instance.GetOrLoadPanelInternalAsync<TPanel>();
        }

        #endregion

        #region 静态公开 API - 获取

        public static TPanel GetPanel<TPanel>() where TPanel : UIPanelBase
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] GetPanel 失败: UIKit 实例为空，Panel={typeof(TPanel).Name}");
                return null;
            }

            if (Instance._panelCache.TryGetValue(typeof(TPanel), out var panel))
            {
                return panel as TPanel;
            }

            return null;
        }

        public static TPanel GetOrLoadPanel<TPanel>() where TPanel : UIPanelBase
        {
            return Instance.GetOrLoadPanelInternalSync<TPanel>();
        }

        public static async UniTask<TPanel> GetOrLoadPanelAsync<TPanel>() where TPanel : UIPanelBase
        {
            return await Instance.GetOrLoadPanelInternalAsync<TPanel>();
        }

        #endregion

        #region 静态公开 API - 刷新

        public static void RefreshPanel<TPanel>(UIPanelDataBase data) where TPanel : UIPanelBase
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] RefreshPanel 失败: UIKit 实例为空，Panel={typeof(TPanel).Name}");
                return;
            }

            Instance.RefreshPanelInternal(typeof(TPanel), data);
        }

        #endregion

        #region 静态公开 API - 关闭

        public static void ClosePanel<TPanel>() where TPanel : UIPanelBase
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] ClosePanel 失败: UIKit 实例为空，Panel={typeof(TPanel).Name}");
                return;
            }

            Instance.ClosePanelInternal(typeof(TPanel));
        }

        public static void ClosePanel(Type panelType)
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] ClosePanel(Type) 失败: UIKit 实例为空，PanelType={panelType?.Name ?? "null"}");
                return;
            }

            Instance.ClosePanelInternal(panelType);
        }

        public static void CloseAllPanels()
        {
            if (Instance == null)
            {
                Debug.LogError("[UIKit] CloseAllPanels 失败: UIKit 实例为空");
                return;
            }

            var keys = new List<Type>(Instance._panelCache.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Instance.ClosePanelInternal(keys[i]);
            }
        }

        public static void DestroyAllPanels()
        {
            if (Instance == null)
            {
                Debug.LogError("[UIKit] DestroyAllPanels 失败: UIKit 实例为空");
                return;
            }

            var keys = new List<Type>(Instance._panelCache.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Type panelType = keys[i];
                if (!Instance._panelCache.TryGetValue(panelType, out var panel) || panel == null)
                {
                    continue;
                }

                if (panel.gameObject.activeSelf)
                {
                    panel.OnClose();
                    panel.CanvasGroup.interactable = false;
                    panel.CanvasGroup.blocksRaycasts = false;
                    panel.gameObject.SetActive(false);
                }

                if (Instance._panelNames.TryGetValue(panelType, out string panelName))
                {
                    Instance._loadStrategy?.UnloadPanelPrefab(panelName);
                }

                if (panel.gameObject != null)
                {
                    Destroy(panel.gameObject);
                }
            }

            Instance._panelCache.Clear();
            Instance._panelNames.Clear();
            Instance._panelLoadingTasks.Clear();

            LogKit.Log("[UIKit] 已强制销毁所有面板并清理缓存");
        }

        #endregion

        #region 内部逻辑 - 获取或加载

        private TPanel GetOrLoadPanelInternalSync<TPanel>() where TPanel : UIPanelBase
        {
            if (!EnsureReadyForSync(typeof(TPanel), "GetOrLoadPanel"))
            {
                return null;
            }

            if (_panelCache.TryGetValue(typeof(TPanel), out var cachedPanel))
            {
                return cachedPanel as TPanel;
            }

            return CreatePanelSync<TPanel>();
        }

        private async UniTask<TPanel> GetOrLoadPanelInternalAsync<TPanel>() where TPanel : UIPanelBase
        {
            if (!EnsureReadyForAsync(typeof(TPanel), "GetOrLoadPanelAsync"))
            {
                return null;
            }

            Type panelType = typeof(TPanel);
            if (_panelCache.TryGetValue(panelType, out var cachedPanel))
            {
                return cachedPanel as TPanel;
            }

            if (_panelLoadingTasks.TryGetValue(panelType, out var loadingTask))
            {
                UIPanelBase existingLoadingPanel = await loadingTask;
                return existingLoadingPanel as TPanel;
            }

            UniTask<UIPanelBase> newTask = CreatePanelAsyncInternal<TPanel>().Preserve();
            _panelLoadingTasks[panelType] = newTask;

            UIPanelBase createdPanel = null;
            try
            {
                createdPanel = await newTask;
            }
            finally
            {
                _panelLoadingTasks.Remove(panelType);
            }

            return createdPanel as TPanel;
        }

        #endregion

        #region 内部逻辑 - 打开

        private TPanel OpenPanelInternalSync<TPanel>(UIPanelDataBase data) where TPanel : UIPanelBase
        {
            TPanel panel = GetOrLoadPanelInternalSync<TPanel>();
            if (panel == null)
            {
                Debug.LogError(
                    $"[UIKit] OpenPanel 失败: 面板创建失败，Panel={typeof(TPanel).Name}，DataType={data?.GetType().Name ?? "null"}");
                return null;
            }

            OpenExistingPanel(panel, data);
            return panel;
        }

        private async UniTask<TPanel> OpenPanelInternalAsync<TPanel>(UIPanelDataBase data) where TPanel : UIPanelBase
        {
            TPanel panel = await GetOrLoadPanelInternalAsync<TPanel>();
            if (panel == null)
            {
                Debug.LogError(
                    $"[UIKit] OpenPanelAsync 失败: 面板创建失败，Panel={typeof(TPanel).Name}，DataType={data?.GetType().Name ?? "null"}");
                return null;
            }

            OpenExistingPanel(panel, data);
            return panel;
        }

        private void OpenExistingPanel(UIPanelBase panel, UIPanelDataBase data)
        {
            if (panel == null)
            {
                Debug.LogError($"[UIKit] OpenExistingPanel 失败: panel 为空，DataType={data?.GetType().Name ?? "null"}");
                return;
            }

            if (!panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);
            }

            panel.transform.SetAsLastSibling();
            panel.CanvasGroup.alpha = 1f;
            panel.CanvasGroup.interactable = true;
            panel.CanvasGroup.blocksRaycasts = true;
            panel.OnOpen(data);
        }

        #endregion

        #region 内部逻辑 - 刷新

        private void RefreshPanelInternal(Type panelType, UIPanelDataBase data)
        {
            if (panelType == null)
            {
                Debug.LogError(
                    $"[UIKit] RefreshPanelInternal 失败: panelType 为空，DataType={data?.GetType().Name ?? "null"}");
                return;
            }

            if (!_panelCache.TryGetValue(panelType, out var panel) || panel == null)
            {
                Debug.LogError(
                    $"[UIKit] RefreshPanelInternal 失败: 面板未缓存，Panel={panelType.Name}，DataType={data?.GetType().Name ?? "null"}");
                return;
            }

            panel.OnRefresh(data);
        }

        #endregion

        #region 内部逻辑 - 关闭

        private void ClosePanelInternal(Type panelType)
        {
            if (panelType == null)
            {
                Debug.LogError("[UIKit] ClosePanelInternal 失败: panelType 为空");
                return;
            }

            if (!_panelCache.TryGetValue(panelType, out var panel) || panel == null)
            {
                return;
            }

            if (panel.gameObject.activeSelf)
            {
                panel.OnClose();
                panel.CanvasGroup.interactable = false;
                panel.CanvasGroup.blocksRaycasts = false;
                panel.gameObject.SetActive(false);
            }

            if (!panel.DestroyOnClose)
            {
                return;
            }

            if (_panelNames.TryGetValue(panelType, out string panelName))
            {
                _loadStrategy?.UnloadPanelPrefab(panelName);
                _panelNames.Remove(panelType);
            }

            _panelCache.Remove(panelType);
            Destroy(panel.gameObject);
        }

        #endregion

        #region 内部逻辑 - 创建面板

        private TPanel CreatePanelSync<TPanel>() where TPanel : UIPanelBase
        {
            Type panelType = typeof(TPanel);
            string panelName = panelType.Name;

            GameObject prefab = _loadStrategy.LoadPanelPrefab(panelName);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[UIKit] CreatePanelSync 失败: Prefab 加载为空，Panel={panelName}，策略={_loadStrategy?.GetType().Name ?? "null"}");
                return null;
            }

            return CreatePanelFromPrefab<TPanel>(prefab, panelName);
        }

        private async UniTask<UIPanelBase> CreatePanelAsyncInternal<TPanel>() where TPanel : UIPanelBase
        {
            Type panelType = typeof(TPanel);
            string panelName = panelType.Name;

            GameObject prefab = await _loadStrategy.LoadPanelPrefabAsync(panelName);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[UIKit] CreatePanelAsync 失败: Prefab 加载为空，Panel={panelName}，策略={_loadStrategy?.GetType().Name ?? "null"}");
                return null;
            }

            return CreatePanelFromPrefab<TPanel>(prefab, panelName);
        }

        private TPanel CreatePanelFromPrefab<TPanel>(GameObject prefab, string panelName) where TPanel : UIPanelBase
        {
            if (prefab == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: prefab 为空，Panel={panelName}");
                return null;
            }

            GameObject go = Instantiate(prefab);
            go.name = panelName;

            TPanel panel = go.GetComponent<TPanel>();
            if (panel == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: 预制体缺少目标组件，Panel={panelName}，物体名={go.name}");
                Destroy(go);
                return null;
            }

            if (!_layers.TryGetValue(panel.Layer, out Transform layerTrans) || layerTrans == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: 层级不存在，Panel={panelName}，Layer={panel.Layer}");
                Destroy(go);
                return null;
            }

            go.transform.SetParent(layerTrans, false);

            RectTransform rt = panel.RectTransform;
            if (rt == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: RectTransform 获取为空，Panel={panelName}，物体名={go.name}");
                Destroy(go);
                return null;
            }

            rt.FillParent();
            rt.localPosition = Vector3.zero;

            go.SetActive(false);
            panel.OnInit();

            Type panelType = typeof(TPanel);
            _panelCache[panelType] = panel;
            _panelNames[panelType] = panelName;

            return panel;
        }

        #endregion

        #region 内部逻辑 - 校验

        private bool EnsureReadyForSync(Type panelType, string apiName)
        {
            if (!_isInitialized)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: UIKit 未初始化，Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (_loadStrategy == null)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: 加载策略为空，Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (!_loadStrategy.SupportSyncLoad)
            {
                Debug.LogError(
                    $"[UIKit] {apiName} 失败: 当前加载策略不支持同步加载，Panel={panelType?.Name ?? "null"}，策略={_loadStrategy.GetType().Name}");
                return false;
            }

            return true;
        }

        private bool EnsureReadyForAsync(Type panelType, string apiName)
        {
            if (!_isInitialized)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: UIKit 未初始化，Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (_loadStrategy == null)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: 加载策略为空，Panel={panelType?.Name ?? "null"}");
                return false;
            }

            return true;
        }

        #endregion

        protected override void OnDestroy()
        {
            if (_destroyCts != null)
            {
                _destroyCts.Cancel();
                _destroyCts.Dispose();
                _destroyCts = null;
            }

            if (_loadStrategy != null)
            {
                _loadStrategy.ReleaseAll();
                _loadStrategy = null;
            }

            _panelLoadingTasks.Clear();
            _panelCache.Clear();
            _panelNames.Clear();
            _layers.Clear();

            RootCanvas = null;
            RootScaler = null;
            UICamera = null;

            base.OnDestroy();
        }
    }
}