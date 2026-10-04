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
    /// ToolsHub UI for the hot-update publishing workflow. Actions stay disabled until project
    /// adapters, a compatible client base, the YooAsset collector and a publish target are ready.
    /// </summary>
    [StellarTool("热更发布器", "热更新", 0,
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
        private const string CreateCollectorMenuPath = "Tools/StellarFramework/热更发布器/创建 YooAsset 资源收集配置";
        private const string CreateAndroidBaseReleaseMenuPath = "Tools/StellarFramework/热更发布器/创建 Android 客户端基包";
        private static readonly string[] TabNames = { "概览", "变更检查", "构建与发布", "发布目标", "发布记录", "高级设置" };

        private SectionTab _selectedTab;
        private string _baseAppVersion = "1.0.0";
        private string _packageName = "DefaultPackage";
        private string _packageVersion = "1.0.1";
        private string _releaseNotes = "";
        private string _hotUpdateAssetOutputRoot = "Assets/HotUpdate/Generated";
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
        private bool _showAdvancedBuildOptions;
        private bool _operationBusy;
        private bool _pendingResumeScheduled;
        private string _operationStatus = string.Empty;
        private string _operationError = string.Empty;
        private CancellationTokenSource _operationCancellation;

        public override string Description => "按步骤配置版本和发布位置，检查变更，构建并发布热更内容。";

        public override void OnEnable()
        {
            string suffix = GetProjectPrefsSuffix();
            _baseAppVersion = EditorPrefs.GetString(PrefsPrefix + suffix + ".baseAppVersion", _baseAppVersion);
            _packageName = EditorPrefs.GetString(PrefsPrefix + suffix + ".packageName", _packageName);
            if (string.IsNullOrWhiteSpace(_packageName))
                _packageName = "DefaultPackage";
            if (string.Equals(_packageName, "HotUpdatePublisherConsumerE2E", StringComparison.Ordinal))
                _packageName = "DefaultPackage";
            _packageVersion = EditorPrefs.GetString(PrefsPrefix + suffix + ".packageVersion", _packageVersion);
            _releaseNotes = EditorPrefs.GetString(PrefsPrefix + suffix + ".releaseNotes", _releaseNotes);
            _hotUpdateAssetOutputRoot = EditorPrefs.GetString(PrefsPrefix + suffix + ".assetOutputRoot", _hotUpdateAssetOutputRoot);
            if (string.Equals(_hotUpdateAssetOutputRoot, "Assets/HotUpdatePublisherConsumerE2E/Generated", StringComparison.Ordinal))
                _hotUpdateAssetOutputRoot = "Assets/HotUpdate/Generated";
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
            EditorGUILayout.LabelField("热更发布器", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "按“填写版本 → 检查变更 → 构建验证 → 发布”完成热更。开发环境默认发布到本机目录；远端正式发布需配置 HTTPS 地址。",
                MessageType.Info);
        }

        private void DrawOverview()
        {
            Section("第一步：填写发布信息");
            DrawReadOnlyRow("当前平台", EditorUserBuildSettings.activeBuildTarget.ToString());
            DrawReadOnlyRow("发布环境", GetEnvironmentLabel(_selectedEnvironment));
            _baseAppVersion = EditorGUILayout.TextField("客户端基包版本", _baseAppVersion);
            _packageName = EditorGUILayout.TextField("YooAsset 资源包名", _packageName);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("热更资源版本", _packageVersion);
            if (GUILayout.Button("生成下一个版本", GUILayout.Width(130))) GenerateNextPackageVersion();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("更新说明");
            _releaseNotes = EditorGUILayout.TextArea(_releaseNotes, GUILayout.MinHeight(52));

            Section("发布前检查");
            DrawGitReadiness();
            DrawReadOnlyRow("远端检查", "尚未检查；模拟发布会检查地址和远端文件，不会上传或修改版本指针");
            DrawReadOnlyRow("代码热更", PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString());
            DrawReadOnlyRow("AOT 元数据", "选择匹配的客户端基包后检查");
            DrawReadOnlyRow("YooAsset 资源包", string.IsNullOrWhiteSpace(_packageName)
                ? "尚未填写资源包名"
                : $"资源包：{_packageName} · 尚未构建");
            HotUpdateEnvironmentProfile selectedProfile = GetSelectedProfile();
            DrawReadOnlyRow("发布地址", string.IsNullOrWhiteSpace(selectedProfile.MainHostServer)
                ? "还没有设置下载地址，请到“发布目标”中配置"
                : selectedProfile.MainHostServer + " · " + GetPublishTargetLabel(selectedProfile.PublishTarget));

            GUILayout.Space(8);
            if (GUILayout.Button("保存发布信息")) SaveLocalInputs();
            GUILayout.Space(8);
            DrawMainActions();
        }

        private void DrawChanges()
        {
            Section("第二步：检查改动是否适合热更");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("扫描项目改动", GUILayout.Width(120))) RefreshChangeClassification();
            if (_classification != null)
            {
                GUILayout.Label($"可直接热更 {_classification.GreenCount} 项   需完整验证 {_classification.YellowCount} 项   阻止热更 {_classification.RedCount} 项");
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_scanError))
            {
                EditorGUILayout.HelpBox(_scanError, MessageType.Error);
            }
            else if (_gitSnapshot?.IsDirty == true && _selectedEnvironment == HotUpdateEnvironmentKind.Production)
            {
                EditorGUILayout.HelpBox("正式环境发布已阻止：Git 工作区有未提交改动。请提交或清理改动后再发布。", MessageType.Error);
            }
            else if (_gitSnapshot?.IsDirty == true)
            {
                EditorGUILayout.HelpBox("Git 工作区有未提交改动；开发或预发布仍可继续，发布记录会标明这一状态。", MessageType.Warning);
            }
            else if (_classification == null)
            {
                EditorGUILayout.HelpBox("点击“扫描项目改动”读取 Git 状态并分类。扫描只读，不会修改项目文件。", MessageType.Info);
            }
            else if (!_classification.CanHotPatch)
            {
                EditorGUILayout.HelpBox("发现不适合制作普通热更包的改动，或客户端与热更代码的依赖边界异常。请先处理红色项目。", MessageType.Error);
            }
            else if (_classification.RequiresFullGate)
            {
                EditorGUILayout.HelpBox("存在需要完整验证的改动。发布前会运行完整验证流程。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("改动分类允许继续；仍需完成构建和发布前验证。", MessageType.Info);
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
                EditorGUILayout.LabelField($"[{GetChangeSafetyLabel(item.Safety)}] {item.Facts.Path}", EditorStyles.boldLabel);
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
            Section("第三步：构建、验证并发布");
            EditorGUILayout.HelpBox(
                "先完成首次配置，再选择下方操作。仅构建会在本地生成并检查热更文件；模拟发布会额外检查目标地址但不上传；构建并发布会上传文件并更新版本指针。",
                MessageType.Info);
            DrawBaseReleasePicker();
            DrawFirstUseSetup();
            DrawMainActions();
            GUILayout.Space(12);
            DrawReadOnlyRow("本机构建", "编译热更代码、导出 DLL 和 AOT 元数据、构建 YooAsset 资源包");
            DrawReadOnlyRow("文件检查", "检查版本清单、DLL 校验值、入口信息、客户端 AOT 元数据和资源包");
            DrawReadOnlyRow("发布前验证", "模拟发布会执行完整验证并只读检查远端文件；Android 会构建并验证客户端");
        }

        private void DrawFirstUseSetup()
        {
            Section("首次使用：准备资源包和客户端基包");

            if (string.IsNullOrWhiteSpace(_packageName))
            {
                EditorGUILayout.HelpBox(
                    "请先在“概览”填写业务资源包名，然后创建对应的 YooAsset 收集配置。验证专用资源包不能用于正式发布。",
                    MessageType.Error);
            }
            else if (_packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EditorGUILayout.HelpBox(
                    $"资源包“{_packageName}”名称包含 Verification（验证）字样，不能用于发布。请填写业务项目自己的资源包名。",
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
                if (GUILayout.Button("创建或打开 YooAsset 资源收集配置"))
                {
                    SaveLocalInputs();
                    if (!EditorApplication.ExecuteMenuItem(CreateCollectorMenuPath))
                    {
                        EditorUtility.DisplayDialog(
                            "YooAsset 资源收集配置",
                            "框架的 YooAsset 编辑器工具尚不可用。请确认已导入 YooAsset 编辑器扩展，然后从 YooAsset 菜单打开资源收集器。",
                            "OK");
                        EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
                    }
                }
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorGUILayout.HelpBox("当前平台不是 Android。需要发布 Android 热更时，先在 Unity Build Settings 切换到 Android。", MessageType.Info);
                return;
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
                    $"读取 Android 客户端基包记录失败：{exception.GetType().Name}: {exception.Message}",
                    MessageType.Error);
            }

            if (androidReleases.Count == 0)
            {
                ScriptingImplementation androidBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
                EditorGUILayout.HelpBox(
                    $"尚无 Android 客户端基包。需要切换到 Android + IL2CPP，生成 HybridCLR 文件并保存 AOT 元数据。当前 Android 脚本后端：{androidBackend}。",
                    MessageType.Error);
                if (GUILayout.Button("创建 Android 客户端基包"))
                {
                    SaveLocalInputs();
                    if (!EditorApplication.ExecuteMenuItem(CreateAndroidBaseReleaseMenuPath))
                    {
                        EditorUtility.DisplayDialog(
                            "Android 客户端基包",
                            "HybridCLR 编辑器工具尚不可用。请确认已导入 ResKit.CodeUpdate.HybridCLR.Tools。",
                            "OK");
                    }
                }
            }
            else
            {
                DrawReadOnlyRow("Android 客户端基包", $"已找到 {androidReleases.Count} 条正式记录");
            }
        }

        private void DrawServer()
        {
            Section("第四步：设置发布位置");
            _selectedEnvironment = (HotUpdateEnvironmentKind)EditorGUILayout.Popup("发布环境", (int)_selectedEnvironment, new[] { "开发（本机）", "预发布", "正式环境" });
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            profile.EnvironmentId = _selectedEnvironment.ToString();
            profile.MainHostServer = EditorGUILayout.TextField("主下载地址", profile.MainHostServer ?? string.Empty);
            profile.FallbackHostServer = EditorGUILayout.TextField("备用下载地址", profile.FallbackHostServer ?? string.Empty);
            profile.RemoteRoot = EditorGUILayout.TextField("服务器上的目录", profile.RemoteRoot ?? string.Empty);
            int publishTargetIndex = string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal)
                ? 1
                : 0;
            publishTargetIndex = EditorGUILayout.Popup("发布方式", publishTargetIndex,
                new[] { "本地文件夹", "S3 兼容存储" });
            profile.PublishTarget = publishTargetIndex == 1 ? "S3Compatible" : "LocalFolder";
            if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                profile.S3ServiceEndpoint = EditorGUILayout.TextField("S3 服务地址", profile.S3ServiceEndpoint ?? string.Empty);
                profile.S3Bucket = EditorGUILayout.TextField("S3 存储桶", profile.S3Bucket ?? string.Empty);
                profile.S3Region = EditorGUILayout.TextField("S3 区域", string.IsNullOrWhiteSpace(profile.S3Region) ? "us-east-1" : profile.S3Region);
                string[] s3Errors = ValidateS3Profile(profile);
                for (int index = 0; index < s3Errors.Length; index++)
                    EditorGUILayout.HelpBox(s3Errors[index], MessageType.Error);
            }
            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
            {
                EditorGUILayout.BeginHorizontal();
                profile.LocalFolderRoot = EditorGUILayout.TextField("本机或已挂载目录", profile.LocalFolderRoot ?? string.Empty);
                if (GUILayout.Button("浏览…", GUILayout.Width(88)))
                {
                    string selectedFolder = EditorUtility.OpenFolderPanel("选择本地或已挂载的发布目录", profile.LocalFolderRoot ?? string.Empty, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selectedFolder)) profile.LocalFolderRoot = selectedFolder;
                }
                if (_selectedEnvironment == HotUpdateEnvironmentKind.Development &&
                    GUILayout.Button("使用默认本机目录", GUILayout.Width(140)))
                {
                    if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot))
                        profile.LocalFolderRoot = HotUpdateEnvironmentProfile.GetDefaultLocalFolderRoot();
                    if (string.IsNullOrWhiteSpace(profile.RemoteRoot))
                        profile.RemoteRoot = "hotupdate/Development";
                    profile.MainHostServer = HotUpdateEnvironmentProfile.CreateLocalFileHost(
                        profile.LocalFolderRoot,
                        profile.RemoteRoot);
                }
                EditorGUILayout.EndHorizontal();
                if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot))
                    EditorGUILayout.HelpBox(_selectedEnvironment == HotUpdateEnvironmentKind.Development
                        ? "选择一个本机文件夹，或点击“使用默认本机目录”。服务器目录会附加在此目录后。"
                        : "选择已挂载的服务器目录；服务器目录会附加在此路径后。", MessageType.Warning);

                if (_selectedEnvironment == HotUpdateEnvironmentKind.Development &&
                    !string.IsNullOrWhiteSpace(profile.LocalFolderRoot) &&
                    !string.IsNullOrWhiteSpace(profile.RemoteRoot) &&
                    Uri.TryCreate(profile.MainHostServer, UriKind.Absolute, out Uri localHost) && localHost.IsFile)
                {
                    string derivedHost = HotUpdateEnvironmentProfile.CreateLocalFileHost(
                        profile.LocalFolderRoot,
                        profile.RemoteRoot);
                    if (!string.Equals(profile.MainHostServer, derivedHost, StringComparison.Ordinal))
                        EditorGUILayout.HelpBox("本机发布地址由本地目录和服务器目录组成；修改路径后重新点击“使用默认本机目录”以更新地址。", MessageType.Info);
                    EditorGUILayout.HelpBox("开发环境默认输出到项目的 BuildArtifacts/HotUpdate/Local 文件夹，电脑可直接读取。Android 设备不能读取 Windows 本机路径；测试 Android 时请改用手机可访问的 HTTP 地址。", MessageType.Info);
                }
            }
            profile.CredentialProfileName = EditorGUILayout.TextField("凭证配置名称", profile.CredentialProfileName ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(_profileLoadDiagnostic))
                EditorGUILayout.HelpBox(_profileLoadDiagnostic, MessageType.Warning);

            HotUpdateEnvironmentProfileValidationResult validation = profile.Validate();
            for (int index = 0; index < validation.Errors.Count; index++)
                EditorGUILayout.HelpBox(validation.Errors[index], MessageType.Error);
            if (validation.IsValid)
                EditorGUILayout.HelpBox("配置格式有效；尚未检查服务器是否可访问，也未检查上传权限。", MessageType.Info);

            string environmentVariableName = string.Empty;
            bool hasCredential = false;
            string secret = null;
            if (EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(profile.CredentialProfileName, out environmentVariableName))
                hasCredential = new EnvironmentVariableCredentialProvider().TryGetSecret(profile.CredentialProfileName, out secret);
            secret = null;
            EditorGUILayout.LabelField("上传凭证", string.IsNullOrEmpty(environmentVariableName)
                ? "未配置（适用于公开下载目录）"
                : hasCredential ? "系统环境变量已设置：" + environmentVariableName : "缺少系统环境变量：" + environmentVariableName);
            EditorGUILayout.HelpBox("发布配置仅保存在当前 Unity 项目的本机设置中。S3 密钥从指定的系统环境变量读取，不会写入项目文件，也不会显示密钥内容。", MessageType.None);
            EditorGUILayout.HelpBox("开发环境默认发布到本机目录。预发布和正式环境需要填写 HTTP(S) 下载地址；正式环境必须使用 HTTPS。开始发布前还需准备匹配的客户端基包。", MessageType.None);

            if (GUILayout.Button("保存发布位置设置")) SaveEnvironmentProfiles();
        }

        private void DrawHistory()
        {
            Section("已发布记录");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("已发布版本的机器记录保存在 BuildArtifacts/HotUpdate/ReleaseHistory。", MessageType.Info);
            if (GUILayout.Button("刷新", GUILayout.Width(90))) RefreshHistory();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(_historyDiagnostic))
                EditorGUILayout.HelpBox(_historyDiagnostic, MessageType.Error);
            if (_historyRecords.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有发布记录。模拟发布不会创建正式发布记录。", MessageType.None);
                return;
            }

            _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll, GUILayout.MinHeight(180));
            const int visibleLimit = 100;
            int count = Math.Min(_historyRecords.Count, visibleLimit);
            for (int index = 0; index < count; index++)
            {
                HotUpdateReleaseRecord record = _historyRecords[index];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{record.PackageName}  {record.PackageVersion}  {GetReleaseStatusLabel(record.Status)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"{record.Platform} · {GetEnvironmentLabel(record.Environment)} · 客户端 {record.BaseAppVersion} · {record.CreatedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    $"变更：{GetChangeSafetyLabel(record.ChangeClassification?.Safety)} · 验证结果：{record.GateResult} · 资源包数：{record.BundleCount} · 总大小：{EditorUtility.FormatBytes(record.TotalBytes)}",
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
                _historyDiagnostic = $"读取发布记录失败：{exception.GetType().Name}: {exception.Message}";
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
                EditorGUILayout.LabelField("回滚", "没有找到可用的历史版本。");
                return;
            }

            HotUpdateEnvironmentKind environment;
            if (!Enum.TryParse(current.Environment, false, out environment) ||
                !Enum.IsDefined(typeof(HotUpdateEnvironmentKind), environment))
            {
                EditorGUILayout.HelpBox("该版本没有环境记录，无法安全回滚。", MessageType.Error);
                return;
            }

            HotUpdateEnvironmentProfile profile = FindProfile(environment) ?? HotUpdateEnvironmentProfile.CreateDefault(environment);
            string targetError = GetTargetReadinessError(profile);
            string[] labels = candidates.Select(record =>
                $"{record.PackageVersion} · {record.Status} · {record.CreatedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC").ToArray();
            if (!_rollbackSelections.TryGetValue(current.ReleaseId, out int selectedIndex)) selectedIndex = 0;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, candidates.Length - 1);
            selectedIndex = EditorGUILayout.Popup("要恢复到的版本", selectedIndex, labels);
            _rollbackSelections[current.ReleaseId] = selectedIndex;

            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(targetError)))
            {
                if (GUILayout.Button("回滚", GUILayout.Width(100)) &&
                    EditorUtility.DisplayDialog(
                        "确认回滚热更版本",
                        $"将 {current.PackageVersion} 回滚到 {candidates[selectedIndex].PackageVersion}（{GetEnvironmentLabel(environment)}）？通过远端完整性检查后，会更新当前资源版本号。",
                        "确认回滚", "取消"))
                {
                    HotUpdateReleaseRecord restored = candidates[selectedIndex];
                    StartOperation("正在检查历史文件并恢复资源版本号…",
                        token => RunRollbackAsync(current, restored, profile, token));
                }
            }

            if (!string.IsNullOrWhiteSpace(targetError))
                EditorGUILayout.HelpBox("无法回滚：" + targetError, MessageType.Warning);
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
            if (!result.Success) throw new InvalidOperationException("回滚失败：" + result.Error);
            return $"Rollback verified: {restored.PackageName} {restored.PackageVersion} is active in {restored.Environment}.";
        }

        private string GetTargetReadinessError(HotUpdateEnvironmentProfile profile)
        {
            if (profile == null) return "没有找到该环境的发布设置。";
            HotUpdateEnvironmentProfileValidationResult validation = profile.Validate();
            if (!validation.IsValid) return string.Join(Environment.NewLine, validation.Errors);

            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot)) return "请选择本地或已挂载的发布目录。";
            }
            else if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                string[] errors = ValidateS3Profile(profile);
                if (errors.Length > 0) return string.Join(Environment.NewLine, errors);
                if (!EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(
                        profile.CredentialProfileName, out string variableName) ||
                    string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName)))
                    return "没有从所选系统环境变量中读取到 S3 上传凭证。";
            }
            else
            {
                return "请选择有效的发布方式。";
            }

            return null;
        }

        private void DrawAdvanced()
        {
            Section("高级设置与诊断");
            EditorGUILayout.HelpBox("高级选项用于排查和调整发布流程。常规使用只需填写发布信息、选择目标并执行模拟发布或正式发布。", MessageType.Warning);

            _showAdvancedBuildOptions = EditorGUILayout.Foldout(_showAdvancedBuildOptions, "构建参数（通常保持默认）", true);
            if (_showAdvancedBuildOptions)
            {
                EditorGUI.indentLevel++;
                _architecture = EditorGUILayout.TextField("设备架构", _architecture);
                _hotUpdateAssetOutputRoot = EditorGUILayout.TextField("热更文件生成目录", _hotUpdateAssetOutputRoot);
                _isMajorHotPatch = EditorGUILayout.Toggle("大版本热更", _isMajorHotPatch);
                EditorGUILayout.HelpBox("客户端基包必须使用匹配的平台、架构和 IL2CPP 设置。只有明确知道需要更改构建参数时才修改这里。", MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox("构建流程依次生成代码热更文件、构建 YooAsset 资源包、检查产物并运行发布验证。", MessageType.Info);
            _unitySkillsUrl = EditorGUILayout.TextField("UnitySkills 服务地址", _unitySkillsUrl);
            if (GUILayout.Button("保存高级设置")) SaveLocalInputs();

            if (GUILayout.Button("打开构建文件夹"))
            {
                string directory = Path.Combine(GetProjectRoot(), "BuildArtifacts", "HotUpdate");
                if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory);
                else EditorUtility.DisplayDialog("构建文件夹", $"文件夹尚不存在：\n{directory}", "OK");
            }

            if (GUILayout.Button("查看热更清单"))
            {
                string manifestPath = _hotUpdateAssetOutputRoot.Replace('\\', '/').TrimEnd('/') + "/Manifest/HotUpdateManifest.json";
                if (!IsSafeAssetRoot(_hotUpdateAssetOutputRoot))
                {
                    EditorUtility.DisplayDialog("热更清单", "请先将热更文件生成目录设为 Assets/ 下的安全路径。", "OK");
                    return;
                }
                UnityEngine.Object manifest = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(manifestPath);
                if (manifest == null)
                    EditorUtility.DisplayDialog("热更清单", $"未找到热更清单：{manifestPath}", "OK");
                else
                {
                    Selection.activeObject = manifest;
                    EditorGUIUtility.PingObject(manifest);
                }
            }

            if (GUILayout.Button("查看客户端基包记录"))
            {
                string directory = Path.Combine(GetProjectRoot(), HotUpdateBaseReleaseRepository.DefaultRelativeRoot);
                if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory);
                else EditorUtility.DisplayDialog("客户端基包记录", "尚未创建客户端基包记录。", "OK");
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
                if (GUILayout.Button(new GUIContent("模拟发布（不上传）", "完整执行构建和验证，并检查远端文件；不会上传文件或修改线上版本号。"), GUILayout.Height(30)))
                    RunDryRun();
            }
            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(buildBlocker)))
            {
                if (GUILayout.Button(new GUIContent("仅构建与检查", "生成代码热更文件，构建 YooAsset 资源包，并检查本地发布文件。"), GUILayout.Height(30)))
                    RunBuildOnly();
            }
            using (new EditorGUI.DisabledScope(_operationBusy || !string.IsNullOrEmpty(publishBlocker)))
            {
                if (GUILayout.Button(new GUIContent("构建并发布", "构建并验证发布文件，上传到所选位置，最后更新资源版本号。"), GUILayout.Height(30)))
                    RunBuildAndPublish();
            }
            EditorGUILayout.EndHorizontal();

            if (_operationBusy)
            {
                if (GUILayout.Button("取消当前操作", GUILayout.Height(24)))
                    CancelCurrentOperation();
            }
            else if (!string.IsNullOrWhiteSpace(buildBlocker))
            {
                EditorGUILayout.HelpBox("暂时无法构建：" + buildBlocker, MessageType.Warning);
            }
            else if (!string.IsNullOrWhiteSpace(publishBlocker))
            {
                EditorGUILayout.HelpBox("暂时无法模拟或发布：" + publishBlocker, MessageType.Warning);
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
                _operationStatus = "已根据当前日期和发布记录生成新的资源版本号。";
                _operationError = string.Empty;
                SaveLocalInputs();
            }
            catch (Exception exception)
            {
                _operationError = "生成资源版本号失败：" + exception.Message;
            }
        }

        private string GetBuildReadinessError(out HotUpdateBaseRelease selectedRelease)
        {
            selectedRelease = null;
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.NoTarget)
                return "请先在 Unity Build Settings 中选择目标平台。";
            if (!IsSafeBusinessPackageName(_packageName))
                return "请输入有效的 YooAsset 业务资源包名；名称不能包含 verification。";
            if (!IsSafeAssetRoot(_hotUpdateAssetOutputRoot))
                return "热更文件生成目录必须是 Assets/ 下的有效文件夹。";

            string adapterError = HotUpdatePublisherBuildAdapters.GetReadinessError();
            if (!string.IsNullOrEmpty(adapterError)) return adapterError;

            HotUpdatePublisherCollectorStatus collector = HotUpdatePublisherCollectorStatus.Check(_packageName);
            if (!collector.IsReady) return collector.Message;

            try
            {
                HotUpdateBaseReleaseRequirements requirements = CreateBaseReleaseRequirements(
                    target, _baseAppVersion, _architecture);
                if (requirements.ScriptingBackend != ScriptingImplementation.IL2CPP)
                    return "当前 Player 脚本后端不是 IL2CPP。请切换为 IL2CPP，并选择对应的客户端基包。";

                var repository = new HotUpdateBaseReleaseRepository();
                selectedRelease = repository.LoadAndValidate(target, _baseAppVersion, requirements);
                return null;
            }
            catch (Exception exception)
            {
                return "没有找到匹配的客户端基包。请确认版本、平台、架构和 IL2CPP 设置一致。详细信息：" + exception.Message;
            }
        }

        private string GetPublishReadinessError(HotUpdateEnvironmentProfile profile)
        {
            string buildError = GetBuildReadinessError(out _);
            if (!string.IsNullOrEmpty(buildError)) return buildError;
            if (profile == null) return "请先选择发布环境。";

            HotUpdateEnvironmentProfileValidationResult profileValidation = profile.Validate();
            if (!profileValidation.IsValid)
                return string.Join(Environment.NewLine, profileValidation.Errors);

            if (string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalFolderRoot))
                    return "请选择本地或已挂载的发布目录。";
            }
            else if (string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
            {
                string[] errors = ValidateS3Profile(profile);
                if (errors.Length > 0) return string.Join(Environment.NewLine, errors);
                if (!EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(
                        profile.CredentialProfileName, out string variableName))
                    return "请输入有效的 S3 凭证配置名称。";
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName)))
                    return "系统环境变量中没有 S3 上传凭证：" + variableName;
            }
            else
            {
                return "请选择有效的发布方式。";
            }

            if (!Uri.TryCreate(_unitySkillsUrl, UriKind.Absolute, out Uri unitySkillsUri) ||
                (unitySkillsUri.Scheme != Uri.UriSchemeHttp && unitySkillsUri.Scheme != Uri.UriSchemeHttps))
                return "高级设置中的 UnitySkills 服务地址必须是完整的 HTTP 或 HTTPS 地址。";

            if (string.Equals(profile.EnvironmentId, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal))
            {
                try
                {
                    _gitSnapshot = new GitHotUpdateSnapshotProvider(GetProjectRoot()).ReadSnapshot();
                    if (_gitSnapshot.IsDirty)
                        return "正式环境发布已阻止：Git 工作区有暂存、未暂存或未跟踪的改动。请先提交或清理改动。";
                }
                catch (Exception exception)
                {
                    return "检查正式发布所需的 Git 状态失败：" + exception.Message;
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
                errors.Add("S3 服务地址必须是完整的 HTTP(S) 地址，不能包含账号密码、查询参数或片段。");
            else if (endpoint.Scheme != Uri.UriSchemeHttps && !endpoint.IsLoopback)
                errors.Add("S3 服务地址必须使用 HTTPS；只有本机回环地址可以使用 HTTP。");

            string bucket = profile.S3Bucket ?? string.Empty;
            if (bucket.Length < 3 || bucket.Length > 63 || bucket.StartsWith(".", StringComparison.Ordinal) ||
                bucket.EndsWith(".", StringComparison.Ordinal) || bucket.StartsWith("-", StringComparison.Ordinal) ||
                bucket.EndsWith("-", StringComparison.Ordinal) || bucket.Contains("..") || bucket.Contains(".-") || bucket.Contains("-."))
                errors.Add("S3 存储桶名称长度必须为 3 到 63 个字符，并符合存储桶命名规则。");
            else
            {
                for (int index = 0; index < bucket.Length; index++)
                {
                    char character = bucket[index];
                    if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') ||
                          character == '.' || character == '-'))
                    {
                        errors.Add("S3 存储桶名称只能包含小写字母、数字、点和连字符。");
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(profile.S3Region) || profile.S3Region.Contains("/") || profile.S3Region.Contains(" "))
                errors.Add("S3 区域不能为空，且不能包含斜线或空格。");
            if (string.IsNullOrWhiteSpace(profile.CredentialProfileName))
                errors.Add("使用 S3 兼容存储时必须填写凭证配置名称。");
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
            StartOperation("正在构建并检查热更文件…", async token =>
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
                    throw new InvalidOperationException($"构建在 {GetStageLabel(result.FailedStage)} 阶段失败：{result.Error}");
                return $"构建和文件检查通过：{context.PackageName} {context.PackageVersion}。";
            });
        }

        private void RunDryRun()
        {
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            StartOperation("正在构建、验证并只读检查发布位置…", async token =>
            {
                HotUpdatePublishContext context = CreatePublishContext(profile, out HotUpdateBaseReleaseRepository repository, out HotUpdateReleaseHistoryRepository history);
                HotUpdatePublisherWorkflow workflow = CreateWorkflow(context, profile, repository, history);
                HotUpdateDryRunResult result = await workflow.DryRun.RunAsync(context, token);
                if (!result.Success)
                    throw new InvalidOperationException($"模拟发布在 {GetStageLabel(result.FailedStage)} 阶段失败：{result.Error}");
                return $"模拟发布通过：版本 {result.PackageVersion}，新增 {result.NewCount} 个文件，可复用 {result.ReuseCount} 个文件，总计 {EditorUtility.FormatBytes(result.TotalBytes)}。没有修改远端文件或线上版本号。";
            });
        }

        private void RunBuildAndPublish()
        {
            HotUpdateEnvironmentProfile profile = GetSelectedProfile();
            if (!EditorUtility.DisplayDialog(
                    "确认发布热更",
                    $"即将为“{GetEnvironmentLabel(_selectedEnvironment)} / {_packageName}”构建热更文件，上传到所选位置，检查远端文件并更新资源版本号。是否继续？",
                    "开始发布", "取消"))
                return;

            StartOperation("正在构建并发布热更…", token => RunPublishPipelineAsync(profile, null, token));
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
                throw new InvalidOperationException($"发布在 {GetStageLabel(result.FailedStage)} 阶段失败：{result.Error}");
            return $"发布成功：{result.ReleaseRecord?.PackageName} {result.ReleaseRecord?.PackageVersion}（{GetEnvironmentLabel(_selectedEnvironment)}）。发布编号：{result.ReleaseRecord?.ReleaseId}。";
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
            Section("匹配的客户端基包");
            var repository = new HotUpdateBaseReleaseRepository();
            IReadOnlyList<HotUpdateBaseRelease> releases;
            try
            {
                releases = repository.List(EditorUserBuildSettings.activeBuildTarget);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("读取客户端基包记录失败：" + exception.Message, MessageType.Error);
                return;
            }

            if (releases.Count == 0)
            {
                DrawReadOnlyRow("当前构建平台", EditorUserBuildSettings.activeBuildTarget.ToString());
                EditorGUILayout.HelpBox("当前平台还没有客户端基包记录。请先使用 IL2CPP 构建一次客户端并创建基包，再生成对应热更包。", MessageType.Warning);
                return;
            }

            string[] labels = releases.Select(item => $"{item.BaseAppVersion} · {item.Architecture} · {item.CreatedAt}").ToArray();
            int selectedIndex = Array.FindIndex(releases.ToArray(), item => string.Equals(item.BaseAppVersion, _baseAppVersion, StringComparison.Ordinal));
            int newIndex = EditorGUILayout.Popup("客户端基包记录", Math.Max(0, selectedIndex), labels);
            if (newIndex >= 0 && newIndex < releases.Count && newIndex != selectedIndex)
            {
                _baseAppVersion = releases[newIndex].BaseAppVersion;
                _architecture = releases[newIndex].Architecture;
                SaveLocalInputs();
            }

            if (selectedIndex < 0)
                EditorGUILayout.HelpBox("选择将要接收热更包的客户端版本，两者必须匹配。", MessageType.Info);
        }

        private void DrawGitReadiness()
        {
            if (_classification == null || _gitSnapshot == null)
            {
                DrawReadOnlyRow("项目改动检查", "尚未扫描");
                return;
            }
            DrawReadOnlyRow("项目改动检查",
                $"分支 {_gitSnapshot.Branch} · 提交 {_gitSnapshot.Commit} · {(_gitSnapshot.IsDirty ? "有未提交改动" : "工作区干净")} · 可直接热更 {_classification.GreenCount} / 需完整验证 {_classification.YellowCount} / 阻止热更 {_classification.RedCount}");
        }

        private static string GetEnvironmentLabel(HotUpdateEnvironmentKind environment)
        {
            switch (environment)
            {
                case HotUpdateEnvironmentKind.Development: return "开发（本机）";
                case HotUpdateEnvironmentKind.Staging: return "预发布";
                case HotUpdateEnvironmentKind.Production: return "正式环境";
                default: return "未知环境";
            }
        }

        private static string GetEnvironmentLabel(string environment)
        {
            return Enum.TryParse(environment, false, out HotUpdateEnvironmentKind parsed) &&
                   Enum.IsDefined(typeof(HotUpdateEnvironmentKind), parsed)
                ? GetEnvironmentLabel(parsed)
                : "未知环境";
        }

        private static string GetPublishTargetLabel(string publishTarget)
        {
            if (string.Equals(publishTarget, "LocalFolder", StringComparison.Ordinal)) return "本地文件夹";
            if (string.Equals(publishTarget, "S3Compatible", StringComparison.Ordinal)) return "S3 兼容存储";
            return "未选择发布方式";
        }

        private static string GetChangeSafetyLabel(HotUpdateChangeSafety? safety)
        {
            if (!safety.HasValue) return "未分类";
            switch (safety.Value)
            {
                case HotUpdateChangeSafety.Green: return "可直接热更";
                case HotUpdateChangeSafety.Yellow: return "需完整验证";
                case HotUpdateChangeSafety.Red: return "阻止热更";
                default: return "未分类";
            }
        }

        private static string GetChangeSafetyLabel(string safety)
        {
            if (string.Equals(safety, "GREEN", StringComparison.OrdinalIgnoreCase)) return "可直接热更";
            if (string.Equals(safety, "YELLOW", StringComparison.OrdinalIgnoreCase)) return "需完整验证";
            if (string.Equals(safety, "RED", StringComparison.OrdinalIgnoreCase)) return "阻止热更";
            return string.IsNullOrWhiteSpace(safety) ? "未分类" : safety;
        }

        private static string GetReleaseStatusLabel(HotUpdateReleaseRecordStatus status)
        {
            switch (status)
            {
                case HotUpdateReleaseRecordStatus.Prepared: return "准备中";
                case HotUpdateReleaseRecordStatus.Active: return "当前版本";
                case HotUpdateReleaseRecordStatus.Superseded: return "已被替换";
                case HotUpdateReleaseRecordStatus.RolledBack: return "已回滚";
                case HotUpdateReleaseRecordStatus.Failed: return "失败";
                case HotUpdateReleaseRecordStatus.RollbackUnverified: return "回滚未验证";
                default: return "未知状态";
            }
        }

        private static string GetStageLabel(HotUpdatePublishStage stage)
        {
            switch (stage)
            {
                case HotUpdatePublishStage.Preflight: return "配置检查";
                case HotUpdatePublishStage.ClassifyChanges: return "变更分类";
                case HotUpdatePublishStage.CompileHotUpdate: return "编译热更代码";
                case HotUpdatePublishStage.ExportHybridCLRAssets: return "导出代码和元数据";
                case HotUpdatePublishStage.BuildYooAsset: return "构建 YooAsset 资源";
                case HotUpdatePublishStage.ValidateArtifacts: return "检查发布文件";
                case HotUpdatePublishStage.RunReleaseGate: return "运行发布验证";
                case HotUpdatePublishStage.PrepareUpload: return "准备上传";
                case HotUpdatePublishStage.UploadFiles: return "上传文件";
                case HotUpdatePublishStage.VerifyRemote: return "检查远端文件";
                case HotUpdatePublishStage.PublishVersion: return "更新资源版本";
                case HotUpdatePublishStage.Finalize: return "完成发布记录";
                default: return stage.ToString();
            }
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
                _scanError = $"扫描项目改动失败：{exception.GetType().Name}: {exception.Message}";
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

            HotUpdateEnvironmentProfile development = FindProfile(HotUpdateEnvironmentKind.Development);
            if (development != null &&
                string.Equals(development.PublishTarget, "LocalFolder", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(development.MainHostServer))
            {
                if (string.IsNullOrWhiteSpace(development.LocalFolderRoot))
                    development.LocalFolderRoot = HotUpdateEnvironmentProfile.GetDefaultLocalFolderRoot();
                if (string.IsNullOrWhiteSpace(development.RemoteRoot))
                    development.RemoteRoot = "hotupdate/Development";
                development.MainHostServer = HotUpdateEnvironmentProfile.CreateLocalFileHost(
                    development.LocalFolderRoot,
                    development.RemoteRoot);
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
