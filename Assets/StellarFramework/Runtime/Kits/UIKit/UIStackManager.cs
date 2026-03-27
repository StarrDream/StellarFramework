using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.UI
{
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public sealed class UIStackManager : ISingleton
    {
        private readonly List<UIPanelBase> _stack = new List<UIPanelBase>(16);
        private bool _isInitialized;

        public static UIStackManager Instance => SingletonFactory.GetSingleton<UIStackManager>();

        public void OnSingletonInit()
        {
            if (_isInitialized)
            {
                return;
            }

            UIPanelBase.OnPanelClosedGlobal += HandlePanelClosed;
            _isInitialized = true;
        }

        /// <summary>
        /// 我显式提供销毁收口，避免静态事件把全局实例长期挂死。
        /// </summary>
        public void Dispose()
        {
            if (!_isInitialized)
            {
                return;
            }

            UIPanelBase.OnPanelClosedGlobal -= HandlePanelClosed;
            _stack.Clear();
            _isInitialized = false;
        }

        #region 静态导航 API

        public static TPanel PushPanel<TPanel>(UIPanelDataBase data = null) where TPanel : UIPanelBase
        {
            TPanel panel = UIKit.OpenPanel<TPanel>(data);
            if (panel == null)
            {
                LogKit.LogError($"[UIStackManager] PushPanel 失败: 打开面板为空, Panel={typeof(TPanel).Name}");
                return null;
            }

            Instance?.Push(panel);
            return panel;
        }

        public static async UniTask<TPanel> PushPanelAsync<TPanel>(UIPanelDataBase data = null)
            where TPanel : UIPanelBase
        {
            TPanel panel = await UIKit.OpenPanelAsync<TPanel>(data);
            if (panel == null)
            {
                LogKit.LogError($"[UIStackManager] PushPanelAsync 失败: 打开面板为空, Panel={typeof(TPanel).Name}");
                return null;
            }

            Instance?.Push(panel);
            return panel;
        }

        public static void PopPanel()
        {
            UIStackManager mgr = Instance;
            if (mgr == null)
            {
                LogKit.LogError("[UIStackManager] PopPanel 失败: Instance 为空");
                return;
            }

            UIPanelBase top = mgr.Peek();
            if (top == null)
            {
                return;
            }

            UIKit.ClosePanel(top.GetType());
        }

        public static void PopToPanel<TPanel>() where TPanel : UIPanelBase
        {
            UIStackManager mgr = Instance;
            if (mgr == null)
            {
                LogKit.LogError($"[UIStackManager] PopToPanel 失败: Instance 为空, TargetPanel={typeof(TPanel).Name}");
                return;
            }

            mgr.CleanupInvalidPanels();

            for (int i = mgr._stack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = mgr._stack[i];
                if (panel == null)
                {
                    continue;
                }

                if (panel is TPanel)
                {
                    mgr.EvaluateVisibility();
                    return;
                }

                UIKit.ClosePanel(panel.GetType());
            }
        }

        public static void ClearStack()
        {
            UIStackManager mgr = Instance;
            if (mgr == null)
            {
                LogKit.LogError("[UIStackManager] ClearStack 失败: Instance 为空");
                return;
            }

            mgr.CleanupInvalidPanels();

            for (int i = mgr._stack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = mgr._stack[i];
                if (panel == null)
                {
                    continue;
                }

                UIKit.ClosePanel(panel.GetType());
            }

            mgr._stack.Clear();
        }

        #endregion

        public void Push(UIPanelBase panel)
        {
            if (panel == null)
            {
                LogKit.LogError("[UIStackManager] Push 失败: panel 为空");
                return;
            }

            CleanupInvalidPanels();

            int existedIndex = _stack.IndexOf(panel);
            if (existedIndex >= 0)
            {
                _stack.RemoveAt(existedIndex);
            }

            _stack.Add(panel);
            EvaluateVisibility();
        }

        public void Remove(UIPanelBase panel)
        {
            if (panel == null)
            {
                return;
            }

            CleanupInvalidPanels();

            if (_stack.Remove(panel))
            {
                EvaluateVisibility();
            }
        }

        public UIPanelBase Peek()
        {
            CleanupInvalidPanels();

            if (_stack.Count == 0)
            {
                return null;
            }

            return _stack[_stack.Count - 1];
        }

        private void HandlePanelClosed(UIPanelBase panel)
        {
            Remove(panel);
        }

        private void EvaluateVisibility()
        {
            CleanupInvalidPanels();

            int topFullscreenIndex = -1;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _stack[i];
                if (panel == null)
                {
                    continue;
                }

                if (panel.IsFullScreen)
                {
                    topFullscreenIndex = i;
                    break;
                }
            }

            for (int i = 0; i < _stack.Count; i++)
            {
                UIPanelBase panel = _stack[i];
                if (panel == null)
                {
                    continue;
                }

                bool visible = topFullscreenIndex < 0 || i >= topFullscreenIndex;
                ApplyVisible(panel, visible);
            }
        }

        private void ApplyVisible(UIPanelBase panel, bool visible)
        {
            if (panel == null)
            {
                return;
            }

            CanvasGroup group = panel.CanvasGroup;
            if (group == null)
            {
                LogKit.LogError(
                    $"[UIStackManager] ApplyVisible 失败: CanvasGroup 为空, Panel={panel.GetType().Name}, TriggerObject={panel.gameObject.name}, Visible={visible}");
                return;
            }

            bool wasVisible = group.alpha > 0.01f;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (visible && !wasVisible)
            {
                panel.OnResume();
            }
            else if (!visible && wasVisible)
            {
                panel.OnPause();
            }
        }

        private void CleanupInvalidPanels()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                UIPanelBase panel = _stack[i];
                if (panel == null || panel.gameObject == null)
                {
                    _stack.RemoveAt(i);
                }
            }
        }
    }
}