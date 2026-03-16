using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanelBase : MonoBehaviour
    {
        public enum PanelLayer
        {
            Bottom = 0,
            Middle = 1,
            Top = 2,
            Popup = 3,
            System = 4
        }

        [Header("Settings")] [SerializeField] protected PanelLayer layer = PanelLayer.Middle;
        [SerializeField] protected bool destroyOnClose = false;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;

        public PanelLayer Layer => layer;
        public bool DestroyOnClose => destroyOnClose;

        // 懒加载属性，防止 Awake 顺序问题
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
                return _rectTransform;
            }
        }

        /// <summary>
        /// 面板首次实例化时调用，用于绑定固定的按钮事件或初始化组件引用
        /// </summary>
        public virtual void OnInit()
        {
        }

        /// <summary>
        /// 面板从隐藏变为显示时调用，用于处理入场动画、状态重置
        /// </summary>
        public virtual async UniTask OnOpen(object uiData = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            // 默认重置 CanvasGroup 状态
            CanvasGroup.alpha = 1;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;

            // 首次打开时，默认触发一次数据刷新逻辑，确保数据与表现同步
            RefreshData(uiData);

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 面板刷新数据时调用。当面板已处于打开状态并再次被 Open，或被外部主动要求刷新时触发
        /// </summary>
        public virtual void RefreshData(object uiData = null)
        {
            // 子类重写此方法以更新 UI 表现
        }

        /// <summary>
        /// 面板关闭时调用，用于处理退场动画、清理临时事件
        /// </summary>
        public virtual async UniTask OnClose()
        {
            await UniTask.CompletedTask;
            gameObject.SetActive(false);
        }

        protected void CloseSelf() => UIKit.ClosePanel(this.GetType());
    }
}