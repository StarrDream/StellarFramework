#region UI拖拽工具

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace StellarFramework
{
    /// <summary>
    ///     UI元素拖拽组件 - 使UI元素可拖拽移动
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("拖拽设置")] [Tooltip("是否限制在父容器内")]
        public bool constrainToParent;

        [Tooltip("是否限制在画布边界内")] public bool constrainToCanvas = true;

        [Tooltip("拖拽时的光标样式")] public Texture2D dragCursor;

        [Header("事件回调")] [SerializeField] private UnityEvent onDragStart = new();

        [SerializeField] private UnityEvent onDragEnd = new();

        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform canvasRectTransform;
        [SerializeField] private bool isInit;
        private CursorMode cursorMode = CursorMode.Auto;
        private Vector2 offset;
        private Vector2 originalPosition;

        /// <summary>
        ///     获取拖拽开始事件
        /// </summary>
        public UnityEvent OnDragStart => onDragStart;

        /// <summary>
        ///     获取拖拽结束事件
        /// </summary>
        public UnityEvent OnDragEnd => onDragEnd;

        public void OnBeginDrag(PointerEventData eventData)
        {
            Init();
            // 更改光标样式
            if (dragCursor != null) Cursor.SetCursor(dragCursor, Vector2.zero, cursorMode);

            // 计算偏移量
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out offset);

            onDragStart?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform == null || canvas == null) return;

            Vector2 localPointerPosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPointerPosition))
            {
                rectTransform.localPosition = localPointerPosition - offset;

                // 应用约束
                ApplyConstraints();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 恢复光标样式
            if (dragCursor != null) Cursor.SetCursor(null, Vector2.zero, cursorMode);

            onDragEnd?.Invoke();
        }

        private void Init()
        {
            if (isInit) return;

            rectTransform = GetComponent<RectTransform>();
            originalPosition = rectTransform.anchoredPosition;
            canvas = GetCanvas();
            if (canvas != null) canvasRectTransform = canvas.GetComponent<RectTransform>();

            isInit = true;
        }

        /// <summary>
        ///     获取UI元素所在的Canvas
        /// </summary>
        private Canvas GetCanvas()
        {
            var canvases = GetComponentsInParent<Canvas>();
            return canvases.Length > 0 ? canvases[0] : null;
        }

        /// <summary>
        ///     重置到原始位置
        /// </summary>
        public void ResetPosition()
        {
            if (rectTransform != null) rectTransform.anchoredPosition = originalPosition;
        }

        /// <summary>
        ///     获取当前锚点位置
        /// </summary>
        public Vector2 GetCurrentAnchoredPosition()
        {
            return rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
        }

        /// <summary>
        ///     应用位置约束
        /// </summary>
        private void ApplyConstraints()
        {
            if (rectTransform == null || canvasRectTransform == null) return;

            var constrainedPosition = rectTransform.anchoredPosition;

            // 限制在画布内
            if (constrainToCanvas)
            {
                var canvasSize = canvasRectTransform.rect.size;
                var pivot = rectTransform.pivot;
                var size = rectTransform.rect.size;

                // 计算边界
                var minX = -canvasSize.x * canvasRectTransform.pivot.x + size.x * pivot.x;
                var maxX = canvasSize.x * (1 - canvasRectTransform.pivot.x) - size.x * (1 - pivot.x);
                var minY = -canvasSize.y * canvasRectTransform.pivot.y + size.y * pivot.y;
                var maxY = canvasSize.y * (1 - canvasRectTransform.pivot.y) - size.y * (1 - pivot.y);

                constrainedPosition.x = Mathf.Clamp(constrainedPosition.x, minX, maxX);
                constrainedPosition.y = Mathf.Clamp(constrainedPosition.y, minY, maxY);
            }

            // 限制在父容器内
            if (constrainToParent && rectTransform.parent != null)
            {
                var parentRectTransform = rectTransform.parent as RectTransform;
                if (parentRectTransform != null)
                {
                    var parentSize = parentRectTransform.rect.size;
                    var pivot = rectTransform.pivot;
                    var size = rectTransform.rect.size;

                    // 计算边界
                    var minX = -parentSize.x * parentRectTransform.pivot.x + size.x * pivot.x;
                    var maxX = parentSize.x * (1 - parentRectTransform.pivot.x) - size.x * (1 - pivot.x);
                    var minY = -parentSize.y * parentRectTransform.pivot.y + size.y * pivot.y;
                    var maxY = parentSize.y * (1 - parentRectTransform.pivot.y) - size.y * (1 - pivot.y);

                    constrainedPosition.x = Mathf.Clamp(constrainedPosition.x, minX, maxX);
                    constrainedPosition.y = Mathf.Clamp(constrainedPosition.y, minY, maxY);
                }
            }

            rectTransform.anchoredPosition = constrainedPosition;
        }

        /// <summary>
        ///     设置拖拽光标
        /// </summary>
        public void SetDragCursor(Texture2D cursor, CursorMode mode = CursorMode.Auto)
        {
            dragCursor = cursor;
            cursorMode = mode;
        }

        /// <summary>
        ///     启用/禁用拖拽功能
        /// </summary>
        public void SetDraggable(bool draggable)
        {
            enabled = draggable;
        }
    }

    /// <summary>
    ///     UI拖拽管理器 - 提供全局拖拽控制
    /// </summary>
    public static class UIDragManager
    {
        private static readonly List<UIDragger> draggerList = new();

        /// <summary>
        ///     注册拖拽组件
        /// </summary>
        public static void RegisterDragger(UIDragger dragger)
        {
            if (!draggerList.Contains(dragger)) draggerList.Add(dragger);
        }

        /// <summary>
        ///     注销拖拽组件
        /// </summary>
        public static void UnregisterDragger(UIDragger dragger)
        {
            draggerList.Remove(dragger);
        }

        /// <summary>
        ///     启用/禁用所有拖拽组件
        /// </summary>
        public static void SetAllDraggable(bool draggable)
        {
            foreach (var dragger in draggerList)
                if (dragger != null)
                    dragger.SetDraggable(draggable);
        }

        /// <summary>
        ///     重置所有注册的拖拽组件的位置
        /// </summary>
        public static void ResetAllPositions()
        {
            foreach (var dragger in draggerList)
                if (dragger != null)
                    dragger.ResetPosition();
        }

        /// <summary>
        ///     获取所有注册的拖拽组件
        /// </summary>
        public static List<UIDragger> GetAllDraggers()
        {
            // 清理已销毁的对象
            draggerList.RemoveAll(d => d == null);
            return new List<UIDragger>(draggerList);
        }
    }
}

#endregion