#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StellarFramework.UI;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    /// <summary>
    /// UIKit 自动化代码生成与绑定引擎 (增强版)
    /// </summary>
    public static class UIKitCodeGen
    {
        // 跨域数据传递 Key
        private const string PREFS_BIND_DATA = "Stellar_UIKit_BindData";

        [Serializable]
        private class BindTaskData
        {
            public string PrefabPath;
            public string ClassName;
            public List<BindNode> Nodes = new List<BindNode>();
        }

        [Serializable]
        private class BindNode
        {
            public string FieldName;
            public string TransformPath;
            public string ComponentTypeName;
            public bool IsGameObject;
        }

        #region 1. 创建 UI 工作区 (Workspace)

        public static void CreateUIWorkspace(string panelName, string scriptDir, string prefabDir, string sceneDir,
            Vector2 resolution, float matchWidthOrHeight)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                Debug.LogError("[UIKitCodeGen] 创建失败：面板名称不能为空");
                return;
            }

            panelName = panelName.Trim();
            if (!panelName.StartsWith("Panel_"))
            {
                panelName = "Panel_" + panelName;
            }

            // 1. 确保目录存在
            string panelScriptDir = $"{scriptDir}/{panelName}";
            EnsureDirectory(panelScriptDir);
            EnsureDirectory(prefabDir);
            EnsureDirectory(sceneDir);

            string scenePath = $"{sceneDir}/{panelName}_Edit.unity";
            string prefabPath = $"{prefabDir}/{panelName}.prefab";
            string scriptPath = $"{panelScriptDir}/{panelName}.cs";

            // 2. 创建独立编辑场景
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            newScene.name = panelName + "_Edit";

            // 3. 构建基础 UI 结构 (带相机)
            GameObject camObj = new GameObject("UICamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            cam.orthographic = true;

            GameObject root = new GameObject("UIRoot_Mock");
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;

            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = resolution;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // 4. 创建 Panel 节点
            GameObject panelGo = new GameObject(panelName);
            panelGo.layer = LayerMask.NameToLayer("UI");
            panelGo.transform.SetParent(root.transform, false);
            var rt = panelGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            panelGo.AddComponent<CanvasGroup>();

            // 5. 创建 root 子节点 (规范要求)
            GameObject rootChild = new GameObject("root");
            rootChild.layer = LayerMask.NameToLayer("UI");
            rootChild.transform.SetParent(panelGo.transform, false);
            var rootChildRt = rootChild.AddComponent<RectTransform>();
            rootChildRt.anchorMin = Vector2.zero;
            rootChildRt.anchorMax = Vector2.one;
            rootChildRt.offsetMin = Vector2.zero;
            rootChildRt.offsetMax = Vector2.zero;

            // 6. 保存 Prefab
            GameObject prefab =
                PrefabUtility.SaveAsPrefabAssetAndConnect(panelGo, prefabPath, InteractionMode.UserAction);

            // 7. 保存 Scene
            EditorSceneManager.SaveScene(newScene, scenePath);

            // 8. 生成基础逻辑脚本 (仅当不存在时)
            if (!File.Exists(scriptPath))
            {
                GenerateLogicScript(panelName, scriptPath);
            }

            // 9. 触发首次生成与绑定
            GenerateAndBind(prefab, panelScriptDir);

            Debug.Log($"<color=#00FF00>[UIKitCodeGen]</color> 工作区创建成功！\n场景: {scenePath}\n预制体: {prefabPath}");
        }

        #endregion

        #region 2. 生成代码与记录绑定任务 (Generate & Record)

        public static void GenerateAndBind(GameObject panelPrefab, string customScriptDir = null)
        {
            if (panelPrefab == null) return;

            string prefabPath = AssetDatabase.GetAssetPath(panelPrefab);
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
            {
                Debug.LogError("[UIKitCodeGen] 只能对保存到工程中的 Prefab 执行生成与绑定。");
                return;
            }

            string className = panelPrefab.name;

            if (string.IsNullOrEmpty(customScriptDir))
            {
                string baseScriptDir = EditorPrefs.GetString("Stellar_UIKit_ScriptDir", "Assets/Scripts/UI/Panels");
                customScriptDir = $"{baseScriptDir}/{className}";
            }

            EnsureDirectory(customScriptDir);

            string logicScriptPath = $"{customScriptDir}/{className}.cs";
            string designerScriptPath = $"{customScriptDir}/{className}.Designer.cs";

            var autoBinds = panelPrefab.GetComponentsInChildren<UIAutoBind>(true);
            var bindData = new BindTaskData { PrefabPath = prefabPath, ClassName = className };

            StringBuilder designerCode = new StringBuilder();
            designerCode.AppendLine(
                "// ==================================================================================");
            designerCode.AppendLine("// Auto-generated by StellarFramework UIKit");
            designerCode.AppendLine($"// 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            designerCode.AppendLine("// 警告：请勿手动修改此文件，每次生成都会全量覆盖。");
            designerCode.AppendLine(
                "// ==================================================================================");
            designerCode.AppendLine("using UnityEngine;");
            designerCode.AppendLine("using UnityEngine.UI;");
            designerCode.AppendLine("using StellarFramework.UI;");
            designerCode.AppendLine();
            designerCode.AppendLine("namespace StellarFramework.UI");
            designerCode.AppendLine("{");
            designerCode.AppendLine($"    public partial class {className}");
            designerCode.AppendLine("    {");

            foreach (var bind in autoBinds)
            {
                if (bind.Target == null) continue;

                bool isGo = bind.Target is GameObject;
                string typeName = isGo ? "GameObject" : bind.Target.GetType().FullName;

                // 【核心修改点】：强制读取 GameObject 的名称作为变量名，并替换掉非法空格
                string fieldName = bind.gameObject.name.Replace(" ", "_");

                if (string.IsNullOrWhiteSpace(fieldName)) continue;

                bindData.Nodes.Add(new BindNode
                {
                    FieldName = "m_" + fieldName,
                    TransformPath = AnimationUtility.CalculateTransformPath(bind.transform, panelPrefab.transform),
                    ComponentTypeName = typeName,
                    IsGameObject = isGo
                });

                designerCode.AppendLine(
                    $"        [SerializeField] [HideInInspector] private {typeName} m_{fieldName};");
                designerCode.AppendLine($"        public {typeName} {fieldName} => m_{fieldName};");
                designerCode.AppendLine();
            }

            designerCode.AppendLine("    }");
            designerCode.AppendLine("}");

            File.WriteAllText(designerScriptPath, designerCode.ToString(), new UTF8Encoding(false));

            if (!File.Exists(logicScriptPath))
            {
                GenerateLogicScript(className, logicScriptPath);
            }

            string json = JsonUtility.ToJson(bindData);
            EditorPrefs.SetString(PREFS_BIND_DATA, json);

            AssetDatabase.Refresh();
            Debug.Log($"[UIKitCodeGen] 代码生成完毕，等待 Unity 编译后自动绑定... ({className})");
        }

        private static void GenerateLogicScript(string className, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using StellarFramework.UI;");
            sb.AppendLine();
            sb.AppendLine("namespace StellarFramework.UI");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {className} : UIPanelBase");
            sb.AppendLine("    {");
            sb.AppendLine("        public override void OnInit()");
            sb.AppendLine("        {");
            sb.AppendLine("            // 在此编写初始化逻辑，可直接使用 Designer 中生成的 UI 属性");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override void OnOpen(UIPanelDataBase data)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        #endregion

        #region 3. 编译后自动挂载与赋值 (Post-Compile Auto Bind)

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (!EditorPrefs.HasKey(PREFS_BIND_DATA)) return;

            string json = EditorPrefs.GetString(PREFS_BIND_DATA);
            EditorPrefs.DeleteKey(PREFS_BIND_DATA); // 阅后即焚

            try
            {
                var bindData = JsonUtility.FromJson<BindTaskData>(json);
                if (bindData == null || string.IsNullOrEmpty(bindData.PrefabPath)) return;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(bindData.PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[UIKitCodeGen] 自动绑定失败：找不到 Prefab {bindData.PrefabPath}");
                    return;
                }

                // 1. 挂载主脚本
                Type scriptType = GetTypeByName(bindData.ClassName);
                if (scriptType == null)
                {
                    Debug.LogError($"[UIKitCodeGen] 自动绑定失败：找不到编译后的类 {bindData.ClassName}");
                    return;
                }

                Component targetScript = prefab.GetComponent(scriptType);
                if (targetScript == null)
                {
                    targetScript = prefab.AddComponent(scriptType);
                }

                // 2. 使用 SerializedObject 物理赋值 (0GC 核心)
                SerializedObject serializedObject = new SerializedObject(targetScript);
                serializedObject.Update();

                int bindCount = 0;
                foreach (var node in bindData.Nodes)
                {
                    Transform childTrans = prefab.transform.Find(node.TransformPath);
                    if (childTrans == null && node.TransformPath == "") childTrans = prefab.transform; // 根节点

                    if (childTrans == null) continue;

                    Object targetObj = node.IsGameObject
                        ? childTrans.gameObject
                        : childTrans.GetComponent(node.ComponentTypeName);
                    if (targetObj != null)
                    {
                        SerializedProperty prop = serializedObject.FindProperty(node.FieldName);
                        if (prop != null)
                        {
                            prop.objectReferenceValue = targetObj;
                            bindCount++;
                        }
                    }
                }

                serializedObject.ApplyModifiedProperties();
                PrefabUtility.SavePrefabAsset(prefab);

                Debug.Log($"<color=#00FFFF>[UIKitCodeGen]</color> 自动绑定完成！成功绑定 {bindCount} 个组件到 {bindData.ClassName}。");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIKitCodeGen] 编译后绑定发生异常: {e.Message}\n{e.StackTrace}");
            }
        }

        private static Type GetTypeByName(string className)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("StellarFramework.UI." + className);
                if (type != null) return type;
            }

            return null;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        #endregion

        #region 4. 右键快捷菜单 (Context Menus)

        [MenuItem("Assets/UIKit/生成 UI 绑定代码 (Generate & Bind)", false, 100)]
        public static void GenerateFromProjectWindow()
        {
            GameObject prefab = Selection.activeGameObject;
            if (prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                GenerateAndBind(prefab);
            }
        }

        [MenuItem("Assets/UIKit/生成 UI 绑定代码 (Generate & Bind)", true)]
        public static bool GenerateFromProjectWindowValidate()
        {
            return Selection.activeGameObject != null && PrefabUtility.IsPartOfPrefabAsset(Selection.activeGameObject);
        }

        [MenuItem("GameObject/UIKit/生成 UI 绑定代码 (Generate & Bind)", false, -10)]
        public static void GenerateFromHierarchyWindow()
        {
            GameObject selectedObj = Selection.activeGameObject;
            if (selectedObj == null) return;

            GameObject prefabAsset = selectedObj;
            if (PrefabUtility.IsPartOfPrefabInstance(selectedObj))
            {
                prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selectedObj);
            }

            if (prefabAsset != null && PrefabUtility.IsPartOfPrefabAsset(prefabAsset))
            {
                GenerateAndBind(prefabAsset);
            }
            else
            {
                Debug.LogError("[UIKitCodeGen] 请先将该 UI 节点保存为 Prefab，然后再执行生成与绑定！");
            }
        }

        [MenuItem("GameObject/UIKit/生成 UI 绑定代码 (Generate & Bind)", true)]
        public static bool GenerateFromHierarchyWindowValidate()
        {
            return Selection.activeGameObject != null;
        }

        #endregion
    }
}
#endif