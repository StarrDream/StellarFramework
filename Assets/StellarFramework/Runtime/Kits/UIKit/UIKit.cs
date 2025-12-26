using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using StellarFramework.Res;

namespace StellarFramework.UI
{
    [Singleton("Managers/UIKit", SingletonLifeCycle.Global, false)]
    public class UIKit : MonoSingleton<UIKit>
    {
        private const string UI_ROOT_PREFAB_PATH = "UIPanel/UIRoot";
        private const string PANEL_PATH_PREFIX = "UIPanel/";

        private IResLoader _resLoader;
        private bool _isInitialized;

        public Canvas RootCanvas { get; private set; }
        public CanvasScaler RootScaler { get; private set; }
        public Camera UICamera { get; private set; }

        private readonly Dictionary<UIPanelBase.PanelLayer, Transform> _layers = new Dictionary<UIPanelBase.PanelLayer, Transform>();
        private readonly Dictionary<Type, UIPanelBase> _panelCache = new Dictionary<Type, UIPanelBase>();
        private readonly Dictionary<Type, string> _panelPaths = new Dictionary<Type, string>();

        //  记录正在加载中的 Panel 类型 (并发锁)
        private readonly HashSet<Type> _loadingPanels = new HashSet<Type>();

        //  销毁时的取消令牌
        private CancellationTokenSource _destroyCts = new CancellationTokenSource();

        public void Init(ResLoaderType loaderType = ResLoaderType.Resources)
        {
            if (_isInitialized) return;
            InitResLoader(loaderType);
            GameObject rootPrefab = Resources.Load<GameObject>(UI_ROOT_PREFAB_PATH);
            SetupUIRoot(rootPrefab);
            _isInitialized = true;
            LogKit.Log($"[UIKit] 同步初始化完成. PanelLoader: {loaderType}");
        }

        public async UniTask InitAsync(ResLoaderType loaderType = ResLoaderType.Resources)
        {
            if (_isInitialized) return;
            InitResLoader(loaderType);
            ResourceRequest req = Resources.LoadAsync<GameObject>(UI_ROOT_PREFAB_PATH);
            await req;
            SetupUIRoot(req.asset as GameObject);
            _isInitialized = true;
            LogKit.Log($"[UIKit] 异步初始化完成. PanelLoader: {loaderType}");
        }

        private void InitResLoader(ResLoaderType loaderType)
        {
            if (loaderType == ResLoaderType.Addressable)
                _resLoader = ResKit.Allocate<AddressableLoader>();
            else
                _resLoader = ResKit.Allocate<ResourceLoader>();
        }

        private void SetupUIRoot(GameObject rootPrefab)
        {
            if (rootPrefab == null)
            {
                LogKit.LogError($"[UIKit] 致命错误: 无法在 Resources/{UI_ROOT_PREFAB_PATH} 找到 UIRoot 预制体！");
                return;
            }

            GameObject rootGo = Instantiate(rootPrefab);
            rootGo.name = "UIRoot";
            rootGo.transform.SetParent(transform);
            RootCanvas = rootGo.GetComponent<Canvas>();
            RootScaler = rootGo.GetComponent<CanvasScaler>();
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

        #region Public API

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

        #endregion

        #region Internal Logic

        private async UniTask<T> LoadPanelInternal<T>(object uiData, bool openAfterLoad) where T : UIPanelBase
        {
            if (!_isInitialized) return null;

            Type type = typeof(T);

            //  并发锁：如果正在加载，等待其完成
            if (_loadingPanels.Contains(type))
            {
                // 绑定销毁令牌，防止死等
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
                _loadingPanels.Add(type); // 上锁
                try
                {
                    string panelName = type.Name;
                    string path = PANEL_PATH_PREFIX + panelName;

                    //  绑定销毁令牌，如果 UIKit 被销毁，取消加载
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
                }
                catch (OperationCanceledException)
                {
                    // 被取消，不做处理
                    return null;
                }
                finally
                {
                    _loadingPanels.Remove(type); // 解锁
                }
            }

            if (panel != null && openAfterLoad)
            {
                await panel.OnOpen(uiData);
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