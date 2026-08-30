using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarFramework.Editor
{
    [Serializable]
    public class BundleRule
    {
        public string bundleName;
        public string path;
        public bool isFolder;
        public List<string> includedAssets = new List<string>();
        public List<string> dependencies = new List<string>();
    }

    [StellarTool("资源打包 (AssetBundle)", "资源管理", 0,
        RequiredAssemblyNames = new[] { "StellarFramework.ResKit.AssetBundle" })]
    public class AssetBundleToolModule : ToolModule
    {
        private sealed class ShaderCollectionResult
        {
            public readonly HashSet<Shader> Shaders = new HashSet<Shader>();
            public readonly HashSet<Material> Materials = new HashSet<Material>();
            public readonly List<string> BundleAssignmentWarnings = new List<string>();
        }

        private sealed class AssetBundleWorkspaceStatus
        {
            public bool HasDefaultRule;
            public bool HasSampleAsset;
            public bool HasAssetMap;
            public bool HasOutputDirectory;
            public bool HasManifestBundle;
            public readonly List<string> MissingItems = new List<string>();

            public bool IsReady => HasDefaultRule && HasSampleAsset && HasAssetMap && HasOutputDirectory && HasManifestBundle;
        }

        private List<BundleRule> _rules = new List<BundleRule>();
        private BundleRule _selectedRule;
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private bool _hasUnappliedChanges = false;
        private bool _isBuilding = false;
        private readonly List<string> _initializationMessages = new List<string>();
        private readonly List<string> _initializationErrors = new List<string>();

        private const string PREFS_KEY = "Stellar_AB_Rules";
        private const string SHADER_BUNDLE_NAME = "shaders"; // 全局 Shader 包名
        private const string DefaultBundleName = "art";
        private const string DefaultSampleAssetPath =
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab";
        private const string DefaultSampleFolderPath =
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle";
        private const string DefaultSampleMaterialPath =
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB_Auto.mat";
        private const string AssetMapAssetPath = "Assets/StellarFramework/Generated/AssetMap/AssetMap.cs";
        private const string ShaderVariantCollectionAssetPath =
            "Assets/StellarFramework/Generated/AssetBundleShaders/AssetBundleShaderVariants.shadervariants";
        private const string GraphicsSettingsAssetPath = "ProjectSettings/GraphicsSettings.asset";

        public override string Icon => "d_PreMatCube";
        public override string Description => "可视化的 AB 包依赖分析、冗余检测与构建工具。";

        public override void OnEnable()
        {
            LoadRules();
        }

        public override void OnDisable()
        {
            SaveRules();
        }

        public override void OnGUI()
        {
            AssetBundleWorkspaceStatus workspaceStatus = EvaluateWorkspaceStatus();
            if (!workspaceStatus.IsReady)
            {
                DrawInitializationGate(workspaceStatus);
                return;
            }

            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();
                GUI.enabled = !_isBuilding;

                if (GUILayout.Button("应用规则 & 生成代码", EditorStyles.toolbarButton, GUILayout.Width(140)))
                {
                    ApplyRulesAndAnalyze(true);
                }

                GUILayout.Space(10);

                if (GUILayout.Button("清理产物", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("清理确认", "确定要删除当前平台的所有 AssetBundle 构建产物吗？\n\n这将导致下次构建变成全量构建（较慢）。", "确定清理", "取消"))
                    {
                        ClearAssetBundles();
                    }
                }

                if (GUILayout.Button("强制重构", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    ForceRebuild();
                }

                if (GUILayout.Button("增量构建", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    BuildBundles();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.BeginHorizontal();
            {
                DrawLeftPanel();
                DrawRightPanel();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInitializationGate(AssetBundleWorkspaceStatus status)
        {
            Section("初始化AB");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "第一次使用 AssetBundle 工作流时，先初始化一次 AB 工作区。这个步骤会补齐 ResKit 的 AB 示例资源、默认规则、AssetMap 以及 StreamingAssets/AssetBundles 输出目录。初始化完成后，才会进入完整的 AB 构建面板。",
                    MessageType.Info);

                if (status.MissingItems.Count > 0)
                {
                    EditorGUILayout.HelpBox("当前缺少：\n- " + string.Join("\n- ", status.MissingItems), MessageType.Warning);
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton("初始化AB", GUILayout.Height(34)))
                    {
                        if (TryInitializeWorkspace(out List<string> messages, out List<string> errors))
                        {
                            _initializationMessages.Clear();
                            _initializationMessages.AddRange(messages);
                            _initializationErrors.Clear();
                            Window?.ShowNotification(new GUIContent("AB 初始化完成"));
                        }
                        else
                        {
                            _initializationMessages.Clear();
                            _initializationMessages.AddRange(messages);
                            _initializationErrors.Clear();
                            _initializationErrors.AddRange(errors);
                            Window?.ShowNotification(new GUIContent("AB 初始化失败"));
                        }

                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("刷新状态", GUILayout.Height(30)))
                    {
                        LoadRules();
                        GUI.FocusControl(null);
                    }
                }
            }

            if (_initializationMessages.Count > 0 || _initializationErrors.Count > 0)
            {
                Section("最近结果");
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    foreach (string message in _initializationMessages)
                    {
                        EditorGUILayout.HelpBox(message, MessageType.Info);
                    }

                    foreach (string error in _initializationErrors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }
                }
            }
        }

        private void DrawLeftPanel()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.Width(280), GUILayout.ExpandHeight(true)))
            {
                var dropRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
                GUI.Box(dropRect, "拖拽 [文件夹] 或 [文件] 到此处\n添加打包规则", "HelpBox");
                HandleDragDrop(dropRect);

                GUILayout.Space(5);
                GUILayout.Label($"打包规则列表 ({_rules.Count})", EditorStyles.boldLabel);

                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                int indexToDelete = -1;

                for (int i = 0; i < _rules.Count; i++)
                {
                    var rule = _rules[i];
                    bool isSelected = _selectedRule == rule;

                    Rect rowRect = EditorGUILayout.BeginHorizontal(isSelected ? "box" : GUIStyle.none, GUILayout.Height(24));
                    {
                        var icon = EditorGUIUtility.IconContent(rule.isFolder ? "Folder Icon" : "TextAsset Icon");
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

                        var alignStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
                        GUILayout.Label(rule.bundleName, alignStyle, GUILayout.Height(20));

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24), GUILayout.Height(18)))
                        {
                            indexToDelete = i;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (UnityEngine.Event.current.type == EventType.MouseDown && rowRect.Contains(UnityEngine.Event.current.mousePosition))
                    {
                        _selectedRule = rule;
                        GUI.FocusControl(null);
                        UnityEngine.Event.current.Use();
                        if (Window != null) Window.Repaint();
                    }
                }

                if (indexToDelete != -1)
                {
                    var rule = _rules[indexToDelete];
                    if (_selectedRule == rule) _selectedRule = null;
                    _rules.RemoveAt(indexToDelete);
                    _hasUnappliedChanges = true;
                    SaveRules();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRightPanel()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                if (_selectedRule == null)
                {
                    GUILayout.Label("请选择左侧规则查看详情", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                GUILayout.Label("规则详情", EditorStyles.boldLabel);
                GUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Bundle Name:", GUILayout.Width(100));
                string newName = EditorGUILayout.TextField(_selectedRule.bundleName);
                if (newName != _selectedRule.bundleName)
                {
                    _selectedRule.bundleName = newName;
                    _hasUnappliedChanges = true;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("源路径:", GUILayout.Width(100));
                EditorGUILayout.SelectableLabel(_selectedRule.path, EditorStyles.textField, GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.Label($"包含资源 ({_selectedRule.includedAssets.Count})", EditorStyles.boldLabel);
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                foreach (var asset in _selectedRule.includedAssets)
                {
                    EditorGUILayout.LabelField(asset, EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void HandleDragDrop(Rect dropArea)
        {
            UnityEngine.Event evt = UnityEngine.Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var path in DragAndDrop.paths)
                    {
                        AddRule(path);
                    }

                    SaveRules();
                }

                evt.Use();
            }
        }

        private void AddRule(string path)
        {
            if (_rules.Any(r => r.path == path)) return;
            _hasUnappliedChanges = true;

            bool isDir = Directory.Exists(path);
            string defaultName = path.Replace("Assets/", "")
                .Replace("/", "_")
                .Replace(" ", "_") // 新增这一行
                .ToLower();

            if (!isDir)
            {
                defaultName = Path.GetFileNameWithoutExtension(path)
                    .Replace(" ", "_") // 这里也要加
                    .ToLower();
            }

            _rules.Add(new BundleRule
            {
                bundleName = defaultName,
                path = path,
                isFolder = isDir
            });
        }

        private AssetBundleWorkspaceStatus EvaluateWorkspaceStatus()
        {
            AssetBundleWorkspaceStatus status = new AssetBundleWorkspaceStatus();
            status.HasDefaultRule = _rules.Any(rule =>
                rule != null &&
                string.Equals(rule.bundleName, DefaultBundleName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rule.path, DefaultSampleFolderPath, StringComparison.Ordinal));
            status.HasSampleAsset = File.Exists(ToAbsoluteProjectPath(DefaultSampleAssetPath));
            status.HasAssetMap = File.Exists(ToAbsoluteProjectPath(AssetMapAssetPath));
            string outputPath = GetCurrentAssetBundleOutputPath();
            status.HasOutputDirectory = Directory.Exists(ToAbsoluteProjectPath(outputPath));
            status.HasManifestBundle = File.Exists(ToAbsoluteProjectPath(Path.Combine(
                outputPath,
                GetPlatformFolderName(EditorUserBuildSettings.activeBuildTarget))));

            if (!status.HasDefaultRule)
            {
                status.MissingItems.Add("默认 AB 规则");
            }

            if (!status.HasSampleAsset)
            {
                status.MissingItems.Add("ResKit AB 示例资源");
            }

            if (!status.HasAssetMap)
            {
                status.MissingItems.Add("AssetMap");
            }

            if (!status.HasOutputDirectory)
            {
                status.MissingItems.Add("StreamingAssets/AssetBundles 输出目录");
            }

            if (!status.HasManifestBundle)
            {
                status.MissingItems.Add("当前平台 AssetBundle Manifest");
            }

            return status;
        }

        private bool TryInitializeWorkspace(out List<string> messages, out List<string> errors)
        {
            messages = new List<string>();
            errors = new List<string>();

            try
            {
                EnsureDefaultSampleAsset(messages);
                EnsureOutputDirectory(messages);
                EnsureDefaultRule(messages);
                SaveRules();
                ApplyRulesAndAnalyze(true);
                if (!BuildBundles(revealInFinder: false, showDialogOnFailure: false))
                {
                    errors.Add("默认 AssetBundle 构建失败，请检查 Console 中的 BuildPipeline 报错。");
                    return false;
                }

                LoadRules();

                AssetBundleWorkspaceStatus status = EvaluateWorkspaceStatus();
                if (!status.IsReady)
                {
                    errors.Add("初始化AB后仍有未满足项： " + string.Join(", ", status.MissingItems));
                    return false;
                }

                messages.Add("已生成或刷新 AssetMap。");
                messages.Add("已构建默认 AssetBundle 和当前平台 Manifest。");
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(exception.GetBaseException().Message);
                return false;
            }
        }

        private void EnsureDefaultSampleAsset(List<string> messages)
        {
            string absoluteAssetPath = ToAbsoluteProjectPath(DefaultSampleAssetPath);
            if (File.Exists(absoluteAssetPath))
            {
                EnsureDefaultSampleAssetMaterial(messages);
                messages.Add("已检测到默认 AB 示例资源 TestCapsule_AB.prefab。");
                return;
            }

            string absoluteFolderPath = ToAbsoluteProjectPath(DefaultSampleFolderPath);
            Directory.CreateDirectory(absoluteFolderPath);
            AssetDatabase.Refresh();

            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "TestCapsule_AB";
            ApplyDefaultSampleMaterial(capsule);
            PrefabUtility.SaveAsPrefabAsset(capsule, DefaultSampleAssetPath);
            UnityEngine.Object.DestroyImmediate(capsule);
            AssetDatabase.ImportAsset(DefaultSampleAssetPath, ImportAssetOptions.ForceUpdate);
            messages.Add("已补齐默认 AB 示例资源 TestCapsule_AB.prefab。");
        }

        private void EnsureDefaultSampleAssetMaterial(List<string> messages)
        {
            GameObject samplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSampleAssetPath);
            if (samplePrefab == null)
            {
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(samplePrefab) as GameObject;
            if (instance == null)
            {
                return;
            }

            try
            {
                if (ApplyDefaultSampleMaterial(instance))
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, DefaultSampleAssetPath);
                    messages.Add("已按当前渲染管线刷新默认 AB 示例材质。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private bool ApplyDefaultSampleMaterial(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return false;
            }

            Material material = LoadOrCreateDefaultSampleMaterial();
            if (material == null)
            {
                return false;
            }

            if (renderer.sharedMaterial == material)
            {
                return false;
            }

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(target);
            return true;
        }

        private static Material LoadOrCreateDefaultSampleMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(DefaultSampleMaterialPath);
            Shader shader = FindPreferredLitShader();
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return material;
            }

            if (material == null)
            {
                material = new Material(shader);
                material.name = "TestCapsule_AB_Auto";
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.89f, 0.30f, 0.28f));
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.89f, 0.30f, 0.28f));
            }

            if (AssetDatabase.GetAssetPath(material) == string.Empty)
            {
                AssetDatabase.CreateAsset(material, DefaultSampleMaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Shader FindPreferredLitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ??
                   Shader.Find("HDRP/Lit") ??
                   Shader.Find("Standard");
        }

        private void EnsureOutputDirectory(List<string> messages)
        {
            string outputDirectory = ToAbsoluteProjectPath(GetCurrentAssetBundleOutputPath());
            if (Directory.Exists(outputDirectory))
            {
                messages.Add("已检测到 StreamingAssets/AssetBundles 输出目录。");
                return;
            }

            Directory.CreateDirectory(outputDirectory);
            AssetDatabase.Refresh();
            messages.Add("已创建 StreamingAssets/AssetBundles 输出目录。");
        }

        private void EnsureDefaultRule(List<string> messages)
        {
            BundleRule existingRule = _rules.FirstOrDefault(rule =>
                rule != null &&
                string.Equals(rule.bundleName, DefaultBundleName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rule.path, DefaultSampleFolderPath, StringComparison.Ordinal));
            if (existingRule != null)
            {
                messages.Add("已检测到默认 AB 规则。");
                return;
            }

            _rules.Add(new BundleRule
            {
                bundleName = DefaultBundleName,
                path = DefaultSampleFolderPath,
                isFolder = true
            });
            _hasUnappliedChanges = true;
            messages.Add("已创建默认 AB 规则。");
        }

        private void ApplyRulesAndAnalyze(bool generateCode = true)
        {
            _isBuilding = true;
            try
            {
                EditorUtility.DisplayProgressBar("AssetBundle", "正在分析依赖...", 0.2f);
                var allAssetPaths = new Dictionary<string, string>();

                foreach (var rule in _rules)
                {
                    rule.includedAssets.Clear();
                    if (!File.Exists(rule.path) && !Directory.Exists(rule.path)) continue;

                    string[] guids;
                    if (rule.isFolder)
                    {
                        guids = AssetDatabase.FindAssets("", new[] { rule.path });
                    }
                    else
                    {
                        guids = new[] { AssetDatabase.AssetPathToGUID(rule.path) };
                    }

                    foreach (var guid in guids)
                    {
                        string p = AssetDatabase.GUIDToAssetPath(guid);
                        if (Directory.Exists(p) || p.EndsWith(".cs") || p.EndsWith(".js")) continue;

                        rule.includedAssets.Add(p);

                        if (!allAssetPaths.ContainsKey(p))
                        {
                            allAssetPaths.Add(p, rule.bundleName);
                        }
                        else
                        {
                            Debug.LogWarning($"[ABTool] 资源 {p} 被多个规则包含，将使用第一个规则: {allAssetPaths[p]}");
                        }
                    }
                }

                // Shader 不只是一个普通依赖：Player 构建会裁剪未显式保留的 Shader / Variant，
                // 即使 AB 文件存在，运行时材质也可能变成紫色。因此构建时同时做 Bundle 归集、
                // Player Shader 保留和实际材质 Variant 的预热集合生成。
                ShaderCollectionResult shaderResult = AutoGroupShaders(allAssetPaths);
                int retainedShaderCount = EnsureAlwaysIncludedShaders(shaderResult.Shaders);
                EnsureShaderVariantCollection(allAssetPaths, shaderResult.Materials);
                if (retainedShaderCount > 0)
                {
                    Debug.Log($"[ABTool] 已将 {retainedShaderCount} 个 AB Shader 加入 GraphicsSettings 的 Always Included Shaders。");
                }

                foreach (string warning in shaderResult.BundleAssignmentWarnings)
                {
                    Debug.LogWarning("[ABTool] " + warning);
                }

                EditorUtility.DisplayProgressBar("AssetBundle", "正在标记资源...", 0.5f);

                var oldNames = AssetDatabase.GetAllAssetBundleNames();
                foreach (var name in oldNames) AssetDatabase.RemoveAssetBundleName(name, true);

                foreach (var kvp in allAssetPaths)
                {
                    AssetImporter ai = AssetImporter.GetAtPath(kvp.Key);
                    if (ai != null) ai.assetBundleName = kvp.Value;
                }

                AssetDatabase.RemoveUnusedAssetBundleNames();

                if (generateCode)
                {
                    GenerateCode(allAssetPaths);
                }

                _hasUnappliedChanges = false;
                if (generateCode)
                {
                    Window?.ShowNotification(new GUIContent("规则应用成功！"));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ABTool] 应用规则失败: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isBuilding = false;
            }
        }

        private ShaderCollectionResult AutoGroupShaders(Dictionary<string, string> assetMap)
        {
            var result = new ShaderCollectionResult();
            var shadersToAdd = new HashSet<string>();

            foreach (var kvp in assetMap.ToArray())
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(kvp.Key);
                if (material != null)
                {
                    result.Materials.Add(material);
                    if (material.shader != null)
                    {
                        result.Shaders.Add(material.shader);
                    }
                }

                string[] deps = AssetDatabase.GetDependencies(kvp.Key, true);
                foreach (var depPath in deps)
                {
                    if (depPath.EndsWith(".cs")) continue;

                    Type type = AssetDatabase.GetMainAssetTypeAtPath(depPath);
                    if (type == typeof(Shader) || type == typeof(ShaderVariantCollection))
                    {
                        shadersToAdd.Add(depPath);
                        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(depPath);
                        if (shader != null)
                        {
                            result.Shaders.Add(shader);
                        }
                    }
                }
            }

            foreach (var shaderPath in shadersToAdd)
            {
                if (assetMap.ContainsKey(shaderPath))
                {
                    if (assetMap[shaderPath] != SHADER_BUNDLE_NAME)
                    {
                        assetMap[shaderPath] = SHADER_BUNDLE_NAME;
                    }
                }
                else
                {
                    assetMap.Add(shaderPath, SHADER_BUNDLE_NAME);
                }
            }

            foreach (Shader shader in result.Shaders)
            {
                string shaderPath = AssetDatabase.GetAssetPath(shader);
                if (!string.IsNullOrWhiteSpace(shaderPath) && !assetMap.ContainsKey(shaderPath))
                {
                    AssetImporter importer = AssetImporter.GetAtPath(shaderPath);
                    if (importer != null)
                    {
                        assetMap.Add(shaderPath, SHADER_BUNDLE_NAME);
                    }
                    else
                    {
                        result.BundleAssignmentWarnings.Add(
                            $"Shader 无法写入 AssetBundle 标签，将依赖 Always Included Shaders 保留：{shader.name}");
                    }
                }
            }

            return result;
        }

        private static int EnsureAlwaysIncludedShaders(IEnumerable<Shader> shaders)
        {
            Shader[] requiredShaders = shaders
                .Where(shader => shader != null)
                .Distinct()
                .ToArray();
            if (requiredShaders.Length == 0)
            {
                return 0;
            }

            UnityEngine.Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsAssetPath)
                .FirstOrDefault();
            if (graphicsSettings == null)
            {
                Debug.LogWarning("[ABTool] 未找到 GraphicsSettings.asset，无法保留 AB Shader。");
                return 0;
            }

            var serializedSettings = new SerializedObject(graphicsSettings);
            SerializedProperty alwaysIncludedShaders = serializedSettings.FindProperty("m_AlwaysIncludedShaders");
            if (alwaysIncludedShaders == null || !alwaysIncludedShaders.isArray)
            {
                Debug.LogWarning("[ABTool] GraphicsSettings 不包含 Always Included Shaders 配置，无法自动保留 AB Shader。");
                return 0;
            }

            var existingShaders = new HashSet<Shader>();
            for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
            {
                Shader existing = alwaysIncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (existing != null)
                {
                    existingShaders.Add(existing);
                }
            }

            int addedCount = 0;
            foreach (Shader shader in requiredShaders)
            {
                if (!existingShaders.Add(shader))
                {
                    continue;
                }

                int index = alwaysIncludedShaders.arraySize;
                alwaysIncludedShaders.InsertArrayElementAtIndex(index);
                alwaysIncludedShaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                addedCount++;
            }

            if (addedCount > 0)
            {
                serializedSettings.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            return addedCount;
        }

        private static void EnsureShaderVariantCollection(Dictionary<string, string> assetMap,
            IEnumerable<Material> materials)
        {
            Material[] shaderMaterials = materials.Where(material => material != null && material.shader != null)
                .Distinct()
                .ToArray();
            if (shaderMaterials.Length == 0)
            {
                return;
            }

            string directory = Path.GetDirectoryName(ShaderVariantCollectionAssetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(
                ShaderVariantCollectionAssetPath);
            if (collection == null)
            {
                collection = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(collection, ShaderVariantCollectionAssetPath);
            }
            else
            {
                collection.Clear();
            }

            int variantCount = 0;
            foreach (Material material in shaderMaterials)
            {
                try
                {
                    var variant = new ShaderVariantCollection.ShaderVariant(
                        material.shader, PassType.Normal, material.shaderKeywords);
                    if (collection.Add(variant))
                    {
                        variantCount++;
                    }
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning($"[ABTool] 无法记录 Shader Variant: {material.shader.name}, Error={exception.Message}");
                }
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            assetMap[ShaderVariantCollectionAssetPath] = SHADER_BUNDLE_NAME;
            Debug.Log($"[ABTool] ShaderVariantCollection 已生成，记录 {variantCount} 个材质 Variant。");
        }

        private bool BuildBundles(bool revealInFinder = true, bool showDialogOnFailure = true)
        {
            if (_hasUnappliedChanges)
            {
                Debug.Log("[ABTool] 检测到未应用的规则，正在自动应用...");
                ApplyRulesAndAnalyze(false);
            }

            var allNames = AssetDatabase.GetAllAssetBundleNames();
            if (allNames.Length == 0)
            {
                if (!showDialogOnFailure)
                {
                    Debug.LogError("[ABTool] 当前没有任何资源被标记，无法构建 AssetBundle。");
                    return false;
                }

                bool autoApply = EditorUtility.DisplayDialog("提示", "当前没有任何资源被标记，是否重新扫描规则？", "扫描并构建", "取消");
                if (autoApply) ApplyRulesAndAnalyze(false);
                else return false;
            }

            string rootPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
            string platformFolder = GetPlatformFolderName(EditorUserBuildSettings.activeBuildTarget);
            string outPath = Path.Combine(rootPath, platformFolder);

            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);

            Debug.Log($"[ABTool] 开始构建平台: {platformFolder} ...");

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outPath,
                BuildAssetBundleOptions.ChunkBasedCompression,
                EditorUserBuildSettings.activeBuildTarget
            );

            if (manifest != null)
            {
                CleanStaleFiles(outPath, manifest, platformFolder);

                Debug.Log($"[ABTool] 构建成功！路径: {outPath}");
                if (revealInFinder)
                {
                    EditorUtility.RevealInFinder(outPath);
                }

                Window?.ShowNotification(new GUIContent("构建成功！"));
                return true;
            }

            Debug.LogError("[ABTool] 构建失败！请检查 Console 报错。");
            if (showDialogOnFailure)
            {
                EditorUtility.DisplayDialog("构建失败", "BuildPipeline 返回 null。\n请检查控制台是否有 Shader 编译错误或资源引用丢失。", "确定");
            }

            return false;
        }

        private void CleanStaleFiles(string outPath, AssetBundleManifest manifest, string platformName)
        {
            var validBundles = new HashSet<string>(manifest.GetAllAssetBundles());
            validBundles.Add(platformName);

            var allFiles = Directory.GetFiles(outPath);
            int deletedCount = 0;

            foreach (var filePath in allFiles)
            {
                string fileName = Path.GetFileName(filePath);

                if (fileName.EndsWith(".meta")) continue;

                string bundleNameToCheck = fileName;
                if (fileName.EndsWith(".manifest"))
                {
                    bundleNameToCheck = fileName.Substring(0, fileName.Length - 9);
                }

                if (!validBundles.Contains(bundleNameToCheck))
                {
                    try
                    {
                        File.Delete(filePath);
                        string metaPath = filePath + ".meta";
                        if (File.Exists(metaPath)) File.Delete(metaPath);

                        Debug.Log($"[ABTool] 清理陈旧文件: {fileName}");
                        deletedCount++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ABTool] 无法删除陈旧文件 {fileName}: {e.Message}");
                    }
                }
            }

            if (deletedCount > 0)
            {
                AssetDatabase.Refresh();
            }
        }

        private void ForceRebuild()
        {
            ClearAssetBundles();
            ApplyRulesAndAnalyze(false);
            BuildBundles();
        }

        private void ClearAssetBundles()
        {
            string rootPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
            string platformFolder = GetPlatformFolderName(EditorUserBuildSettings.activeBuildTarget);
            string outPath = Path.Combine(rootPath, platformFolder);

            if (Directory.Exists(outPath))
            {
                try
                {
                    Directory.Delete(outPath, true);
                    string metaPath = outPath + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                    AssetDatabase.Refresh();
                    Debug.Log($"[ABTool] 已清理: {outPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ABTool] 清理失败: {e.Message}");
                }
            }
        }

        private void GenerateCode(Dictionary<string, string> assetMap)
        {
            string code = "using System.Collections.Generic;\n";
            code += "namespace StellarFramework.Res.AB {\n";
            code += "public static class AssetMap {\n";
            code += "    public static Dictionary<string, string> GetMap() {\n";
            code += "        return new Dictionary<string, string> {\n";

            foreach (var kvp in assetMap)
            {
                if (kvp.Value == SHADER_BUNDLE_NAME) continue;
                code += $"            {{ \"{kvp.Key}\", \"{kvp.Value}\" }},\n";
            }

            code += "        };\n    }\n";

            code += "    public static class Bundles {\n";
            var bundles = assetMap.Values.Distinct().OrderBy(x => x);
            foreach (var b in bundles)
            {
                string fieldName = b.ToUpper().Replace("/", "_").Replace(".", "_");
                code += $"        public const string {fieldName} = \"{b}\";\n";
            }

            code += "    }\n";
            code += "}}";

            string path = Path.Combine(Application.dataPath, "StellarFramework/Generated/AssetMap/AssetMap.cs");
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, code);
            AssetDatabase.Refresh();
            Debug.Log($"[ABTool] 代码已生成: {path}");
        }

        private string GetPlatformFolderName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android: return "Android";
                case BuildTarget.iOS: return "iOS";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: return "Windows";
                case BuildTarget.StandaloneOSX: return "OSX";
                case BuildTarget.WebGL: return "WebGL";
                default: return "Unknown";
            }
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string normalizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalizedPath);
        }

        private string GetCurrentAssetBundleOutputPath()
        {
            return $"Assets/StreamingAssets/AssetBundles/{GetPlatformFolderName(EditorUserBuildSettings.activeBuildTarget)}";
        }

        private void SaveRules()
        {
            string json = JsonUtility.ToJson(new SerializationWrapper { rules = _rules });
            EditorPrefs.SetString(PREFS_KEY, json);
        }

        private void LoadRules()
        {
            if (EditorPrefs.HasKey(PREFS_KEY))
            {
                string json = EditorPrefs.GetString(PREFS_KEY);
                var wrapper = JsonUtility.FromJson<SerializationWrapper>(json);
                if (wrapper != null && wrapper.rules != null)
                {
                    _rules = wrapper.rules;
                }
            }
        }

        [Serializable]
        private class SerializationWrapper
        {
            public List<BundleRule> rules;
        }
    }
}
