#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("UIKit 工具", "框架核心", 0)]
    public class UIKitHubModule : ToolModule
    {
        public override string Icon => "d_Canvas Icon";
        public override string Description => "UI 工作区管理与代码自动生成工具。";

        private string _newPanelName = "Panel_Main";

        // 路径配置缓存
        private string _scriptDir;
        private string _prefabDir;
        private string _sceneDir;

        // Canvas 配置缓存
        private Vector2 _resolution;
        private float _matchWidthOrHeight;

        public override void OnEnable()
        {
            base.OnEnable();
            // 加载配置，默认值放在 Assets 目录下，不污染框架内部
            _scriptDir = EditorPrefs.GetString("Stellar_UIKit_ScriptDir", "Assets/Scripts/UI/Panels");
            _prefabDir = EditorPrefs.GetString("Stellar_UIKit_PrefabDir", "Assets/Resources/UIPanel");
            _sceneDir = EditorPrefs.GetString("Stellar_UIKit_SceneDir", "Assets/Scenes/UI_Edit");

            _resolution.x = EditorPrefs.GetFloat("Stellar_UIKit_ResX", 1920f);
            _resolution.y = EditorPrefs.GetFloat("Stellar_UIKit_ResY", 1080f);
            _matchWidthOrHeight = EditorPrefs.GetFloat("Stellar_UIKit_Match", 0.5f);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            // 保存配置
            EditorPrefs.SetString("Stellar_UIKit_ScriptDir", _scriptDir);
            EditorPrefs.SetString("Stellar_UIKit_PrefabDir", _prefabDir);
            EditorPrefs.SetString("Stellar_UIKit_SceneDir", _sceneDir);

            EditorPrefs.SetFloat("Stellar_UIKit_ResX", _resolution.x);
            EditorPrefs.SetFloat("Stellar_UIKit_ResY", _resolution.y);
            EditorPrefs.SetFloat("Stellar_UIKit_Match", _matchWidthOrHeight);
        }

        public override void OnGUI()
        {
            Section("1. 路径与环境配置 (自动保存)");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _scriptDir = EditorGUILayout.TextField("脚本生成目录", _scriptDir);
                _prefabDir = EditorGUILayout.TextField("预制体保存目录", _prefabDir);
                _sceneDir = EditorGUILayout.TextField("工作区场景目录", _sceneDir);

                GUILayout.Space(5);
                _resolution = EditorGUILayout.Vector2Field("Canvas 默认分辨率", _resolution);
                _matchWidthOrHeight = EditorGUILayout.Slider("Match (Width-Height)", _matchWidthOrHeight, 0f, 1f);
            }

            Section("2. 基础结构");
            EditorGUILayout.HelpBox("UIRoot 和 Panel Template 统一从这里创建；保留 Hierarchy 右键入口，避免把常用操作散落到顶层菜单。", MessageType.Info);
            using (new GUILayout.HorizontalScope())
            {
                if (PrimaryButton("生成或覆盖 UIRoot", GUILayout.Height(30)))
                {
                    UIKitEditor.CreateUIRootPrefab();
                }

                GUI.enabled = Selection.activeGameObject != null;
                if (GUILayout.Button("在当前选中节点下创建 Panel Template", GUILayout.Height(30)))
                {
                    UIKitEditor.CreatePanelTemplateUnderSelection();
                }

                GUI.enabled = true;
            }

            Section("3. UI 工作区 (Workspace)");
            EditorGUILayout.HelpBox("输入面板名称，一键创建独立的带相机 UI 编辑场景、逻辑脚本与标准预制体(自带 root 节点)。", MessageType.Info);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("面板名称:", GUILayout.Width(60));
                _newPanelName = EditorGUILayout.TextField(_newPanelName);
                if (GUILayout.Button("创建工作区", GUILayout.Width(100), GUILayout.Height(24)))
                {
                    UIKitCodeGen.CreateUIWorkspace(_newPanelName, _scriptDir, _prefabDir, _sceneDir, _resolution,
                        _matchWidthOrHeight);
                }
            }

            Section("4. 代码生成与自动绑定 (Auto Bind)");
            EditorGUILayout.HelpBox(
                "提示：你现在可以直接在 Project 窗口使用 [Assets/UIKit/生成 UI 绑定代码]，或在 Hierarchy 窗口使用 [GameObject/UIKit/生成 UI 绑定代码] 进行快速绑定，无需每次打开此面板。",
                MessageType.Info);

            GameObject selectedObj = Selection.activeGameObject;
            bool isValidSelection = selectedObj != null;

            GUI.enabled = isValidSelection;
            if (PrimaryButton("对当前选中对象生成代码并绑定", GUILayout.Height(34)))
            {
                GameObject prefabAsset = selectedObj;
                if (PrefabUtility.IsPartOfPrefabInstance(selectedObj))
                {
                    prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selectedObj);
                }
                else if (!PrefabUtility.IsPartOfPrefabAsset(selectedObj))
                {
                    Window.ShowNotification(new GUIContent("请先将 UI 保存为 Prefab！"));
                    return;
                }

                UIKitCodeGen.GenerateAndBind(prefabAsset, null); // null 表示使用 EditorPrefs 中的默认路径
            }

            GUI.enabled = true;

            if (!isValidSelection)
            {
                GUILayout.Label("当前未选中任何 GameObject", EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}
#endif
