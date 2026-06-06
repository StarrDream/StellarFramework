using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor
{
    public abstract class ToolsHubEmbeddedPanel
    {
        protected StellarFrameworkTools Window { get; private set; }

        private bool _isActive;

        public void Activate(StellarFrameworkTools window)
        {
            BindWindow(window);
            if (_isActive)
            {
                return;
            }

            _isActive = true;
            OnActivated();
        }

        public void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            OnDeactivated();
            _isActive = false;
        }

        public void HandleSelectionChange()
        {
            OnSelectionChanged();
        }

        public VisualElement CreateView(StellarFrameworkTools window)
        {
            BindWindow(window);

            VisualElement content = BuildView();
            if (content != null)
            {
                content.style.flexGrow = 1f;
                return content;
            }

            return CreateLegacyContainer();
        }

        public void DrawLegacyContent(StellarFrameworkTools window)
        {
            BindWindow(window);
            DrawIMGUI();
        }

        protected void Notify(string message)
        {
            Window?.ShowNotification(new GUIContent(message));
        }

        protected void RequestRepaint()
        {
            Window?.Repaint();
        }

        protected bool IsHostFocused =>
            EditorWindow.focusedWindow == Window || EditorWindow.mouseOverWindow == Window;

        protected virtual void OnActivated()
        {
        }

        protected virtual void OnDeactivated()
        {
        }

        protected virtual void OnSelectionChanged()
        {
        }

        protected virtual VisualElement BuildView()
        {
            return null;
        }

        protected IMGUIContainer CreateLegacyContainer()
        {
            return new IMGUIContainer(() =>
            {
                DrawIMGUI();
            })
            {
                style =
                {
                    flexGrow = 1f
                }
            };
        }

        protected abstract void DrawIMGUI();

        private void BindWindow(StellarFrameworkTools window)
        {
            Window = window;
        }
    }
}
