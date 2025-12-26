
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

        [Header("Settings")]
        [SerializeField] protected PanelLayer layer = PanelLayer.Middle;
        [SerializeField] protected bool destroyOnClose = false;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        
        public PanelLayer Layer => layer;
        public bool DestroyOnClose => destroyOnClose;
        
        // 懒加载属性，防止 Awake 顺序问题
        public CanvasGroup CanvasGroup 
        {
            get { if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>(); return _canvasGroup; }
        }
        
        public RectTransform RectTransform
        {
            get { if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>(); return _rectTransform; }
        }

        public virtual void OnInit() { }

        public virtual async UniTask OnOpen(object uiData = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            
            // 默认重置 CanvasGroup
            CanvasGroup.alpha = 1;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;

            await UniTask.CompletedTask;
        }

        public virtual async UniTask OnClose()
        {
            await UniTask.CompletedTask;
            gameObject.SetActive(false);
        }

        protected void CloseSelf() => UIKit.ClosePanel(this.GetType());
    }
}