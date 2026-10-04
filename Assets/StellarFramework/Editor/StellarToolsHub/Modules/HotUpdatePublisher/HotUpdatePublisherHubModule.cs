using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using StellarFramework.Editor.HotUpdatePublisher;
using UPMInfo = UnityEditor.PackageManager.PackageInfo;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ToolsHub surface for the staged HotUpdate Publisher workflow. Execution buttons remain gated
    /// until the required SDK adapters, BaseRelease, Collector and publish target are ready.
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
        private const string PendingPublishSessionKey = "StellarFramework.HotUpdatePublisher.PendingPublish";
        private const string CreateCollectorMenuPath = "Tools/StellarFramework/HotUpdate Publisher/Configure Recommended YooAsset Collector";
        private const string CreateAndroidBaseReleaseMenuPath = "Tools/StellarFramework/HotUpdate Publisher/Create Android Base Release";
        private static readonly string[] TabNames = { "Overview", "Changes", "Build", "Server", "History", "Advanced" };

        private SectionTab _selectedTab;
        private string _baseAppVersion = "1.0.0";
        private string _packageName = "HotUpdatePublisherConsumerE2E";
        private string _packageVersion = "1.0.1";
        private string _releaseNotes = "";
        private string _hotUpdateAssetOutputRoot = "Assets/HotUpdatePublisherConsumerE2E/Generated";
        private string _architecture = "x86_64";
        private string _unitySkillsUrl = "http://localhost:8090";
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
        private readonly Dictionary<string, int> _rollbackSelections = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _isMajorHotPatch;
        private bool _operationBusy;
        private bool _pendingResumeScheduled;
        private string _operationStatus = string.Empty;
        private string _operationError = string.Empty;
        private CancellationTokenSource _operationCancellation;

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
            _hotUpdateAssetOutputRoot = EditorPrefs.GetString(PrefsPrefix + suffix + ".assetOutputRoot", _hotUpdateAssetOutputRoot);
            _architecture = EditorPrefs.GetString(PrefsPrefix + suffix + ".architecture", _architecture);
            _unitySkillsUrl = EditorPrefs.GetString(PrefsPrefix + suffix + ".unitySkillsUrl", _unitySkillsUrl);
            LoadEnvironmentProfiles(PrefsPrefix + suffix + ProfilesPrefsSuffix);
            RefreshHistory();
            SchedulePendingPublishResume();
        }

        public override void OnDisable()
        {
            EditorApplication.update -= WaitForPendingPublishResume;
            _pendingResumeScheduled = false;
            if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
                _operationCancellation?.Cancel();
            SaveLocalInputs();
        }

        public override void OnGUI()
        {
            DrawHeader();
            DrawOperationStatus();
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
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Next Package Version", _packageVersion);
            if (GUILayout.Button("Generate Next", GUILayout.Width(112))) GenerateNextPackageVersion();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Release Notes");
            _releaseNotes = EditorGUILayout.TextArea(_releaseNotes, GUILayout.MinHeight(52));

            Section("Readiness");
            DrawGitReadiness();
            DrawReadOnlyRow("Remote Release", "尚未运行 · Dry Run 会检查远端清单与资源完整性，不修改远端");
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
                "Build 执行 HybridCLR 编译/导出、YooAsset 构建和产物校验；Dry Run 继续运行 Release Gate 与只读远端校验。按钮会根据 SDK、BaseRelease、Collector 和目标配置显示具体阻塞原因。",
                MessageType.Info);
            DrawBaseReleasePicker();
            DrawFirstUseSetup();
            _architecture = EditorGUILayout.TextField("Player Architecture", _architecture);
            _hotUpdateAssetOutputRoot = EditorGUILayout.TextField("HotUpdate Assets Root", _hotUpdateAssetOutputRoot);
            _isMajorHotPatch = EditorGUILayout.Toggle("Major Hot Patch", _isMajorHotPatch);
            DrawMainActions();
            GUILayout.Space(12);
            DrawReadOnlyRow("Compile / Export / YooAsset", "Build、Dry Run 与 Build & Publish 的本地阶段");
            DrawReadOnlyRow("Artifact Validation", "Manifest · DLL SHA256 · Entry · BaseRelease AOT · YooAsset 输出");
            DrawReadOnlyRow("Release Gate / Dry Run", "Dry Run 执行 Gate 与远端只读完整性验证；Android 会执行完整 Player Gate");
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
            if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                profile.S3ServiceEndpoint = EditorGUILayout.TextField("S3 Service Endpoint", profile.S3ServiceEndpoint ?? string.Empty);
                profile.S3Bucket = EditorGUILayout.TextField("S3 Bucket", profile.S3Bucket ?? string.Empty);
                profile.S3Region = EditorGUILayout.TextField("S3 Region", string.IsNullOrWhiteSpace(profile.S3Region) ? "us-east-1" : profile.S3Region);
                string[] s3Errors = ValidateS3Profile(profile);
                for (int index = 0; index < s3Errors.Length; index++)
                    EditorGUILayout.HelpBox(s3Errors[index], MessageType.Error);
            }
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
            EditorGUILayout.HelpBox("Profile settings are stored in project-scoped EditorPrefs. S3 credentials are read from the named environment variable as JSON with accessKeyId and secretAccessKey fields; secret values are never saved or displayed.", MessageType.None);
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
                DrawRollbackControls(record);
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

        private void DrawRollbackControls(HotUpdateReleaseRecord current)
        {
            if (current.Status != HotUpdateReleaseRecordStatus.Active) return;

            HotUpdateReleaseRecord[] candidates = _historyRecords
                .Where(record => record.Status != HotUpdateReleaseRecordStatus.Active &&
                                 record.Platform == current.Platform &&
                                 string.Equals(record.Environment, current.Environment, StringComparison.Ordinal) &&
                                 string.Equals(record.PackageName, current.PackageName, StringComparison.Ordinal) &&
                                 !string.Equals(record.ReleaseId, current.ReleaseId, StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length == 0)
            {
                EditorGUILayout.LabelField("Rollback", "No compatible historical release is recorded.");
                return;
            }

            HotUpdateEnvironmentKind environment;
            if (!Enum.TryParse(current.Environment, false, out environment) ||
                !Enum.IsDefined(typeof(HotUpdateEnvironmentKind), environment))
            {
                EditorGUILayout.HelpBox("Rollback is blocked because this release has an unknown environment.", MessageType.Error);
                return;
            }

            HotUpdateEnvironmentProfile profile = FindProfile(environment) ?? HotUpdateEnvironmentProfile.CreateDefault(environment);
            string targetError = GetTargetReadinessError(profile);
            string[] labels = candidates.Select(record =>
                $"{record.PackageVersion} · {record.Status} · {record.CreatedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC").ToArray();
            if (!_rollbackSelections.TryGetValue(current.ReleaseId, out int selectedIndex)) selectedIndex = 0;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, candidates.Length - 1);
            selectedIndex = EditorGUILayout.Popup("Restore Release", selectedIndex, labels);
            _rollbackSelections[current.ReleaseId] = selectedIndex;

            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(targetError)))
            {
                if (GUILayout.Button("Rollback", GUILayout.Width(100)) &&
                    EditorUtility.DisplayDialog(
                        "Rollback HotUpdate",
                        $"Restore PackageVersion from {current.PackageVersion} to {candidates[selectedIndex].PackageVersion} for {current.Environment}? The remote pointer will change after integrity checks.",
                        "Rollback", "Cancel"))
                {
                    HotUpdateReleaseRecord restored = candidates[selectedIndex];
                    StartOperation("Verifying historical release and restoring PackageVersion…",
                        token => RunRollbackAsync(current, restored, profile, token));
                }
            }

            if (!string.IsNullOrWhiteSpace(targetError))
                EditorGUILayout.HelpBox("Rollback is blocked: " + targetError, MessageType.Warning);
        }

        private async Task<string> RunRollbackAsync(
            HotUpdateReleaseRecord current,
            HotUpdateReleaseRecord restored,
            HotUpdateEnvironmentProfile profile,
            CancellationToken cancellationToken)
        {
            var history = HotUpdateReleaseHistoryRepository.CreateForProject(GetProjectRoot());
            var service = new HotUpdateReleaseRollbackService(
                history,
                CreatePublishTarget(profile),
                new HotUpdateRemoteValidator(profile));
            HotUpdateRollbackResult result = await service.RollbackAsync(
                current.ReleaseId, restored.ReleaseId, cancellationToken);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            return $"Rollback verified: {restored.PackageName} {restored.PackageVersion} is active in {restored.Environment}.";
        }

        private string GetTargetReadinessError(HotUpdateEnvironmentProfile profile)
        {
            if (profile == null) return "Environment profile is missing.";
            HotUpdateEnvironmentProfileValidationResult validation = profile.Validate();
            if (!validation.IsValid) return string.Join(Environment.NewLine, validation.Errors);

            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot)) return "Set the mounted LocalFolder publish root.";
            }
            else if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                string[] errors = ValidateS3Profile(profile);
                if (errors.Length > 0) return string.Join(Environment.NewLine, errors);
                if (!EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(
                        profile.CredentialProfileName, out string variableName) ||
                    string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName)))
                    return "S3 credentials are not available from the configured environment variable.";
            }
            else
            {
                return "Publish Target must be LocalFolder or S3Compatible.";
            }

            return null;
        }

        private void DrawAdvanced()
        {
            Section("Advanced Tools");
            EditorGUILayout.HelpBox("底层操作只在高级区显示。Build、Dry Run 与 Build & Publish 会根据目标、BaseRelease、Collector、发布目标和 Gate 配置的就绪状态启用。", MessageType.Warning);

            EditorGUILayout.HelpBox("HybridCLR compile/export, YooAsset build, artifact validation and Release Gate run as ordered stages from Build, Dry Run and Build & Publish.", MessageType.Info);
            _unitySkillsUrl = EditorGUILayout.TextField("UnitySkills URL", _unitySkillsUrl);
            if (GUILayout.Button("Save Advanced Settings")) SaveLocalInputs();

            if (GUILayout.Button("Open Build Folder"))
            {
                string directory = Path.Combine(GetProjectRoot(), "BuildArtifacts", "HotUpdate");
                if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory);
                else EditorUtility.DisplayDialog("Build Folder", $"Folder does not exist yet:\n{directory}", "OK");
            }

            if (GUILayout.Button("View Manifest"))
            {
                string manifestPath = _hotUpdateAssetOutputRoot.Replace('\\', '/').TrimEnd('/') + "/Manifest/HotUpdateManifest.json";
                if (!IsSafeAssetRoot(_hotUpdateAssetOutputRoot))
                {
                    EditorUtility.DisplayDialog("HotUpdate Manifest", "Set a safe HotUpdate Assets Root inside Assets/ first.", "OK");
                    return;
                }
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
            string buildBlocker = GetBuildReadinessError(out _);
            string publishBlocker = string.IsNullOrEmpty(buildBlocker)
                ? GetPublishReadinessError(GetSelectedProfile())
                : buildBlocker;

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(publishBlocker)))
            {
                if (GUILayout.Button(new GUIContent("Dry Run", "Build, validate, run the required gate, and inspect remote files without uploading or changing the version pointer."), GUILayout.Height(30)))
                    RunDryRun();
            }
            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(buildBlocker)))
            {
                if (GUILayout.Button(new GUIContent("Build", "Compile HotUpdate, export DLL/AOT assets, build the YooAsset package and validate local artifacts."), GUILayout.Height(30)))
                    RunBuildOnly();
            }
            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(publishBlocker)))
            {
                if (GUILayout.Button(new GUIContent("Build & Publish", "Upload immutable files, verify remote content, then atomically update the PackageVersion pointer."), GUILayout.Height(30)))
                    RunBuildAndPublish();
            }
            EditorGUILayout.EndHorizontal();

            if (_operationBusy)
            {
                if (GUILayout.Button("Cancel Current Operation", GUILayout.Height(24)))
                    CancelCurrentOperation();
            }
            else if (!string.IsNullOrWhiteSpace(buildBlocker))
            {
                EditorGUILayout.HelpBox("Build is blocked: " + buildBlocker, MessageType.Warning);
            }
            else if (!string.IsNullOrWhiteSpace(publishBlocker))
            {
                EditorGUILayout.HelpBox("Dry Run and Build & Publish are blocked: " + publishBlocker, MessageType.Warning);
            }
        }

        private void DrawOperationStatus()
        {
            if (_operationBusy)
                EditorGUILayout.HelpBox(_operationStatus, MessageType.Info);
            else if (!string.IsNullOrWhiteSpace(_operationError))
                EditorGUILayout.HelpBox(_operationError, MessageType.Error);
            else if (!string.IsNullOrWhiteSpace(_operationStatus))
                EditorGUILayout.HelpBox(_operationStatus, MessageType.Info);
        }

        private void GenerateNextPackageVersion()
        {
            try
            {
                RefreshHistory();
                string[] existingVersions = _historyRecords
                    .Where(record => string.Equals(record.PackageName, _packageName, StringComparison.Ordinal) &&
                                     record.Platform == EditorUserBuildSettings.activeBuildTarget)
                    .Select(record => record.PackageVersion)
                    .ToArray();
                _packageVersion = new DailyHotUpdateVersionPolicy()
                    .CreateNextVersion(DateTime.UtcNow, existingVersions);
                _operationStatus = "Next PackageVersion generated from UTC date and recorded package history.";
                _operationError = string.Empty;
                SaveLocalInputs();
            }
            catch (Exception exception)
            {
                _operationError = "PackageVersion could not be generated: " + exception.Message;
            }
        }

        private string GetBuildReadinessError(out HotUpdateBaseRelease selectedRelease)
        {
            selectedRelease = null;
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.NoTarget)
                return "Select a Unity BuildTarget first.";
            if (!IsSafeBusinessPackageName(_packageName))
                return "Enter a path-safe YooAsset business package name that does not contain 'verification'.";
            if (!IsSafeAssetRoot(_hotUpdateAssetOutputRoot))
                return "HotUpdate Assets Root must be a safe folder inside Assets/.";

            string adapterError = HotUpdatePublisherBuildAdapters.GetReadinessError();
            if (!string.IsNullOrEmpty(adapterError)) return adapterError;

            HotUpdatePublisherCollectorStatus collector = HotUpdatePublisherCollectorStatus.Check(_packageName);
            if (!collector.IsReady) return collector.Message;

            try
            {
                HotUpdateBaseReleaseRequirements requirements = CreateBaseReleaseRequirements(
                    target, _baseAppVersion, _architecture);
                if (requirements.ScriptingBackend != ScriptingImplementation.IL2CPP)
                    return "The active Player scripting backend is not IL2CPP. Select the matching IL2CPP BaseRelease configuration before building.";

                var repository = new HotUpdateBaseReleaseRepository();
                selectedRelease = repository.LoadAndValidate(target, _baseAppVersion, requirements);
                return null;
            }
            catch (Exception exception)
            {
                return "No compatible BaseRelease is selected: " + exception.Message;
            }
        }

        private string GetPublishReadinessError(HotUpdateEnvironmentProfile profile)
        {
            string buildError = GetBuildReadinessError(out _);
            if (!string.IsNullOrEmpty(buildError)) return buildError;
            if (profile == null) return "Select a publish environment.";

            HotUpdateEnvironmentProfileValidationResult profileValidation = profile.Validate();
            if (!profileValidation.IsValid)
                return string.Join(Environment.NewLine, profileValidation.Errors);

            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot))
                    return "Set the mounted LocalFolder publish root.";
            }
            else if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                string[] errors = ValidateS3Profile(profile);
                if (errors.Length > 0) return string.Join(Environment.NewLine, errors);
                if (!EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(
                        profile.CredentialProfileName, out string variableName))
                    return "Set a valid S3 credential profile name.";
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName)))
                    return "S3 credentials are missing from environment variable " + variableName + ".";
            }
            else
            {
                return "Publish Target must be LocalFolder or S3Compatible.";
            }

            if (!Uri.TryCreate(_unitySkillsUrl, UriKind.Absolute, out Uri unitySkillsUri) ||
                (unitySkillsUri.Scheme != Uri.UriSchemeHttp && unitySkillsUri.Scheme != Uri.UriSchemeHttps))
                return "UnitySkills URL must be an absolute HTTP(S) URL.";

            if (string.Equals(profile.EnvironmentId, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal))
            {
                try
                {
                    _gitSnapshot = new GitHotUpdateSnapshotProvider(GetProjectRoot()).ReadSnapshot();
                    if (_gitSnapshot.IsDirty)
                        return "Production publishing is blocked while the Dev Git working tree has staged, unstaged or untracked changes.";
                }
                catch (Exception exception)
                {
                    return "Production Git preflight failed: " + exception.Message;
                }
            }

            return null;
        }

        private static string[] ValidateS3Profile(HotUpdateEnvironmentProfile profile)
        {
            var errors = new List<string>();
            if (!Uri.TryCreate(profile.S3ServiceEndpoint, UriKind.Absolute, out Uri endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) ||
                !string.IsNullOrEmpty(endpoint.Fragment))
                errors.Add("S3 Service Endpoint must be an absolute HTTP(S) URL without credentials, query or fragment.");
            else if (endpoint.Scheme != Uri.UriSchemeHttps && !endpoint.IsLoopback)
                errors.Add("S3 Service Endpoint must use HTTPS unless it points to loopback.");

            string bucket = profile.S3Bucket ?? string.Empty;
            if (bucket.Length < 3 || bucket.Length > 63 || bucket.StartsWith(".", StringComparison.Ordinal) ||
                bucket.EndsWith(".", StringComparison.Ordinal) || bucket.StartsWith("-", StringComparison.Ordinal) ||
                bucket.EndsWith("-", StringComparison.Ordinal) || bucket.Contains("..") || bucket.Contains(".-") || bucket.Contains("-."))
                errors.Add("S3 Bucket must be a valid 3–63 character bucket name.");
            else
            {
                for (int index = 0; index < bucket.Length; index++)
                {
                    char character = bucket[index];
                    if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') ||
                          character == '.' || character == '-'))
                    {
                        errors.Add("S3 Bucket may contain only lowercase letters, digits, dots and hyphens.");
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(profile.S3Region) || profile.S3Region.Contains("/") || profile.S3Region.Contains(" "))
                errors.Add("S3 Region is invalid.");
            if (string.IsNullOrWhiteSpace(profile.CredentialProfileName))
                errors.Add("Credential Profile Name is required for S3Compatible publishing.");
            return errors.ToArray();
        }

        private static bool IsSafeBusinessPackageName(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName) || packageName.Length > 64 ||
                packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            for (int index = 0; index < packageName.Length; index++)
            {
                char character = packageName[index];
                bool letter = (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
                if (!letter && !(character >= '0' && character <= '9') && character != '_' && character != '-')
                    return false;
                if (index == 0 && !letter && !(character >= '0' && character <= '9')) return false;
            }
            return true;
        }

        private static bool IsSafeAssetRoot(string assetRoot)
        {
            if (string.IsNullOrWhiteSpace(assetRoot)) return false;
            string normalized = assetRoot.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.Contains(":") || normalized.Contains("%"))
                return false;
            string[] segments = normalized.Split('/');
            for (int index = 0; index < segments.Length; index++)
                if (string.IsNullOrWhiteSpace(segments[index]) || segments[index] == "." || segments[index] == "..") return false;

            string candidate = Path.GetFullPath(Path.Combine(GetProjectRoot(), normalized.Replace('/', Path.DirectorySeparatorChar)));
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(assets, StringComparison.OrdinalIgnoreCase);
        }

        private static HotUpdateBaseReleaseRequirements CreateBaseReleaseRequirements(
            BuildTarget target, string baseAppVersion, string architecture)
        {
            BuildTargetGroup targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(target);
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(targetGroup);
            if (target == BuildTarget.Android)
            {
                AndroidArchitecture expected = architecture == "ARM64" ? AndroidArchitecture.ARM64 :
                    architecture == "ARMv7" ? AndroidArchitecture.ARMv7 :
                    architecture == "x86" ? AndroidArchitecture.X86 :
                    architecture == "x86_64" ? AndroidArchitecture.X86_64 : 0;
                if (expected == 0 || PlayerSettings.Android.targetArchitectures != expected)
                    throw new InvalidOperationException("For Android HotUpdate, select exactly the Android architecture recorded in the BaseRelease. The current Android architecture setting does not match.");
            }

            string hybridCLRVersion = HotUpdatePublisherBuildAdapters.GetHybridCLRPackageVersion();
            UPMInfo yooAsset = UPMInfo.GetAllRegisteredPackages()
                .FirstOrDefault(item => string.Equals(item.name, "com.tuyoogame.yooasset", StringComparison.Ordinal));
            if (yooAsset == null || string.IsNullOrWhiteSpace(yooAsset.version))
                throw new InvalidOperationException("YooAsset package version could not be read from the Unity Package Manager.");

            return new HotUpdateBaseReleaseRequirements
            {
                BaseAppVersion = (baseAppVersion ?? string.Empty).Trim(),
                Platform = target,
                Architecture = (architecture ?? string.Empty).Trim(),
                UnityVersion = Application.unityVersion,
                HybridCLRVersion = hybridCLRVersion,
                YooAssetVersion = yooAsset.version,
                ScriptingBackend = backend
            };
        }

        private HotUpdatePublishContext CreatePublishContext(
            HotUpdateEnvironmentProfile profile,
            out HotUpdateBaseReleaseRepository baseReleaseRepository,
            out HotUpdateReleaseHistoryRepository historyRepository)
        {
            string readinessError = GetBuildReadinessError(out _);
            if (!string.IsNullOrEmpty(readinessError)) throw new InvalidOperationException(readinessError);

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            HotUpdateBaseReleaseRequirements requirements = CreateBaseReleaseRequirements(target, _baseAppVersion, _architecture);
            baseReleaseRepository = new HotUpdateBaseReleaseRepository();
            historyRepository = HotUpdateReleaseHistoryRepository.CreateForProject(GetProjectRoot());
            RefreshHistory();

            string[] existingVersions = _historyRecords
                .Where(record => string.Equals(record.PackageName, _packageName, StringComparison.Ordinal) && record.Platform == target)
                .Select(record => record.PackageVersion)
                .ToArray();
            _packageVersion = new DailyHotUpdateVersionPolicy().CreateNextVersion(DateTime.UtcNow, existingVersions);
            SaveLocalInputs();

            HotUpdateReleaseRecord activeRecord = _historyRecords.FirstOrDefault(record =>
                record.Status == HotUpdateReleaseRecordStatus.Active &&
                record.Platform == target &&
                string.Equals(record.Environment, profile.EnvironmentId, StringComparison.Ordinal) &&
                string.Equals(record.PackageName, _packageName, StringComparison.Ordinal));

            var context = new HotUpdatePublishContext
            {
                Platform = target,
                Environment = profile.EnvironmentId,
                BaseAppVersion = requirements.BaseAppVersion,
                PackageName = _packageName.Trim(),
                PackageVersion = _packageVersion,
                ReleaseId = "release_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ReleaseNotes = _releaseNotes ?? string.Empty,
                HotUpdateAssetOutputRoot = _hotUpdateAssetOutputRoot.Replace('\\', '/').TrimEnd('/'),
                HotUpdateManifestAssetPath = _hotUpdateAssetOutputRoot.Replace('\\', '/').TrimEnd('/') + "/Manifest/HotUpdateManifest.json",
                YooAssetBuildOutputRoot = Path.Combine(GetProjectRoot(), "BuildArtifacts", "HotUpdate", "YooAsset"),
                YooAssetCompression = "LZ4",
                DevelopmentBuild = EditorUserBuildSettings.development,
                IsMajorHotPatch = _isMajorHotPatch,
                PublishTarget = profile.PublishTarget,
                ServerRoot = profile.RemoteRoot,
                ExpectedCurrentPackageVersion = activeRecord?.PackageVersion ?? string.Empty
            };
            context.SelectBaseRelease(baseReleaseRepository, requirements);
            return context;
        }

        private HotUpdateChangeClassifier CreateChangeClassifier()
        {
            string projectRoot = GetProjectRoot();
            return new HotUpdateChangeClassifier(
                new GitHotUpdateWorkspaceChangeSource(projectRoot),
                new UnityHotUpdateChangeFactsProvider(projectRoot));
        }

        private static IHotUpdateBuildAdapter CreateBuildAdapter(HotUpdateBaseReleaseRepository repository)
        {
            return HotUpdatePublisherBuildAdapters.Create(repository);
        }

        private HotUpdatePublisherWorkflow CreateWorkflow(
            HotUpdatePublishContext context,
            HotUpdateEnvironmentProfile profile,
            HotUpdateBaseReleaseRepository baseReleaseRepository,
            HotUpdateReleaseHistoryRepository historyRepository)
        {
            IHotUpdatePublishTarget target = CreatePublishTarget(profile);
            var remoteVerifier = new HotUpdateRemoteValidator(profile);
            var fastGate = new PowerShellHotUpdateFastReleaseGateRunner(
                GetProjectRoot(), _unitySkillsUrl, new SystemHotUpdateGateProcessRunner());
            IHotUpdateFullReleaseGateRunner fullGate = context.Platform == BuildTarget.Android
                ? new PowerShellHotUpdateAndroidFullReleaseGateRunner(
                    GetProjectRoot(), _unitySkillsUrl, new SystemHotUpdateGateProcessRunner())
                : null;

            return HotUpdatePublisherWorkflowAssembly.Create(
                new GitHotUpdateSnapshotProvider(GetProjectRoot()),
                context,
                CreateChangeClassifier(),
                CreateBuildAdapter(baseReleaseRepository),
                new HotUpdateArtifactValidator(baseReleaseRepository),
                fastGate,
                fullGate,
                target,
                remoteVerifier,
                historyRepository);
        }

        private static IHotUpdatePublishTarget CreatePublishTarget(HotUpdateEnvironmentProfile profile)
        {
            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
                return new LocalFolderPublishTarget(profile);
            if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                return new S3CompatiblePublishTarget(
                    profile,
                    new S3CompatiblePublishTargetOptions
                    {
                        ServiceEndpoint = new Uri(profile.S3ServiceEndpoint),
                        Bucket = profile.S3Bucket,
                        Region = profile.S3Region
                    },
                    new EnvironmentVariableCredentialProvider());
            }
            throw new InvalidOperationException("Publish Target must be LocalFolder or S3Compatible.");
        }

        private void RunBuildOnly()
        {
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            StartOperation("Building and validating HotUpdate artifacts…", async token =>
            {
                HotUpdatePublishContext context = CreatePublishContext(profile, out HotUpdateBaseReleaseRepository repository, out _);
                HotUpdatePublishResult result = await HotUpdatePublisherBuildOnly.RunAsync(
                    context,
                    new GitHotUpdateSnapshotProvider(GetProjectRoot()),
                    CreateChangeClassifier(),
                    CreateBuildAdapter(repository),
                    new HotUpdateArtifactValidator(repository),
                    token);
                if (!result.Success)
                    throw new InvalidOperationException($"Build failed at {result.FailedStage}: {result.Error}");
                return $"Build and artifact validation passed for {context.PackageName} {context.PackageVersion}.";
            });
        }

        private void RunDryRun()
        {
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            StartOperation("Running build, gate and read-only remote inspection…", async token =>
            {
                HotUpdatePublishContext context = CreatePublishContext(profile, out HotUpdateBaseReleaseRepository repository, out HotUpdateReleaseHistoryRepository history);
                HotUpdatePublisherWorkflow workflow = CreateWorkflow(context, profile, repository, history);
                HotUpdateDryRunResult result = await workflow.DryRun.RunAsync(context, token);
                if (!result.Success)
                    throw new InvalidOperationException($"Dry Run failed at {result.FailedStage}: {result.Error}");
                return $"Dry Run passed for {result.PackageVersion}: {result.NewCount} new files, {result.ReuseCount} reusable files, {result.TotalBytes:N0} total bytes. No remote files or version pointer were changed.";
            });
        }

        private void RunBuildAndPublish()
        {
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            if (!EditorUtility.DisplayDialog(
                    "Build & Publish HotUpdate",
                    $"This will build {profile.EnvironmentId}/{_packageName}, upload immutable files to '{profile.PublishTarget}', verify the remote content and update PackageVersion. Continue?",
                    "Build & Publish", "Cancel"))
                return;

            StartOperation("Building and publishing HotUpdate…", token => RunPublishPipelineAsync(profile, null, token));
        }

        private async Task<string> RunPublishPipelineAsync(
            HotUpdateEnvironmentProfile profile,
            PendingPublishOperation pending,
            CancellationToken cancellationToken)
        {
            if (pending != null) ApplyPendingPublishInputs(pending);
            profile = pending == null ? profile : GetSelectedProfile();

            HotUpdatePublishContext context = CreatePublishContext(
                profile, out HotUpdateBaseReleaseRepository repository, out HotUpdateReleaseHistoryRepository history);
            if (pending == null)
            {
                pending = PendingPublishOperation.FromContext(context, _selectedEnvironment, _isMajorHotPatch,
                    _architecture, _releaseNotes, _hotUpdateAssetOutputRoot);
                SessionState.SetString(PendingPublishSessionKey, JsonUtility.ToJson(pending));
            }
            else
            {
                if ((int)context.Platform != pending.platform)
                    throw new InvalidOperationException("The active BuildTarget changed while the HotUpdate publish was interrupted. Resume it on the original target.");
                context.ReleaseId = pending.releaseId;
                context.PackageVersion = pending.packageVersion;
                _packageVersion = pending.packageVersion;
                SaveLocalInputs();
            }

            HotUpdatePublisherWorkflow workflow = CreateWorkflow(context, profile, repository, history);
            HotUpdatePublishResult result = await workflow.Pipeline.RunAsync(context, cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException($"Publish failed at {result.FailedStage}: {result.Error}");
            return $"Published {result.ReleaseRecord?.PackageName} {result.ReleaseRecord?.PackageVersion} to {profile.EnvironmentId}; ReleaseId={result.ReleaseRecord?.ReleaseId}.";
        }

        private void SchedulePendingPublishResume()
        {
            if (_pendingResumeScheduled || string.IsNullOrWhiteSpace(SessionState.GetString(PendingPublishSessionKey, string.Empty)))
                return;

            _pendingResumeScheduled = true;
            EditorApplication.update -= WaitForPendingPublishResume;
            EditorApplication.update += WaitForPendingPublishResume;
        }

        private void WaitForPendingPublishResume()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitForPendingPublishResume;
            _pendingResumeScheduled = false;
            EditorApplication.delayCall += ResumePendingPublish;
        }

        private void ResumePendingPublish()
        {
            string json = SessionState.GetString(PendingPublishSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;
            PendingPublishOperation pending;
            try
            {
                pending = JsonUtility.FromJson<PendingPublishOperation>(json);
            }
            catch (ArgumentException)
            {
                pending = null;
            }

            if (pending == null || string.IsNullOrWhiteSpace(pending.releaseId) ||
                string.IsNullOrWhiteSpace(pending.packageVersion) || !Enum.IsDefined(typeof(HotUpdateEnvironmentKind), pending.environment))
            {
                SessionState.SetString(PendingPublishSessionKey, string.Empty);
                return;
            }

            ApplyPendingPublishInputs(pending);
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            StartOperation("Resuming interrupted HotUpdate publish…", token => RunPublishPipelineAsync(profile, pending, token));
        }

        private void ApplyPendingPublishInputs(PendingPublishOperation pending)
        {
            _packageName = pending.packageName;
            _baseAppVersion = pending.baseAppVersion;
            _packageVersion = pending.packageVersion;
            _hotUpdateAssetOutputRoot = pending.hotUpdateAssetOutputRoot;
            _architecture = pending.architecture;
            _releaseNotes = pending.releaseNotes;
            _selectedEnvironment = (HotUpdateEnvironmentKind)pending.environment;
            _isMajorHotPatch = pending.isMajorHotPatch;
            SaveLocalInputs();
            SaveEnvironmentProfiles();
        }

        private void CancelCurrentOperation()
        {
            SessionState.SetString(PendingPublishSessionKey, string.Empty);
            _operationCancellation?.Cancel();
        }

        private void StartOperation(string initialStatus, Func<CancellationToken, Task<string>> operation)
        {
            if (_operationBusy) return;
            _operationBusy = true;
            _operationStatus = initialStatus;
            _operationError = string.Empty;
            _operationCancellation = new CancellationTokenSource();
            Window?.Repaint();
            CompleteOperationAsync(operation, _operationCancellation.Token);
        }

        private async void CompleteOperationAsync(Func<CancellationToken, Task<string>> operation, CancellationToken cancellationToken)
        {
            try
            {
                _operationStatus = await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _operationStatus = "Operation cancelled.";
            }
            catch (Exception exception)
            {
                _operationError = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                _operationBusy = false;
                _operationCancellation?.Dispose();
                _operationCancellation = null;
                SessionState.SetString(PendingPublishSessionKey, string.Empty);
                RefreshHistory();
                SaveLocalInputs();
                SaveEnvironmentProfiles();
                Window?.Repaint();
            }
        }

        private void DrawBaseReleasePicker()
        {
            Section("Compatible BaseRelease");
            var repository = new HotUpdateBaseReleaseRepository();
            IReadOnlyList<HotUpdateBaseRelease> releases;
            try
            {
                releases = repository.List(EditorUserBuildSettings.activeBuildTarget);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("BaseRelease repository could not be read: " + exception.Message, MessageType.Error);
                return;
            }

            if (releases.Count == 0)
            {
                DrawReadOnlyRow("Build Target", EditorUserBuildSettings.activeBuildTarget.ToString());
                EditorGUILayout.HelpBox("No BaseRelease is recorded for the active BuildTarget. Create a BaseRelease from a real IL2CPP Player configuration before building a Hot Patch.", MessageType.Warning);
                return;
            }

            string[] labels = releases.Select(item => $"{item.BaseAppVersion} · {item.Architecture} · {item.CreatedAt}").ToArray();
            int selectedIndex = Array.FindIndex(releases.ToArray(), item => string.Equals(item.BaseAppVersion, _baseAppVersion, StringComparison.Ordinal));
            int newIndex = EditorGUILayout.Popup("BaseRelease", Math.Max(0, selectedIndex), labels);
            if (newIndex >= 0 && newIndex < releases.Count && newIndex != selectedIndex)
            {
                _baseAppVersion = releases[newIndex].BaseAppVersion;
                _architecture = releases[newIndex].Architecture;
                SaveLocalInputs();
            }

            if (selectedIndex < 0)
                EditorGUILayout.HelpBox("Select the exact Base App version that will receive this Hot Patch.", MessageType.Info);
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
            EditorPrefs.SetString(PrefsPrefix + suffix + ".assetOutputRoot", _hotUpdateAssetOutputRoot ?? string.Empty);
            EditorPrefs.SetString(PrefsPrefix + suffix + ".architecture", _architecture ?? string.Empty);
            EditorPrefs.SetString(PrefsPrefix + suffix + ".unitySkillsUrl", _unitySkillsUrl ?? string.Empty);
        }

        [Serializable]
        private sealed class PendingPublishOperation
        {
            public string releaseId;
            public string packageVersion;
            public string packageName;
            public string baseAppVersion;
            public string hotUpdateAssetOutputRoot;
            public string architecture;
            public string releaseNotes;
            public int environment;
            public int platform;
            public bool isMajorHotPatch;

            public static PendingPublishOperation FromContext(
                HotUpdatePublishContext context,
                HotUpdateEnvironmentKind environment,
                bool isMajorHotPatch,
                string architecture,
                string releaseNotes,
                string hotUpdateAssetOutputRoot)
            {
                return new PendingPublishOperation
                {
                    releaseId = context.ReleaseId,
                    packageVersion = context.PackageVersion,
                    packageName = context.PackageName,
                    baseAppVersion = context.BaseAppVersion,
                    hotUpdateAssetOutputRoot = hotUpdateAssetOutputRoot,
                    architecture = architecture,
                    releaseNotes = releaseNotes,
                    environment = (int)environment,
                    platform = (int)context.Platform,
                    isMajorHotPatch = isMajorHotPatch
                };
            }
        }
    }
}
