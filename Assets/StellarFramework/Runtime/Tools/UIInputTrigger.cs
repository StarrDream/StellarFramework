/*
 * 文件: UIInputTrigger.cs
 * 用途: 通用 UI 输入触发器 (单击 / 双击 / 长按 / 悬浮)
 * 使用:
 *   1. 在 Unity 中创建一个 UI 对象（如 Image），添加本脚本。
 *   2. 调整 Color Transition/ColorBlock 与普通 Button 一样。
 *   3. 选择 Input Mode，绑定相应 UnityEvent。
 *   4. 若使用长按，可设置 longPressDelay (开始前延迟) 与在事件中执行逻辑。
 *   5. 绑定悬浮事件 onPointerEnter / onPointerExit / onPointerHover。
 *
 * 注意:
 *   - 若你需要与原 Button 共存，请不要同时挂 Button（避免重复逻辑），改用本组件。
 *   - 若需要导航/键盘支持，可扩展 OnSubmit。
 */

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StellarFramework
{
    [AddComponentMenu("UI/Interaction/UI Input Trigger")]
    public class UIInputTrigger : Selectable,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        public enum InputMode
        {
            SingleClick,
            DoubleClick,
            LongPress
        }

        [Header("Mode")] [SerializeField] private InputMode inputMode = InputMode.SingleClick;

        [Header("Common")] [Tooltip("是否忽略时间缩放(Time.timeScale)。长按节奏依赖此项。")] [SerializeField]
        private bool useUnscaledTime;

        [Header("Single Click Event")] [SerializeField]
        private UnityEvent onClick;

        [Header("Double Click Settings")] [Tooltip("双击最大间隔秒数")] [SerializeField]
        private float doubleClickInterval = 0.3f;

        [SerializeField] private UnityEvent onDoubleClick;

        [Header("Long Press Settings")] [Tooltip("从按下到进入长按循环的延迟(秒)，0 表示立即进入长按状态")] [SerializeField]
        private float longPressDelay;

        [SerializeField] private UnityEvent onLongPressStart;
        [SerializeField] private UnityEvent onLongPressTick;
        [SerializeField] private UnityEvent onLongPressEnd;

        [Header("Hover Settings")] [Tooltip("鼠标进入时触发")] [SerializeField]
        private UnityEvent onPointerEnter;

        [Tooltip("鼠标停留时每帧触发")] [SerializeField]
        private UnityEvent onPointerHover;

        [Tooltip("鼠标离开时触发")] [SerializeField] private UnityEvent onPointerExit;
        private float lastClickTime = -999f;
        private bool longPressActive;

        // 内部状态
        private bool pointerDown;
        private float pointerDownTime;
        private bool waitingSecondClick;

        // 缓存 deltaTime
        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        private float TimeNow => useUnscaledTime ? Time.unscaledTime : Time.time;

        private void Update()
        {
            if (!IsActive() || !IsInteractable()) return;

            switch (inputMode)
            {
                case InputMode.LongPress:
                    UpdateLongPress();
                    break;
                case InputMode.DoubleClick:
                    UpdateDoubleClickTimeout();
                    break;
                // SingleClick 不需要在 Update 中处理
            }

            //悬浮时每帧触发
            if (IsPointerInside) UpdateHover();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetStates();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;

            switch (inputMode)
            {
                case InputMode.SingleClick:
                    onClick?.Invoke();
                    break;

                case InputMode.DoubleClick:
                    HandleDoubleClick();
                    break;

                case InputMode.LongPress:
                    // 长按模式下点击抬起不触发点击事件
                    break;
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData); // 保持 Selectable 的默认行为

            if (!IsActive() || !IsInteractable()) return;
            if (inputMode == InputMode.LongPress)
            {
                pointerDown = true;
                longPressActive = false;
                pointerDownTime = TimeNow;
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData); // 保持 Selectable 的默认行为

            if (!IsActive() || !IsInteractable()) return;
            if (inputMode == InputMode.LongPress)
                if (pointerDown)
                {
                    if (longPressActive) onLongPressEnd?.Invoke();

                    pointerDown = false;
                    longPressActive = false;
                }
        }

        #region Long Press

        private void UpdateLongPress()
        {
            if (!pointerDown) return;

            if (!longPressActive)
            {
                // 等待进入长按
                if (TimeNow - pointerDownTime >= longPressDelay)
                {
                    longPressActive = true;
                    onLongPressStart?.Invoke();
                }
            }
            else
            {
                // 每帧 Tick
                onLongPressTick?.Invoke();
            }
        }

        #endregion

        #region Double Click

        private void UpdateDoubleClickTimeout()
        {
            if (waitingSecondClick)
                if (TimeNow - lastClickTime > doubleClickInterval)
                    // 超时，视为失败，回到等待首次点击
                    waitingSecondClick = false;
        }

        #endregion

        private void HandleDoubleClick()
        {
            var t = TimeNow;
            if (!waitingSecondClick)
            {
                // 第一次点击
                waitingSecondClick = true;
                lastClickTime = t;
            }
            else
            {
                if (t - lastClickTime <= doubleClickInterval)
                {
                    // 双击成功
                    waitingSecondClick = false;
                    lastClickTime = -999f;
                    onDoubleClick?.Invoke();
                }
                else
                {
                    // 超时后作为新一次首次点击
                    lastClickTime = t;
                }
            }
        }

        private void ResetStates()
        {
            pointerDown = false;
            longPressActive = false;
            waitingSecondClick = false;
            IsPointerInside = false; //重置悬浮状态
        }

        #region Hover

        private void UpdateHover()
        {
            onPointerHover?.Invoke();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;

            IsPointerInside = true;
            onPointerEnter?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;

            IsPointerInside = false;
            onPointerExit?.Invoke();
        }

        #endregion

        #region 外部 API

        public void SetMode(InputMode mode)
        {
            inputMode = mode;
            ResetStates();
        }

        public void SimulateClick()
        {
            if (!IsActive() || !IsInteractable()) return;
            if (inputMode == InputMode.SingleClick) onClick?.Invoke();
        }

        /// <summary>
        ///     获取鼠标是否在 UI 内部
        /// </summary>
        public bool IsPointerInside { get; private set; }

        #endregion

        #region 可选: 公开事件访问器 (若想在代码中添加/移除监听)

        public UnityEvent OnClick => onClick;
        public UnityEvent OnDoubleClick => onDoubleClick;
        public UnityEvent OnLongPressStart => onLongPressStart;
        public UnityEvent OnLongPressTick => onLongPressTick;
        public UnityEvent OnLongPressEnd => onLongPressEnd;

        //悬浮事件访问器
        public UnityEvent OnPointerEnterEvent => onPointerEnter;
        public UnityEvent OnPointerHoverEvent => onPointerHover;
        public UnityEvent OnPointerExitEvent => onPointerExit;

        #endregion
    }
}