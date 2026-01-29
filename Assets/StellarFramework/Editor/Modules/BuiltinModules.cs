using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor.Modules
{
    // =========================================================
    // 常用工具组
    // =========================================================

    [StellarTool("批量重命名", "常用工具", 0)]
    public class BatchRenameModule : ToolModule
    {
        public override string Icon => "d_TextAsset Icon";
        public override string Description => "支持前缀、后缀、数字编号替换。支持场景物体和资源文件。";

        private string _renameBase = "Object";
        private string _renamePrefix = "";
        private string _renameSuffix = "";
        private int _renameStartIndex = 0;
        private int _renameDigits = 2;
        private bool _renameReplaceAll = true;

        public override void OnGUI()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _renameReplaceAll = EditorGUILayout.Toggle("完全替换原名", _renameReplaceAll);
                if (_renameReplaceAll) _renameBase = EditorGUILayout.TextField("基础名", _renameBase);

                using (new GUILayout.HorizontalScope())
                {
                    _renamePrefix = EditorGUILayout.TextField("前缀", _renamePrefix);
                    _renameSuffix = EditorGUILayout.TextField("后缀", _renameSuffix);
                }

                _renameStartIndex = EditorGUILayout.IntField("起始编号", _renameStartIndex);
                _renameDigits = EditorGUILayout.IntSlider("编号位数", _renameDigits, 1, 5);

                GUILayout.Space(10);
                if (PrimaryButton("执行重命名"))
                {
                    ExecuteRename();
                }
            }
        }

        private void ExecuteRename()
        {
            Object[] os = Selection.objects;
            if (os.Length == 0)
            {
                Window.ShowNotification(new GUIContent("未选中任何对象"));
                return;
            }

            Undo.RecordObjects(os, "Batch Rename");
            for (int i = 0; i < os.Length; i++)
            {
                string idx = (_renameStartIndex + i).ToString("D" + _renameDigits);
                string b = _renameReplaceAll ? _renameBase : os[i].name;
                string n = $"{_renamePrefix}{b}_{idx}{_renameSuffix}";

                if (AssetDatabase.Contains(os[i]))
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(os[i]), n);
                else
                    os[i].name = n;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[BatchRename] 已重命名 {os.Length} 个对象");
        }
    }

    [StellarTool("变换工具集", "常用工具", 1)]
    public class TransformToolsModule : ToolModule
    {
        public override string Icon => "d_MoveTool";
        public override string Description => "包含物理对齐、阵列复制、随机变换、等距对齐等功能。";

        // Physics Snap
        private int _groundLayerMask = -1;

        // Layout
        private Vector3 _duplicateOffset = new Vector3(2, 0, 0);

        // Random
        private float _minScale = 0.8f, _maxScale = 1.2f;

        private bool _randYRotation = true;

        // Align
        private int _alignAxis = 0;
        private bool _alignMode = false;

        public override void OnGUI()
        {
            Section("物理对齐 (Snap to Ground)");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _groundLayerMask = EditorGUILayout.MaskField("地面层级", _groundLayerMask, UnityEditorInternal.InternalEditorUtility.layers);
                if (PrimaryButton("⬇️ 选中物体对齐地面")) SnapToGround();
            }

            Section("布局助手");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _duplicateOffset = EditorGUILayout.Vector3Field("阵列偏移量", _duplicateOffset);
                if (GUILayout.Button("📋 偏移复制并移动")) DuplicateWithOffset();
                if (GUILayout.Button("📁 快速打组 (Parent)")) QuickGroup();
            }

            Section("随机变换");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _randYRotation = EditorGUILayout.Toggle("随机 Y 轴旋转", _randYRotation);
                using (new GUILayout.HorizontalScope())
                {
                    _minScale = EditorGUILayout.FloatField("Min Scale", _minScale);
                    _maxScale = EditorGUILayout.FloatField("Max Scale", _maxScale);
                }

                if (GUILayout.Button("🎲 应用随机效果")) ApplyRandomization();
            }

            Section("对齐与分布");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _alignAxis = EditorGUILayout.Popup("轴向", _alignAxis, new[] { "X 轴", "Y 轴", "Z 轴" });
                _alignMode = EditorGUILayout.Toggle("等距分布模式", _alignMode);
                if (GUILayout.Button(_alignMode ? "📏 执行等距分布" : "📐 执行对齐")) AlignAndDistribute();
            }
        }

        private void SnapToGround()
        {
            Transform[] ts = Selection.transforms;
            Undo.RecordObjects(ts, "Snap To Ground");
            foreach (var t in ts)
            {
                float hgt = 2.0f;
                if (t.TryGetComponent<Renderer>(out var r)) hgt = r.bounds.size.y + 0.5f;

                if (Physics.Raycast(t.position + Vector3.up * hgt, Vector3.down, out RaycastHit h, 2000f, _groundLayerMask))
                {
                    Vector3 p = h.point;
                    if (t.TryGetComponent<Renderer>(out var ren)) p.y += (t.position.y - ren.bounds.min.y);
                    t.position = p;
                }
            }
        }

        private void DuplicateWithOffset()
        {
            GameObject act = Selection.activeGameObject;
            if (act == null) return;
            GameObject n = Object.Instantiate(act, act.transform.parent);
            n.name = act.name;
            Undo.RegisterCreatedObjectUndo(n, "Duplicate Offset");
            n.transform.position = act.transform.position + _duplicateOffset;
            Selection.activeGameObject = n;
        }

        private void QuickGroup()
        {
            Transform[] ss = Selection.transforms;
            if (ss.Length == 0) return;
            GameObject p = new GameObject("Group_New");
            Undo.RegisterCreatedObjectUndo(p, "Quick Group");
            p.transform.position = ss[0].position;
            foreach (var t in ss) Undo.SetTransformParent(t, p.transform, "Group");
            Selection.activeGameObject = p;
        }

        private void ApplyRandomization()
        {
            Undo.RecordObjects(Selection.transforms, "Randomize");
            foreach (var t in Selection.transforms)
            {
                if (_randYRotation) t.Rotate(0, UnityEngine.Random.Range(0, 360f), 0);
                t.localScale = Vector3.one * UnityEngine.Random.Range(_minScale, _maxScale);
            }
        }

        private void AlignAndDistribute()
        {
            Transform[] transforms = Selection.transforms;
            if (transforms.Length < 2) return;
            Undo.RecordObjects(transforms, "Align/Distribute");

            if (_alignMode) // 分布
            {
                var sorted = transforms.OrderBy(t => GetAxisValue(t.position, _alignAxis)).ToList();
                float start = GetAxisValue(sorted[0].position, _alignAxis);
                float end = GetAxisValue(sorted.Last().position, _alignAxis);
                float step = (end - start) / (sorted.Count - 1);

                for (int i = 0; i < sorted.Count; i++)
                {
                    Vector3 pos = sorted[i].position;
                    SetAxisValue(ref pos, _alignAxis, start + step * i);
                    sorted[i].position = pos;
                }
            }
            else // 对齐
            {
                float avg = transforms.Average(t => GetAxisValue(t.position, _alignAxis));
                foreach (var t in transforms)
                {
                    Vector3 pos = t.position;
                    SetAxisValue(ref pos, _alignAxis, avg);
                    t.position = pos;
                }
            }
        }

        private float GetAxisValue(Vector3 v, int axis) => axis == 0 ? v.x : (axis == 1 ? v.y : v.z);

        private void SetAxisValue(ref Vector3 v, int axis, float val)
        {
            if (axis == 0) v.x = val;
            else if (axis == 1) v.y = val;
            else v.z = val;
        }
    }

    [StellarTool("材质与资源工具", "常用工具", 2)]
    public class SmartMaterialModule : ToolModule
    {
        public override string Icon => "d_Material Icon";
        public override string Description => "PBR 材质一键生成，以及 UI/TMP 批量设置。";

        // PBR
        private readonly string[] _albedoKeys = { "_albedo", "_basecolor", "_maintex", "diffuse" };
        private readonly string[] _normalKeys = { "_normal", "_bump", "_n" };
        private readonly string[] _maskKeys = { "_mask", "_metallic", "_ao", "_roughness" };

        // UI
        private Material _targetImageMat;
        private UnityEngine.Object _targetFont; // TMP_FontAsset

        public override void OnGUI()
        {
            Section("PBR 智能材质生成");
            EditorGUILayout.HelpBox("选中包含贴图的文件夹或多张贴图，根据命名规则自动生成材质。", MessageType.Info);
            if (PrimaryButton("✨ 识别并生成材质")) CreateMaterialsFromSelection();

            Section("UI Image 材质批量设置");
            _targetImageMat = (Material)EditorGUILayout.ObjectField("目标材质", _targetImageMat, typeof(Material), false);
            if (GUILayout.Button("应用到选中物体 (含子物体)")) ApplyImageMaterial();

            Section("TMP 字体批量设置");
            _targetFont = EditorGUILayout.ObjectField("目标字体 (SDF)", _targetFont, typeof(Object), false); // 弱引用避免依赖
            if (GUILayout.Button("应用到选中物体 (含子物体)")) ApplyTMPFont();
        }

        private void CreateMaterialsFromSelection()
        {
            var textures = Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets);
            if (textures.Length == 0)
            {
                Debug.LogWarning("未选中贴图");
                return;
            }

            var groups = textures.GroupBy(t => t.name.Split('_')[0]).ToList();
            int count = 0;

            foreach (var group in groups)
            {
                string baseName = group.Key;
                string path = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(group.First()));
                string matPath = $"{path}/{baseName}_Mat.mat";

                Material mat = new Material(Shader.Find("Standard"));
                foreach (var tex in group)
                {
                    string lower = tex.name.ToLower();
                    if (_albedoKeys.Any(k => lower.Contains(k))) mat.SetTexture("_MainTex", tex);
                    else if (_normalKeys.Any(k => lower.Contains(k))) mat.SetTexture("_BumpMap", tex);
                    else if (_maskKeys.Any(k => lower.Contains(k))) mat.SetTexture("_MetallicGlossMap", tex);
                }

                AssetDatabase.CreateAsset(mat, matPath);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"生成了 {count} 个材质");
        }

        private void ApplyImageMaterial()
        {
            if (_targetImageMat == null) return;
            foreach (var go in Selection.gameObjects)
            {
                var imgs = go.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                Undo.RecordObjects(imgs, "Set Image Material");
                foreach (var img in imgs) img.material = _targetImageMat;
            }
        }

        private void ApplyTMPFont()
        {
            if (_targetFont == null) return;
            foreach (var go in Selection.gameObjects)
            {
                var tmps = go.GetComponentsInChildren<Component>(true).Where(c => c.GetType().Name.Contains("TextMeshPro")).ToArray();
                Undo.RecordObjects(tmps, "Set TMP Font");
                foreach (var tmp in tmps)
                {
                    var prop = tmp.GetType().GetProperty("font");
                    if (prop != null) prop.SetValue(tmp, _targetFont);
                }
            }
        }
    }

    [StellarTool("场景优化与清理", "常用工具", 3)]
    public class SceneOptimizationModule : ToolModule
    {
        public override string Icon => "d_SceneViewTools";
        public override string Description => "Missing Script 清理、重复物体查找、批量静态设置、Prefab 替换。";

        private GameObject _replacementPrefab;
        private bool _batchContributeGI = true;
        private bool _batchReflectionProbe = true;
        private bool _batchOccluder = false;
        private bool _batchBatching = false;

        public override void OnGUI()
        {
            Section("Missing Script 清理");
            if (DangerButton("⚠️ 清理当前场景 Missing Scripts")) FindAndCleanMissingScripts();

            Section("重复物体查找");
            if (GUILayout.Button("🔍 扫描重复物体 (位置/旋转/Mesh)")) FindDuplicateObjects();

            Section("资产替换");
            _replacementPrefab = (GameObject)EditorGUILayout.ObjectField("替换为", _replacementPrefab, typeof(GameObject), false);
            if (GUILayout.Button("🔄 替换选中物体")) ReplaceWithPrefab();

            Section("批量静态设置");
            using (new GUILayout.HorizontalScope())
            {
                _batchContributeGI = EditorGUILayout.ToggleLeft("GI", _batchContributeGI, GUILayout.Width(40));
                _batchBatching = EditorGUILayout.ToggleLeft("Batching", _batchBatching, GUILayout.Width(70));
                _batchOccluder = EditorGUILayout.ToggleLeft("Occluder", _batchOccluder, GUILayout.Width(70));
                _batchReflectionProbe = EditorGUILayout.ToggleLeft("Reflect", _batchReflectionProbe, GUILayout.Width(60));
            }

            if (GUILayout.Button("⚙️ 应用静态标志")) ApplyStaticFlags();
        }

        private void FindAndCleanMissingScripts()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int count = 0;
            foreach (var root in roots) count += ProcessClean(root);
            Debug.Log($"清理了 {count} 个 Missing Scripts");
        }

        private int ProcessClean(GameObject go)
        {
            int c = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform) c += ProcessClean(child.gameObject);
            return c;
        }

        private void FindDuplicateObjects()
        {
            var all = Object.FindObjectsOfType<MeshFilter>();
            var duplicates = new List<GameObject>();
            var processed = new HashSet<MeshFilter>();

            for (int i = 0; i < all.Length; i++)
            {
                if (processed.Contains(all[i])) continue;
                var mf1 = all[i];
                for (int j = i + 1; j < all.Length; j++)
                {
                    var mf2 = all[j];
                    if (processed.Contains(mf2)) continue;

                    if (mf1.sharedMesh == mf2.sharedMesh &&
                        Vector3.Distance(mf1.transform.position, mf2.transform.position) < 0.01f)
                    {
                        duplicates.Add(mf2.gameObject);
                        processed.Add(mf2);
                    }
                }
            }

            Selection.objects = duplicates.ToArray();
            Debug.Log($"发现 {duplicates.Count} 个重复物体");
        }

        private void ReplaceWithPrefab()
        {
            if (_replacementPrefab == null) return;
            var selection = Selection.gameObjects;
            Undo.RecordObjects(selection, "Replace Prefab");
            foreach (var go in selection)
            {
                var n = (GameObject)PrefabUtility.InstantiatePrefab(_replacementPrefab, go.transform.parent);
                n.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
                n.transform.localScale = go.transform.localScale;
                Undo.RegisterCreatedObjectUndo(n, "Replace");
                Undo.DestroyObjectImmediate(go);
            }
        }

        private void ApplyStaticFlags()
        {
            var flags = (StaticEditorFlags)0;
            if (_batchContributeGI) flags |= StaticEditorFlags.ContributeGI;
            if (_batchBatching) flags |= StaticEditorFlags.BatchingStatic;
            if (_batchOccluder) flags |= StaticEditorFlags.OccluderStatic;
            if (_batchReflectionProbe) flags |= StaticEditorFlags.ReflectionProbeStatic;

            foreach (var go in Selection.gameObjects)
            {
                Undo.RecordObject(go, "Set Static");
                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }
        }
    }

    // =========================================================
    // 生产力组
    // =========================================================

    [StellarTool("烘焙助手", "生产力", 0)]
    public class BakeToolsModule : ToolModule
    {
        public override string Icon => "d_Lighting";
        public override string Description => "快速切换烘焙质量预设。";

        private bool _isPreview = true;
        private int _presetIndex = 0;
        private string[] _presets = { "极速预览", "中等质量", "生产级", "影视级" };

        public override void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUI.color = _isPreview ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button(_isPreview ? "当前：预览模式" : "当前：生产模式", GUILayout.Height(30)))
                    _isPreview = !_isPreview;
                GUI.color = Color.white;
            }

            _presetIndex = EditorGUILayout.Popup("质量预设", _presetIndex, _presets);

            if (PrimaryButton("应用设置到 Lighting Settings"))
            {
                ApplyLightingSettings();
            }

            if (GUILayout.Button("🔥 开始烘焙"))
            {
                if (Lightmapping.isRunning) Lightmapping.ForceStop();
                else Lightmapping.BakeAsync();
            }
        }

        private void ApplyLightingSettings()
        {
            Debug.Log($"应用预设: {_presets[_presetIndex]} (模式: {(_isPreview ? "Preview" : "Production")})");
        }
    }

    [StellarTool("快速创建", "生产力", 1)]
    public class QuickCreateModule : ToolModule
    {
        public override string Icon => "d_CreateAddNew";
        public override string Description => "快速创建常用物体到当前视图中心或选中物体位置。";

        public override void OnGUI()
        {
            Section("基础几何体");
            Row(() => CreatePrim(PrimitiveType.Cube), "Cube", "PreMatCube");
            Row(() => CreatePrim(PrimitiveType.Sphere), "Sphere", "PreMatSphere");
            Row(() => CreatePrim(PrimitiveType.Plane), "Plane", "PreMatCylinder");

            Section("灯光与探针");
            Row(() => CreateObj("Directional Light", typeof(Light)), "Dir Light", "DirectionalLight Icon");
            Row(() => CreateObj("Point Light", typeof(Light)), "Point Light", "Light Icon");
            Row(() => CreateObj("Reflection Probe", typeof(ReflectionProbe)), "Refl Probe", "ReflectionProbe Icon");
        }

        private void Row(Action action, string name, string icon)
        {
            if (GUILayout.Button(new GUIContent("  " + name, EditorGUIUtility.IconContent(icon).image), Window.SidebarButtonStyle))
                action();
        }

        private void CreatePrim(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            Place(go);
        }

        private void CreateObj(string name, Type comp)
        {
            var go = new GameObject(name);
            if (comp != null) go.AddComponent(comp);
            Place(go);
        }

        private void Place(GameObject go)
        {
            if (Selection.activeTransform != null) go.transform.position = Selection.activeTransform.position;
            else if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        }
    }

    [StellarTool("Mesh 合并碰撞体生成工具", "生产力", 0)]
    public class CombinedMeshColliderHubModule : ToolModule
    {
        public override string Icon => "d_ScriptableObject Icon";
        public override string Description => "打开 Mesh 合并碰撞体生成工具。";

        public override void OnGUI()
        {
            if (PrimaryButton("打开窗口", GUILayout.Height(34))) CombinedMeshColliderWindow.ShowWindow();
        }
    }
    // =========================================================
    // 框架核心组
    // =========================================================

    [StellarTool("字典序列化 (增强)", "框架核心", 0)]
    public class DictionarySerializerHubModule : ToolModule
    {
        public override string Icon => "d_ScriptableObject Icon";
        public override string Description => "打开 DictionarySerializerWindow。";

        public override void OnGUI()
        {
            if (PrimaryButton("打开窗口", GUILayout.Height(34))) DictionarySerializerWindow.ShowWindow();
        }
    }

    [StellarTool("列表序列化 (增强)", "框架核心", 0)]
    public class ListSerializerWindowHubModule : ToolModule
    {
        public override string Icon => "d_ScriptableObject Icon";
        public override string Description => "打开 ListSerializerWindow。";

        public override void OnGUI()
        {
            if (PrimaryButton("打开窗口", GUILayout.Height(34))) ListSerializerWindow.ShowWindow();
        }
    }

    [StellarTool("脚本内容复制", "框架核心", 1)]
    public class FolderCopyHubModule : ToolModule
    {
        public override string Icon => "d_Folder Icon";
        public override string Description => "打开 FolderContentCopyTool。";

        public override void OnGUI()
        {
            if (PrimaryButton("打开窗口", GUILayout.Height(34))) FolderContentCopyTool.ShowWindow();
        }
    }

    [StellarTool("URP 材质转换", "框架核心", 2)]
    public class URPConverterHubModule : ToolModule
    {
        public override string Icon => "d_Material Icon";
        public override string Description => "打开 URPMaterialConverterWindow。";

        public override void OnGUI()
        {
            if (PrimaryButton("打开窗口", GUILayout.Height(34))) URPMaterialConverterWindow.Open();
        }
    }

    [StellarTool("UIKit 工具", "框架核心", 3)]
    public class UIKitHubModule : ToolModule
    {
        public override string Icon => "d_Canvas Icon";
        public override string Description => "UIRoot/Panel Template 入口。";

        public override void OnGUI()
        {
            if (PrimaryButton("生成 / 覆盖 UIRoot Prefab", GUILayout.Height(34))) UIKitEditor.CreateUIRootPrefab();
            if (PrimaryButton("创建 Panel Template", GUILayout.Height(34))) UIKitEditor.CreatePanelTemplateUnderSelection();
        }
    }

    [StellarTool("AppConfig 工具", "框架核心", 4)]
    public class AppConfigHubModule : ToolModule
    {
        public override string Icon => "d_TextAsset Icon";
        public override string Description => "生成/打开/清除 AppConfig。";

        public override void OnGUI()
        {
            if (PrimaryButton("生成默认配置", GUILayout.Height(30))) AppConfigEditor.GenerateDefaultConfig();
            if (PrimaryButton("打开默认配置文件", GUILayout.Height(30))) AppConfigEditor.OpenDefaultConfig();
            if (DangerButton("清除本地存档", GUILayout.Height(30))) AppConfigEditor.ClearSaveConfig();
        }
    }

    [StellarTool("UrlConfig 工具", "框架核心", 5)]
    public class UrlConfigHubModule : ToolModule
    {
        public override string Icon => "d_UnityEditor.ConsoleWindow";
        public override string Description => "切换 Dev/Release、生成默认 urlConfig。";

        public override void OnGUI()
        {
            EditorGUILayout.LabelField($"当前环境：{UrlConfigEditor.GetCurrentEnvLabel()}", EditorStyles.miniBoldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (PrimaryButton("Dev")) UrlConfigEditor.SwitchToDev();
                if (DangerButton("Release")) UrlConfigEditor.SwitchToRelease();
            }

            if (GUILayout.Button("打开配置文件")) UrlConfigEditor.OpenConfigFile();
            if (GUILayout.Button("生成默认配置")) UrlConfigEditor.GenerateDefaultConfig();
        }
    }
}