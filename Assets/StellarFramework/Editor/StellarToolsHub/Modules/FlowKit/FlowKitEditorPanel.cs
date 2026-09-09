using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal sealed class FlowKitEditorPanel : ToolsHubEmbeddedPanel
    {
        private FlowKitEditorWorkspace _workspace;

        protected override VisualElement BuildView()
        {
            EnsureWorkspace();
            return _workspace.Root;
        }

        protected override void DrawIMGUI()
        {
            EnsureWorkspace();
            UnityEngine.GUILayout.Label("FlowKit Visual Workflow Editor uses the UI Toolkit view.");
        }

        protected override void OnActivated()
        {
            EnsureWorkspace();
        }

        protected override void OnDeactivated()
        {
            DisposeWorkspace();
        }

        protected override void OnSelectionChanged()
        {
        }

        private void EnsureWorkspace()
        {
            if (_workspace == null) _workspace = new FlowKitEditorWorkspace();
        }

        private void DisposeWorkspace()
        {
            _workspace?.Dispose();
            _workspace = null;
        }
    }
}
