#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using StellarFramework.UI;

namespace StellarFramework.Editor
{
    /// <summary>
    /// UIPanelBase 的通用检视面板扩展
    /// 职责：提供快捷的代码生成与绑定入口，并自动处理 Prefab 的 Overrides 应用与保存。
    /// </summary>
    [CustomEditor(typeof(UIPanelBase), true)]
    public class UIPanelBaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(15);

            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("自动 Apply 并生成绑定代码", GUILayout.Height(34)))
            {
                GUI.backgroundColor = Color.white;
                ExecuteGenerateAndBind();
            }

            GUI.backgroundColor = Color.white;
        }

        private void ExecuteGenerateAndBind()
        {
            GameObject currentObj = ((Component)target).gameObject;
            GameObject prefabAsset = null;

            // 场景 1：在普通 Scene 中选中了 Prefab 实例
            if (PrefabUtility.IsPartOfPrefabInstance(currentObj))
            {
                GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(currentObj);
                if (instanceRoot != null)
                {
                    // 核心：自动将 Hierarchy 中的修改 (Overrides) 应用到原始 Prefab
                    PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.UserAction);
                    Debug.Log($"<color=#00FF00>[UIKitCodeGen]</color> 已自动 Apply 预制体修改: {instanceRoot.name}");
                }

                prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(currentObj);
            }
            // 场景 2：直接在 Project 窗口选中了 Prefab Asset
            else if (PrefabUtility.IsPartOfPrefabAsset(currentObj))
            {
                prefabAsset = currentObj;
            }
            // 场景 3：处于 Prefab 隔离编辑模式 (Prefab Stage)
            else
            {
                // 兼容 Unity 最新的 Prefab Stage API
                var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(currentObj);
                if (prefabStage != null)
                {
                    string assetPath = prefabStage.assetPath;
                    // 自动保存 Prefab 模式下的所有修改
                    PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, assetPath);
                    prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    Debug.Log($"<color=#00FF00>[UIKitCodeGen]</color> 已自动保存 Prefab 模式修改: {prefabAsset.name}");
                }
            }

            // 执行最终的生成与绑定引擎
            if (prefabAsset != null)
            {
                UIKitCodeGen.GenerateAndBind(prefabAsset);
            }
            else
            {
                Debug.LogError("[UIKitCodeGen] 绑定失败：请确保当前对象是合法的 Prefab 实例或处于 Prefab 编辑模式！");
            }
        }
    }
}
#endif