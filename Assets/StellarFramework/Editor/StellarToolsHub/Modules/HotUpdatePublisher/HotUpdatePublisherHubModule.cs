using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ToolsHub surface for the staged HotUpdate Publisher workflow. Execution buttons remain gated
    /// until their owning pipeline phases are available, so the UI never reports a placeholder as success.
    /// </summary>
    [StellarTool("HotUpdate Publisher", "热更新", 0,
        RequiredAssemblyNames = new[] { "StellarFramework.ToolsHub.HotUpdatePublisher.Editor" })]
    public sealed class HotUpdatePublisherHubModule : ToolModule
    {
        private enum SectionTab
        {
            Overview,
            Changes,
            Build,
            Server,
            History,
            Advanced
        }

        private const string PrefsPrefix = "StellarFramework.HotUpdatePublisher.";
        private const string ProfilesPrefsSuffix = ".environmentProfiles";
        private const string CreateCollectorMenuPath = "Tools/StellarFramework/HotUpdate Publisher/Configure Recommended YooAsset Collector";
        private const string CreateAndroidBaseReleaseMenuPath = "Tools/StellarFramework/HotUpdate Publisher/Create Android Base Release";
        private static readonly string[] TabNames = { "Overview", "Changes", "Build", "Server", "History", "Advanced" };

        private SectionTab _selectedTab;
        private string _baseAppVersion = "1.0.0";
        private string _packageName = "HotUpdatePublisherConsumerE2E";
        private string _packageVersion = "1.0.1";
        private string _releaseNotes = "";
        private string _scanError = "";
        private HotUpdateChangeClassificationResult _classification;
        private HotUpdateGitSnapshot _gitSnapshot;
        private HotUpdateEnvironmentKind _selectedEnvironment;
        private readonly List<HotUpdateEnvironmentProfile> _environmentProfiles = new List<HotUpdateEnvironmentProfile>();
        private string _profileLoadDiagnostic = string.Empty;
        private Vector2 _changesScroll;
        private Vector2 _historyScroll;
        private IReadOnlyList<HotUpdateReleaseRecord> _historyRecords = Array.Empty<HotUpdateReleaseRecord>();
        private string _historyDiagnostic = string.Empty;

        public override string Description => "查看热更变更风险、目标环境和发布产物状态。";

        public override void OnEnable()
        {
            string suffix = GetProjectPrefsSuffix();
            _baseAppVersion = EditorPrefs.GetString(PrefsPrefix + suffix + ".baseAppVersion", _baseAppVersion);
            _packageName = EditorPrefs.GetString(PrefsPrefix + suffix + ".packageName", _packageName);
            if (string.IsNullOrWhiteSpace(_packageName))
                _packageName = "HotUpdatePublisherConsumerE2E";
            _packageVersion = EditorPrefs.GetString(PrefsPrefix + suffix + ".packageVersion", _packageVersion);
            _releaseNotes = EditorPrefs.GetString(PrefsPrefix + suffix + ".releaseNotes", _releaseNotes);
            LoadEnvironmentProfiles(PrefsPrefix + suffix + ProfilesPrefsSuffix);
            RefreshHistory();
        }

        public override void OnDisable()
        {
            SaveLocalInputs();
        }

        public override void OnGUI()
        {
            DrawHeader();
            _selectedTab = (SectionTab)GUILayout.Toolbar((int)_selectedTab, TabNames, GUILayout.Height(28));
            GUILayout.Space(8);

            switch (_selectedTab)
            {
                case SectionTab.Overview: DrawOverview(); break;
                case SectionTab.Changes: DrawChanges(); break;
                case SectionTab.Build: DrawBuild(); break;
                case SectionTab.Server: DrawServer(); break;
                case SectionTab.History: DrawHistory(); break;
                case SectionTab.Advanced: DrawAdvanced(); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("HotUpdate Publisher", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "Editor-only 发布工作台。正式发布必须经过完整构建、产物校验、Release Gate 和远端校验。",
                MessageType.Info);
        }

        private void DrawOverview()
        {
            Section("Release Inputs");
            DrawReadOnlyRow("Platform", EditorUserBuildSettings.activeBuildTarget.ToString());
            DrawReadOnlyRow("Environment", _selectedEnvironment.ToString());
            _baseAppVersion = EditorGUILayout.TextField("Base App", _baseAppVersion);
            _packageName = EditorGUILayout.TextField("YooAsset Package", _packageName);
            _packageVersion = EditorGUILayout.TextField("Next Release", _packageVersion);
            EditorGUILayout.LabelField("Release Notes");
            _releaseNotes = EditorGUILayout.TextArea(_releaseNotes, GUILayout.MinHeight(52));

            Section("Readiness");
            DrawGitReadiness();
            DrawReadOnlyRow("Remote Release", "未查询 · Remote Verification 尚未接入");
            DrawReadOnlyRow("HybridCLR", PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString());
            DrawReadOnlyRow("AOT", "需选择兼容的 BaseRelease 后校验");
            DrawReadOnlyRow("YooAsset", string.IsNullOrWhiteSpace(_packageName)
                ? "未配置生产 Package"
                : $"Package: {_packageName} · 输出尚未构建");
            HotUpdateEnvironmentProfile selectedProfile = GetSelectedProfile();
            DrawReadOnlyRow("Server", string.IsNullOrWhiteSpace(selectedProfile.MainHostServer)
                ? "当前环境尚未配置 Host"
                : selectedProfile.MainHostServer + " · " + selectedProfile.PublishTarget);

            GUILayout.Space(8);
            if (GUILayout.Button("保存本地输入")) SaveLocalInputs();
            GUILayout.Space(8);
            DrawMainActions();
        }

        private void DrawChanges()
        {
            Section("Change Safety");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新 Git 变更", GUILayout.Width(120))) RefreshChangeClassification();
            if (_classification != null)
            {
                GUILayout.Label($"Green {_classification.GreenCount}   Yellow {_classification.YellowCount}   Red {_classification.RedCount}");
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_scanError))
            {
                EditorGUILayout.HelpBox(_scanError, MessageType.Error);
            }
            else if (_gitSnapshot?.IsDirty == true && _selectedEnvironment == HotUpdateEnvironmentKind.Production)
            {
                EditorGUILayout.HelpBox("Production 发布被禁止：当前 Git 工作区有 staged、unstaged 或 untracked 改动。", MessageType.Error);
            }
            else if (_gitSnapshot?.IsDirty == true)
            {
                EditorGUILayout.HelpBox("当前 Git 工作区有改动；Development/Staging 可继续，但发布历史会记录 Dirty 状态。", MessageType.Warning);
            }
            else if (_classification == null)
            {
                EditorGUILayout.HelpBox("点击刷新读取 Git 状态并执行 P0 变更分类。扫描不会修改工作区。", MessageType.Info);
            }
            else if (!_classification.CanHotPatch)
            {
                EditorGUILayout.HelpBox("发现阻止普通 Hot Patch 的 Red 变更或 Base → HotUpdate 依赖违规。", MessageType.Error);
            }
            else if (_classification.RequiresFullGate)
            {
                EditorGUILayout.HelpBox("存在 Yellow 变更，发布前需要 Full Gate。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("当前分类允许进入 Fast Gate；这不是构建或发布通过证明。", MessageType.Info);
            }

            if (_classification == null) return;
            _changesScroll = EditorGUILayout.BeginScrollView(_changesScroll, GUILayout.MinHeight(160));
            IReadOnlyList<HotUpdateClassifiedChange> changes = _classification.Changes;
            const int visibleChangeLimit = 200;
            int visibleChangeCount = Math.Min(changes.Count, visibleChangeLimit);
            for (int index = 0; index < visibleChangeCount; index++)
            {
                HotUpdateClassifiedChange item = changes[index];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{item.Safety}] {item.Facts.Path}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(item.Reason, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
            if (changes.Count > visibleChangeCount)
                EditorGUILayout.HelpBox($"仅显示前 {visibleChangeCount} 项，共 {changes.Count} 项变更。", MessageType.Info);
            foreach (HotUpdateDependencyBoundaryViolation violation in _classification.DependencyViolations)
            {
                EditorGUILayout.HelpBox(violation.Message, MessageType.Error);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBuild()
        {
            Section("Build and Publish");
            EditorGUILayout.HelpBox(
                "HybridCLR/YooAsset 构建、产物校验、Release Gate 与只读 Dry Run 引擎已实现。ToolsHub 仍要求完整生产 Collector、构建阶段配置及发布目标后才会开放执行；不会把未配置流程显示为成功。",
                MessageType.Info);
            DrawFirstUseSetup();
            DrawMainActions();
            GUILayout.Space(12);
            DrawReadOnlyRow("Compile / Export / YooAsset", "由配置完整的发布流水线执行");
            DrawReadOnlyRow("Artifact Validation", "Manifest · DLL SHA256 · Entry · BaseRelease AOT · YooAsset 输出");
            DrawReadOnlyRow("Release Gate / Dry Run", "执行器已实现 · 等待完整生产构建与目标配置");
        }

        private void DrawFirstUseSetup()
        {
            Section("First Use Setup");

            if (string.IsNullOrWhiteSpace(_packageName))
            {
                EditorGUILayout.HelpBox(
                    "缺少 YooAsset 业务 Package 名称。请先在 Overview 填写 Package，再创建独立的业务 Collector；StellarHotUpdateVerification 仅用于测试，不能用于发布。",
                    MessageType.Error);
            }
            else if (_packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EditorGUILayout.HelpBox(
                    $"Package '{_packageName}' 被识别为 Verification 用途，Publisher 会拒绝使用它。请填写独立业务 Package 名称。",
                    MessageType.Error);
            }
            else
            {
                HotUpdatePublisherCollectorStatus status = HotUpdatePublisherCollectorStatus.Check(_packageName);
                EditorGUILayout.HelpBox(status.Message,
                    status.IsReady ? MessageType.Info : MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(
                       string.IsNullOrWhiteSpace(_packageName) ||
                       _packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (GUILayout.Button("配置 / 创建推荐 YooAsset Collector"))
                {
                    SaveLocalInputs();
                    if (!EditorApplication.ExecuteMenuItem(CreateCollectorMenuPath))
                    {
                        EditorUtility.DisplayDialog(
                            "YooAsset Collector",
                            "Publisher 的 YooAsset Adapter 菜单不可用。请确认 YooAsset Editor Adapter 已编译，然后从 YooAsset 菜单打开 AssetBundle Collector。",
                            "OK");
                        EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
                    }
                }
            }

            IReadOnlyList<HotUpdateBaseRelease> androidReleases;
            try
            {
                androidReleases = new HotUpdateBaseReleaseRepository().List(BuildTarget.Android);
            }
            catch (Exception exception)
            {
                androidReleases = Array.Empty<HotUpdateBaseRelease>();
                EditorGUILayout.HelpBox(
                    $"Android BaseRelease 仓库读取失败：{exception.GetType().Name}: {exception.Message}",
                    MessageType.Error);
            }

            if (androidReleases.Count == 0)
            {
                ScriptingImplementation androidBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
                EditorGUILayout.HelpBox(
                    $"缺少 Android BaseRelease。正式创建流程要求 Android BuildTarget + IL2CPP，并调用 HybridCLR Generate/All 后保存真实 AOT metadata。当前 Android Backend：{androidBackend}。",
                    MessageType.Error);
                if (GUILayout.Button("创建首个 Android Base Release"))
                {
                    SaveLocalInputs();
                    if (!EditorApplication.ExecuteMenuItem(CreateAndroidBaseReleaseMenuPath))
                    {
                        EditorUtility.DisplayDialog(
                            "Android Base Release",
                            "HybridCLR Publisher Adapter 菜单不可用。请确认 HybridCLR Editor Adapter 已编译。",
                            "OK");
                    }
                }
            }
            else
            {
                DrawReadOnlyRow("Android BaseRelease", $"已找到 {androidReleases.Count} 条正式记录");
            }
        }

        private void DrawServer()
        {
            Section("Server Profiles");
            _selectedEnvironment = (HotUpdateEnvironmentKind)EditorGUILayout.EnumPopup("Environment", _selectedEnvironment);
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            profile.EnvironmentId = _selectedEnvironment.ToString();
            profile.MainHostServer = EditorGUILayout.TextField("Main Host Server", profile.MainHostServer ?? string.Empty);
            profile.FallbackHostServer = EditorGUILayout.TextField("Fallback Host Server", profile.FallbackHostServer ?? string.Empty);
            profile.RemoteRoot = EditorGUILayout.TextField("Remote Root", profile.RemoteRoot ?? string.Empty);
            profile.PublishTarget = EditorGUILayout.TextField("Publish Target", profile.PublishTarget ?? string.Empty);
            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
            {
                EditorGUILayout.BeginHorizontal();
                profile.LocalFolderRoot = EditorGUILayout.TextField("Local Folder Root", profile.LocalFolderRoot ?? string.Empty);
                if (GUILayout.Button("Browse...", GUILayout.Width(88)))
                {
                    string selectedFolder = EditorUtility.OpenFolderPanel("Select mounted LocalFolder publish root", profile.LocalFolderRoot ?? string.Empty, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selectedFolder)) profile.LocalFolderRoot = selectedFolder;
                }
                EditorGUILayout.EndHorizontal();
                if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot))
                    EditorGUILayout.HelpBox("Select the mounted folder root. The profile Remote Root is appended below this directory.", MessageType.Warning);
            }
            profile.CredentialProfileName = EditorGUILayout.TextField("Credential Profile Name", profile.CredentialProfileName ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(_profileLoadDiagnostic))
                EditorGUILayout.HelpBox(_profileLoadDiagnostic, MessageType.Warning);

            HotUpdateEnvironmentProfileValidationResult validation = profile.Validate();
            for (int index = 0; index < validation.Errors.Count; index++)
                EditorGUILayout.HelpBox(validation.Errors[index], MessageType.Error);
            if (validation.IsValid)
                EditorGUILayout.HelpBox("Profile fields are valid. This does not verify server reachability or publish permissions.", MessageType.Info);

            string environmentVariableName = string.Empty;
            bool hasCredential = false;
            string secret = null;
            if (EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(profile.CredentialProfileName, out environmentVariableName))
                hasCredential = new EnvironmentVariableCredentialProvider().TryGetSecret(profile.CredentialProfileName, out secret);
            secret = null;
            EditorGUILayout.LabelField("Credential", string.IsNullOrEmpty(environmentVariableName)
                ? "Anonymous / no credential profile"
                : hasCredential ? environmentVariableName + " is set" : environmentVariableName + " is missing");
            EditorGUILayout.HelpBox("Only profile settings are stored in project-scoped EditorPrefs. Secret values are read at runtime from the environment and are never saved or displayed.", MessageType.None);
            EditorGUILayout.HelpBox("LocalFolderRoot stores only a local mount path. Publisher still requires the project-specific build stages, BaseRelease and release-gate configuration before execution.", MessageType.None);

            if (GUILayout.Button("Save Environment Profiles")) SaveEnvironmentProfiles();
        }

        private void DrawHistory()
        {
            Section("Release History");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("已发布版本的机器记录保存在 BuildArtifacts/HotUpdate/ReleaseHistory。", MessageType.Info);
            if (GUILayout.Button("Refresh", GUILayout.Width(90))) RefreshHistory();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(_historyDiagnostic))
                EditorGUILayout.HelpBox(_historyDiagnostic, MessageType.Error);
            if (_historyRecords.Count == 0)
            {
                EditorGUILayout.HelpBox("尚无已完成发布记录。Dry Run 不会创建 ACTIVE History。", MessageType.None);
                return;
            }

            _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll, GUILayout.MinHeight(180));
            const int visibleLimit = 100;
            int count = Math.Min(_historyRecords.Count, visibleLimit);
            for (int index = 0; index < count; index++)
            {
                HotUpdateReleaseRecord record = _historyRecords[index];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{record.ReleaseId}  {record.Status}  {record.PackageVersion}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"{record.Platform} · {record.Environment} · Base {record.BaseAppVersion} · {record.CreatedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    $"Change {record.ChangeClassification?.Safety ?? "Unknown"} · Gate {record.GateResult} · Bundles {record.BundleCount} · {record.TotalBytes:N0} bytes",
                    EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button(new GUIContent("Rollback", "Rollback service is implemented; ToolsHub enables it after the matching remote target and host verifier are configured."), GUILayout.Width(100));
                EditorGUILayout.EndVertical();
            }
            if (_historyRecords.Count > count)
                EditorGUILayout.HelpBox($"仅显示最近 {count} 项，共 {_historyRecords.Count} 项记录。", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void RefreshHistory()
        {
            try
            {
                _historyRecords = HotUpdateReleaseHistoryRepository.CreateForProject(GetProjectRoot()).List();
                _historyDiagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                _historyRecords = Array.Empty<HotUpdateReleaseRecord>();
                _historyDiagnostic = $"Release history could not be read: {exception.GetType().Name}: {exception.Message}";
            }
        }

        private void DrawAdvanced()
        {
            Section("Advanced Tools");
            EditorGUILayout.HelpBox("底层操作只在高级区显示。导出与构建按钮在目标/BaseRelease/生产 Collector 完整绑定前禁用。", MessageType.Warning);

            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button(new GUIContent("HybridCLR Export", "Requires a selected compatible BaseRelease and complete publish context."));
                GUILayout.Button(new GUIContent("YooAsset Build", "Requires a production collector configuration; the verification-only package is not accepted."));
                GUILayout.Button(new GUIContent("Run Gate", "Release Gate execution is integrated in a later phase."));
            }

            if (GUILayout.Button("Open Build Folder"))
            {
                string directory = Path.Combine(GetProjectRoot(), "BuildArtifacts", "HotUpdate");
                if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory);
                else EditorUtility.DisplayDialog("Build Folder", $"Folder does not exist yet:\n{directory}", "OK");
            }

            if (GUILayout.Button("View Manifest"))
            {
                const string manifestPath = "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json";
                UnityEngine.Object manifest = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(manifestPath);
                if (manifest == null)
                    EditorUtility.DisplayDialog("HotUpdate Manifest", $"Manifest was not found at {manifestPath}.", "OK");
                else
                {
                    Selection.activeObject = manifest;
                    EditorGUIUtility.PingObject(manifest);
                }
            }

            if (GUILayout.Button("View BaseRelease"))
            {
                string directory = Path.Combine(GetProjectRoot(), HotUpdateBaseReleaseRepository.DefaultRelativeRoot);
                if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory);
                else EditorUtility.DisplayDialog("BaseRelease", "No BaseRelease repository exists yet.", "OK");
            }
        }

        private void DrawMainActions()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button(new GUIContent("Dry Run", "Requires a production YooAsset Collector, configured build stages and a publish target."), GUILayout.Height(30));
                GUILayout.Button(new GUIContent("Build", "A configured build transaction is added after the UI phase."), GUILayout.Height(30));
                GUILayout.Button(new GUIContent("Build & Publish", "Publish target and release gate are not yet configured."), GUILayout.Height(30));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGitReadiness()
        {
            if (_classification == null || _gitSnapshot == null)
            {
                DrawReadOnlyRow("Git / Change Safety", "尚未扫描");
                return;
            }
            DrawReadOnlyRow("Git / Change Safety",
                $"{_gitSnapshot.Branch} · {_gitSnapshot.Commit} · {(_gitSnapshot.IsDirty ? "Dirty" : "Clean")} · Green {_classification.GreenCount} / Yellow {_classification.YellowCount} / Red {_classification.RedCount}");
        }

        private void RefreshChangeClassification()
        {
            try
            {
                string root = GetProjectRoot();
                _gitSnapshot = new GitHotUpdateSnapshotProvider(root).ReadSnapshot();
                var source = new GitHotUpdateWorkspaceChangeSource(root);
                var facts = new UnityHotUpdateChangeFactsProvider(root);
                _classification = new HotUpdateChangeClassifier(source, facts).AnalyzeWorkspace();
                _scanError = string.Empty;
            }
            catch (Exception exception)
            {
                _classification = null;
                _gitSnapshot = null;
                _scanError = $"Git / Unity change scan failed: {exception.GetType().Name}: {exception.Message}";
                Debug.LogError("[HotUpdatePublisher] " + _scanError);
            }
        }

        private static void DrawReadOnlyRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            EditorGUILayout.LabelField(value, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            if (parent == null) throw new DirectoryNotFoundException("Unity project root could not be resolved.");
            return parent.FullName;
        }

        private static string GetProjectPrefsSuffix()
        {
            return GetProjectRoot().Replace('\\', '/');
        }

        private void LoadEnvironmentProfiles(string profilesPrefsKey)
        {
            _environmentProfiles.Clear();
            _profileLoadDiagnostic = string.Empty;
            string json = EditorPrefs.GetString(profilesPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    HotUpdateEnvironmentProfileDocument document = JsonUtility.FromJson<HotUpdateEnvironmentProfileDocument>(json);
                    if (document?.Profiles != null)
                    {
                        for (int index = 0; index < document.Profiles.Length; index++)
                        {
                            HotUpdateEnvironmentProfile profile = document.Profiles[index];
                            if (profile == null || !Enum.TryParse(profile.EnvironmentId, false, out HotUpdateEnvironmentKind environment) ||
                                !Enum.IsDefined(typeof(HotUpdateEnvironmentKind), environment))
                                continue;
                            if (FindProfile(environment) == null) _environmentProfiles.Add(profile);
                        }
                    }
                    else
                    {
                        _profileLoadDiagnostic = "Saved environment profile data has no profile list. Defaults were loaded; save to replace the invalid document.";
                    }
                }
                catch (Exception exception)
                {
                    _profileLoadDiagnostic = $"Saved environment profiles could not be parsed: {exception.Message}. Defaults were loaded; save to replace the invalid document.";
                }
            }

            foreach (HotUpdateEnvironmentKind environment in Enum.GetValues(typeof(HotUpdateEnvironmentKind)))
            {
                if (FindProfile(environment) == null) _environmentProfiles.Add(HotUpdateEnvironmentProfile.CreateDefault(environment));
            }

            int selected = EditorPrefs.GetInt(profilesPrefsKey + ".selected", 0);
            _selectedEnvironment = Enum.IsDefined(typeof(HotUpdateEnvironmentKind), selected)
                ? (HotUpdateEnvironmentKind)selected
                : HotUpdateEnvironmentKind.Development;
        }

        private void SaveEnvironmentProfiles()
        {
            string suffix = GetProjectPrefsSuffix();
            string profilesPrefsKey = PrefsPrefix + suffix + ProfilesPrefsSuffix;
            var document = new HotUpdateEnvironmentProfileDocument
            {
                Profiles = _environmentProfiles.ToArray()
            };
            EditorPrefs.SetString(profilesPrefsKey, JsonUtility.ToJson(document, true));
            EditorPrefs.SetInt(profilesPrefsKey + ".selected", (int)_selectedEnvironment);
            _profileLoadDiagnostic = string.Empty;
        }

        private HotUpdateEnvironmentProfile GetSelectedProfile()
        {
            HotUpdateEnvironmentProfile profile = FindProfile(_selectedEnvironment);
            if (profile != null) return profile;

            profile = HotUpdateEnvironmentProfile.CreateDefault(_selectedEnvironment);
            _environmentProfiles.Add(profile);
            return profile;
        }

        private HotUpdateEnvironmentProfile FindProfile(HotUpdateEnvironmentKind environment)
        {
            string environmentId = environment.ToString();
            for (int index = 0; index < _environmentProfiles.Count; index++)
            {
                if (string.Equals(_environmentProfiles[index].EnvironmentId, environmentId, StringComparison.Ordinal))
                    return _environmentProfiles[index];
            }
            return null;
        }

        private void SaveLocalInputs()
        {
            string suffix = GetProjectPrefsSuffix();
            EditorPrefs.SetString(PrefsPrefix + suffix + ".baseAppVersion", _baseAppVersion ?? string.Empty);
            EditorPrefs.SetString(PrefsPrefix + suffix + ".packageName", _packageName ?? string.Empty);
            EditorPrefs.SetString(PrefsPrefix + suffix + ".packageVersion", _packageVersion ?? string.Empty);
            EditorPrefs.SetString(PrefsPrefix + suffix + ".releaseNotes", _releaseNotes ?? string.Empty);
        }
    }
}
