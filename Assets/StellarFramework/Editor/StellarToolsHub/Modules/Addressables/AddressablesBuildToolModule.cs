using System;
using System.Collections.Generic;
using StellarFramework.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    public sealed class AddressablesLocalConfigurationReport
    {
        public readonly List<string> Messages = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool Success => Errors.Count == 0;
    }

    /// <summary>
    /// Addressables editor helpers owned by ResKit tooling.
    /// This surface deliberately does not implement content hot-update orchestration.
    /// </summary>
    public static class AddressablesBuildToolLogic
    {
        public static AddressableAssetSettings GetOrCreateSettings()
        {
            return AddressableAssetSettingsDefaultObject.GetSettings(true);
        }

        public static AddressablesLocalConfigurationReport InspectLocalConfiguration()
        {
            var report = new AddressablesLocalConfigurationReport();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                report.Errors.Add("Addressables Settings does not exist.");
                return report;
            }

            if (settings.BuildRemoteCatalog)
            {
                report.Errors.Add("Build Remote Catalog is enabled. StellarFramework Addressables support is local loading only; use YooAsset for content hot update.");
            }

            if (settings.groups == null)
            {
                report.Errors.Add("Addressables group list is unavailable.");
                return report;
            }

            int bundledGroupCount = 0;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null || string.Equals(group.Name, "Built In Data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                bundledGroupCount++;
                string buildVariable = schema.BuildPath.GetName(settings);
                string loadVariable = schema.LoadPath.GetName(settings);
                if (!string.Equals(buildVariable, AddressableAssetSettings.kLocalBuildPath, StringComparison.Ordinal) ||
                    !string.Equals(loadVariable, AddressableAssetSettings.kLocalLoadPath, StringComparison.Ordinal))
                {
                    report.Errors.Add(
                        $"Group '{group.Name}' is not using LocalBuildPath/LocalLoadPath. Build={buildVariable}, Load={loadVariable}");
                }
            }

            report.Messages.Add($"Bundled groups checked: {bundledGroupCount}.");
            return report;
        }

        public static AddressablesLocalConfigurationReport ApplyLocalBuiltInDefaults()
        {
            var report = new AddressablesLocalConfigurationReport();
            AddressableAssetSettings settings = GetOrCreateSettings();
            if (settings == null)
            {
                report.Errors.Add("Unable to create Addressables Settings.");
                return report;
            }

            settings.BuildRemoteCatalog = false;
            int changedGroups = 0;
            if (settings.groups != null)
            {
                foreach (AddressableAssetGroup group in settings.groups)
                {
                    if (group == null || string.Equals(group.Name, "Built In Data", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                    if (schema == null) continue;

                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                    EditorUtility.SetDirty(group);
                    changedGroups++;
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.ProfileModified, null, true, true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            report.Messages.Add($"Local Addressables defaults applied. Bundled groups updated: {changedGroups}.");
            return report;
        }

        public static AddressablesPlayerBuildResult BuildPlayerContent()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            return result;
        }
    }

    [StellarTool("Addressables", "资源管理", 1,
        RequiredAssemblyNames = new[] { "StellarFramework.ResKit.Addressables" })]
    public sealed class AddressablesBuildHubModule : ToolModule
    {
        private AddressablesLocalConfigurationReport _lastReport;
        private AddressablesPlayerBuildResult _lastBuildResult;

        public override string Icon => "d_Folder Icon";
        public override string Description =>
            "配置和构建本地 Addressables 内容。Addressables 在 StellarFramework 中只作为 ResKit 加载后端；内容热更新请使用 YooAsset。";

        public override void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Addressables 只负责 Load/Release 与本地内容构建。这里不提供 catalog 热更新、DLL 发布或 HybridCLR 编排。需要版本、下载、缓存和内容热更新时请使用 YooAsset；代码热更由 HybridCLRKit 独立负责。",
                MessageType.Info);

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            EditorGUILayout.LabelField("Settings", settings != null ? settings.name : "未创建");
            if (settings != null)
            {
                EditorGUILayout.LabelField("Active Profile", settings.activeProfileId ?? string.Empty);
                EditorGUILayout.LabelField("Build Remote Catalog", settings.BuildRemoteCatalog ? "开启（不符合当前框架约定）" : "关闭");
            }

            GUILayout.Space(8);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建 / 读取 Settings", GUILayout.Height(30)))
                {
                    AddressablesBuildToolLogic.GetOrCreateSettings();
                    _lastReport = AddressablesBuildToolLogic.InspectLocalConfiguration();
                }

                if (GUILayout.Button("应用本地内置配置", GUILayout.Height(30)))
                {
                    _lastReport = AddressablesBuildToolLogic.ApplyLocalBuiltInDefaults();
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("检查配置", GUILayout.Height(30)))
                {
                    _lastReport = AddressablesBuildToolLogic.InspectLocalConfiguration();
                }

                if (GUILayout.Button("构建 Player Content", GUILayout.Height(30)))
                {
                    _lastBuildResult = AddressablesBuildToolLogic.BuildPlayerContent();
                }

                if (GUILayout.Button("打开 Addressables Groups", GUILayout.Height(30)))
                {
                    EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                }
            }

            DrawReport();
        }

        private void DrawReport()
        {
            if (_lastReport != null)
            {
                GUILayout.Space(8);
                foreach (string message in _lastReport.Messages)
                {
                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }
                foreach (string warning in _lastReport.Warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                foreach (string error in _lastReport.Errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (_lastBuildResult != null)
            {
                MessageType type = string.IsNullOrEmpty(_lastBuildResult.Error) ? MessageType.Info : MessageType.Error;
                string text = string.IsNullOrEmpty(_lastBuildResult.Error)
                    ? "Addressables Player Content 构建完成。"
                    : "Addressables Player Content 构建失败：" + _lastBuildResult.Error;
                EditorGUILayout.HelpBox(text, type);
            }
        }
    }
}
