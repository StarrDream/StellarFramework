using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

    [StellarTool("资源打包 (AssetBundle)", "框架核心", 10)]
    public class AssetBundleToolModule : ToolModule
    {
        private List<BundleRule> _rules = new List<BundleRule>();
        private BundleRule _selectedRule;
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private bool _hasUnappliedChanges = false;
        private bool _isBuilding = false;

        private const string PREFS_KEY = "Stellar_AB_Rules";
        private const string SHADER_BUNDLE_NAME = "shaders"; // 全局 Shader 包名

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

                // 自动归集 Shader
                AutoGroupShaders(allAssetPaths);

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
                    Window.ShowNotification(new GUIContent("规则应用成功！"));
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

        private void AutoGroupShaders(Dictionary<string, string> assetMap)
        {
            var shadersToAdd = new HashSet<string>();

            foreach (var kvp in assetMap)
            {
                string[] deps = AssetDatabase.GetDependencies(kvp.Key, true);
                foreach (var depPath in deps)
                {
                    if (depPath.EndsWith(".cs")) continue;

                    Type type = AssetDatabase.GetMainAssetTypeAtPath(depPath);
                    if (type == typeof(Shader) || type == typeof(ShaderVariantCollection))
                    {
                        shadersToAdd.Add(depPath);
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
        }

        private void BuildBundles()
        {
            if (_hasUnappliedChanges)
            {
                Debug.Log("[ABTool] 检测到未应用的规则，正在自动应用...");
                ApplyRulesAndAnalyze(false);
            }

            var allNames = AssetDatabase.GetAllAssetBundleNames();
            if (allNames.Length == 0)
            {
                bool autoApply = EditorUtility.DisplayDialog("提示", "当前没有任何资源被标记，是否重新扫描规则？", "扫描并构建", "取消");
                if (autoApply) ApplyRulesAndAnalyze(false);
                else return;
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
                EditorUtility.RevealInFinder(outPath);
                Window.ShowNotification(new GUIContent("构建成功！"));
            }
            else
            {
                Debug.LogError("[ABTool] 构建失败！请检查 Console 报错。");
                EditorUtility.DisplayDialog("构建失败", "BuildPipeline 返回 null。\n请检查控制台是否有 Shader 编译错误或资源引用丢失。", "确定");
            }
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

            string path = Path.Combine(Application.dataPath, "StellarFramework/Generated/AssetMap.cs");
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