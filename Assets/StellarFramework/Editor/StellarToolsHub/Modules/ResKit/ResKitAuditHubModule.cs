using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using StellarFramework.Res;

#if UNITY_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ResKit 运行时资源审计工具
    /// 职责：在 Editor 环境下通过反射读取 ResMgr 的内部缓存，提供实时的资源驻留与泄漏排查视图。
    /// </summary>
    [StellarTool("ResKit 资源审计", "框架核心", 1)]
    public class ResKitAuditHubModule : ToolModule
    {
        public override string Icon => "d_SettingsIcon";
        public override string Description => "实时监控 ResKit 内存中的资源驻留状态、引用计数与具体持有者，用于排查内存泄漏。";

        private bool _autoRefresh = true;
        private Vector2 _scrollPosition;
        private object _sharedCacheInstance;
        private FieldInfo _sharedCacheField;
        private MethodInfo _gcMethod;

        // 缓存反射获取的数据，避免每帧高频反射产生严重卡顿
        private readonly List<ResDataSnapshot> _snapshotList = new List<ResDataSnapshot>();
        private double _lastRefreshTime;
        private const double RefreshInterval = 1.0; // 自动刷新间隔（秒）

        private class ResDataSnapshot
        {
            public string Key;
            public string Path;
            public string LoaderName;
            public int RefCount;
            public List<string> Owners = new List<string>();
            public bool IsExpanded;
        }

        public override void OnEnable()
        {
            InitializeReflection();
            RefreshSnapshot();
        }

        private void InitializeReflection()
        {
            Type resMgrType = typeof(ResKit).Assembly.GetType("StellarFramework.Res.ResMgr");
            if (resMgrType == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 初始化失败: 无法通过反射获取 StellarFramework.Res.ResMgr 类型，请检查命名空间或类名是否变更。");
                return;
            }

            _sharedCacheField = resMgrType.GetField("_sharedCache", BindingFlags.NonPublic | BindingFlags.Static);
            if (_sharedCacheField == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 初始化失败: 无法获取 _sharedCache 字段。");
                return;
            }

            _gcMethod = resMgrType.GetMethod("GarbageCollect", BindingFlags.Public | BindingFlags.Static);
        }

        public override void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("资源审计功能仅在游戏运行 (Play Mode) 时提供实时数据。", MessageType.Info);
                return;
            }

            if (_sharedCacheField == null)
            {
                EditorGUILayout.HelpBox("反射初始化失败，无法读取资源缓存。", MessageType.Error);
                return;
            }

            DrawToolbar();
            HandleAutoRefresh();
            DrawResourceList();
        }

        private void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("手动刷新", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RefreshSnapshot();
                }

                _autoRefresh = GUILayout.Toggle(_autoRefresh, "自动刷新 (1s)", EditorStyles.toolbarButton,
                    GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                GUILayout.Label($"驻留总数: {_snapshotList.Count}", EditorStyles.miniLabel);

                if (GUILayout.Button("强制 GC 与卸载", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    ExecuteGarbageCollect();
                }
            }
        }

        private void HandleAutoRefresh()
        {
            if (!_autoRefresh) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshSnapshot();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Window.Repaint();
            }
        }

        private void RefreshSnapshot()
        {
            if (_sharedCacheField == null) return;

            _sharedCacheInstance = _sharedCacheField.GetValue(null);
            if (_sharedCacheInstance is IDictionary dict)
            {
                // 记录旧的展开状态
                Dictionary<string, bool> expandStates = new Dictionary<string, bool>();
                foreach (var snap in _snapshotList)
                {
                    expandStates[snap.Key] = snap.IsExpanded;
                }

                _snapshotList.Clear();

                foreach (DictionaryEntry kvp in dict)
                {
                    ResData resData = kvp.Value as ResData;
                    if (resData == null) continue;

                    var snapshot = new ResDataSnapshot
                    {
                        Key = kvp.Key.ToString(),
                        Path = resData.Path,
                        LoaderName = resData.LoaderName,
                        RefCount = resData.RefCount
                    };

                    if (expandStates.TryGetValue(snapshot.Key, out bool isExp))
                    {
                        snapshot.IsExpanded = isExp;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (resData.Owners != null)
                    {
                        foreach (var owner in resData.Owners)
                        {
                            snapshot.Owners.Add(owner);
                        }
                    }
#endif
                    _snapshotList.Add(snapshot);
                }

                // 按照引用计数降序，再按路径升序排序
                _snapshotList.Sort((a, b) =>
                {
                    int refCompare = b.RefCount.CompareTo(a.RefCount);
                    return refCompare != 0 ? refCompare : string.Compare(a.Path, b.Path, StringComparison.Ordinal);
                });
            }
        }

        private void DrawResourceList()
        {
            if (_snapshotList.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("当前无驻留资源", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var snap in _snapshotList)
            {
                using (new GUILayout.VerticalScope("box"))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        snap.IsExpanded = EditorGUILayout.Foldout(snap.IsExpanded, snap.Path, true,
                            EditorStyles.foldoutHeader);

                        GUILayout.FlexibleSpace();

                        GUILayout.Label($"[{snap.LoaderName}]", EditorStyles.miniLabel, GUILayout.Width(100));

                        // 引用计数颜色高亮
                        Color defaultColor = GUI.contentColor;
                        if (snap.RefCount == 0) GUI.contentColor = Color.red;
                        else if (snap.RefCount > 5) GUI.contentColor = Color.cyan;

                        GUILayout.Label($"Ref: {snap.RefCount}", EditorStyles.boldLabel, GUILayout.Width(60));

                        GUI.contentColor = defaultColor;
                    }

                    if (snap.IsExpanded)
                    {
                        EditorGUI.indentLevel++;
                        if (snap.Owners.Count == 0)
                        {
                            EditorGUILayout.LabelField("无明确持有者 (可能存在泄漏或处于对象池游离态)", Window.DangerButtonStyle);
                        }
                        else
                        {
                            foreach (var owner in snap.Owners)
                            {
                                EditorGUILayout.LabelField($"-> {owner}", EditorStyles.miniLabel);
                            }
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ExecuteGarbageCollect()
        {
            if (_gcMethod != null)
            {
                _gcMethod.Invoke(null, null);
                RefreshSnapshot();
                Window.ShowNotification(new GUIContent("已触发强制 GC 与资源卸载"));
            }
            else
            {
                Debug.LogError("[ResKitAuditHubModule] 无法调用 GarbageCollect 方法。");
            }
        }
    }

    [StellarTool("资源配置 (Addressables)", "框架核心", 11)]
    public class AddressablesToolModule : ToolModule
    {
        private const string DefaultGroupName = "ResKit Remote";
        private const string DefaultLabels = "hotupdate";

        private UnityEngine.Object _rootAsset;
        private string _groupName = DefaultGroupName;
        private string _labels = DefaultLabels;
        private string _hybridClrDllSource = "HybridCLRData/HotUpdateDlls/StandaloneWindows64";
        private string _hybridClrOutputFolder = "Assets/StellarFramework/HotUpdateAssets/Code";
        private string _hybridClrLabels = "hotupdate,code";
        private bool _includeSelection = true;
        private Vector2 _scrollPosition;
        private readonly List<string> _lastReport = new List<string>();

        public override string Icon => "d_PreMatCube";
        public override string Description => "检查 Addressables 远端配置，将 address 批量设置为 Assets/... 路径，维护 labels，并跳转官方 Addressables 构建界面。";

        public override void OnGUI()
        {
#if UNITY_ADDRESSABLES
            DrawSettings();
            DrawActions();
            DrawReport();
#else
            EditorGUILayout.HelpBox("Addressables package is not available. Install com.unity.addressables to enable the AA helper tools.", MessageType.Warning);
#endif
        }

#if UNITY_ADDRESSABLES
        private void DrawSettings()
        {
            Section("Addressables 配置");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _rootAsset = EditorGUILayout.ObjectField("资源根目录/文件", _rootAsset, typeof(UnityEngine.Object), false);
                _includeSelection = EditorGUILayout.ToggleLeft("同时处理当前 Project 选中资源", _includeSelection);
                _groupName = EditorGUILayout.TextField("目标 Group", _groupName);
                _labels = EditorGUILayout.TextField("Labels (, 分隔)", _labels);
                EditorGUILayout.HelpBox("运行时会使用 Assets/... 路径作为 key，因此这里会把 Addressables address 同步成资产路径。", MessageType.Info);
                EditorGUILayout.HelpBox("AA/YooAsset 等插件已有官方构建界面。ToolHub 只做检查、配置和地址/标签辅助，不替代插件构建器。", MessageType.Info);
            }

            Section("HybridCLR 代码热更产物");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _hybridClrDllSource = EditorGUILayout.TextField("DLL 源目录/文件", _hybridClrDllSource);
                _hybridClrOutputFolder = EditorGUILayout.TextField("输出资源目录", _hybridClrOutputFolder);
                _hybridClrLabels = EditorGUILayout.TextField("代码 Labels (, 分隔)", _hybridClrLabels);
                EditorGUILayout.HelpBox("会复制 .dll 为 .dll.bytes，计算 SHA256，并把输出资源加入 Addressables。", MessageType.Info);
            }
        }

        private void DrawActions()
        {
            Section("操作");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("检查 Settings/Profile", GUILayout.Height(28)))
                {
                    ValidateSettings();
                }

                if (GUILayout.Button("创建/定位 Runtime Settings", GUILayout.Height(28)))
                {
                    CreateOrSelectRuntimeSettings();
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用 Address/Labels", GUILayout.Height(28)))
                {
                    ApplyAddressAndLabels();
                }

                if (GUILayout.Button("使用配置 Labels", GUILayout.Height(28)))
                {
                    UseLabelsFromRuntimeSettings();
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (PrimaryButton("打开 Addressables Groups", GUILayout.Height(30)))
                {
                    OpenAddressablesGroups();
                }

                if (GUILayout.Button("打开 Addressables Analyze", GUILayout.Height(30)))
                {
                    OpenAddressablesAnalyze();
                }
            }

            if (GUILayout.Button("处理 HybridCLR dll.bytes 并同步 AA", GUILayout.Height(30)))
            {
                ProcessHybridCLROutput();
            }
        }

        private void DrawReport()
        {
            Section("结果");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_lastReport.Count == 0)
                {
                    GUILayout.Label("暂无操作结果", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MinHeight(120));
                foreach (var line in _lastReport)
                {
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void ValidateSettings()
        {
            _lastReport.Clear();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                ReportError("Addressables Settings 不存在，请先通过 Window/Asset Management/Addressables 创建。");
                return;
            }

            Report($"Settings: {AssetDatabase.GetAssetPath(settings)}");
            Report($"Active Profile: {settings.activeProfileId}");
            Report(settings.BuildRemoteCatalog
                ? "Remote Catalog: enabled"
                : "Remote Catalog: disabled (远端热更通常需要开启)");

            string remoteBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath);
            string remoteLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath);

            Report(string.IsNullOrWhiteSpace(remoteBuildPath)
                ? "Remote Build Path: missing"
                : $"Remote Build Path: {remoteBuildPath}");
            Report(string.IsNullOrWhiteSpace(remoteLoadPath)
                ? "Remote Load Path: missing"
                : $"Remote Load Path: {remoteLoadPath}");

            AddressableAssetGroup group = GetOrCreateGroup(settings, false);
            if (group == null)
            {
                ReportError($"Group: {_groupName} missing");
            }
            else
            {
                Report($"Group: {group.Name}");
                ValidateGroup(settings, group);
            }

            ResKitRuntimeSettings runtimeSettings = ResKitRuntimeSettings.LoadOrCreateDefault();
            ResKitRuntimeSettingsValidationReport validation = runtimeSettings.Validate(true);
            foreach (string warning in validation.Warnings)
            {
                Report("WARNING: " + warning);
            }

            foreach (string error in validation.Errors)
            {
                ReportError(error);
            }
        }

        private void ApplyAddressAndLabels()
        {
            _lastReport.Clear();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                ReportError("Addressables Settings 不存在，无法应用。");
                return;
            }

            AddressableAssetGroup group = GetOrCreateGroup(settings, true);
            if (group == null)
            {
                ReportError("无法创建或获取 Addressables Group。");
                return;
            }

            ConfigureGroupForRemote(settings, group);

            List<string> labels = ParseLabels(_labels);
            foreach (var label in labels)
            {
                if (!settings.GetLabels().Contains(label))
                {
                    settings.AddLabel(label);
                }
            }

            List<string> paths = CollectAssetPaths();
            if (paths.Count == 0)
            {
                ReportError("没有找到可处理资源。请设置根目录/文件，或在 Project 中选择资源。");
                return;
            }

            int changedCount = 0;
            foreach (var path in paths)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry == null)
                {
                    continue;
                }

                entry.address = path;
                foreach (var label in labels)
                {
                    entry.SetLabel(label, true, false, false);
                }

                changedCount++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, group, true);
            AssetDatabase.SaveAssets();
            Report($"已处理资源: {changedCount}");
            Report("Address 已同步为 Assets/... 路径。");
            Window?.ShowNotification(new GUIContent("Addressables 规则已应用"));
        }

        private void OpenAddressablesGroups()
        {
            OpenAddressablesMenu("Window/Asset Management/Addressables/Groups");
        }

        private void OpenAddressablesAnalyze()
        {
            OpenAddressablesMenu("Window/Asset Management/Addressables/Analyze");
        }

        private void OpenAddressablesMenu(string menuPath)
        {
            _lastReport.Clear();
            if (EditorApplication.ExecuteMenuItem(menuPath))
            {
                Report($"已打开官方菜单: {menuPath}");
                Report("普通构建、清理构建缓存和 Content Update 请继续使用 Addressables 官方窗口执行。");
                return;
            }

            ReportError($"无法打开菜单: {menuPath}");
        }

        private void CreateOrSelectRuntimeSettings()
        {
            _lastReport.Clear();
            const string resourcesFolder = "Assets/StellarFramework/Resources";
            const string assetPath = resourcesFolder + "/ResKitRuntimeSettings.asset";

            ResKitRuntimeSettings settings = AssetDatabase.LoadAssetAtPath<ResKitRuntimeSettings>(assetPath);
            if (settings == null)
            {
                if (!Directory.Exists(resourcesFolder))
                {
                    Directory.CreateDirectory(resourcesFolder);
                }

                settings = ScriptableObject.CreateInstance<ResKitRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
                AssetDatabase.SaveAssets();
                Report($"已创建 Runtime Settings: {assetPath}");
            }
            else
            {
                Report($"已定位 Runtime Settings: {assetPath}");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private void ProcessHybridCLROutput()
        {
            _lastReport.Clear();
            try
            {
                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    ReportError("Addressables Settings 不存在，无法同步 HybridCLR 产物。");
                    return;
                }

                string source = NormalizeFileSystemPath(_hybridClrDllSource);
                if (string.IsNullOrWhiteSpace(source) || (!File.Exists(source) && !Directory.Exists(source)))
                {
                    ReportError($"DLL 源目录/文件不存在: {_hybridClrDllSource}");
                    return;
                }

                string outputFolder = NormalizeAssetFolder(_hybridClrOutputFolder);
                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    ReportError("输出资源目录必须在 Assets 下。");
                    return;
                }

                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                List<string> dllFiles = CollectDllFiles(source);
                if (dllFiles.Count == 0)
                {
                    ReportError("没有找到 .dll 文件。");
                    return;
                }

                AddressableAssetGroup group = GetOrCreateGroup(settings, true);
                ConfigureGroupForRemote(settings, group);

                List<string> labels = ParseLabels(string.IsNullOrWhiteSpace(_hybridClrLabels)
                    ? _labels
                    : _hybridClrLabels);
                foreach (string label in labels)
                {
                    if (!settings.GetLabels().Contains(label))
                    {
                        settings.AddLabel(label);
                    }
                }

                string firstHotUpdateKey = null;
                string firstHotUpdateSha256 = null;
                int processedCount = 0;

                foreach (string dllFile in dllFiles)
                {
                    string fileName = Path.GetFileName(dllFile);
                    string assetPath = outputFolder.TrimEnd('/', '\\') + "/" + fileName + ".bytes";
                    File.Copy(dllFile, assetPath, true);

                    string sha256 = ComputeFileSha256(assetPath);
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrEmpty(guid))
                    {
                        AssetDatabase.ImportAsset(assetPath);
                        guid = AssetDatabase.AssetPathToGUID(assetPath);
                    }

                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    if (entry != null)
                    {
                        entry.address = assetPath;
                        foreach (string label in labels)
                        {
                            entry.SetLabel(label, true, false, false);
                        }
                    }

                    if (firstHotUpdateKey == null ||
                        string.Equals(fileName, "HotUpdate.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        firstHotUpdateKey = assetPath;
                        firstHotUpdateSha256 = sha256;
                    }

                    processedCount++;
                    Report($"{fileName} -> {assetPath}, SHA256={sha256}");
                }

                UpdateRuntimeSettingsHotUpdateInfo(firstHotUpdateKey, firstHotUpdateSha256);

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, group, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Report($"HybridCLR 产物处理完成: {processedCount}");
                Window?.ShowNotification(new GUIContent("HybridCLR dll.bytes 已处理"));
            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
            }
        }

        private void UseLabelsFromRuntimeSettings()
        {
            ResKitRuntimeSettings settings = ResKitRuntimeSettings.LoadOrCreateDefault();
            List<string> labels = ResKitRuntimeSettings.ToDistinctStringList(settings.AddressablesDefaultHotUpdateLabels);
            _labels = labels.Count > 0 ? string.Join(",", labels) : DefaultLabels;
            _lastReport.Clear();
            Report($"Labels 已同步为: {_labels}");
        }

        private AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, bool create)
        {
            string groupName = string.IsNullOrWhiteSpace(_groupName) ? DefaultGroupName : _groupName.Trim();
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null || !create)
            {
                return group;
            }

            group = settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
            ConfigureGroupForRemote(settings, group);
            return group;
        }

        private void ConfigureGroupForRemote(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            if (settings == null || group == null)
            {
                return;
            }

            BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledSchema == null)
            {
                bundledSchema = group.AddSchema<BundledAssetGroupSchema>();
            }

            bundledSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            bundledSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            bundledSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;

            ContentUpdateGroupSchema contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdateSchema == null)
            {
                contentUpdateSchema = group.AddSchema<ContentUpdateGroupSchema>();
            }

            contentUpdateSchema.StaticContent = false;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(bundledSchema);
            EditorUtility.SetDirty(contentUpdateSchema);
        }

        private void ValidateGroup(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledSchema == null)
            {
                ReportError("Group 缺少 BundledAssetGroupSchema。");
            }
            else
            {
                string buildPathName = bundledSchema.BuildPath.GetName(settings);
                string loadPathName = bundledSchema.LoadPath.GetName(settings);
                Report($"Group Build Path Variable: {buildPathName}");
                Report($"Group Load Path Variable: {loadPathName}");

                if (!string.Equals(buildPathName, AddressableAssetSettings.kRemoteBuildPath, StringComparison.Ordinal))
                {
                    ReportError("Group Build Path 未指向 RemoteBuildPath。");
                }

                if (!string.Equals(loadPathName, AddressableAssetSettings.kRemoteLoadPath, StringComparison.Ordinal))
                {
                    ReportError("Group Load Path 未指向 RemoteLoadPath。");
                }
            }

            ContentUpdateGroupSchema contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdateSchema == null)
            {
                ReportError("Group 缺少 ContentUpdateGroupSchema。");
            }
            else
            {
                Report(contentUpdateSchema.StaticContent
                    ? "Content Update: Prevent Updates enabled"
                    : "Content Update: dynamic updates allowed");
            }

            List<string> requiredLabels = ParseLabels(_labels);
            int totalEntries = 0;
            int invalidAddressCount = 0;
            int missingLabelCount = 0;

            foreach (AddressableAssetEntry entry in group.entries)
            {
                totalEntries++;
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.address) ||
                    !entry.address.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    invalidAddressCount++;
                }

                for (int i = 0; i < requiredLabels.Count; i++)
                {
                    if (!entry.labels.Contains(requiredLabels[i]))
                    {
                        missingLabelCount++;
                        break;
                    }
                }
            }

            Report($"Entries: {totalEntries}, Invalid Address: {invalidAddressCount}, Missing Labels: {missingLabelCount}");
            if (invalidAddressCount > 0)
            {
                ReportError("存在 Address 非 Assets/... 的 entry，请执行“应用 Address/Labels”。");
            }

            if (missingLabelCount > 0)
            {
                ReportError("存在缺少目标 label 的 entry，请执行“应用 Address/Labels”。");
            }
        }

        private List<string> CollectAssetPaths()
        {
            HashSet<string> paths = new HashSet<string>();

            if (_rootAsset != null)
            {
                AddAssetPath(AssetDatabase.GetAssetPath(_rootAsset), paths);
            }

            if (_includeSelection)
            {
                foreach (var selected in Selection.objects)
                {
                    AddAssetPath(AssetDatabase.GetAssetPath(selected), paths);
                }
            }

            return paths.OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private static void AddAssetPath(string path, HashSet<string> paths)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            if (Directory.Exists(path))
            {
                foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { path }))
                {
                    AddAssetPath(AssetDatabase.GUIDToAssetPath(guid), paths);
                }

                return;
            }

            if (!File.Exists(path))
            {
                return;
            }

            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            paths.Add(path);
        }

        private static List<string> CollectDllFiles(string source)
        {
            if (File.Exists(source))
            {
                return string.Equals(Path.GetExtension(source), ".dll", StringComparison.OrdinalIgnoreCase)
                    ? new List<string> { source }
                    : new List<string>();
            }

            return Directory.GetFiles(source, "*.dll", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeFileSystemPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string trimmed = path.Trim().Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(trimmed))
            {
                return trimmed;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot) ? trimmed : Path.Combine(projectRoot, trimmed);
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return string.Empty;
            }

            string normalized = folder.Trim().Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                string.Equals(normalized, "Assets", StringComparison.Ordinal))
            {
                return normalized;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string full = Path.GetFullPath(normalized).Replace('\\', '/');
                if (full.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return full.Substring(projectRoot.Length + 1);
                }
            }

            return string.Empty;
        }

        private static string ComputeFileSha256(string assetPath)
        {
            using (FileStream stream = File.OpenRead(assetPath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private void UpdateRuntimeSettingsHotUpdateInfo(string dllKey, string sha256)
        {
            if (string.IsNullOrWhiteSpace(dllKey))
            {
                return;
            }

            const string resourcesFolder = "Assets/StellarFramework/Resources";
            const string assetPath = resourcesFolder + "/ResKitRuntimeSettings.asset";
            if (!Directory.Exists(resourcesFolder))
            {
                Directory.CreateDirectory(resourcesFolder);
            }

            ResKitRuntimeSettings runtimeSettings = AssetDatabase.LoadAssetAtPath<ResKitRuntimeSettings>(assetPath);
            if (runtimeSettings == null)
            {
                runtimeSettings = ScriptableObject.CreateInstance<ResKitRuntimeSettings>();
                AssetDatabase.CreateAsset(runtimeSettings, assetPath);
            }

            SerializedObject serializedObject = new SerializedObject(runtimeSettings);
            SerializedProperty keyProperty = serializedObject.FindProperty("hotUpdateAssemblyKey");
            SerializedProperty shaProperty = serializedObject.FindProperty("hotUpdateAssemblySha256");
            if (keyProperty != null)
            {
                keyProperty.stringValue = dllKey;
            }

            if (shaProperty != null)
            {
                shaProperty.stringValue = sha256 ?? string.Empty;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtimeSettings);
            Report($"Runtime Settings 已同步: HotUpdateAssemblyKey={dllKey}");
        }

        private static List<string> ParseLabels(string labelsText)
        {
            if (string.IsNullOrWhiteSpace(labelsText))
            {
                return new List<string>();
            }

            return labelsText
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(label => label.Trim())
                .Where(label => !string.IsNullOrEmpty(label))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private void Report(string message)
        {
            _lastReport.Add(message);
            Debug.Log($"[AddressablesTool] {message}");
        }

        private void ReportError(string message)
        {
            _lastReport.Add("ERROR: " + message);
            Debug.LogError($"[AddressablesTool] {message}");
        }
#endif
    }
}
