using UnityEditor;
using UnityEngine;

namespace StellarFrameworkInstaller
{
    internal sealed class StellarFrameworkInstallerWindow : EditorWindow
    {
        private readonly StellarFrameworkInstallerState _state = new StellarFrameworkInstallerState();
        private readonly StellarFrameworkInstallOrchestrator _orchestrator = new StellarFrameworkInstallOrchestrator();
        private Vector2 _scroll;
        private bool _advancedFoldout;

        [MenuItem(StellarFrameworkInstallerConstants.MenuPath)]
        public static void Open()
        {
            StellarFrameworkInstallerWindow window = GetWindow<StellarFrameworkInstallerWindow>("Stellar Installer");
            window.minSize = new Vector2(620, 460);
            window.Show();
        }

        private void Update()
        {
            _orchestrator.Tick(_state);
            if (_state.IsBusy)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();
            DrawMainActions();
            DrawAdvancedActions();
            DrawStatus();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("StellarFramework Installer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "导入 Installer 后先装基础框架；需要资源和代码热更新时，再装 AA + HybridCLR 热更新能力。默认只补缺失项，不覆盖已有项目配置。",
                MessageType.Info);
        }

        private void DrawMainActions()
        {
            using (new GUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("安装阶段", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(_state.IsBusy))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "安装基础框架",
                                "安装 Newtonsoft.Json、UniTask，导入 StellarFrameworkCore payload，并打开 ToolsHub Quick Start。"),
                            GUILayout.Height(36)))
                    {
                        _orchestrator.StartBasicInstall(_state);
                    }

                    if (GUILayout.Button(
                            new GUIContent(
                                "安装 AA + HybridCLR 热更新能力",
                                "安装 Addressables、HybridCLR，写入宏，创建 GameHotUpdate 目录、默认 Manifest、ResKitRuntimeSettings 和 AA 工作流默认配置。"),
                            GUILayout.Height(36)))
                    {
                        _orchestrator.StartHotUpdateInstall(_state);
                    }
                }
            }
        }

        private void DrawAdvancedActions()
        {
            _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "高级 / 离线 / 导入", true);
            if (!_advancedFoldout)
            {
                return;
            }

            using (new GUILayout.VerticalScope("box"))
            {
                using (new EditorGUI.DisabledScope(_state.IsBusy))
                {
                    if (GUILayout.Button("选择并导入本地 Core unitypackage", GUILayout.Height(24)))
                    {
                        string path = EditorUtility.OpenFilePanel("选择 StellarFrameworkCore.unitypackage", "", "unitypackage");
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            StellarFrameworkCoreImporter.ImportUnityPackageIfExists(path, _state.Report);
                        }
                    }

                    if (GUILayout.Button("选择并导入本地 HotUpdate Addon unitypackage", GUILayout.Height(24)))
                    {
                        string path = EditorUtility.OpenFilePanel("选择 StellarFrameworkHotUpdateAddon.unitypackage", "", "unitypackage");
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            StellarFrameworkCoreImporter.ImportUnityPackageIfExists(path, _state.Report);
                        }
                    }

                    if (GUILayout.Button("备份当前热更新相关配置", GUILayout.Height(24)))
                    {
                        StellarFrameworkInstallerBackupUtility.BackupDefaultHotUpdateTargets(_state.Report);
                        AssetDatabase.Refresh();
                    }

                    if (GUILayout.Button("打开 ToolsHub", GUILayout.Height(24)))
                    {
                        if (!EditorApplication.ExecuteMenuItem(StellarFrameworkInstallerConstants.ToolsHubMenuPath))
                        {
                            _state.Report.AddWarning("ToolsHub 菜单暂不可用，请确认 Core 已导入且编译完成。");
                        }
                    }
                }
            }
        }

        private void DrawStatus()
        {
            GUILayout.Space(12);
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_state.Phase.ToString());
            EditorGUILayout.HelpBox(_state.Report.Summary, _state.Report.IsValid ? MessageType.Info : MessageType.Error);

            DrawReportList("消息", _state.Report.Messages, MessageType.Info);
            DrawReportList("警告", _state.Report.Warnings, MessageType.Warning);
            DrawReportList("错误", _state.Report.Errors, MessageType.Error);
        }

        private static void DrawReportList(string title, System.Collections.Generic.IReadOnlyList<string> values, MessageType type)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < values.Count; i++)
            {
                EditorGUILayout.HelpBox(values[i], type);
            }
        }
    }
}
