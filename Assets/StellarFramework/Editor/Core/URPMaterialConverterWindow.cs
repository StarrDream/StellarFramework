using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarFramework.Editor
{
    public class URPMaterialConverterWindow : EditorWindow
    {
        // Hub 调用入口，不再挂菜单
        public static void Open()
        {
            var wnd = GetWindow<URPMaterialConverterWindow>("URP Material Converter");
            wnd.minSize = new Vector2(860, 580);
            wnd.Show();
        }

        private Shader _urpLit;
        private Material _defaultUrpMaterial;
        private Vector2 _scroll;

        private bool _logEachMaterial;
        private bool _replaceDefaultMaterialSlots = true;
        private bool _replaceMissingMaterialSlots = true;

        private readonly List<Material> _scannedMaterials = new List<Material>(2048);

        private void OnEnable()
        {
            _urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Debug.Log($"[URPMaterialConverterWindow] OnEnable, URP Lit Shader: {(_urpLit ? _urpLit.name : "null")}");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("URP 材质转换工具", EditorStyles.boldLabel);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _urpLit = (Shader)EditorGUILayout.ObjectField("URP Lit Shader", _urpLit, typeof(Shader), false);
                _defaultUrpMaterial = (Material)EditorGUILayout.ObjectField("默认 URP 材质(可选)", _defaultUrpMaterial, typeof(Material), false);

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

                GUI.enabled = _scannedMaterials.Count > 0;
                if (GUILayout.Button("批量转换到 URP Lit", GUILayout.Height(30)))
                {
                    ConvertScannedToUrp();
                }

                GUI.enabled = true;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = _defaultUrpMaterial != null;
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
                GUILayout.Label("提示：这里只列出所有材质，你可以在 Project 里进一步过滤/分组。", EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _scannedMaterials.Count; i++)
            {
                EditorGUILayout.ObjectField(_scannedMaterials[i], typeof(Material), false);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanProjectMaterials()
        {
            _scannedMaterials.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat) _scannedMaterials.Add(mat);
            }

            Debug.Log($"[URPMaterialConverterWindow] 扫描完成：{_scannedMaterials.Count} 个材质");
            ShowNotification(new GUIContent($"扫描到 {_scannedMaterials.Count} 个材质"));
        }

        private void ConvertScannedToUrp()
        {
            if (_urpLit == null)
            {
                Debug.LogError("[URPMaterialConverterWindow] URP Lit Shader 为空（确认项目已启用 URP 且 Shader 可用）");
                return;
            }

            Undo.SetCurrentGroupName("StellarTools - Convert Materials To URP");
            int group = Undo.GetCurrentGroup();

            int converted = 0;
            int skipped = 0;

            Undo.RecordObjects(_scannedMaterials.ToArray(), "Convert Materials");

            for (int i = 0; i < _scannedMaterials.Count; i++)
            {
                var mat = _scannedMaterials[i];
                if (!mat)
                {
                    skipped++;
                    continue;
                }

                // 只转换 Standard，避免误伤其它自定义 Shader
                if (mat.shader != null && mat.shader.name == "Standard")
                {
                    mat.shader = _urpLit;
                    EditorUtility.SetDirty(mat);
                    converted++;

                    if (_logEachMaterial)
                        Debug.Log($"[URPMaterialConverterWindow] Converted: {AssetDatabase.GetAssetPath(mat)}");
                }
                else
                {
                    skipped++;
                }
            }

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[URPMaterialConverterWindow] 转换完成：converted={converted}, skipped={skipped}, total={_scannedMaterials.Count}");
            ShowNotification(new GUIContent($"转换 {converted}/{_scannedMaterials.Count}"));
        }

        private void ReplaceSceneMaterialSlots()
        {
            if (_defaultUrpMaterial == null)
            {
                Debug.LogError("[URPMaterialConverterWindow] 默认 URP 材质为空，无法替换材质槽");
                return;
            }

            // 标记场景脏，方便用户保存
            var scene = SceneManager.GetActiveScene();
            Debug.Log($"[URPMaterialConverterWindow] 开始替换材质槽：Scene={scene.name}");

            var renderers = Object.FindObjectsOfType<Renderer>(true);

            Undo.SetCurrentGroupName("StellarTools - Replace Scene Material Slots");
            int group = Undo.GetCurrentGroup();

            int changedRendererCount = 0;
            int changedSlotCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;

                for (int k = 0; k < mats.Length; k++)
                {
                    var m = mats[k];

                    // Missing：m == null
                    if (_replaceMissingMaterialSlots && m == null)
                    {
                        mats[k] = _defaultUrpMaterial;
                        changed = true;
                        changedSlotCount++;
                        continue;
                    }

                    // Default-Material：Unity 内置默认材质的常见 name
                    if (_replaceDefaultMaterialSlots && m != null && (m.name == "Default-Material" || m.name == "Default Material"))
                    {
                        mats[k] = _defaultUrpMaterial;
                        changed = true;
                        changedSlotCount++;
                    }
                }

                if (changed)
                {
                    Undo.RecordObject(r, "Replace Renderer Materials");
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    changedRendererCount++;

                    if (_logEachMaterial)
                        Debug.Log($"[URPMaterialConverterWindow] Replaced slots: {GetHierarchyPath(r.gameObject)}");
                }
            }

            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[URPMaterialConverterWindow] 替换完成：RendererChanged={changedRendererCount}, SlotChanged={changedSlotCount}");
            ShowNotification(new GUIContent($"替换槽位 {changedSlotCount}"));
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (!go) return "(null)";
            var stack = new Stack<string>();
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