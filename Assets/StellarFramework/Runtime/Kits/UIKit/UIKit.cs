using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using StellarFramework.Res;
using StellarFramework.Res.AB;

namespace StellarFramework.UI
{
    [Singleton("Managers/UIKit", SingletonLifeCycle.Global, false)]
    public class UIKit : MonoSingleton<UIKit>
    {
        // 基础配置
        private const string UI_ROOT_NAME = "UIRoot";
        private const string RELATIVE_ROOT_PATH = "UIPanel/UIRoot";
        private const string RELATIVE_PANEL_PREFIX = "UIPanel/";
        private const string AB_ROOT_PATH = "Assets/Resources/UIPanel/UIRoot.prefab";
        private const string AB_PANEL_PREFIX = "Assets/Resources/UIPanel/";

        private IResLoader _resLoader;
        private ResLoaderType _currentLoaderType;
        private bool _isInitialized;

        public Canvas RootCanvas { get; private set; }
        public CanvasScaler RootScaler { get; private set; }
        public Camera UICamera { get; private set; }

        private readonly Dictionary<UIPanelBase.PanelLayer, Transform> _layers = new Dictionary<UIPanelBase.PanelLayer, Transform>();
        private readonly Dictionary<Type, UIPanelBase> _panelCache = new Dictionary<Type, UIPanelBase>();
        private readonly Dictionary<Type, string> _panelPaths = new Dictionary<Type, string>();
        private readonly HashSet<Type> _loadingPanels = new HashSet<Type>();
        private CancellationTokenSource _destroyCts = new CancellationTokenSource();

        #region 初始化流程

        public void Init(ResLoaderType loaderType = ResLoaderType.Resources)
        {
            if (_isInitialized) return;
            _currentLoaderType = loaderType;
            InitResLoader(loaderType);

            GameObject rootPrefab = Resources.Load<GameObject>(RELATIVE_ROOT_PATH);
            SetupUIRoot(rootPrefab);
            _isInitialized = true;
            LogKit.Log($"[UIKit] 同步初始化完成. Mode: {loaderType}");
        }

        public async UniTask InitAsync(ResLoaderType loaderType = ResLoaderType.Resources)
        {
            if (_isInitialized) return;
            _currentLoaderType = loaderType;
            InitResLoader(loaderType);

            GameObject rootPrefab = await Resources.LoadAsync<GameObject>(RELATIVE_ROOT_PATH) as GameObject;
            SetupUIRoot(rootPrefab);
            _isInitialized = true;
            LogKit.Log($"[UIKit] 异步初始化完成. Mode: {loaderType}");
        }

        private void InitResLoader(ResLoaderType loaderType)
        {
            switch (loaderType)
            {
                case ResLoaderType.Resources:
                    _resLoader = ResKit.Allocate<ResourceLoader>();
                    break;
                case ResLoaderType.AssetBundle:
                    if (AssetBundleManager.Instance == null)
                    {
                        LogKit.LogWarning("[UIKit] 检测到 AssetBundleManager 未初始化，正在自动初始化...");
                        AssetBundleManager.Instance.OnSingletonInit();
                    }

                    _resLoader = ResKit.Allocate<AssetBundleLoader>();
                    break;
                case ResLoaderType.Addressable:
                    _resLoader = ResKit.Allocate<AddressableLoader>();
                    break;
                default:
                    LogKit.LogError($"[UIKit] 未知的加载器类型: {loaderType}，回退到 Resources");
                    _resLoader = ResKit.Allocate<ResourceLoader>();
                    break;
            }
        }

        #endregion

        #region 内部逻辑：路径适配

        private string GetUIAssetPath(string assetName, bool isRoot = false)
        {
            if (_currentLoaderType == ResLoaderType.Resources)
            {
                return isRoot ? RELATIVE_ROOT_PATH : $"{RELATIVE_PANEL_PREFIX}{assetName}";
            }

            if (_currentLoaderType == ResLoaderType.AssetBundle)
            {
                return isRoot ? AB_ROOT_PATH : $"{AB_PANEL_PREFIX}{assetName}.prefab";
            }

            return isRoot ? RELATIVE_ROOT_PATH : $"{RELATIVE_PANEL_PREFIX}{assetName}";
        }

        #endregion

        private void SetupUIRoot(GameObject rootPrefab)
        {
            if (rootPrefab == null)
            {
                LogKit.LogError($"[UIKit] 致命错误: 无法加载 UIRoot！请检查 Resources/UIPanel/UIRoot 是否存在，或是否已打入 AB 包。");
                return;
            }

            GameObject rootGo = Instantiate(rootPrefab);
            rootGo.name = "UIRoot";
            rootGo.transform.SetParent(transform);

            RootCanvas = rootGo.GetComponent<Canvas>();
            RootScaler = rootGo.GetComponent<CanvasScaler>();
            UICamera = rootGo.GetComponentInChildren<Camera>();

            DontDestroyOnLoad(rootGo);

            foreach (UIPanelBase.PanelLayer layer in Enum.GetValues(typeof(UIPanelBase.PanelLayer)))
            {
                string layerName = layer.ToString();
                Transform layerTrans = rootGo.transform.Find(layerName);
                if (layerTrans == null)
                {
                    var go = new GameObject(layerName);
                    layerTrans = go.AddComponent<RectTransform>().transform;
                    layerTrans.SetParent(rootGo.transform, false);
                    var rt = (RectTransform)layerTrans;
                    rt.FillParent();
                }

                layerTrans.SetSiblingIndex((int)layer);
                _layers[layer] = layerTrans;
            }
        }

        public void SetResolution(Vector2 resolution, float matchWidthOrHeight)
        {
            if (RootScaler == null) return;
            RootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            RootScaler.referenceResolution = resolution;
            RootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            RootScaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        #region 公开函数 (Async)

        public static async UniTask<T> PreloadPanelAsync<T>(object uiData = null) where T : UIPanelBase
        {
            return await Instance.LoadPanelInternal<T>(uiData, false);
        }

        public static async UniTask<T> OpenPanelAsync<T>(object uiData = null) where T : UIPanelBase
        {
            return await Instance.LoadPanelInternal<T>(uiData, true);
        }

        public static void ClosePanel<T>() where T : UIPanelBase
        {
            if (Instance == null) return;
            Instance.ClosePanelInternal(typeof(T));
        }

        public static void ClosePanel(Type type)
        {
            if (Instance == null) return;
            Instance.ClosePanelInternal(type);
        }

        public static T GetPanel<T>() where T : UIPanelBase
        {
            if (Instance == null) return null;
            if (Instance._panelCache.TryGetValue(typeof(T), out var panel))
                return panel as T;
            return null;
        }

        /// <summary>
        /// 仅刷新已打开的面板数据，不改变其激活状态或触发入场动画
        /// </summary>
        public static void RefreshPanel<T>(object uiData = null) where T : UIPanelBase
        {
            if (Instance == null) return;
            if (Instance._panelCache.TryGetValue(typeof(T), out var panel))
            {
                if (panel.gameObject.activeSelf)
                {
                    panel.RefreshData(uiData);
                }
            }
        }

        #endregion

        #region 内部逻辑 (Async)

        private async UniTask<T> LoadPanelInternal<T>(object uiData, bool openAfterLoad) where T : UIPanelBase
        {
            if (!_isInitialized)
            {
                LogKit.LogError("[UIKit] 未初始化，请先调用 UIKit.Instance.Init()");
                return null;
            }

            Type type = typeof(T);

            if (_loadingPanels.Contains(type))
            {
                await UniTask.WaitUntil(() => !_loadingPanels.Contains(type), cancellationToken: _destroyCts.Token);
                if (_panelCache.TryGetValue(type, out var p)) return p as T;
            }

            T panel = null;
            if (_panelCache.TryGetValue(type, out var cachedPanel))
            {
                panel = cachedPanel as T;
            }

            if (panel == null)
            {
                _loadingPanels.Add(type);
                try
                {
                    string panelName = type.Name;
                    string path = GetUIAssetPath(panelName);

                    var prefab = await _resLoader.LoadAsync<GameObject>(path).AttachExternalCancellation(_destroyCts.Token);
                    if (prefab != null)
                    {
                        _panelPaths[type] = path;
                        var go = Instantiate(prefab);
                        panel = go.GetComponent<T>();

                        if (panel == null)
                        {
                            LogKit.LogError($"[UIKit] Prefab {panelName} 缺少脚本组件！");
                            Destroy(go);
                            return null;
                        }

                        go.name = panelName;
                        if (_layers.TryGetValue(panel.Layer, out Transform layerTrans))
                            go.transform.SetParent(layerTrans, false);
                        else
                            go.transform.SetParent(_layers[UIPanelBase.PanelLayer.Middle], false);

                        var rt = panel.RectTransform;
                        rt.FillParent();
                        rt.localPosition = Vector3.zero;
                        go.SetActive(false);

                        panel.OnInit();
                        _panelCache[type] = panel;
                    }
                    else
                    {
                        LogKit.LogError($"[UIKit] 加载失败: {path} (Mode: {_currentLoaderType})");
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                finally
                {
                    _loadingPanels.Remove(type);
                }
            }

            if (panel != null && openAfterLoad)
            {
                // 如果面板已经处于激活状态，仅置顶并刷新数据，防止重复执行入场动画
                if (panel.gameObject.activeSelf)
                {
                    panel.transform.SetAsLastSibling();
                    panel.RefreshData(uiData);
                }
                else
                {
                    await panel.OnOpen(uiData);
                }
            }

            return panel;
        }

        private async void ClosePanelInternal(Type type)
        {
            if (_panelCache.TryGetValue(type, out var panel))
            {
                if (panel.gameObject.activeSelf)
                {
                    await panel.OnClose();
                }

                if (panel.DestroyOnClose)
                {
                    Destroy(panel.gameObject);
                    _panelCache.Remove(type);

                    if (_panelPaths.TryGetValue(type, out string path))
                    {
                        _resLoader.Unload(path);
                        _panelPaths.Remove(type);
                    }
                }
            }
        }

        #endregion

        #region 公开函数 (Sync)

        public static T PreloadPanel<T>(object uiData = null) where T : UIPanelBase
        {
            return Instance.LoadPanelInternalSync<T>(uiData, false);
        }

        public static T OpenPanel<T>(object uiData = null) where T : UIPanelBase
        {
            return Instance.LoadPanelInternalSync<T>(uiData, true);
        }

        #endregion

        #region 内部逻辑 (Sync)

        private T LoadPanelInternalSync<T>(object uiData, bool openAfterLoad) where T : UIPanelBase
        {
            if (!_isInitialized)
            {
                LogKit.LogError("[UIKit] 未初始化，请先调用 UIKit.Instance.Init()");
                return null;
            }

            Type type = typeof(T);

            if (_panelCache.TryGetValue(type, out var cachedPanel))
            {
                var p = cachedPanel as T;
                if (openAfterLoad && p != null)
                {
                    // 核心修改：同步加载分支同样处理状态判定
                    if (p.gameObject.activeSelf)
                    {
                        p.transform.SetAsLastSibling();
                        p.RefreshData(uiData);
                    }
                    else
                    {
                        p.OnOpen(uiData).Forget();
                    }
                }

                return p;
            }

            string panelName = type.Name;
            string path = GetUIAssetPath(panelName);
            GameObject prefab = _resLoader.Load<GameObject>(path);

            if (prefab != null)
            {
                _panelPaths[type] = path;
                var go = Instantiate(prefab);
                T panel = go.GetComponent<T>();

                if (panel == null)
                {
                    LogKit.LogError($"[UIKit] Prefab {panelName} 缺少脚本组件！");
                    Destroy(go);
                    return null;
                }

                go.name = panelName;
                if (_layers.TryGetValue(panel.Layer, out Transform layerTrans))
                {
                    go.transform.SetParent(layerTrans, false);
                }
                else
                {
                    go.transform.SetParent(_layers[UIPanelBase.PanelLayer.Middle], false);
                }

                var rt = panel.RectTransform;
                rt.FillParent();
                rt.localPosition = Vector3.zero;
                go.SetActive(false);

                panel.OnInit();
                _panelCache[type] = panel;

                if (openAfterLoad)
                {
                    panel.OnOpen(uiData).Forget();
                }

                return panel;
            }
            else
            {
                LogKit.LogError($"[UIKit] 同步加载失败，未找到资源: {path} (Mode: {_currentLoaderType})");
                return null;
            }
        }

        #endregion

        protected override void OnDestroy()
        {
            _destroyCts.Cancel();
            _destroyCts.Dispose();

            if (_resLoader != null)
            {
                ResKit.Recycle(_resLoader);
                _resLoader = null;
            }

            base.OnDestroy();
        }
    }
}