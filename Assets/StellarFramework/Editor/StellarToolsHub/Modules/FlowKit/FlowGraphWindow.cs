using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules.FlowKit
{
    public sealed class FlowGraphWindow : EditorWindow
    {
        private FlowKitEditorWorkspace _workspace;

        [MenuItem("StellarFramework/FlowKit/Open Flow Editor")]
        public static void ShowWindow()
        {
            FlowGraphWindow window = GetWindow<FlowGraphWindow>();
            window.titleContent = new UnityEngine.GUIContent("FlowKit");
            window.minSize = new UnityEngine.Vector2(900f, 560f);
            window.Show();
        }

        [MenuItem("StellarFramework/FlowKit/示例/消防演练流程（4人）", priority = 100)]
        public static void OpenFireDrillSample()
        {
            ShowWindow();
            FlowGraphWindow window = GetWindow<FlowGraphWindow>();
            if (window._workspace == null) window.Build();
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            string path = Path.Combine(projectRoot,
                "Assets/StellarFramework/Samples/KitSamples/Example_FlowKit/FireDrillWorkflow4P.flow.json");
            window._workspace.OpenPath(path, false);
            EditorApplication.delayCall += () =>
            {
                if (window != null && window._workspace != null) window._workspace.FrameAll();
            };
        }

        private void OnEnable()
        {
            Build();
        }

        private void CreateGUI()
        {
            Build();
        }

        private void Build()
        {
            rootVisualElement.Clear();
            _workspace?.Dispose();
            _workspace = new FlowKitEditorWorkspace();
            VisualElement root = _workspace.Root;
            root.style.flexGrow = 1f;
            rootVisualElement.Add(root);
        }

        private void OnDisable()
        {
            _workspace?.Dispose();
            _workspace = null;
        }
    }
}
