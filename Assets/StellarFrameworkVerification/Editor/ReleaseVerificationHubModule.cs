#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using StellarFramework.Editor;
using StellarFramework.Editor.Modules;
using StellarFramework.HotUpdate;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarFrameworkVerification.Editor
{
    [StellarTool("发布前自检", "Verification", 50)]
    internal sealed class ReleaseVerificationHubModule : ToolModule
    {
        private const string FrameworkValidationScenePath =
            "Assets/StellarFrameworkVerification/Scenes/FrameworkValidation_Playable.unity";

        private string _playerRootDirectory = string.Empty;
        private string _lastSummary = "等待执行。";
        private Vector2 _scroll;
        private readonly List<string> _details = new List<string>();

        public override string Icon => "d_Profiler.FirstFrame";
        public override string Description => "框架开发者使用的发布前自检与 Windows64 Player 热更冒烟验证入口。";

        public override void OnGUI()
        {
            Section("验证场景");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "FrameworkValidation 已迁入框架外验证区。它用于框架开发者做集中回归，不会打进用户安装包。",
                    MessageType.Info);

                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton("打开 FrameworkValidation 场景", GUILayout.Height(30)))
                    {
                        OpenFrameworkValidationScene();
                    }

                    if (GUILayout.Button("定位验证目录", GUILayout.Height(26)))
                    {
                        UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/StellarFrameworkVerification");
                        if (folder != null)
                        {
                            EditorGUIUtility.PingObject(folder);
                        }
                    }
                }
            }

            Section("Windows64 发布前自检");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "发布前自检会检查 HotUpdateSettings 严格模式、Manifest、DLL SHA、AOT metadata，以及 Addressables 目录结构。",
                    MessageType.None);

                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton("检查本地内置 AA", GUILayout.Height(30)))
                    {
                        ValidateWorkflow(AAWorkflowMode.LocalBuiltIn, BuildTarget.StandaloneWindows64);
                    }

                    if (PrimaryButton("检查远端热更 AA", GUILayout.Height(30)))
                    {
                        ValidateWorkflow(AAWorkflowMode.RemoteHotUpdate, BuildTarget.StandaloneWindows64);
                    }
                }
            }

            Section("Windows64 Player 冒烟验证");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    _playerRootDirectory = EditorGUILayout.TextField(
                        new GUIContent("Player 根目录", "选择 Windows64 Player 根目录，工具会检查 *_Data/StreamingAssets/aa。"),
                        _playerRootDirectory);

                    if (GUILayout.Button("...", GUILayout.Width(32)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("选择 Windows64 Player 根目录", _playerRootDirectory, string.Empty);
                        if (!string.IsNullOrWhiteSpace(selected))
                        {
                            _playerRootDirectory = selected.Replace('\\', '/');
                        }
                    }
                }

                if (PrimaryButton("检查 Windows64 Player 热更产物", GUILayout.Height(30)))
                {
                    ValidateWindows64Player();
                }
            }

            Section("最近结果");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(_lastSummary, MessageType.None);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180f));
                for (int i = 0; i < _details.Count; i++)
                {
                    EditorGUILayout.LabelField(_details[i], EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void OpenFrameworkValidationScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!File.Exists(ToAbsoluteProjectPath(FrameworkValidationScenePath)))
            {
                SetFailure("FrameworkValidation 场景不存在。", FrameworkValidationScenePath);
                return;
            }

            EditorSceneManager.OpenScene(FrameworkValidationScenePath, OpenSceneMode.Single);
            SetSuccess("已打开 FrameworkValidation 场景。", FrameworkValidationScenePath);
        }

        private void ValidateWorkflow(AAWorkflowMode mode, BuildTarget target)
        {
            ClearDetails();

            AAWorkflowWorkspaceStatus workspaceStatus = AAWorkflowWorkspaceInitializer.Evaluate(target);
            if (!workspaceStatus.IsReady)
            {
                SetFailure("AA 工作区尚未初始化。", string.Join(" | ", workspaceStatus.MissingItems));
                return;
            }

            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport strictSettingsReport = settings.Validate(strictProduction: true);
            AppendSettingsReport(strictSettingsReport);
            if (!strictSettingsReport.IsValid)
            {
                SetFailure("HotUpdateSettings 未通过生产严格校验。");
                return;
            }

            AAWorkflowConfigSet configSet = AAWorkflowConfigStore.ConfigSet;
            AAWorkflowConfig config = mode == AAWorkflowMode.LocalBuiltIn
                ? configSet.GetFirstConfig(AAWorkflowMode.LocalBuiltIn)
                : configSet.GetFirstConfig(AAWorkflowMode.RemoteHotUpdate);

            string publishDirectory = AAWorkflowPathUtility.GetPublishDirectory(config, target);
            string hotUpdateDllBytesPath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Assets",
                "GameHotUpdate",
                "Code",
                "HotUpdate.dll.bytes");

            AAHotUpdatePublishValidationReport report = mode == AAWorkflowMode.LocalBuiltIn
                ? AAWorkflowValidator.ValidatePublishDirectory(
                    publishDirectory,
                    hotUpdateDllBytesPath,
                    requireCatalogHash: false,
                    requireSettingsJson: true,
                    warnAboutMetaFiles: false)
                : AAWorkflowValidator.ValidatePublishDirectory(
                    publishDirectory,
                    hotUpdateDllBytesPath,
                    requireCatalogHash: true,
                    requireSettingsJson: false,
                    warnAboutMetaFiles: true);

            AppendValidationReport(report, publishDirectory);
            if (report.IsValid)
            {
                SetSuccess(mode == AAWorkflowMode.LocalBuiltIn
                    ? "本地内置 AA 发布前自检通过。"
                    : "远端热更 AA 发布前自检通过。");
                return;
            }

            SetFailure(mode == AAWorkflowMode.LocalBuiltIn
                ? "本地内置 AA 发布前自检失败。"
                : "远端热更 AA 发布前自检失败。");
        }

        private void ValidateWindows64Player()
        {
            ClearDetails();

            if (string.IsNullOrWhiteSpace(_playerRootDirectory))
            {
                SetFailure("请先选择 Windows64 Player 根目录。");
                return;
            }

            AAWorkflowConfig config = AAWorkflowConfig.CreateLocalBuiltInDefault();
            config.TestPlayerRootDirectory = _playerRootDirectory;
            string playerAaDirectory = AAWorkflowPathUtility.ResolveTestPlayerStreamingAssetsAaDirectory(
                config,
                BuildTarget.StandaloneWindows64);

            if (!AAWorkflowPathUtility.IsSafeTestPlayerStreamingAssetsAaDirectory(
                    playerAaDirectory,
                    BuildTarget.StandaloneWindows64))
            {
                SetFailure("所选目录不是安全的 Windows64 Player StreamingAssets/aa 目标。", playerAaDirectory);
                return;
            }

            string hotUpdateDllBytesPath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Assets",
                "GameHotUpdate",
                "Code",
                "HotUpdate.dll.bytes");

            AAHotUpdatePublishValidationReport report = AAWorkflowValidator.ValidatePublishDirectory(
                playerAaDirectory,
                hotUpdateDllBytesPath,
                requireCatalogHash: false,
                requireSettingsJson: true,
                warnAboutMetaFiles: false);

            AppendValidationReport(report, playerAaDirectory);
            if (report.IsValid)
            {
                SetSuccess("Windows64 Player 热更产物检查通过，可以进入启动冒烟验证。");
                return;
            }

            SetFailure("Windows64 Player 热更产物检查失败。");
        }

        private void AppendSettingsReport(HotUpdateSettingsValidationReport report)
        {
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                _details.Add("[警告] " + report.Warnings[i]);
            }

            for (int i = 0; i < report.Errors.Count; i++)
            {
                _details.Add("[错误] " + report.Errors[i]);
            }
        }

        private void AppendValidationReport(AAHotUpdatePublishValidationReport report, string publishDirectory)
        {
            _details.Add("目录: " + publishDirectory);

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                _details.Add("[警告] " + report.Warnings[i]);
            }

            for (int i = 0; i < report.Errors.Count; i++)
            {
                _details.Add("[错误] " + report.Errors[i]);
            }

            if (report.IsValid)
            {
                _details.Add("[通过] 目录结构和热更产物一致。");
            }
        }

        private void SetSuccess(string summary, string extra = null)
        {
            _lastSummary = summary;
            if (!string.IsNullOrWhiteSpace(extra))
            {
                _details.Add(extra);
            }

            Window.ShowNotification(new GUIContent(summary));
        }

        private void SetFailure(string summary, string extra = null)
        {
            _lastSummary = summary;
            if (!string.IsNullOrWhiteSpace(extra))
            {
                _details.Add(extra);
            }

            Window.ShowNotification(new GUIContent(summary));

            // 批处理（CI）模式下，失败必须以非零退出码结束，
            // 否则 CI 流水线无法感知"发布前自检"失败（假通过）。
            if (Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(1);
            }
        }

        private void ClearDetails()
        {
            _details.Clear();
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
