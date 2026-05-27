using System;
using UnityEngine;

namespace StellarFramework.UI
{
    public abstract class UIPanelDataBase
    {
    }

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

        public enum PanelCanvasRole
        {
            Dynamic = 0,
            Static = 1
        }

        [Header("Base")]
        [SerializeField] protected PanelLayer layer = PanelLayer.Middle;
        [SerializeField] protected PanelCanvasRole canvasRole = PanelCanvasRole.Dynamic;
        [SerializeField] protected bool destroyOnClose = false;

        [Header("Stack")]
        [Tooltip("Fullscreen panels pause and hide lower stack panels.")]
        [SerializeField] protected bool isFullScreen = false;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private GameObject _rootObj;

        public PanelLayer Layer => layer;
        public PanelCanvasRole CanvasRole => canvasRole;
        public bool DestroyOnClose => destroyOnClose;
        public bool IsFullScreen => isFullScreen;

        public static event Action<UIPanelBase> OnPanelClosedGlobal;

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

        public GameObject Root
        {
            get
            {
                if (_rootObj != null)
                {
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

        public virtual void OnInit()
        {
        }

        public virtual void OnOpen(UIPanelDataBase data)
        {
        }

        public virtual void OnRefresh(UIPanelDataBase data)
        {
        }

        public virtual void OnClose()
        {
            OnPanelClosedGlobal?.Invoke(this);
        }

        public virtual void OnPause()
        {
        }

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
