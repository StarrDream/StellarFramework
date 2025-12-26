// ========== ColliderEventObj.cs ==========
// Path: Assets/StellarFramework/Runtime/Kits/RaycastKit/ColliderEventObj.cs

using System;
using UnityEngine;
using UnityEngine.Events;

namespace StellarFramework
{
    /// <summary>
    /// 物体鼠标交互组件
    /// <para>功能：为 3D/2D 物体提供类似 UI 的交互事件 (Click, DoubleClick, Drag, Hover)。</para>
    /// <para>特点：支持自定义相机、Raycast 过滤、UI 遮挡处理。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ColliderEventObj : MonoBehaviour
    {
        [Header("Detection Mode (检测模式)")] [Tooltip("是否使用自定义 Raycast 进行检测（推荐 True，更可控，支持屏蔽 UI 等）。False 则使用 Unity 内置 OnMouseXXX。")]
        public bool useRaycastDetection;

        [Tooltip("射线检测所用摄像机；为空则自动获取 MainCamera")]
        public Camera customCamera;

        [Header("Mouse Settings (鼠标设置)")] [Tooltip("响应的鼠标按键 (0=左键, 1=右键, 2=中键)")]
        public int mouseButton;

        [Tooltip("视为点击的最大移动像素（拖拽超过该值则不算点击）")] public float maxClickMoveDistance = 5f;

        [Tooltip("双击最大间隔时间 (秒)")] public float doubleClickInterval = 0.28f;

        [Tooltip("在拖拽过程中是否连续触发 Drag 事件（每帧）")] public bool continuousDragCallback = true;

        [Header("Layer / UI Filtering (过滤设置)")] [Tooltip("当鼠标悬停在 UI 上时，是否忽略对物体的检测")]
        public bool ignoreWhenPointerOverUI = true;

        [Tooltip("Raycast 检测的层级掩码 (仅在 useRaycastDetection=True 时有效)")]
        public LayerMask raycastLayerMask = ~0;

        [Header("Unity Events (事件回调)")] public MouseUnityEvent onMouseEnter;
        public MouseUnityEvent onMouseExit;
        public MouseUnityEvent onMouseDown;
        public MouseUnityEvent onMouseUp;
        public MouseUnityEvent onClick;
        public MouseUnityEvent onDoubleClick;
        public MouseDragUnityEvent onBeginDrag;
        public MouseDragUnityEvent onDrag;
        public MouseDragUnityEvent onEndDrag;

        private Collider2D _col2D;
        private Collider _col3D;

        // 内部状态变量
        private float _lastClickTime = -999f; // 上次点击时间 (用于双击)
        private Vector2 _lastScreenPos; // 上一帧鼠标位置
        private bool _mouseDownInside; // 鼠标是否在物体范围内按下
        private Vector2 _mouseDownScreenPos; // 按下时的屏幕位置
        private Vector2 _totalDragDelta; // 总拖拽位移

        // 公开状态
        public bool IsPointerOver { get; private set; }
        public bool IsDragging { get; private set; }

        private void Awake()
        {
            _col3D = GetComponent<Collider>();
            _col2D = GetComponent<Collider2D>();
            if (_col3D == null && _col2D == null) LogKit.LogWarning($"[ColliderEventObj] {name} Missing Collider/Collider2D.");
        }

        private void Update()
        {
            if (!useRaycastDetection) return;

            // 1. UI 遮挡检测 (使用 RaycastKit)
            if (ignoreWhenPointerOverUI && RaycastKit.IsPointerOverUI())
            {
                HandlePotentialExit();
                return;
            }

            // 2. 目标命中检测 (使用 RaycastKit)
            Vector2 mousePos = Input.mousePosition;
            bool hitThis = CheckHit(mousePos, out Vector3 hitPoint);

            if (hitThis)
            {
                // 处理进入事件
                if (!IsPointerOver)
                {
                    IsPointerOver = true;
                    FireMouseEnter(mousePos, hitPoint);
                }

                // 处理按下事件
                if (Input.GetMouseButtonDown(mouseButton))
                {
                    _mouseDownInside = true;
                    _mouseDownScreenPos = mousePos;
                    _totalDragDelta = Vector2.zero;
                    _lastScreenPos = mousePos;
                    FireMouseDown(mousePos, hitPoint);
                }
            }
            else
            {
                // 处理离开事件
                HandlePotentialExit();
            }

            // 3. 拖拽逻辑处理
            HandleDragLogic(mousePos, hitThis, hitPoint);
        }

        /// <summary>
        /// 检查鼠标是否击中了当前物体
        /// </summary>
        private bool CheckHit(Vector2 screenPos, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;

            // 优先检测 3D Collider
            if (_col3D != null)
            {
                if (RaycastKit.IsTargetUnderScreenPoint(screenPos, _col3D, out hitPoint, customCamera, raycastLayerMask))
                    return true;
            }

            // 其次检测 2D Collider
            if (_col2D != null)
            {
                if (RaycastKit.IsTargetUnderScreenPoint2D(screenPos, _col2D, out hitPoint, customCamera, raycastLayerMask))
                    return true;
            }

            return false;
        }

        private void HandlePotentialExit()
        {
            if (IsPointerOver)
            {
                IsPointerOver = false;
                FireMouseExit(Input.mousePosition, null);
            }
        }

        private void HandleDragLogic(Vector2 mousePos, bool hitThis, Vector3 hitPoint)
        {
            if (!_mouseDownInside) return;

            // 按住鼠标：处理拖拽
            if (Input.GetMouseButton(mouseButton))
            {
                var frameDelta = mousePos - _lastScreenPos;
                _totalDragDelta += frameDelta;

                // 判断是否达到拖拽阈值
                if (!IsDragging && _totalDragDelta.magnitude > maxClickMoveDistance)
                {
                    IsDragging = true;
                    FireBeginDrag(mousePos, hitThis ? hitPoint : null, frameDelta);
                }
                else if (IsDragging && continuousDragCallback)
                {
                    FireDrag(mousePos, hitThis ? hitPoint : null, frameDelta);
                }

                _lastScreenPos = mousePos;
            }

            // 松开鼠标：处理点击或结束拖拽
            if (Input.GetMouseButtonUp(mouseButton))
            {
                FireMouseUp(mousePos, hitThis ? hitPoint : null);

                if (IsDragging)
                {
                    FireEndDrag(mousePos, hitThis ? hitPoint : null);
                }
                else if (hitThis && (mousePos - _mouseDownScreenPos).magnitude <= maxClickMoveDistance)
                {
                    // 判定为点击
                    var t = Time.time;
                    var isDouble = t - _lastClickTime <= doubleClickInterval;
                    _lastClickTime = t;
                    FireClick(mousePos, hitPoint, isDouble);
                }

                _mouseDownInside = false;
                IsDragging = false;
            }
        }

        #region Events & Fire Methods (事件分发)

        [Serializable]
        public class MouseUnityEvent : UnityEvent<ColliderEventObj, MouseEventData>
        {
        }

        [Serializable]
        public class MouseDragUnityEvent : UnityEvent<ColliderEventObj, MouseDragEventData>
        {
        }

        public class MouseEventData
        {
            public int button;
            public bool isDoubleClick;
            public Vector2 screenPosition;
            public float time;
            public Vector3? worldHitPoint;
        }

        public class MouseDragEventData : MouseEventData
        {
            public Vector2 dragDelta;
            public bool isDragging;
            public Vector2 totalDragDelta;
        }

        private void FireMouseEnter(Vector2 sp, Vector3? hp)
        {
            var d = new MouseEventData { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton };
            MouseEnterEvent?.Invoke(this, d);
            onMouseEnter?.Invoke(this, d);
        }

        private void FireMouseExit(Vector2 sp, Vector3? hp)
        {
            var d = new MouseEventData { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton };
            MouseExitEvent?.Invoke(this, d);
            onMouseExit?.Invoke(this, d);
        }

        private void FireMouseDown(Vector2 sp, Vector3? hp)
        {
            var d = new MouseEventData { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton };
            MouseDownEvent?.Invoke(this, d);
            onMouseDown?.Invoke(this, d);
        }

        private void FireMouseUp(Vector2 sp, Vector3? hp)
        {
            var d = new MouseEventData { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton };
            MouseUpEvent?.Invoke(this, d);
            onMouseUp?.Invoke(this, d);
        }

        private void FireClick(Vector2 sp, Vector3? hp, bool isDouble)
        {
            var d = new MouseEventData { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton, isDoubleClick = isDouble };
            if (isDouble)
            {
                DoubleClickEvent?.Invoke(this, d);
                onDoubleClick?.Invoke(this, d);
            }
            else
            {
                ClickEvent?.Invoke(this, d);
                onClick?.Invoke(this, d);
            }
        }

        private void FireBeginDrag(Vector2 sp, Vector3? hp, Vector2 delta)
        {
            var d = new MouseDragEventData
                { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton, dragDelta = delta, totalDragDelta = _totalDragDelta, isDragging = IsDragging };
            BeginDragEvent?.Invoke(this, d);
            onBeginDrag?.Invoke(this, d);
        }

        private void FireDrag(Vector2 sp, Vector3? hp, Vector2 delta)
        {
            var d = new MouseDragEventData
                { screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton, dragDelta = delta, totalDragDelta = _totalDragDelta, isDragging = IsDragging };
            DragEvent?.Invoke(this, d);
            onDrag?.Invoke(this, d);
        }

        private void FireEndDrag(Vector2 sp, Vector3? hp)
        {
            var d = new MouseDragEventData
            {
                screenPosition = sp, worldHitPoint = hp, time = Time.time, button = mouseButton, dragDelta = Vector2.zero, totalDragDelta = _totalDragDelta, isDragging = IsDragging
            };
            EndDragEvent?.Invoke(this, d);
            onEndDrag?.Invoke(this, d);
        }

        // C# 事件定义
        public event Action<ColliderEventObj, MouseEventData> MouseEnterEvent;
        public event Action<ColliderEventObj, MouseEventData> MouseExitEvent;
        public event Action<ColliderEventObj, MouseEventData> MouseDownEvent;
        public event Action<ColliderEventObj, MouseEventData> MouseUpEvent;
        public event Action<ColliderEventObj, MouseEventData> ClickEvent;
        public event Action<ColliderEventObj, MouseEventData> DoubleClickEvent;
        public event Action<ColliderEventObj, MouseDragEventData> BeginDragEvent;
        public event Action<ColliderEventObj, MouseDragEventData> DragEvent;
        public event Action<ColliderEventObj, MouseDragEventData> EndDragEvent;

        #endregion

        // 兼容旧的 Unity OnMouseXXX (仅当不使用 RaycastDetection 时启用)
        private void OnMouseEnter()
        {
            if (!useRaycastDetection && !RaycastKit.IsPointerOverUI())
            {
                IsPointerOver = true;
                FireMouseEnter(Input.mousePosition, null);
            }
        }

        private void OnMouseExit()
        {
            if (!useRaycastDetection)
            {
                IsPointerOver = false;
                FireMouseExit(Input.mousePosition, null);
            }
        }

        private void OnMouseDown()
        {
            if (!useRaycastDetection && !RaycastKit.IsPointerOverUI() && Input.GetMouseButtonDown(mouseButton))
            {
                _mouseDownInside = true;
                _mouseDownScreenPos = Input.mousePosition;
                _lastScreenPos = _mouseDownScreenPos;
                _totalDragDelta = Vector2.zero;
                FireMouseDown(_mouseDownScreenPos, null);
            }
        }

        private void OnMouseUp()
        {
            if (!useRaycastDetection && _mouseDownInside && Input.GetMouseButtonUp(mouseButton))
            {
                FireMouseUp(Input.mousePosition, null);
                if (!IsDragging && (Input.mousePosition - (Vector3)_mouseDownScreenPos).magnitude <= maxClickMoveDistance)
                {
                    var t = Time.time;
                    var isDouble = t - _lastClickTime <= doubleClickInterval;
                    _lastClickTime = t;
                    FireClick(Input.mousePosition, null, isDouble);
                }

                _mouseDownInside = false;
                IsDragging = false;
            }
        }

        private void OnMouseDrag()
        {
            if (!useRaycastDetection && _mouseDownInside)
            {
                Vector2 pos = Input.mousePosition;
                var delta = pos - _lastScreenPos;
                _totalDragDelta += delta;
                if (!IsDragging && _totalDragDelta.magnitude > maxClickMoveDistance)
                {
                    IsDragging = true;
                    FireBeginDrag(pos, null, delta);
                }
                else if (IsDragging && continuousDragCallback)
                {
                    FireDrag(pos, null, delta);
                }

                _lastScreenPos = pos;
            }
        }
    }
}