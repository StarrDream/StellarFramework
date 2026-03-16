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
        private GameObject _rootObj;

        public PanelLayer Layer => layer;
        public bool DestroyOnClose => destroyOnClose;

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

        public GameObject Root
        {
            get
            {
                if (_rootObj == null)
                {
                    var rootTrans = transform.FindChildByName("root");
                    if (rootTrans != null)
                    {
                        _rootObj = rootTrans.gameObject;
                    }
                    else
                    {
                        LogKit.LogError($"[UIPanelBase] 面板 {gameObject.name} 缺少名为 'root' 的子节点！");
                    }
                }

                return _rootObj;
            }
        }

        public virtual void OnInit()
        {
        }

        public virtual async UniTask OnOpen(object uiData = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            CanvasGroup.alpha = 1;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;

            RefreshData(uiData);

            await UniTask.CompletedTask;
        }

        public virtual void RefreshData(object uiData = null)
        {
        }

        public virtual async UniTask OnClose()
        {
            await UniTask.CompletedTask;
            gameObject.SetActive(false);
        }

        protected void CloseSelf() => UIKit.ClosePanel(this.GetType());
    }
}