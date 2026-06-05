using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarFramework.Editor
{
    public class URPMaterialConverterWindow : EditorWindow
    {
        private Shader _targetLitShader;
        private Material _defaultPipelineMaterial;
        private Vector2 _scroll;

        private bool _logEachMaterial;
        private bool _replaceDefaultMaterialSlots = true;
        private bool _replaceMissingMaterialSlots = true;

        private readonly List<Material> _scannedMaterials = new List<Material>(2048);

        public static void Open()
        {
            URPMaterialConverterWindow window =
                GetWindow<URPMaterialConverterWindow>("Render Pipeline Material Converter");
            window.minSize = new Vector2(860, 580);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshTargetShader();
        }

        private void OnGUI()
        {
            FrameworkRenderPipelineFamily family = StellarFramework.RenderPipelineCompatibility.CurrentFamily;
            bool requiresConversion = family != FrameworkRenderPipelineFamily.BuiltIn;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("渲染管线材质转换工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                requiresConversion
                    ? $"当前渲染管线: {family}。工具会将 Standard 材质迁移到当前管线默认 Lit Shader。"
                    : "当前项目使用 Built-in 管线，无需执行 SRP 材质迁移。若后续切到 URP/HDRP，可重新打开此工具执行转换。",
                MessageType.Info);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    _targetLitShader =
                        (Shader)EditorGUILayout.ObjectField("目标 Lit Shader", _targetLitShader, typeof(Shader), false);
                    if (GUILayout.Button("重新检测", GUILayout.Width(90)))
                    {
                        RefreshTargetShader();
                    }
                }

                _defaultPipelineMaterial = (Material)EditorGUILayout.ObjectField(
                    "默认目标材质(可选)",
                    _defaultPipelineMaterial,
                    typeof(Material),
                    false);

                _logEachMaterial = EditorGUILayout.Toggle("逐条输出转换日志", _logEachMaterial);
                _replaceDefaultMaterialSlots = EditorGUILayout.Toggle("替换 Default-Material 槽", _replaceDefaultMaterialSlots);
                _replaceMissingMaterialSlots = EditorGUILayout.Toggle("替换 Missing 材质槽", _replaceMissingMaterialSlots);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描 Project 材质", GUILayout.Height(30)))
                {
                    ScanProjectMaterials();
                }

                GUI.enabled = _scannedMaterials.Count > 0 && requiresConversion;
                if (GUILayout.Button("批量转换到当前管线 Lit", GUILayout.Height(30)))
                {
                    ConvertScannedToCurrentPipeline();
                }

                GUI.enabled = true;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = _defaultPipelineMaterial != null;
                if (GUILayout.Button("替换场景材质槽(Default/Missing)", GUILayout.Height(30)))
                {
                    ReplaceSceneMaterialSlots();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.Space(6);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"扫描结果：{_scannedMaterials.Count} 个材质", EditorStyles.miniBoldLabel);
                GUILayout.Label("提示：这里只列出所有材质，你可以在 Project 里进一步过滤或分组。", EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _scannedMaterials.Count; i++)
            {
                EditorGUILayout.ObjectField(_scannedMaterials[i], typeof(Material), false);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RefreshTargetShader()
        {
            _targetLitShader = StellarFramework.RenderPipelineCompatibility.FindPreferredLitShader();
            Debug.Log(
                $"[URPMaterialConverterWindow] Pipeline={StellarFramework.RenderPipelineCompatibility.CurrentFamily}, TargetShader={(_targetLitShader ? _targetLitShader.name : "null")}");
        }

        private void ScanProjectMaterials()
        {
            _scannedMaterials.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    _scannedMaterials.Add(material);
                }
            }

            Debug.Log($"[URPMaterialConverterWindow] 扫描完成：{_scannedMaterials.Count} 个材质");
            ShowNotification(new GUIContent($"扫描到 {_scannedMaterials.Count} 个材质"));
        }

        private void ConvertScannedToCurrentPipeline()
        {
            if (_targetLitShader == null)
            {
                Debug.LogError("[URPMaterialConverterWindow] 目标 Lit Shader 为空，无法执行转换。");
                return;
            }

            Undo.SetCurrentGroupName("StellarTools - Convert Materials To Current Pipeline");
            int group = Undo.GetCurrentGroup();

            int converted = 0;
            int skipped = 0;

            Undo.RecordObjects(_scannedMaterials.ToArray(), "Convert Materials");

            for (int i = 0; i < _scannedMaterials.Count; i++)
            {
                Material material = _scannedMaterials[i];
                if (material == null)
                {
                    skipped++;
                    continue;
                }

                if (material.shader != null && material.shader.name == "Standard")
                {
                    material.shader = _targetLitShader;
                    EditorUtility.SetDirty(material);
                    converted++;

                    if (_logEachMaterial)
                    {
                        Debug.Log($"[URPMaterialConverterWindow] Converted: {AssetDatabase.GetAssetPath(material)}");
                    }
                }
                else
                {
                    skipped++;
                }
            }

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[URPMaterialConverterWindow] 转换完成：converted={converted}, skipped={skipped}, total={_scannedMaterials.Count}");
            ShowNotification(new GUIContent($"转换 {converted}/{_scannedMaterials.Count}"));
        }

        private void ReplaceSceneMaterialSlots()
        {
            if (_defaultPipelineMaterial == null)
            {
                Debug.LogError("[URPMaterialConverterWindow] 默认目标材质为空，无法替换材质槽。");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            Debug.Log($"[URPMaterialConverterWindow] 开始替换材质槽：Scene={scene.name}");

            Renderer[] renderers = FindSceneRenderers();

            Undo.SetCurrentGroupName("StellarTools - Replace Scene Material Slots");
            int group = Undo.GetCurrentGroup();

            int changedRendererCount = 0;
            int changedSlotCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    continue;
                }

                bool changed = false;

                for (int k = 0; k < materials.Length; k++)
                {
                    Material material = materials[k];

                    if (_replaceMissingMaterialSlots && material == null)
                    {
                        materials[k] = _defaultPipelineMaterial;
                        changed = true;
                        changedSlotCount++;
                        continue;
                    }

                    if (_replaceDefaultMaterialSlots &&
                        material != null &&
                        (material.name == "Default-Material" || material.name == "Default Material"))
                    {
                        materials[k] = _defaultPipelineMaterial;
                        changed = true;
                        changedSlotCount++;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                Undo.RecordObject(renderer, "Replace Renderer Materials");
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                changedRendererCount++;

                if (_logEachMaterial)
                {
                    Debug.Log($"[URPMaterialConverterWindow] Replaced slots: {GetHierarchyPath(renderer.gameObject)}");
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[URPMaterialConverterWindow] 替换完成：RendererChanged={changedRendererCount}, SlotChanged={changedSlotCount}");
            ShowNotification(new GUIContent($"替换槽位 {changedSlotCount}"));
        }

        private static Renderer[] FindSceneRenderers()
        {
#if UNITY_2020_1_OR_NEWER
            return Object.FindObjectsOfType<Renderer>(true);
#else
            var result = new List<Renderer>();
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!renderer.gameObject.scene.IsValid() || EditorUtility.IsPersistent(renderer))
                {
                    continue;
                }

                result.Add(renderer);
            }

            return result.ToArray();
#endif
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (go == null)
            {
                return "(null)";
            }

            var stack = new Stack<string>();
            Transform transform = go.transform;
            while (transform != null)
            {
                stack.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", stack);
        }
    }
}
