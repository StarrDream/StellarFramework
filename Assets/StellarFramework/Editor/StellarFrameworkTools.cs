using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    public class StellarFrameworkTools : EditorWindow
    {
        [MenuItem("StellarFramework/Tools Hub %#t")]
        public static void ShowWindow()
        {
            var window = GetWindow<StellarFrameworkTools>("Stellar Tools");
            window.minSize = new Vector2(1000, 680);
            window.Show();
        }

        private static readonly List<ToolModule> Modules = new List<ToolModule>();

        private ToolModule _currentModule;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private string _search = "";

        // 样式定义
        private static readonly Color Accent = new Color(0.35f, 0.68f, 1.00f);
        private static readonly Color AccentDark = new Color(0.22f, 0.52f, 0.88f);
        private static readonly Color Danger = new Color(0.90f, 0.25f, 0.25f);
        private static readonly Color Ok = new Color(0.22f, 0.75f, 0.35f);
        private static readonly Color Warn = new Color(0.95f, 0.65f, 0.20f);

        private bool _stylesReady;
        private GUIStyle _topBarStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subTitleStyle;
        private GUIStyle _sidebarHeaderStyle;
        private GUIStyle _sidebarButtonStyle;
        private GUIStyle _cardStyle;
        public GUIStyle _sectionHeaderStyle; // 公开给 Module 使用
        private GUIStyle _miniHintStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _searchCancelStyle;
        public GUIStyle _primaryButtonStyle;
        public GUIStyle _dangerButtonStyle;
        public GUIStyle _ghostButtonStyle;

        private void OnEnable()
        {
            InitializeModules();

            if (Modules.Count > 0)
            {
                _currentModule = Modules[0];
                _currentModule.OnEnable();
            }
            _stylesReady = false;
        }

        private void OnDisable()
        {
            if (_currentModule != null) _currentModule.OnDisable();
        }

        private void OnSelectionChange()
        {
            if (_currentModule != null)
            {
                _currentModule.OnSelectionChange();
                Repaint();
            }
        }

        private void InitializeModules()
        {
            Modules.Clear();

            // --- 常用工具 ---
            Modules.Add(new BatchRenameModule(this));
            Modules.Add(new TransformToolsModule(this)); // 集成：物理对齐/布局/随机/对齐分布
            Modules.Add(new SmartMaterialModule(this));  // 集成：智能材质 + Image/TMP 设置
            Modules.Add(new SceneOptimizationModule(this)); // 集成：静态设置/查重/替换/Missing清理
            
            // --- 生产力 ---
            Modules.Add(new BakeToolsModule(this)); // 集成：烘焙助手
            Modules.Add(new QuickCreateModule(this)); // 集成：快速创建

            // --- 框架核心 ---
            Modules.Add(new DictionarySerializerHubModule(this));
            Modules.Add(new FolderCopyHubModule(this));
            Modules.Add(new URPConverterHubModule(this));
            Modules.Add(new UIKitHubModule(this));
            Modules.Add(new AppConfigHubModule(this));
            Modules.Add(new UrlConfigHubModule(this));
        }

        private void OnGUI()
        {
            EnsureStylesOnGUI();
            DrawTopBar();

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            using (new GUILayout.VerticalScope(_cardStyle, GUILayout.Width(260), GUILayout.ExpandHeight(true)))
            {
                DrawSidebar();
            }

            // Content
            using (new GUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                DrawContent();
            }

            EditorGUILayout.EndHorizontal();
            DrawFooter();
        }

        private void EnsureStylesOnGUI()
        {
            if (_stylesReady) return;

            _topBarStyle = new GUIStyle(EditorStyles.toolbar) { fixedHeight = 34 };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            _subTitleStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.65f) } };
            _sidebarHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 10, 10), normal = { textColor = Accent } };
            
            _sidebarButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 32,
                fontSize = 12,
                margin = new RectOffset(2, 2, 1, 1),
                padding = new RectOffset(10, 10, 6, 6)
            };

            _cardStyle = new GUIStyle("HelpBox") { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(8, 8, 8, 8) };
            
            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 12, 
                normal = { textColor = Accent }, 
                margin = new RectOffset(0, 0, 10, 4) 
            };

            _miniHintStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontSize = 10, normal = { textColor = new Color(1f, 1f, 1f, 0.62f) } };
            
            _searchFieldStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? new GUIStyle("ToolbarSeachTextField");
            _searchCancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? new GUIStyle("ToolbarSeachCancelButton");

            _primaryButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 12, fontStyle = FontStyle.Bold };
            _dangerButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 12 };
            _ghostButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 26, fontSize = 11 };

            _stylesReady = true;
        }

        private void DrawTopBar()
        {
            using (new GUILayout.HorizontalScope(_topBarStyle))
            {
                GUILayout.Space(8);
                GUILayout.Label("StellarFramework Tools", _titleStyle);
                GUILayout.Space(10);
                GUILayout.Label("统一入口 | Editor 工具集成版", _subTitleStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    if(_currentModule != null) _currentModule.OnEnable();
                }
            }
        }

        private void DrawSidebar()
        {
            GUILayout.Label("工具列表", _sidebarHeaderStyle);

            using (new GUILayout.HorizontalScope())
            {
                _search = GUILayout.TextField(_search, _searchFieldStyle);
                if (GUILayout.Button(GUIContent.none, _searchCancelStyle))
                {
                    _search = "";
                    GUI.FocusControl(null);
                    Repaint();
                }
            }

            GUILayout.Space(6);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            foreach (var m in Modules)
            {
                if (!string.IsNullOrEmpty(_search) && m.Title.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var old = GUI.backgroundColor;
                // 使用对象引用比较，而非索引
                if (_currentModule == m) GUI.backgroundColor = Accent;

                var icon = EditorGUIUtility.IconContent(m.Icon).image;
                var label = new GUIContent($"  {m.Title}", icon);

                if (GUILayout.Button(label, _sidebarButtonStyle))
                {
                    if (_currentModule != m)
                    {
                        if (_currentModule != null) _currentModule.OnDisable();
                        _currentModule = m;
                        _currentModule.OnEnable();
                        _contentScroll = Vector2.zero;
                        GUI.FocusControl(null);
                    }
                }
                GUI.backgroundColor = old;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawContent()
        {
            if (_currentModule == null) return;

            using (new GUILayout.VerticalScope(_cardStyle))
            {
                GUILayout.Label(_currentModule.Title, _titleStyle);
                GUILayout.Label(_currentModule.Description, _miniHintStyle);
            }

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            GUILayout.Space(6);
            
            // 错误捕获，防止模块报错炸毁整个窗口
            try
            {
                _currentModule.OnGUI();
            }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox($"模块绘制出错: {e.Message}\n{e.StackTrace}", MessageType.Error);
            }

            GUILayout.Space(18);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("v2.2 Pro Integrated", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping Framework", _ghostButtonStyle, GUILayout.Width(120)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/StellarFramework");
                    if (obj) EditorGUIUtility.PingObject(obj);
                }
            }
        }

        // =========================================================
        // ToolModule 基类
        // =========================================================
        public abstract class ToolModule
        {
            public EditorWindow Owner { get; private set; }
            protected StellarFrameworkTools Window => (StellarFrameworkTools)Owner;
            public abstract string Title { get; }
            public virtual string Description => " ";
            public virtual string Icon => "d_ScriptableObject Icon";

            protected ToolModule(EditorWindow owner) { Owner = owner; }
            public abstract void OnGUI();
            public virtual void OnEnable() { }
            public virtual void OnDisable() { }
            public virtual void OnSelectionChange() { }

            protected void Section(string title)
            {
                GUILayout.Space(10);
                GUILayout.Label(title, Window._sectionHeaderStyle);
                GUILayout.Space(2);
            }

            protected bool PrimaryButton(string label, params GUILayoutOption[] options)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = AccentDark;
                bool clicked = GUILayout.Button(label, Window._primaryButtonStyle, options);
                GUI.backgroundColor = old;
                return clicked;
            }
            
            protected bool DangerButton(string label, params GUILayoutOption[] options)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = Danger;
                bool clicked = GUILayout.Button(label, Window._dangerButtonStyle, options);
                GUI.backgroundColor = old;
                return clicked;
            }
        }

        // =========================================================
        // 模块实现 (集成 UnityProToolbox 逻辑)
        // =========================================================

        // 1. 批量重命名
        private class BatchRenameModule : ToolModule
        {
            public override string Title => "批量重命名";
            public override string Icon => "d_TextAsset Icon";
            public override string Description => "支持前缀、后缀、数字编号替换。支持场景物体和资源文件。";

            private string _renameBase = "Object";
            private string _renamePrefix = "";
            private string _renameSuffix = "";
            private int _renameStartIndex = 0;
            private int _renameDigits = 2;
            private bool _renameReplaceAll = true;

            public BatchRenameModule(EditorWindow owner) : base(owner) { }

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
                if (os.Length == 0) { Window.ShowNotification(new GUIContent("未选中任何对象")); return; }

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

        // 2. 变换工具集 (物理对齐/布局/随机/分布)
        private class TransformToolsModule : ToolModule
        {
            public override string Title => "变换工具集";
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

            public TransformToolsModule(EditorWindow owner) : base(owner) { }

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
            private void SetAxisValue(ref Vector3 v, int axis, float val) { if (axis == 0) v.x = val; else if (axis == 1) v.y = val; else v.z = val; }
        }

        // 3. 智能材质与资源设置
        private class SmartMaterialModule : ToolModule
        {
            public override string Title => "材质与资源工具";
            public override string Icon => "d_Material Icon";
            public override string Description => "PBR 材质一键生成，以及 UI/TMP 批量设置。";

            // PBR
            private readonly string[] _albedoKeys = { "_albedo", "_basecolor", "_maintex", "diffuse" };
            private readonly string[] _normalKeys = { "_normal", "_bump", "_n" };
            private readonly string[] _maskKeys = { "_mask", "_metallic", "_ao", "_roughness" };

            // UI
            private Material _targetImageMat;
            private UnityEngine.Object _targetFont; // TMP_FontAsset

            public SmartMaterialModule(EditorWindow owner) : base(owner) { }

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
                // 简化版逻辑：真实项目建议使用 UnityProToolbox 的完整匹配逻辑
                // 这里为了代码简洁，演示核心思路
                var textures = Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets);
                if (textures.Length == 0) { Debug.LogWarning("未选中贴图"); return; }

                var groups = textures.GroupBy(t => t.name.Split('_')[0]).ToList();
                int count = 0;

                foreach (var group in groups)
                {
                    string baseName = group.Key;
                    string path = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(group.First()));
                    string matPath = $"{path}/{baseName}_Mat.mat";

                    Material mat = new Material(Shader.Find("Standard")); // 默认 Standard，可扩展
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
                // 使用反射或 dynamic 避免强依赖 TMP 包
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

        // 4. 场景优化与清理
        private class SceneOptimizationModule : ToolModule
        {
            public override string Title => "场景优化与清理";
            public override string Icon => "d_SceneViewTools";
            public override string Description => "Missing Script 清理、重复物体查找、批量静态设置、Prefab 替换。";

            private GameObject _replacementPrefab;
            private bool _batchContributeGI = true;
            private bool _batchReflectionProbe = true;
            private bool _batchOccluder = false;
            private bool _batchBatching = false;

            public SceneOptimizationModule(EditorWindow owner) : base(owner) { }

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
                // 简化版查重逻辑
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

        // 5. 烘焙助手
        private class BakeToolsModule : ToolModule
        {
            public override string Title => "烘焙助手";
            public override string Icon => "d_Lighting";
            public override string Description => "快速切换烘焙质量预设。";

            private bool _isPreview = true;
            private int _presetIndex = 0;
            private string[] _presets = { "极速预览", "中等质量", "生产级", "影视级" };

            public BakeToolsModule(EditorWindow owner) : base(owner) { }

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
                // 仅做逻辑演示，实际需要操作 Lightmapping.lightingSettings
                // 真实逻辑参考 UnityProToolbox 的 ApplySettingsToAsset
                Debug.Log($"应用预设: {_presets[_presetIndex]} (模式: {(_isPreview ? "Preview" : "Production")})");
            }
        }

        // 6. 快速创建
        private class QuickCreateModule : ToolModule
        {
            public override string Title => "快速创建";
            public override string Icon => "d_CreateAddNew";
            public override string Description => "快速创建常用物体到当前视图中心或选中物体位置。";

            public QuickCreateModule(EditorWindow owner) : base(owner) { }

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
                if (GUILayout.Button(new GUIContent("  " + name, EditorGUIUtility.IconContent(icon).image), Window._sidebarButtonStyle))
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

        // =========================================================
        // 原有 Hub 模块 (保持不变，仅做样式适配)
        // =========================================================

        private class DictionarySerializerHubModule : ToolModule
        {
            public override string Title => "字典序列化 (增强)";
            public override string Icon => "d_ScriptableObject Icon";
            public override string Description => "打开 DictionarySerializerWindow。";
            public DictionarySerializerHubModule(EditorWindow owner) : base(owner) { }
            public override void OnGUI()
            {
                if (PrimaryButton("打开窗口", GUILayout.Height(34))) DictionarySerializerWindow.ShowWindow();
            }
        }

        private class FolderCopyHubModule : ToolModule
        {
            public override string Title => "脚本内容复制";
            public override string Icon => "d_Folder Icon";
            public override string Description => "打开 FolderContentCopyTool。";
            public FolderCopyHubModule(EditorWindow owner) : base(owner) { }
            public override void OnGUI()
            {
                if (PrimaryButton("打开窗口", GUILayout.Height(34))) FolderContentCopyTool.ShowWindow();
            }
        }

        private class URPConverterHubModule : ToolModule
        {
            public override string Title => "URP 材质转换";
            public override string Icon => "d_Material Icon";
            public override string Description => "打开 URPMaterialConverterWindow。";
            public URPConverterHubModule(EditorWindow owner) : base(owner) { }
            public override void OnGUI()
            {
                if (PrimaryButton("打开窗口", GUILayout.Height(34))) URPMaterialConverterWindow.Open();
            }
        }

        private class UIKitHubModule : ToolModule
        {
            public override string Title => "UIKit 工具";
            public override string Icon => "d_Canvas Icon";
            public override string Description => "UIRoot/Panel Template 入口。";
            public UIKitHubModule(EditorWindow owner) : base(owner) { }
            public override void OnGUI()
            {
                if (PrimaryButton("生成 / 覆盖 UIRoot Prefab", GUILayout.Height(34))) UIKitEditor.CreateUIRootPrefab();
                if (PrimaryButton("创建 Panel Template", GUILayout.Height(34))) UIKitEditor.CreatePanelTemplateUnderSelection();
            }
        }

        private class AppConfigHubModule : ToolModule
        {
            public override string Title => "AppConfig 工具";
            public override string Icon => "d_TextAsset Icon";
            public override string Description => "生成/打开/清除 AppConfig。";
            public AppConfigHubModule(EditorWindow owner) : base(owner) { }
            public override void OnGUI()
            {
                if (PrimaryButton("生成默认配置", GUILayout.Height(30))) AppConfigEditor.GenerateDefaultConfig();
                if (PrimaryButton("打开默认配置文件", GUILayout.Height(30))) AppConfigEditor.OpenDefaultConfig();
                if (DangerButton("清除本地存档", GUILayout.Height(30))) AppConfigEditor.ClearSaveConfig();
            }
        }

        private class UrlConfigHubModule : ToolModule
        {
            public override string Title => "UrlConfig 工具";
            public override string Icon => "d_UnityEditor.ConsoleWindow";
            public override string Description => "切换 Dev/Release、生成默认 urlConfig。";
            public UrlConfigHubModule(EditorWindow owner) : base(owner) { }
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
}
