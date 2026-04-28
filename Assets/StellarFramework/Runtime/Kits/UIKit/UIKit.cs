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
        private bool _isInitializing;
        private bool _isDisposed;

        public Canvas RootCanvas { get; private set; }
        public CanvasScaler RootScaler { get; private set; }
        public Camera UICamera { get; private set; }

        private readonly Dictionary<UIPanelBase.PanelLayer, Transform> _layers =
            new Dictionary<UIPanelBase.PanelLayer, Transform>();

        private readonly Dictionary<Type, UIPanelBase> _panelCache =
            new Dictionary<Type, UIPanelBase>();

        private readonly Dictionary<Type, string> _panelNames =
            new Dictionary<Type, string>();

        private readonly Dictionary<Type, UniTaskCompletionSource<UIPanelBase>> _panelLoadingTasks =
            new Dictionary<Type, UniTaskCompletionSource<UIPanelBase>>();

        private CancellationTokenSource _destroyCts = new CancellationTokenSource();

        #region 配置与初始化

        public void Configure(IUILoadStrategy loadStrategy)
        {
            if (_isInitialized || _isInitializing)
            {
                Debug.LogError(
                    $"[UIKit] Configure 失败: UIKit 已初始化或正在初始化, CurrentStrategy={_loadStrategy?.GetType().Name ?? "null"}, NewStrategy={loadStrategy?.GetType().Name ?? "null"}");
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
            if (_isDisposed)
            {
                Debug.LogError("[UIKit] Init 失败: UIKit 已销毁");
                return;
            }

            if (_isInitialized)
            {
                return;
            }

            if (_isInitializing)
            {
                Debug.LogError("[UIKit] Init 失败: 当前正在初始化中");
                return;
            }

            _isInitializing = true;
            EnsureDefaultStrategy();

            if (_loadStrategy == null)
            {
                Debug.LogError("[UIKit] Init 失败: 加载策略为空");
                _isInitializing = false;
                return;
            }

            GameObject rootPrefab = _loadStrategy.LoadUIRoot();
            if (rootPrefab == null)
            {
                Debug.LogError($"[UIKit] Init 失败: UIRoot 加载为空, Strategy={_loadStrategy.GetType().Name}");
                _isInitializing = false;
                return;
            }

            if (!SetupUIRoot(rootPrefab))
            {
                Debug.LogError(
                    $"[UIKit] Init 失败: UIRoot 结构非法, Strategy={_loadStrategy.GetType().Name}, Prefab={rootPrefab.name}");
                _isInitializing = false;
                return;
            }

            _isInitialized = true;
            _isInitializing = false;
            LogKit.Log($"[UIKit] 同步初始化完成, Strategy={_loadStrategy.GetType().Name}");
        }

        public async UniTask InitAsync()
        {
            if (_isDisposed)
            {
                Debug.LogError("[UIKit] InitAsync 失败: UIKit 已销毁");
                return;
            }

            if (_isInitialized)
            {
                return;
            }

            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;
            try
            {
                EnsureDefaultStrategy();

                if (_loadStrategy == null)
                {
                    Debug.LogError("[UIKit] InitAsync 失败: 加载策略为空");
                    return;
                }

                GameObject rootPrefab = await _loadStrategy.LoadUIRootAsync(_destroyCts.Token);

                if (_isDisposed || this == null)
                {
                    return;
                }

                if (rootPrefab == null)
                {
                    Debug.LogError($"[UIKit] InitAsync 失败: UIRoot 加载为空, Strategy={_loadStrategy.GetType().Name}");
                    return;
                }

                if (!SetupUIRoot(rootPrefab))
                {
                    Debug.LogError(
                        $"[UIKit] InitAsync 失败: UIRoot 结构非法, Strategy={_loadStrategy.GetType().Name}, Prefab={rootPrefab.name}");
                    return;
                }

                _isInitialized = true;
                LogKit.Log($"[UIKit] 异步初始化完成, Strategy={_loadStrategy.GetType().Name}");
            }
            catch (OperationCanceledException)
            {
                if (!_isDisposed)
                {
                    LogKit.LogWarning("[UIKit] InitAsync 被取消");
                }
            }
            finally
            {
                _isInitializing = false;
            }
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
                    $"[UIKit] SetupUIRoot 失败: 已存在 UIRoot, CurrentCanvas={RootCanvas.gameObject.name}, NewPrefab={rootPrefab.name}");
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
                    $"[UIKit] SetupUIRoot 失败: UIRoot 缺少 Canvas, GameObject={rootGo.name}, Prefab={rootPrefab.name}");
                Destroy(rootGo);
                return false;
            }

            Dictionary<UIPanelBase.PanelLayer, Transform> newLayers =
                new Dictionary<UIPanelBase.PanelLayer, Transform>();
            foreach (UIPanelBase.PanelLayer layer in Enum.GetValues(typeof(UIPanelBase.PanelLayer)))
            {
                string layerName = layer.ToString();
                Transform layerTrans = rootGo.transform.Find(layerName);
                if (layerTrans == null)
                {
                    Debug.LogError(
                        $"[UIKit] SetupUIRoot 失败: 缺少层级节点, GameObject={rootGo.name}, MissingLayer={layerName}, Prefab={rootPrefab.name}");
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
            foreach (KeyValuePair<UIPanelBase.PanelLayer, Transform> pair in newLayers)
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
                    $"[UIKit] SetResolution 失败: RootScaler 为空, Resolution={resolution}, Match={matchWidthOrHeight}");
                return;
            }

            RootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            RootScaler.referenceResolution = resolution;
            RootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            RootScaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        #endregion

        #region 静态公开 API

        public static TPanel OpenPanel<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            return TryGetRuntimeInstance(nameof(OpenPanel), typeof(TPanel), out UIKit instance)
                ? instance.OpenPanelInternalSync<TPanel>(data)
                : null;
        }

        public static async UniTask<TPanel> OpenPanelAsync<TPanel>(UIPanelDataBase data = null)
            where TPanel : UIPanelBase
        {
            if (!TryGetRuntimeInstance(nameof(OpenPanelAsync), typeof(TPanel), out UIKit instance))
            {
                return null;
            }

            return await instance.OpenPanelInternalAsync<TPanel>(data);
        }

        public static TPanel PreloadPanel<TPanel>() where TPanel : UIPanelBase
        {
            return TryGetRuntimeInstance(nameof(PreloadPanel), typeof(TPanel), out UIKit instance)
                ? instance.GetOrLoadPanelInternalSync<TPanel>()
                : null;
        }

        public static async UniTask<TPanel> PreloadPanelAsync<TPanel>() where TPanel : UIPanelBase
        {
            if (!TryGetRuntimeInstance(nameof(PreloadPanelAsync), typeof(TPanel), out UIKit instance))
            {
                return null;
            }

            return await instance.GetOrLoadPanelInternalAsync<TPanel>();
        }

        public static TPanel GetPanel<TPanel>() where TPanel : UIPanelBase
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] GetPanel 失败: UIKit 实例为空, Panel={typeof(TPanel).Name}");
                return null;
            }

            if (Instance._panelCache.TryGetValue(typeof(TPanel), out UIPanelBase panel))
            {
                return panel as TPanel;
            }

            return null;
        }

        public static void RefreshPanel<TPanel>(UIPanelDataBase data) where TPanel : UIPanelBase
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] RefreshPanel 失败: UIKit 实例为空, Panel={typeof(TPanel).Name}");
                return;
            }

            Instance.RefreshPanelInternal(typeof(TPanel), data);
        }

        public static void ClosePanel<TPanel>() where TPanel : UIPanelBase
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] ClosePanel 失败: UIKit 实例为空, Panel={typeof(TPanel).Name}");
                return;
            }

            Instance.ClosePanelInternal(typeof(TPanel));
        }

        public static void ClosePanel(Type panelType)
        {
            if (Instance == null)
            {
                Debug.LogError($"[UIKit] ClosePanel(Type) 失败: UIKit 实例为空, PanelType={panelType?.Name ?? "null"}");
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

            List<Type> keys = new List<Type>(Instance._panelCache.Keys);
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

            List<Type> keys = new List<Type>(Instance._panelCache.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Type panelType = keys[i];
                if (!Instance._panelCache.TryGetValue(panelType, out UIPanelBase panel) || panel == null)
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

            if (_panelCache.TryGetValue(typeof(TPanel), out UIPanelBase cachedPanel))
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

            if (_panelCache.TryGetValue(panelType, out UIPanelBase cachedPanel))
            {
                return cachedPanel as TPanel;
            }

            if (_panelLoadingTasks.TryGetValue(panelType, out UniTaskCompletionSource<UIPanelBase> loadingTask))
            {
                UIPanelBase existingLoadingPanel = await loadingTask.Task;
                return existingLoadingPanel as TPanel;
            }

            UniTaskCompletionSource<UIPanelBase> loadingSource = new UniTaskCompletionSource<UIPanelBase>();
            _panelLoadingTasks[panelType] = loadingSource;

            UIPanelBase createdPanel = null;
            try
            {
                createdPanel = await CreatePanelAsyncInternal<TPanel>(_destroyCts.Token);
                loadingSource.TrySetResult(createdPanel);
            }
            catch (OperationCanceledException)
            {
                loadingSource.TrySetCanceled();
                return null;
            }
            catch (Exception ex)
            {
                loadingSource.TrySetException(ex);
                throw;
            }
            finally
            {
                _panelLoadingTasks.Remove(panelType);
            }

            if (_isDisposed || this == null)
            {
                return null;
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
                    $"[UIKit] OpenPanel 失败: 面板创建失败, Panel={typeof(TPanel).Name}, DataType={data?.GetType().Name ?? "null"}");
                return null;
            }

            OpenExistingPanel(panel, data);
            return panel;
        }

        private async UniTask<TPanel> OpenPanelInternalAsync<TPanel>(UIPanelDataBase data) where TPanel : UIPanelBase
        {
            TPanel panel = await GetOrLoadPanelInternalAsync<TPanel>();
            if (_isDisposed || this == null)
            {
                return null;
            }

            if (panel == null)
            {
                Debug.LogError(
                    $"[UIKit] OpenPanelAsync 失败: 面板创建失败, Panel={typeof(TPanel).Name}, DataType={data?.GetType().Name ?? "null"}");
                return null;
            }

            OpenExistingPanel(panel, data);
            return panel;
        }

        private void OpenExistingPanel(UIPanelBase panel, UIPanelDataBase data)
        {
            if (panel == null)
            {
                Debug.LogError($"[UIKit] OpenExistingPanel 失败: panel 为空, DataType={data?.GetType().Name ?? "null"}");
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
                    $"[UIKit] RefreshPanelInternal 失败: panelType 为空, DataType={data?.GetType().Name ?? "null"}");
                return;
            }

            if (!_panelCache.TryGetValue(panelType, out UIPanelBase panel) || panel == null)
            {
                Debug.LogError(
                    $"[UIKit] RefreshPanelInternal 失败: 面板未缓存, Panel={panelType.Name}, DataType={data?.GetType().Name ?? "null"}");
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

            if (!_panelCache.TryGetValue(panelType, out UIPanelBase panel) || panel == null)
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
                    $"[UIKit] CreatePanelSync 失败: Prefab 加载为空, Panel={panelName}, Strategy={_loadStrategy?.GetType().Name ?? "null"}");
                return null;
            }

            return CreatePanelFromPrefab<TPanel>(prefab, panelName);
        }

        private async UniTask<UIPanelBase> CreatePanelAsyncInternal<TPanel>(CancellationToken token)
            where TPanel : UIPanelBase
        {
            Type panelType = typeof(TPanel);
            string panelName = panelType.Name;

            GameObject prefab = await _loadStrategy.LoadPanelPrefabAsync(panelName, token);
            if (token.IsCancellationRequested || _isDisposed || this == null)
            {
                return null;
            }

            if (prefab == null)
            {
                Debug.LogError(
                    $"[UIKit] CreatePanelAsync 失败: Prefab 加载为空, Panel={panelName}, Strategy={_loadStrategy?.GetType().Name ?? "null"}");
                return null;
            }

            return CreatePanelFromPrefab<TPanel>(prefab, panelName);
        }

        private TPanel CreatePanelFromPrefab<TPanel>(GameObject prefab, string panelName) where TPanel : UIPanelBase
        {
            if (_isDisposed)
            {
                return null;
            }

            if (prefab == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: prefab 为空, Panel={panelName}");
                return null;
            }

            GameObject go = Instantiate(prefab);
            go.name = panelName;

            TPanel panel = go.GetComponent<TPanel>();
            if (panel == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: 预制体缺少目标组件, Panel={panelName}, GameObject={go.name}");
                Destroy(go);
                return null;
            }

            if (!_layers.TryGetValue(panel.Layer, out Transform layerTrans) || layerTrans == null)
            {
                Debug.LogError($"[UIKit] CreatePanelFromPrefab 失败: 层级不存在, Panel={panelName}, Layer={panel.Layer}");
                Destroy(go);
                return null;
            }

            go.transform.SetParent(layerTrans, false);

            RectTransform rt = panel.RectTransform;
            if (rt == null)
            {
                Debug.LogError(
                    $"[UIKit] CreatePanelFromPrefab 失败: RectTransform 获取为空, Panel={panelName}, GameObject={go.name}");
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
            if (_isDisposed)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: UIKit 已销毁, Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (!_isInitialized)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: UIKit 未初始化, Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (_loadStrategy == null)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: 加载策略为空, Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (!_loadStrategy.SupportSyncLoad)
            {
                Debug.LogError(
                    $"[UIKit] {apiName} 失败: 当前加载策略不支持同步加载, Panel={panelType?.Name ?? "null"}, Strategy={_loadStrategy.GetType().Name}");
                return false;
            }

            return true;
        }

        private bool EnsureReadyForAsync(Type panelType, string apiName)
        {
            if (_isDisposed)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: UIKit 已销毁, Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (!_isInitialized)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: UIKit 未初始化, Panel={panelType?.Name ?? "null"}");
                return false;
            }

            if (_loadStrategy == null)
            {
                Debug.LogError($"[UIKit] {apiName} 失败: 加载策略为空, Panel={panelType?.Name ?? "null"}");
                return false;
            }

            return true;
        }

        #endregion

        protected override void OnDestroy()
        {
            _isDisposed = true;

            if (SingletonFactory.TryGetRegisteredSingleton<UIStackManager>(out UIStackManager stackManager))
            {
                stackManager.Dispose();
            }

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

            if (RootCanvas != null && RootCanvas.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(RootCanvas.gameObject);
                }
                else
                {
                    DestroyImmediate(RootCanvas.gameObject);
                }
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

        private static bool TryGetRuntimeInstance(string apiName, Type panelType, out UIKit instance)
        {
            instance = Instance;
            if (instance != null)
            {
                return true;
            }

            Debug.LogError($"[UIKit] {apiName} 失败: UIKit 实例为空, Panel={panelType?.Name ?? "null"}");
            return false;
        }
    }
}
