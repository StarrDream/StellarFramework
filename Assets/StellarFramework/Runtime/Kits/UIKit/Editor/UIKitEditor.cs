// =========================================================
// File: UIKitEditor.cs
// Path: Assets/StellarFramework/Runtime/Kits/UIKit/Editor/UIKitEditor.cs
//
// 变更点：
// 1) 移除原本 Tools/UIKit/Create UI Root Prefab 的菜单入口（统一收拢到 Hub）
// 2) 保留 GameObject 右键创建 Panel Template 的入口（按你的要求保留）
// 3) 提供两个静态方法供 Hub 调用：
//    - CreateUIRootPrefab()
//    - CreatePanelTemplateUnderSelection()
//
// 注意：
// - 此文件在 Runtime/Kits/UIKit/Editor 下，但本质仍是 Editor-only 脚本（#if UNITY_EDITOR）
// - 不做额外容错：路径不存在就按逻辑创建，失败就直接抛/打日志，方便你定位
// =========================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StellarFramework.UI;

namespace StellarFramework.Editor
{
    public static class UIKitEditor
    {
        /// <summary>
        /// 生成 UIRoot Prefab（Hub 调用入口）
        /// </summary>
        public static void CreateUIRootPrefab()
        {
            // 目录：Assets/Resources/UIPanel
            string folderPath = "Assets/StellarFramework/Resources/UIPanel";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log($"[UIKitEditor] 创建目录: {folderPath}");
            }

            string prefabPath = $"{folderPath}/UIRoot.prefab";

            // 如果已存在，直接覆盖（Hub 那边是“生成/覆盖”按钮）
            if (File.Exists(prefabPath))
            {
                Debug.Log($"[UIKitEditor] 覆盖已存在的 UIRoot: {prefabPath}");
            }

            // 创建根节点
            GameObject root = new GameObject("UIRoot");
            root.layer = LayerMask.NameToLayer("UI");

            // Canvas
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            // Scaler（默认：1920x1080 / Match 0.5）
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Raycaster
            root.AddComponent<GraphicRaycaster>();

            // 创建层级节点
            CreateLayerNode(root, UIPanelBase.PanelLayer.Bottom);
            CreateLayerNode(root, UIPanelBase.PanelLayer.Middle);
            CreateLayerNode(root, UIPanelBase.PanelLayer.Top);
            CreateLayerNode(root, UIPanelBase.PanelLayer.Popup);
            CreateLayerNode(root, UIPanelBase.PanelLayer.System);

            // 保存 Prefab
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            // 清理场景对象
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[UIKitEditor] UIRoot Prefab 已生成: {prefabPath}");
        }

        /// <summary>
        /// 在当前 Selection 的 GameObject 下创建 Panel Template（Hub 调用入口）
        /// </summary>
        public static void CreatePanelTemplateUnderSelection()
        {
            var parent = Selection.activeGameObject;
            if (parent == null)
            {
                Debug.LogError("[UIKitEditor] 未选中父物体：请在 Hierarchy 里选中一个父节点（Canvas 或 Layer 节点）");
                return;
            }

            CreatePanelTemplateInternal(parent);
        }

        // ------------------------------------------------------
        // GameObject 右键菜单入口
        // ------------------------------------------------------
        [MenuItem("GameObject/StellarFramework/UIKit/Panel Template", false, 10)]
        public static void CreatePanelTemplate(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            if (parent == null)
            {
                // 如果没有上下文，就退回到当前 Selection
                parent = Selection.activeGameObject;
                if (parent == null)
                {
                    Debug.LogError("[UIKitEditor] 创建 Panel Template 失败：没有父物体上下文且当前未选中对象");
                    return;
                }
            }

            CreatePanelTemplateInternal(parent);
        }

        private static void CreatePanelTemplateInternal(GameObject parent)
        {
            // 1) 创建根节点
            GameObject root = new GameObject("Panel_New");
            GameObjectUtility.SetParentAndAlign(root, parent);

            // Layer 设置为 UI（如果项目没有 UI Layer，会得到 -1，这里不做兜底，方便你立刻发现配置问题）
            root.layer = LayerMask.NameToLayer("UI");

            // 2) 必要组件：CanvasGroup + RectTransform
            root.AddComponent<CanvasGroup>();

            RectTransform rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            rootRT.localScale = Vector3.one;

            // 3) 背景节点
            GameObject bg = new GameObject("root");
            bg.layer = LayerMask.NameToLayer("UI");
            bg.transform.SetParent(root.transform, false);

            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.6f);

            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // 4) Undo
            Undo.RegisterCreatedObjectUndo(root, "Create Panel Template");

            // 5) 选中
            Selection.activeGameObject = root;

            Debug.Log($"[UIKitEditor] Panel Template Created under: {GetHierarchyPath(parent)}");
        }

        private static void CreateLayerNode(GameObject root, UIPanelBase.PanelLayer layer)
        {
            GameObject go = new GameObject(layer.ToString());
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(root.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Popup / System 通常需要阻断点击，这里加 CanvasGroup 方便控制
            if (layer == UIPanelBase.PanelLayer.Popup || layer == UIPanelBase.PanelLayer.System)
            {
                go.AddComponent<CanvasGroup>();
            }
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (go == null) return "(null)";
            var stack = new System.Collections.Generic.Stack<string>();
            var t = go.transform;
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", stack);
        }
    }
}
#endif