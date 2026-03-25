using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UI 栈管理器 (横向扩展模块)
    /// 职责：提供基于栈的 UI 导航（Push/Pop），自动管理全屏界面的底层遮挡剔除（降低 DrawCall）。
    /// </summary>
    public class UIStackManager : Singleton<UIStackManager>
    {
        // 使用 List 模拟栈结构，方便从顶向下遍历计算遮挡关系
        private readonly List<UIPanelBase> _panelStack = new List<UIPanelBase>();

        public int StackCount => _panelStack.Count;

        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            // 监听全局关闭事件，防止开发者绕过 Pop 直接调用 CloseSelf 导致栈状态残留
            UIPanelBase.OnPanelClosedGlobal += RemoveFromStack;
            LogKit.Log("[UIStackManager] UI 栈管理器已初始化，已挂载全局关闭监听");
        }

        /// <summary>
        /// 同步压栈打开面板
        /// </summary>
        public TPanel PushPanel<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            TPanel panel = UIKit.OpenPanel<TPanel>(data);
            if (panel == null) return null;

            ProcessPush(panel);
            return panel;
        }

        /// <summary>
        /// 异步压栈打开面板
        /// </summary>
        public async UniTask<TPanel> PushPanelAsync<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            TPanel panel = await UIKit.OpenPanelAsync<TPanel>(data);
            if (panel == null) return null;

            ProcessPush(panel);
            return panel;
        }

        private void ProcessPush(UIPanelBase panel)
        {
            // 如果已经在栈顶，忽略重复压栈
            if (_panelStack.Count > 0 && _panelStack[_panelStack.Count - 1] == panel)
            {
                return;
            }

            // 如果在栈中其他位置，先移出（等同于将其提至栈顶）
            if (_panelStack.Contains(panel))
            {
                _panelStack.Remove(panel);
            }

            _panelStack.Add(panel);
            EvaluateVisibility();
        }

        /// <summary>
        /// 弹出栈顶面板
        /// </summary>
        public void PopPanel()
        {
            if (_panelStack.Count == 0)
            {
                LogKit.LogWarning("[UIStackManager] 弹栈失败：当前 UI 栈为空");
                return;
            }

            UIPanelBase topPanel = _panelStack[_panelStack.Count - 1];
            _panelStack.RemoveAt(_panelStack.Count - 1);

            // 调用 UIKit 的核心关闭逻辑
            UIKit.ClosePanel(topPanel.GetType());

            EvaluateVisibility();
        }

        /// <summary>
        /// 弹出直到指定类型的面板暴露在栈顶
        /// </summary>
        public void PopToPanel<TPanel>() where TPanel : UIPanelBase
        {
            Type targetType = typeof(TPanel);
            int targetIndex = -1;

            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                if (_panelStack[i].GetType() == targetType)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                LogKit.LogWarning($"[UIStackManager] 弹栈失败：栈中未找到目标面板 {targetType.Name}");
                return;
            }

            // 从顶向下 Pop，直到目标索引
            for (int i = _panelStack.Count - 1; i > targetIndex; i--)
            {
                UIPanelBase panel = _panelStack[i];
                _panelStack.RemoveAt(i);
                UIKit.ClosePanel(panel.GetType());
            }

            EvaluateVisibility();
        }

        /// <summary>
        /// 清空整个栈
        /// </summary>
        public void ClearStack()
        {
            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                UIKit.ClosePanel(_panelStack[i].GetType());
            }

            _panelStack.Clear();
        }

        /// <summary>
        /// 供 UIPanelBase 全局事件回调使用，清理脏数据
        /// </summary>
        private void RemoveFromStack(UIPanelBase panel)
        {
            if (_panelStack.Remove(panel))
            {
                EvaluateVisibility();
            }
        }

        /// <summary>
        /// 核心逻辑：从栈顶向下遍历，遇到全屏面板则隐藏其下方的所有面板
        /// 采用 CanvasGroup 调节透明度与交互，避免触发 GameObject.SetActive 带来的高昂开销与生命周期混乱
        /// </summary>
        private void EvaluateVisibility()
        {
            bool blockBelow = false;

            // 倒序遍历，同时清理可能存在的空引用（如被 DestroyAllPanels 强制销毁的面板）
            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _panelStack[i];

                if (panel == null || !panel.gameObject.activeSelf)
                {
                    _panelStack.RemoveAt(i);
                    continue;
                }

                if (!blockBelow)
                {
                    // 处于可见层，若之前被隐藏则恢复
                    if (panel.CanvasGroup.alpha < 1f || !panel.CanvasGroup.interactable)
                    {
                        panel.CanvasGroup.alpha = 1f;
                        panel.CanvasGroup.interactable = true;
                        panel.CanvasGroup.blocksRaycasts = true;
                        panel.OnResume();
                    }

                    if (panel.IsFullScreen)
                    {
                        blockBelow = true; // 标记阻断，下方的面板将被全部剔除
                    }
                }
                else
                {
                    // 被全屏面板遮挡，执行视觉剔除与交互阻断
                    if (panel.CanvasGroup.alpha > 0f || panel.CanvasGroup.interactable)
                    {
                        panel.CanvasGroup.alpha = 0f;
                        panel.CanvasGroup.interactable = false;
                        panel.CanvasGroup.blocksRaycasts = false;
                        panel.OnPause();
                    }
                }
            }
        }
    }
}