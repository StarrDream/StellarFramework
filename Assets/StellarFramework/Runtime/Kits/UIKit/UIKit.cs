using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.UI
{
    public sealed class UIKitRuntimeSnapshot
    {
        public bool IsInitialized;
        public bool IsInitializing;
        public bool IsDisposed;
        public bool HasRootCanvas;
        public bool HasStaticCanvas;
        public bool HasDynamicCanvas;
        public string LoadStrategyName;
        public readonly List<string> CachedPanels = new List<string>();
        public readonly List<string> ActivePanels = new List<string>();
        public readonly List<string> LoadingPanels = new List<string>();

        public int CachedPanelCount => CachedPanels.Count;
        public int ActivePanelCount => ActivePanels.Count;
        public int LoadingPanelCount => LoadingPanels.Count;

        public static UIKitRuntimeSnapshot Empty(string reason)
        {
            return new UIKitRuntimeSnapshot
            {
                LoadStrategyName = reason
            };
        }

        public string ToMultilineString()
        {
            StringBuilder builder = new StringBuilder(256);
            builder.Append("[UIKitSnapshot] ");
            builder.Append("Initialized=").Append(IsInitialized);
            builder.Append(", Initializing=").Append(IsInitializing);
            builder.Append(", Disposed=").Append(IsDisposed);
            builder.Append(", Strategy=").Append(string.IsNullOrEmpty(LoadStrategyName) ? "null" : LoadStrategyName);
            builder.Append(", Root=").Append(HasRootCanvas);
            builder.Append(", StaticCanvas=").Append(HasStaticCanvas);
            builder.Append(", DynamicCanvas=").Append(HasDynamicCanvas);
            builder.Append(", Cached=").Append(CachedPanelCount);
            builder.Append(", Active=").Append(ActivePanelCount);
            builder.Append(", Loading=").Append(LoadingPanelCount);
            builder.AppendLine();
            builder.Append("  CachedPanels: ").AppendLine(FormatList(CachedPanels));
            builder.Append("  ActivePanels: ").AppendLine(FormatList(ActivePanels));
            builder.Append("  LoadingPanels: ").Append(FormatList(LoadingPanels));
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToMultilineString();
        }

        private static string FormatList(List<string> values)
        {
            return values == null || values.Count == 0 ? "(none)" : string.Join(", ", values);
        }
    }

    [Singleton("Managers/UIKit", SingletonLifeCycle.Global, false)]
    public class UIKit : MonoSingleton<UIKit>
    {
        private const string UI_ROOT_NAME = "UIRoot";

        private IUILoadStrategy _loadStrategy;
        private UIKitSettings _settings;
        private bool _isInitialized;
        private bool _isInitializing;
        private bool _isDisposed;

        public Canvas RootCanvas { get; private set; }
        public Canvas StaticCanvas { get; private set; }
        public Canvas DynamicCanvas { get; private set; }
        public CanvasScaler RootScaler { get; private set; }
        public Camera UICamera { get; private set; }

        private readonly Dictionary<UIPanelBase.PanelLayer, Transform> _layers =
            new Dictionary<UIPanelBase.PanelLayer, Transform>();

        private readonly Dictionary<UIPanelBase.PanelCanvasRole, Dictionary<UIPanelBase.PanelLayer, Transform>>
            _roleLayers =
                new Dictionary<UIPanelBase.PanelCanvasRole, Dictionary<UIPanelBase.PanelLayer, Transform>>();

        private readonly Dictionary<Type, UIPanelBase> _panelCache =
            new Dictionary<Type, UIPanelBase>();

        private readonly Dictionary<Type, string> _panelNames =
            new Dictionary<Type, string>();

        private readonly Dictionary<Type, UniTaskCompletionSource<UIPanelBase>> _panelLoadingTasks =
            new Dictionary<Type, UniTaskCompletionSource<UIPanelBase>>();

        private readonly List<UIPanelBase> _panelStack = new List<UIPanelBase>(16);
        private CancellationTokenSource _destroyCts = new CancellationTokenSource();
        private bool _stackCallbacksRegistered;

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

        public void Configure(UIKitSettings settings)
        {
            if (_isInitialized || _isInitializing)
            {
                Debug.LogError("[UIKit] Configure failed: UIKit is already initialized or initializing.");
                return;
            }

            _settings = settings != null ? settings : UIKitSettings.LoadOrCreateDefault();
            _loadStrategy = new ResKitUILoadStrategy(_settings);
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
            RegisterStackCallbacks();
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
                RegisterStackCallbacks();
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

            _settings = _settings != null ? _settings : UIKitSettings.LoadOrCreateDefault();
            _loadStrategy = new ResKitUILoadStrategy(_settings);
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

            Dictionary<UIPanelBase.PanelLayer, Transform> dynamicLayers;
            Dictionary<UIPanelBase.PanelLayer, Transform> staticLayers;
            if (!BuildLayerMap(rootGo.transform, UIPanelBase.PanelCanvasRole.Dynamic, out dynamicLayers) ||
                !BuildLayerMap(rootGo.transform, UIPanelBase.PanelCanvasRole.Static, out staticLayers))
            {
                Debug.LogError($"[UIKit] SetupUIRoot failed: UIRoot layer structure is invalid. Prefab={rootPrefab.name}");
                Destroy(rootGo);
                return false;
            }

            rootGo.transform.SetParent(null, false);
            DontDestroyOnLoad(rootGo);

            RootCanvas = rootCanvas;
            DynamicCanvas = FindRoleCanvas(rootGo.transform, UIPanelBase.PanelCanvasRole.Dynamic) ?? rootCanvas;
            StaticCanvas = FindRoleCanvas(rootGo.transform, UIPanelBase.PanelCanvasRole.Static) ?? rootCanvas;
            RootScaler = rootScaler;
            UICamera = uiCamera;

            _layers.Clear();
            foreach (KeyValuePair<UIPanelBase.PanelLayer, Transform> pair in dynamicLayers)
            {
                _layers[pair.Key] = pair.Value;
            }

            _roleLayers.Clear();
            _roleLayers[UIPanelBase.PanelCanvasRole.Dynamic] = dynamicLayers;
            _roleLayers[UIPanelBase.PanelCanvasRole.Static] = staticLayers;

            return true;
        }

        private bool BuildLayerMap(Transform root, UIPanelBase.PanelCanvasRole role,
            out Dictionary<UIPanelBase.PanelLayer, Transform> layerMap)
        {
            layerMap = new Dictionary<UIPanelBase.PanelLayer, Transform>();
            Transform roleRoot = FindRoleRoot(root, role);
            if (roleRoot == null)
            {
                roleRoot = root;
            }

            foreach (UIPanelBase.PanelLayer layer in Enum.GetValues(typeof(UIPanelBase.PanelLayer)))
            {
                string layerName = layer.ToString();
                Transform layerTrans = roleRoot.Find(layerName);
                if (layerTrans == null && roleRoot != root)
                {
                    layerTrans = root.Find(layerName);
                }

                if (layerTrans == null)
                {
                    Debug.LogError(
                        $"[UIKit] Missing layer. Role={role}, Layer={layerName}, Root={root.gameObject.name}");
                    return false;
                }

                layerMap[layer] = layerTrans;
            }

            return true;
        }

        private static Canvas FindRoleCanvas(Transform root, UIPanelBase.PanelCanvasRole role)
        {
            Transform roleRoot = FindRoleRoot(root, role);
            return roleRoot != null ? roleRoot.GetComponent<Canvas>() : null;
        }

        private static Transform FindRoleRoot(Transform root, UIPanelBase.PanelCanvasRole role)
        {
            if (root == null)
            {
                return null;
            }

            switch (role)
            {
                case UIPanelBase.PanelCanvasRole.Static:
                    return root.Find("StaticCanvas") ?? root.Find("Static");
                case UIPanelBase.PanelCanvasRole.Dynamic:
                    return root.Find("DynamicCanvas") ?? root.Find("Dynamic");
                default:
                    return null;
            }
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

        public static TPanel Open<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            return OpenPanel<TPanel>(data);
        }

        public static UniTask<TPanel> OpenAsync<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            return OpenPanelAsync<TPanel>(data);
        }

        public static TPanel Push<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            TPanel panel = OpenPanel<TPanel>(data);
            if (panel != null && TryGetRuntimeInstance(nameof(Push), typeof(TPanel), out UIKit instance))
            {
                instance.PushToStack(panel);
            }

            return panel;
        }

        public static async UniTask<TPanel> PushAsync<TPanel>(UIPanelDataBase data = null)
            where TPanel : UIPanelBase
        {
            TPanel panel = await OpenPanelAsync<TPanel>(data);
            if (panel != null && TryGetRuntimeInstance(nameof(PushAsync), typeof(TPanel), out UIKit instance))
            {
                instance.PushToStack(panel);
            }

            return panel;
        }

        public static void Pop()
        {
            if (!TryGetRuntimeInstance(nameof(Pop), null, out UIKit instance))
            {
                return;
            }

            instance.TryPop();
        }

        public static void PopTo<TPanel>() where TPanel : UIPanelBase
        {
            if (!TryGetRuntimeInstance(nameof(PopTo), typeof(TPanel), out UIKit instance))
            {
                return;
            }

            instance.TryPopTo<TPanel>();
        }

        public static void ClearStack()
        {
            if (!TryGetRuntimeInstance(nameof(ClearStack), null, out UIKit instance))
            {
                return;
            }

            instance.ClearStackInternal();
        }

        public static void Close<TPanel>() where TPanel : UIPanelBase
        {
            ClosePanel<TPanel>();
        }

        public static TPanel Preload<TPanel>() where TPanel : UIPanelBase
        {
            return PreloadPanel<TPanel>();
        }

        public static UniTask<TPanel> PreloadAsync<TPanel>() where TPanel : UIPanelBase
        {
            return PreloadPanelAsync<TPanel>();
        }

        public static UIKitRuntimeSnapshot TakeSnapshot()
        {
            return Instance != null
                ? Instance.CreateSnapshot()
                : UIKitRuntimeSnapshot.Empty("UIKit instance is null");
        }

        public static void LogSnapshot()
        {
            LogKit.Log(TakeSnapshot().ToMultilineString());
        }

        public static async UniTask<UIKitRuntimeSnapshot> StressOpenCloseAsync<TPanel>(
            int iterations,
            UIPanelDataBase data = null,
            int yieldEvery = 1,
            CancellationToken cancellationToken = default)
            where TPanel : UIPanelBase
        {
            if (iterations < 0)
            {
                Debug.LogError($"[UIKit] StressOpenCloseAsync failed: iterations must be >= 0, Value={iterations}");
                return TakeSnapshot();
            }

            for (int i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TPanel panel = await OpenAsync<TPanel>(data);
                if (panel == null)
                {
                    Debug.LogError(
                        $"[UIKit] StressOpenCloseAsync stopped: panel open failed, Panel={typeof(TPanel).Name}, Iteration={i + 1}/{iterations}");
                    break;
                }

                Close<TPanel>();

                if (yieldEvery > 0 && (i + 1) % yieldEvery == 0)
                {
                    await UniTask.Yield(cancellationToken);
                }
            }

            UIKitRuntimeSnapshot snapshot = TakeSnapshot();
            LogKit.Log(snapshot.ToMultilineString());
            return snapshot;
        }

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

            RemoveFromStack(panel);

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

            if (!TryGetLayer(panel.CanvasRole, panel.Layer, out Transform layerTrans) || layerTrans == null)
            {
                Debug.LogError(
                    $"[UIKit] CreatePanelFromPrefab 失败: 层级不存在, Panel={panelName}, CanvasRole={panel.CanvasRole}, Layer={panel.Layer}");
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

        private bool TryGetLayer(UIPanelBase.PanelCanvasRole role, UIPanelBase.PanelLayer layer, out Transform layerTrans)
        {
            layerTrans = null;
            if (_roleLayers.TryGetValue(role, out Dictionary<UIPanelBase.PanelLayer, Transform> roleMap) &&
                roleMap.TryGetValue(layer, out layerTrans) &&
                layerTrans != null)
            {
                return true;
            }

            return _layers.TryGetValue(layer, out layerTrans) && layerTrans != null;
        }

        #endregion

        #region 内部逻辑 - 堆栈

        private void RegisterStackCallbacks()
        {
            if (_stackCallbacksRegistered)
            {
                return;
            }

            UIPanelBase.OnPanelClosedGlobal += HandlePanelClosed;
            _stackCallbacksRegistered = true;
        }

        private void UnregisterStackCallbacks()
        {
            if (!_stackCallbacksRegistered)
            {
                return;
            }

            UIPanelBase.OnPanelClosedGlobal -= HandlePanelClosed;
            _stackCallbacksRegistered = false;
        }

        private void PushToStack(UIPanelBase panel)
        {
            if (panel == null)
            {
                LogKit.LogError("[UIKit] PushToStack 失败: panel 为空");
                return;
            }

            CleanupInvalidStackPanels();

            int existedIndex = _panelStack.IndexOf(panel);
            if (existedIndex >= 0)
            {
                _panelStack.RemoveAt(existedIndex);
            }

            _panelStack.Add(panel);
            EvaluateStackVisibility();
        }

        private void RemoveFromStack(UIPanelBase panel)
        {
            if (panel == null)
            {
                return;
            }

            CleanupInvalidStackPanels();

            if (_panelStack.Remove(panel))
            {
                EvaluateStackVisibility();
            }
        }

        private UIPanelBase PeekStack()
        {
            CleanupInvalidStackPanels();

            if (_panelStack.Count == 0)
            {
                return null;
            }

            return _panelStack[_panelStack.Count - 1];
        }

        private bool TryPop()
        {
            UIPanelBase top = PeekStack();
            if (top == null)
            {
                LogKit.LogWarning("[UIKit] Pop skipped: stack is empty.");
                return false;
            }

            ClosePanel(top.GetType());
            return true;
        }

        private bool TryPopTo<TPanel>() where TPanel : UIPanelBase
        {
            CleanupInvalidStackPanels();

            if (_panelStack.Count == 0)
            {
                LogKit.LogWarning($"[UIKit] PopTo skipped: stack is empty, TargetPanel={typeof(TPanel).Name}");
                return false;
            }

            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _panelStack[i];
                if (panel == null)
                {
                    continue;
                }

                if (panel is TPanel)
                {
                    EvaluateStackVisibility();
                    return true;
                }

                ClosePanel(panel.GetType());
            }

            LogKit.LogWarning($"[UIKit] PopTo failed: target panel was not found in stack. TargetPanel={typeof(TPanel).Name}");
            return false;
        }

        private void ClearStackInternal()
        {
            CleanupInvalidStackPanels();

            if (_panelStack.Count == 0)
            {
                LogKit.LogWarning("[UIKit] ClearStack skipped: stack is empty.");
                return;
            }

            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _panelStack[i];
                if (panel == null)
                {
                    continue;
                }

                ClosePanel(panel.GetType());
            }

            _panelStack.Clear();
        }

        private void HandlePanelClosed(UIPanelBase panel)
        {
            RemoveFromStack(panel);
        }

        private void EvaluateStackVisibility()
        {
            CleanupInvalidStackPanels();

            int topFullscreenIndex = -1;
            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _panelStack[i];
                if (panel == null)
                {
                    continue;
                }

                if (panel.IsFullScreen)
                {
                    topFullscreenIndex = i;
                    break;
                }
            }

            for (int i = 0; i < _panelStack.Count; i++)
            {
                UIPanelBase panel = _panelStack[i];
                if (panel == null)
                {
                    continue;
                }

                bool visible = topFullscreenIndex < 0 || i >= topFullscreenIndex;
                ApplyStackVisible(panel, visible);
            }
        }

        private void ApplyStackVisible(UIPanelBase panel, bool visible)
        {
            if (panel == null)
            {
                return;
            }

            CanvasGroup group = panel.CanvasGroup;
            if (group == null)
            {
                LogKit.LogError(
                    $"[UIKit] ApplyStackVisible 失败: CanvasGroup 为空, Panel={panel.GetType().Name}, TriggerObject={panel.gameObject.name}, Visible={visible}");
                return;
            }

            bool wasVisible = group.alpha > 0.01f;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (visible && !wasVisible)
            {
                panel.OnResume();
            }
            else if (!visible && wasVisible)
            {
                panel.OnPause();
            }
        }

        private void CleanupInvalidStackPanels()
        {
            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _panelStack[i];
                if (panel == null || panel.gameObject == null)
                {
                    _panelStack.RemoveAt(i);
                }
            }
        }

        #endregion

        #region 诊断

        private UIKitRuntimeSnapshot CreateSnapshot()
        {
            UIKitRuntimeSnapshot snapshot = new UIKitRuntimeSnapshot
            {
                IsInitialized = _isInitialized,
                IsInitializing = _isInitializing,
                IsDisposed = _isDisposed,
                HasRootCanvas = RootCanvas != null,
                HasStaticCanvas = StaticCanvas != null,
                HasDynamicCanvas = DynamicCanvas != null,
                LoadStrategyName = _loadStrategy != null ? _loadStrategy.GetType().Name : "null"
            };

            foreach (KeyValuePair<Type, UIPanelBase> pair in _panelCache)
            {
                string panelName = pair.Key != null ? pair.Key.Name : "null";
                snapshot.CachedPanels.Add(panelName);

                UIPanelBase panel = pair.Value;
                if (panel != null && panel.gameObject != null && panel.gameObject.activeSelf)
                {
                    snapshot.ActivePanels.Add(panelName);
                }
            }

            foreach (Type loadingType in _panelLoadingTasks.Keys)
            {
                snapshot.LoadingPanels.Add(loadingType != null ? loadingType.Name : "null");
            }

            snapshot.CachedPanels.Sort(StringComparer.Ordinal);
            snapshot.ActivePanels.Sort(StringComparer.Ordinal);
            snapshot.LoadingPanels.Sort(StringComparer.Ordinal);
            return snapshot;
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
            UnregisterStackCallbacks();

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
            _panelStack.Clear();
            _layers.Clear();
            _roleLayers.Clear();

            RootCanvas = null;
            StaticCanvas = null;
            DynamicCanvas = null;
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
