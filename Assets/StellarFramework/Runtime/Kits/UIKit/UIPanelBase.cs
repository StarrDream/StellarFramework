using System;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UIKit 面板数据基类。
    /// 需要向 Panel 传参时定义强类型子类，而不是使用 Dictionary/object 弱类型传递。
    /// </summary>
    public abstract class UIPanelDataBase
    {
    }

    /// <summary>
    /// UIKit 面板基类。
    /// 提供层级、Canvas 角色、缓存/销毁策略和标准生命周期回调。
    /// </summary>
    /// <remarks>
    /// OnInit 只在面板实例首次创建时调用；OnOpen 可在缓存面板每次重新打开时重复调用。
    /// destroyOnClose=false 时 Close 只隐藏并保留实例；true 时 Close 后实例会被销毁并从缓存移除。
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanelBase : MonoBehaviour
    {
        /// <summary>Panel 在对应 Canvas 下的渲染/逻辑层级。</summary>
        public enum PanelLayer
        {
            Bottom = 0,
            Middle = 1,
            Top = 2,
            Popup = 3,
            System = 4
        }

        /// <summary>
        /// Panel 使用的 Canvas 角色。
        /// Dynamic 适合常规可切换页面，Static 适合常驻 HUD 等内容。
        /// </summary>
        public enum PanelCanvasRole
        {
            Dynamic = 0,
            Static = 1
        }

        /// <summary>
        /// Panel 在 UIRoot 中使用的布局区域。
        /// FullScreen 适合背景、遮罩、转场和必须铺满屏幕的页面；
        /// SafeArea 适合顶部导航、按钮、文字等需要避开刘海/圆角区域的交互内容。
        /// </summary>
        public enum PanelLayoutRegion
        {
            FullScreen = 0,
            SafeArea = 1
        }

        [Header("Base")]
        [SerializeField] protected PanelLayer layer = PanelLayer.Middle;
        [SerializeField] protected PanelCanvasRole canvasRole = PanelCanvasRole.Dynamic;
        [SerializeField] protected PanelLayoutRegion layoutRegion = PanelLayoutRegion.FullScreen;
        [SerializeField] protected bool destroyOnClose = false;

        [Header("Stack")]
        [Tooltip("Fullscreen panels pause and hide lower stack panels.")]
        [SerializeField] protected bool isFullScreen = false;

        [Header("Root")]
        [Tooltip("面板主内容根节点。可在 Inspector 中显式绑定；未绑定时自动查找名为 root 的子节点。")]
        [SerializeField] private GameObject rootNode;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private GameObject _rootObj;

        /// <summary>配置的面板层级。</summary>
        public PanelLayer Layer => layer;
        /// <summary>配置的 Canvas 角色。</summary>
        public PanelCanvasRole CanvasRole => canvasRole;
        /// <summary>Panel 使用全屏区域还是 Safe Area 区域。</summary>
        public PanelLayoutRegion LayoutRegion => layoutRegion;
        /// <summary>关闭时是否销毁实例而不是缓存隐藏。</summary>
        public bool DestroyOnClose => destroyOnClose;
        /// <summary>是否作为全屏栈面板处理。</summary>
        public bool IsFullScreen => isFullScreen;

        /// <summary>
        /// 任意 Panel 执行 OnClose 时触发的全局通知。
        /// 适合场景级导航器更新入口状态，不建议承载具体业务逻辑。
        /// </summary>
        public static event Action<UIPanelBase> OnPanelClosedGlobal;

        /// <summary>当前 Panel 的 CanvasGroup，首次访问时缓存组件引用。</summary>
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                }

                return _canvasGroup;
            }
        }

        /// <summary>当前 Panel 的 RectTransform，首次访问时缓存组件引用。</summary>
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }

                return _rectTransform;
            }
        }

        /// <summary>
        /// Panel 主内容根节点。
        /// 优先使用 Inspector 绑定；未绑定时查找名为 "root" 的直接子节点。
        /// </summary>
        public GameObject Root
        {
            get
            {
                if (_rootObj != null)
                {
                    return _rootObj;
                }

                // 优先使用 Inspector 绑定的根节点；未绑定时再自动查找名为 root 的子节点。
                if (rootNode != null)
                {
                    _rootObj = rootNode;
                    return _rootObj;
                }

                Transform rootTrans = transform.Find("root");
                if (rootTrans == null)
                {
                    Debug.LogError(
                        $"[UIPanelBase] Root not found. Panel={GetType().Name}, GameObject={name}, RequiredChild=root");
                    return null;
                }

                _rootObj = rootTrans.gameObject;
                return _rootObj;
            }
        }

        /// <summary>面板实例首次创建完成后的初始化回调。</summary>
        public virtual void OnInit()
        {
        }

        /// <summary>每次打开面板时调用。</summary>
        public virtual void OnOpen(UIPanelDataBase data)
        {
        }

        /// <summary>对已打开/缓存面板应用新数据时调用。</summary>
        public virtual void OnRefresh(UIPanelDataBase data)
        {
        }

        /// <summary>
        /// 面板关闭时调用。重写时若需要保留全局关闭通知，应调用 base.OnClose()。
        /// </summary>
        public virtual void OnClose()
        {
            OnPanelClosedGlobal?.Invoke(this);
        }

        /// <summary>被更高层全屏面板压入栈上方时调用。</summary>
        public virtual void OnPause()
        {
        }

        /// <summary>重新成为栈顶活动面板时调用。</summary>
        public virtual void OnResume()
        {
        }

        protected bool TryGetPanelData<T>(UIPanelDataBase data, out T typedData) where T : UIPanelDataBase
        {
            typedData = null;
            if (data == null)
            {
                Debug.LogError(
                    $"[UIPanelBase] Panel data is null. Panel={GetType().Name}, Expected={typeof(T).Name}");
                return false;
            }

            typedData = data as T;
            if (typedData != null)
            {
                return true;
            }

            Debug.LogError(
                $"[UIPanelBase] Panel data type mismatch. Panel={GetType().Name}, Expected={typeof(T).Name}, Actual={data.GetType().Name}");
            return false;
        }

        protected void CloseSelf()
        {
            UIKit.ClosePanel(GetType());
        }
    }
}
