using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UI 面板数据基类
    /// 我统一约束所有面板入参必须继承此基类，拒绝 object 弱类型传参，避免值类型装箱
    /// </summary>
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

        [Header("设置")] [SerializeField] protected PanelLayer layer = PanelLayer.Middle;
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

                var rootTrans = transform.FindChildByName("root");
                if (rootTrans == null)
                {
                    Debug.LogError($"[UIPanelBase] Root 获取失败: 面板类={GetType().Name}，物体名={name}，状态=缺少名为 root 的子节点");
                    return null;
                }

                _rootObj = rootTrans.gameObject;
                return _rootObj;
            }
        }

        /// <summary>
        /// 我只在首次实例化后调用一次，用于做按钮绑定、组件缓存等初始化
        /// </summary>
        public virtual void OnInit()
        {
        }

        /// <summary>
        /// 我在面板从关闭到打开时调用
        /// </summary>
        public virtual void OnOpen(UIPanelDataBase data)
        {
        }

        /// <summary>
        /// 我在面板已打开状态下再次请求打开，或者外部主动刷新时调用
        /// </summary>
        public virtual void OnRefresh(UIPanelDataBase data)
        {
        }

        /// <summary>
        /// 我在面板关闭时调用
        /// </summary>
        public virtual void OnClose()
        {
        }

        /// <summary>
        /// 我提供统一的强类型取参入口，避免子类重复写强转和判空逻辑。
        /// 如果数据为空或类型不匹配，我会打印详细错误并返回 false，调用方应立即 return。
        /// </summary>
        protected bool TryGetPanelData<T>(UIPanelDataBase data, out T typedData) where T : UIPanelDataBase
        {
            typedData = null;

            if (data == null)
            {
                Debug.LogError(
                    $"[UIPanelBase] 面板数据为空: 面板类={GetType().Name}，物体名={name}，期望数据类型={typeof(T).Name}，当前数据=null");
                return false;
            }

            typedData = data as T;
            if (typedData != null)
            {
                return true;
            }

            Debug.LogError(
                $"[UIPanelBase] 面板数据类型不匹配: 面板类={GetType().Name}，物体名={name}，期望数据类型={typeof(T).Name}，当前数据类型={data.GetType().Name}");
            typedData = null;
            return false;
        }

        /// <summary>
        /// 我提供一个主动关闭自身的便捷入口，避免业务层直接依赖外部关闭逻辑
        /// </summary>
        protected void CloseSelf()
        {
            UIKit.ClosePanel(GetType());
        }
    }
}