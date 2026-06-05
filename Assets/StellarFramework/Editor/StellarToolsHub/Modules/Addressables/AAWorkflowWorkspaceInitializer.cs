using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    public sealed class AAWorkflowWorkspaceStatus
    {
        public bool HasAddressablesSettings;
        public bool HasConfigAsset;
        public bool HasLocalProfile;
        public bool HasRemoteProfile;
        public bool HasHotUpdateSettingsAsset;
        public bool HasRuntimeSettingsAsset;
        public bool HasLocalResourcesGroup;
        public bool HasHotUpdateCodeGroup;
        public bool HasSeedEntries;
        public bool HasRecognizedRuntimeSettings;
        public bool HasRecognizedGroupPaths;
        public bool HasHybridClrPackage;
        public bool HasHybridClrDefine;
        public bool HasHybridClrInstalled;
        public bool HasHybridClrGeneratedAssets;
        public readonly List<string> MissingItems = new List<string>();

        public bool IsReady =>
            HasAddressablesSettings &&
            HasConfigAsset &&
            HasLocalProfile &&
            HasRemoteProfile &&
            HasHotUpdateSettingsAsset &&
            HasRuntimeSettingsAsset &&
            HasLocalResourcesGroup &&
            HasHotUpdateCodeGroup &&
            HasSeedEntries &&
            HasRecognizedRuntimeSettings &&
            HasRecognizedGroupPaths &&
            HasHybridClrPackage &&
            HasHybridClrDefine &&
            HasHybridClrInstalled &&
            HasHybridClrGeneratedAssets;
    }

    public static class AAWorkflowWorkspaceInitializer
    {
        public static readonly string LocalBuildPathVariableName = AddressableAssetSettings.kLocalBuildPath;
        public static readonly string LocalLoadPathVariableName = AddressableAssetSettings.kLocalLoadPath;
        public static readonly string RemoteBuildPathVariableName = AddressableAssetSettings.kRemoteBuildPath;
        public static readonly string RemoteLoadPathVariableName = AddressableAssetSettings.kRemoteLoadPath;

        internal const string HotUpdateSettingsAssetPath = "Assets/Resources/HotUpdateSettings.asset";
        internal const string ResKitRuntimeSettingsAssetPath = "Assets/Resources/ResKitRuntimeSettings.asset";
        private const string HybridClrDefineSymbol = "HYBRIDCLR_ENABLE";
        private const string HotUpdateDllAssetPath = "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes";
        private const string HotUpdateManifestAssetPath = "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json";
        private const string StreamingHotUpdateManifestAssetPath = "Assets/StreamingAssets/aa/HotUpdateManifest.json";
        private static readonly string[] DefaultHybridClrHotUpdateAssemblies = { "HotUpdate" };
        private static readonly string[] DefaultHybridClrPatchAotAssemblies =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine.CoreModule"
        };
        public const string LocalResourcesGroupName = "StellarFramework Local Resources";
        public const string HotUpdateCodeGroupName = "StellarFramework Hot Update Code";

        private static readonly SeedEntry[] LocalResourceSeeds =
        {
            new SeedEntry("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab"),
            new SeedEntry("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab", "hotupdate", "reskit-aa-test"),
            new SeedEntry("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab")
        };

        private static readonly SeedEntry[] HotUpdateSeeds =
        {
            new SeedEntry("Assets/GameHotUpdate/Code/HotUpdate.dll.bytes", "codehotfix", "hotupdate"),
            new SeedEntry("Assets/GameHotUpdate/Metadata/UnityEngine.CoreModule.dll.bytes", "codehotfix", "hotupdate"),
            new SeedEntry("Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes", "codehotfix", "hotupdate"),
            new SeedEntry("Assets/GameHotUpdate/Metadata/System.dll.bytes", "codehotfix", "hotupdate"),
            new SeedEntry("Assets/GameHotUpdate/Metadata/System.Core.dll.bytes", "codehotfix", "hotupdate")
        };

        private readonly struct SeedEntry
        {
            public SeedEntry(string assetPath, params string[] labels)
            {
                AssetPath = assetPath;
                Labels = labels ?? Array.Empty<string>();
            }

            public string AssetPath { get; }
            public string[] Labels { get; }
        }

        public static AAWorkflowWorkspaceStatus Evaluate(BuildTarget target)
        {
            AAWorkflowWorkspaceStatus status = new AAWorkflowWorkspaceStatus();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AAWorkflowConfig localConfig = null;
            AAWorkflowConfig remoteConfig = null;
            status.HasAddressablesSettings = settings != null;
            if (!status.HasAddressablesSettings)
            {
                status.MissingItems.Add("Addressables Settings");
            }

            AAWorkflowConfigSet configSet =
                AssetDatabase.LoadAssetAtPath<AAWorkflowConfigSet>(AAWorkflowConfigSet.DefaultAssetPath);
            status.HasConfigAsset = configSet != null;
            if (!status.HasConfigAsset)
            {
                status.MissingItems.Add("AAWorkflowConfigSet");
            }

            if (settings != null && configSet != null)
            {
                configSet.EnsureDefaults();
                localConfig = configSet.GetFirstConfig(AAWorkflowMode.LocalBuiltIn);
                remoteConfig = configSet.GetFirstConfig(AAWorkflowMode.RemoteHotUpdate);
                string localProfileName = string.IsNullOrWhiteSpace(localConfig.AddressablesProfileName)
                    ? "Stellar Local Built-in"
                    : localConfig.AddressablesProfileName.Trim();
                string remoteProfileName = string.IsNullOrWhiteSpace(remoteConfig.AddressablesProfileName)
                    ? "Stellar Remote HotUpdate"
                    : remoteConfig.AddressablesProfileName.Trim();

                status.HasLocalProfile = !string.IsNullOrEmpty(settings.profileSettings.GetProfileId(localProfileName));
                status.HasRemoteProfile = !string.IsNullOrEmpty(settings.profileSettings.GetProfileId(remoteProfileName));
            }

            if (!status.HasLocalProfile)
            {
                status.MissingItems.Add("本地内置 AA Profile");
            }

            if (!status.HasRemoteProfile)
            {
                status.MissingItems.Add("远端热更 AA Profile");
            }

            if (settings != null)
            {
                AddressableAssetGroup localGroup = settings.FindGroup(LocalResourcesGroupName);
                AddressableAssetGroup hotUpdateGroup = settings.FindGroup(HotUpdateCodeGroupName);
                status.HasLocalResourcesGroup = localGroup != null;
                status.HasHotUpdateCodeGroup = hotUpdateGroup != null;
                status.HasSeedEntries = status.HasLocalResourcesGroup &&
                                        status.HasHotUpdateCodeGroup &&
                                        HasSeedEntries(localGroup, LocalResourceSeeds) &&
                                        HasSeedEntries(hotUpdateGroup, HotUpdateSeeds);
                if (status.HasLocalResourcesGroup && status.HasHotUpdateCodeGroup)
                {
                    status.HasRecognizedGroupPaths =
                        AreRequiredGroupPathsConfigured(AAAddressablesConfigurator.GetGroupPathStatuses(null));
                }
            }

            if (!status.HasLocalResourcesGroup)
            {
                status.MissingItems.Add("StellarFramework Local Resources Group");
            }

            if (!status.HasHotUpdateCodeGroup)
            {
                status.MissingItems.Add("StellarFramework Hot Update Code Group");
            }

            if (status.HasLocalResourcesGroup && status.HasHotUpdateCodeGroup && !status.HasSeedEntries)
            {
                status.MissingItems.Add("默认 Addressables 条目");
            }

            HotUpdate.HotUpdateSettings hotUpdateSettings =
                FindResourcesAsset<HotUpdate.HotUpdateSettings>("t:HotUpdateSettings");
            status.HasHotUpdateSettingsAsset = hotUpdateSettings != null;
            if (!status.HasHotUpdateSettingsAsset)
            {
                status.MissingItems.Add("HotUpdateSettings.asset");
            }

            status.HasRuntimeSettingsAsset = FindResourcesAsset<Res.ResKitRuntimeSettings>("t:ResKitRuntimeSettings") != null;
            if (!status.HasRuntimeSettingsAsset)
            {
                status.MissingItems.Add("ResKitRuntimeSettings.asset");
            }

            if (hotUpdateSettings != null && localConfig != null && remoteConfig != null)
            {
                status.HasRecognizedRuntimeSettings = IsRuntimeSettingsConfiguredForAnyWorkflow(
                    hotUpdateSettings,
                    target,
                    localConfig,
                    remoteConfig);
            }

            if (status.HasHotUpdateSettingsAsset && !status.HasRecognizedRuntimeSettings)
            {
                status.MissingItems.Add("默认运行时热更配置");
            }

            if (status.HasLocalResourcesGroup && status.HasHotUpdateCodeGroup && !status.HasRecognizedGroupPaths)
            {
                status.MissingItems.Add("默认 Group 路径");
            }

            status.HasHybridClrPackage = HasHybridClrPackage();
            status.HasHybridClrDefine = IsScriptingDefineEnabled(HybridClrDefineSymbol);
            status.HasHybridClrInstalled = status.HasHybridClrPackage && IsHybridClrInstalledAndCurrent();
            status.HasHybridClrGeneratedAssets = HasHybridClrGeneratedAssets();

            if (!status.HasHybridClrPackage)
            {
                status.MissingItems.Add("HybridCLR package");
            }
            else
            {
                if (!status.HasHybridClrDefine)
                {
                    status.MissingItems.Add("HYBRIDCLR_ENABLE define");
                }

                if (!status.HasHybridClrInstalled)
                {
                    status.MissingItems.Add("HybridCLR Install");
                }
            }

            if (!status.HasHybridClrGeneratedAssets)
            {
                status.MissingItems.Add("HybridCLR 热更产物");
            }

            return status;
        }

        public static bool TryInitialize(BuildTarget target, out List<string> messages, out List<string> errors)
        {
            messages = new List<string>();
            errors = new List<string>();

            try
            {
                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null)
                {
                    errors.Add("未能创建 Addressables Settings。");
                    return false;
                }

                messages.Add("已创建或加载 Addressables Settings。");

                AAWorkflowConfigStore.Reload();
                AAWorkflowConfigSet configSet = AAWorkflowConfigStore.ConfigSet;
                if (configSet == null)
                {
                    errors.Add("未能创建或加载 AAWorkflowConfigSet。");
                    return false;
                }

                configSet.EnsureDefaults();
                AAWorkflowConfigStore.Save();
                messages.Add("已创建或加载 AAWorkflowConfigSet 默认配置。");

                EnsureDefaultLabels(settings, messages);
                if (!EnsureDefaultGroupsAndEntries(settings, out List<string> groupMessages, out List<string> groupErrors))
                {
                    messages.AddRange(groupMessages);
                    errors.AddRange(groupErrors);
                    if (errors.Count == 0)
                    {
                        errors.Add("默认 Addressables Group 初始化失败。");
                    }

                    return false;
                }

                messages.AddRange(groupMessages);

                AAWorkflowConfig localConfig = configSet.GetFirstConfig(AAWorkflowMode.LocalBuiltIn);
                AAWorkflowConfig remoteConfig = configSet.GetFirstConfig(AAWorkflowMode.RemoteHotUpdate);
                AAWorkflowConfig selectedConfig = configSet.SelectedConfig ?? localConfig;

                AAHotUpdatePublishRunReport localReport = new AAHotUpdatePublishRunReport();
                AAHotUpdatePublishRunReport remoteReport = new AAHotUpdatePublishRunReport();
                AAHotUpdatePublishRunReport selectedReport = new AAHotUpdatePublishRunReport();

                if (!AAAddressablesConfigurator.TryApply(localConfig, target, localReport))
                {
                    errors.AddRange(localReport.Errors);
                    if (errors.Count == 0)
                    {
                        errors.Add("本地内置 AA 配置初始化失败。");
                    }

                    return false;
                }

                if (!AAAddressablesConfigurator.TryApply(remoteConfig, target, remoteReport))
                {
                    errors.AddRange(remoteReport.Errors);
                    if (errors.Count == 0)
                    {
                        errors.Add("远端热更 AA 配置初始化失败。");
                    }

                    return false;
                }

                if (!AAAddressablesConfigurator.TryApply(selectedConfig, target, selectedReport))
                {
                    errors.AddRange(selectedReport.Errors);
                    if (errors.Count == 0)
                    {
                        errors.Add("恢复当前 AA 配置失败。");
                    }

                    return false;
                }

                messages.Add("已创建默认 Local/Remote AA Profile，并应用当前工作流的 Group 路径。");

                EnsureResourcesFolderExists();
                EnsureResKitRuntimeSettingsAsset(messages);
                EnsureHotUpdateSettingsAsset(messages);

                if (!AAWorkflowRuntimeSettingsWriter.TryWrite(selectedConfig, target, out string runtimeMessage))
                {
                    errors.Add(runtimeMessage);
                    return false;
                }

                messages.Add(runtimeMessage);

                if (!TryEnsureHybridClrReady(target, messages, errors))
                {
                    return false;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(exception.GetBaseException().Message);
                return false;
            }
        }

        public static bool IsRuntimeSettingsConfiguredForAnyWorkflow(
            HotUpdate.HotUpdateSettings settings,
            BuildTarget target,
            params AAWorkflowConfig[] configs)
        {
            if (settings == null || configs == null || configs.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < configs.Length; i++)
            {
                if (IsRuntimeSettingsConfiguredForWorkflow(settings, configs[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AreRequiredGroupPathsConfigured(IEnumerable<AAGroupPathStatus> statuses)
        {
            if (statuses == null)
            {
                return false;
            }

            bool hasLocalResourcesGroup = false;
            bool hasHotUpdateGroup = false;
            foreach (AAGroupPathStatus status in statuses)
            {
                if (status == null)
                {
                    continue;
                }

                if (string.Equals(status.GroupName, LocalResourcesGroupName, StringComparison.Ordinal))
                {
                    hasLocalResourcesGroup = IsRecognizedGroupPathMode(status);
                    continue;
                }

                if (string.Equals(status.GroupName, HotUpdateCodeGroupName, StringComparison.Ordinal))
                {
                    hasHotUpdateGroup = IsRecognizedGroupPathMode(status);
                }
            }

            return hasLocalResourcesGroup && hasHotUpdateGroup;
        }

        private static void EnsureResourcesFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureResKitRuntimeSettingsAsset(List<string> messages)
        {
            Res.ResKitRuntimeSettings settings = FindResourcesAsset<Res.ResKitRuntimeSettings>("t:ResKitRuntimeSettings");
            if (settings != null)
            {
                messages.Add("已检测到 ResKitRuntimeSettings.asset。");
                return;
            }

            settings = ScriptableObject.CreateInstance<Res.ResKitRuntimeSettings>();
            AssetDatabase.CreateAsset(settings, ResKitRuntimeSettingsAssetPath);
            messages.Add("已创建 ResKitRuntimeSettings.asset。");
        }

        private static void EnsureHotUpdateSettingsAsset(List<string> messages)
        {
            HotUpdate.HotUpdateSettings settings = FindResourcesAsset<HotUpdate.HotUpdateSettings>("t:HotUpdateSettings");
            if (settings != null)
            {
                messages.Add("已检测到 HotUpdateSettings.asset。");
                return;
            }

            settings = ScriptableObject.CreateInstance<HotUpdate.HotUpdateSettings>();
            AssetDatabase.CreateAsset(settings, HotUpdateSettingsAssetPath);
            messages.Add("已创建 HotUpdateSettings.asset。");
        }

        private static void EnsureDefaultLabels(AddressableAssetSettings settings, List<string> messages)
        {
            EnsureLabel(settings, "default");
            EnsureLabel(settings, "hotupdate");
            EnsureLabel(settings, "reskit-aa-test");
            EnsureLabel(settings, "codehotfix");
            messages.Add("已确认默认 Addressables Labels。");
        }

        private static void EnsureLabel(AddressableAssetSettings settings, string label)
        {
            if (settings == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            settings.AddLabel(label.Trim());
        }

        private static bool EnsureDefaultGroupsAndEntries(
            AddressableAssetSettings settings,
            out List<string> messages,
            out List<string> errors)
        {
            messages = new List<string>();
            errors = new List<string>();
            if (settings == null)
            {
                errors.Add("Addressables Settings 为空。");
                return false;
            }

            try
            {
                AddressableAssetGroup localGroup = FindOrCreateGroup(settings, LocalResourcesGroupName, true);
                AddressableAssetGroup hotUpdateGroup = FindOrCreateGroup(settings, HotUpdateCodeGroupName, false);

                EnsureEntries(settings, localGroup, LocalResourceSeeds, messages);
                EnsureEntries(settings, hotUpdateGroup, HotUpdateSeeds, messages);

                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(exception.GetBaseException().Message);
                return false;
            }
        }

        private static AddressableAssetGroup FindOrCreateGroup(
            AddressableAssetSettings settings,
            string groupName,
            bool setAsDefaultGroup)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null)
            {
                return group;
            }

            group = settings.CreateGroup(
                groupName,
                setAsDefaultGroup,
                false,
                true,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));

            BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledSchema != null)
            {
                bundledSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bundledSchema.IncludeInBuild = true;
                bundledSchema.IncludeAddressInCatalog = true;
                bundledSchema.IncludeGUIDInCatalog = true;
                bundledSchema.IncludeLabelsInCatalog = true;
                EditorUtility.SetDirty(bundledSchema);
            }

            ContentUpdateGroupSchema contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdateSchema != null)
            {
                contentUpdateSchema.StaticContent = false;
                EditorUtility.SetDirty(contentUpdateSchema);
            }

            EditorUtility.SetDirty(group);
            return group;
        }

        private static void EnsureEntries(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            SeedEntry[] seeds,
            List<string> messages)
        {
            if (group == null || seeds == null)
            {
                return;
            }

            for (int i = 0; i < seeds.Length; i++)
            {
                SeedEntry seed = seeds[i];
                string guid = AssetDatabase.AssetPathToGUID(seed.AssetPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    messages.Add("跳过缺失资源：" + seed.AssetPath);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group, false, true);
                if (entry == null)
                {
                    messages.Add("创建 Addressables 条目失败：" + seed.AssetPath);
                    continue;
                }

                entry.SetAddress(seed.AssetPath, true);
                for (int labelIndex = 0; labelIndex < seed.Labels.Length; labelIndex++)
                {
                    string label = seed.Labels[labelIndex];
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    entry.SetLabel(label.Trim(), true, true, true);
                }

                EditorUtility.SetDirty(group);
            }
        }

        private static bool HasSeedEntries(AddressableAssetGroup group, SeedEntry[] seeds)
        {
            if (group == null || seeds == null)
            {
                return false;
            }

            for (int i = 0; i < seeds.Length; i++)
            {
                string guid = AssetDatabase.AssetPathToGUID(seeds[i].AssetPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                if (group.GetAssetEntry(guid) == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static T FindResourcesAsset<T>(string filter) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.Contains("/Resources/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static bool IsRuntimeSettingsConfiguredForWorkflow(
            HotUpdate.HotUpdateSettings settings,
            AAWorkflowConfig config,
            BuildTarget target)
        {
            if (settings == null || config == null)
            {
                return false;
            }

            return string.Equals(
                       NormalizePathOrUrl(settings.HotUpdateManifestPathOrUrl),
                       NormalizePathOrUrl(AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(config, target)),
                       StringComparison.OrdinalIgnoreCase) &&
                   settings.HotUpdateManifestFallbackToStreamingAssets == config.AllowStreamingAssetsFallback &&
                   settings.HotUpdateManifestFallbackToResources == config.AllowResourcesFallback &&
                   settings.AddressablesUpdateCatalogsOnCheck == (config.Mode == AAWorkflowMode.RemoteHotUpdate);
        }

        private static bool IsRecognizedGroupPathMode(AAGroupPathStatus status)
        {
            if (status == null || !status.HasBundledSchema || !status.Included)
            {
                return false;
            }

            bool localMode =
                string.Equals(status.BuildPathVariable, LocalBuildPathVariableName, StringComparison.Ordinal) &&
                string.Equals(status.LoadPathVariable, LocalLoadPathVariableName, StringComparison.Ordinal);
            bool remoteMode =
                string.Equals(status.BuildPathVariable, RemoteBuildPathVariableName, StringComparison.Ordinal) &&
                string.Equals(status.LoadPathVariable, RemoteLoadPathVariableName, StringComparison.Ordinal);
            return localMode || remoteMode;
        }

        private static string NormalizePathOrUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');
        }

        private static bool HasHybridClrPackage()
        {
            return Type.GetType("HybridCLR.Editor.Installer.InstallerController, HybridCLR.Editor") != null
                   && Type.GetType("HybridCLR.Editor.Commands.PrebuildCommand, HybridCLR.Editor") != null
                   && Type.GetType("HybridCLR.Editor.SettingsUtil, HybridCLR.Editor") != null;
        }

        private static bool IsHybridClrInstalledAndCurrent()
        {
            if (!TryCreateHybridClrInstaller(out object installer, out Type installerType, out _))
            {
                return false;
            }

            return IsHybridClrInstalledAndCurrent(installer, installerType);
        }

        private static bool HasHybridClrGeneratedAssets()
        {
            if (!File.Exists(ToAbsoluteProjectPath(HotUpdateDllAssetPath)) ||
                !File.Exists(ToAbsoluteProjectPath(HotUpdateManifestAssetPath)) ||
                !File.Exists(ToAbsoluteProjectPath(StreamingHotUpdateManifestAssetPath)))
            {
                return false;
            }

            string[] requiredMetadataPaths =
            {
                "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes",
                "Assets/GameHotUpdate/Metadata/System.dll.bytes",
                "Assets/GameHotUpdate/Metadata/System.Core.dll.bytes",
                "Assets/GameHotUpdate/Metadata/UnityEngine.CoreModule.dll.bytes"
            };

            for (int i = 0; i < requiredMetadataPaths.Length; i++)
            {
                if (!File.Exists(ToAbsoluteProjectPath(requiredMetadataPaths[i])))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryEnsureHybridClrReady(
            BuildTarget target,
            List<string> messages,
            List<string> errors)
        {
            if (!HasHybridClrPackage())
            {
                errors.Add("未检测到 HybridCLR.Editor。请先确认 HybridCLR 包已安装完成。");
                return false;
            }

            if (TryAddDefineForSelectedBuildTarget(HybridClrDefineSymbol, out string defineMessage) &&
                !string.IsNullOrWhiteSpace(defineMessage))
            {
                messages.Add(defineMessage);
            }

            if (!TryEnableHybridClr(out string enableError))
            {
                errors.Add(enableError);
                return false;
            }

            if (!TryConfigureHybridClrSettings(messages, out string configureError))
            {
                errors.Add(configureError);
                return false;
            }

            if (!TryCreateHybridClrInstaller(out object installer, out Type installerType, out string installerError))
            {
                errors.Add(installerError);
                return false;
            }

            bool wasInstalledAndCurrent = IsHybridClrInstalledAndCurrent(installer, installerType);
            if (!wasInstalledAndCurrent)
            {
                if (!TryInstallHybridClr(installerType, messages, out string installError))
                {
                    errors.Add(installError);
                    return false;
                }
            }
            else
            {
                messages.Add("已检测到 HybridCLR Install 已完成。");
            }

            bool shouldGenerateAndExport = !HasHybridClrGeneratedAssets() || !wasInstalledAndCurrent;
            if (!shouldGenerateAndExport)
            {
                messages.Add("已检测到现有 HybridCLR 热更产物，跳过重复生成。");
                return true;
            }

            if (!TryGenerateHybridClr(messages, out string generateError))
            {
                errors.Add(generateError);
                return false;
            }

            HybridCLRHotUpdateExportReport exportReport =
                HybridCLRHotUpdateAssetExporter.ExportGeneratedAssets(target);
            for (int i = 0; i < exportReport.Warnings.Count; i++)
            {
                messages.Add("[HybridCLR] " + exportReport.Warnings[i]);
            }

            if (!exportReport.Success)
            {
                for (int i = 0; i < exportReport.Errors.Count; i++)
                {
                    errors.Add("[HybridCLR] " + exportReport.Errors[i]);
                }

                if (exportReport.Errors.Count == 0)
                {
                    errors.Add("HybridCLR 热更 DLL / Manifest 导出失败。");
                }

                return false;
            }

            messages.Add("已导出 HybridCLR 热更 DLL、AOT metadata 与 HotUpdateManifest.json。");
            return true;
        }

        private static bool TryAddDefineForSelectedBuildTarget(string define, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(define))
            {
                return false;
            }

            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown)
            {
                message = "当前 BuildTargetGroup 未知，无法自动写入 HYBRIDCLR_ENABLE。";
                return false;
            }

#if UNITY_2021_2_OR_NEWER
            UnityEditor.Build.NamedBuildTarget namedBuildTarget =
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            string current = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            string merged = MergeDefineSymbols(current, define);
            if (string.Equals(current, merged, StringComparison.Ordinal))
            {
                return true;
            }

            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, merged);
#else
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            string merged = MergeDefineSymbols(current, define);
            if (string.Equals(current, merged, StringComparison.Ordinal))
            {
                return true;
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, merged);
#endif
            message = "已为当前 BuildTarget 写入 HYBRIDCLR_ENABLE。";
            return true;
        }

        private static string MergeDefineSymbols(string currentSymbols, params string[] requiredSymbols)
        {
            List<string> symbols = string.IsNullOrWhiteSpace(currentSymbols)
                ? new List<string>()
                : currentSymbols
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

            if (requiredSymbols != null)
            {
                for (int i = 0; i < requiredSymbols.Length; i++)
                {
                    string symbol = requiredSymbols[i];
                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        continue;
                    }

                    string trimmed = symbol.Trim();
                    if (!symbols.Contains(trimmed))
                    {
                        symbols.Add(trimmed);
                    }
                }
            }

            return string.Join(";", symbols);
        }

        private static bool IsScriptingDefineEnabled(string define)
        {
            if (string.IsNullOrWhiteSpace(define))
            {
                return false;
            }

            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown)
            {
                return false;
            }

#if UNITY_2021_2_OR_NEWER
            UnityEditor.Build.NamedBuildTarget namedBuildTarget =
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
            return defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(item => string.Equals(item.Trim(), define, StringComparison.Ordinal));
        }

        private static bool TryEnableHybridClr(out string error)
        {
            error = string.Empty;
            Type settingsUtilType = Type.GetType("HybridCLR.Editor.SettingsUtil, HybridCLR.Editor");
            PropertyInfo enableProperty = settingsUtilType?.GetProperty("Enable", BindingFlags.Public | BindingFlags.Static);
            if (enableProperty == null || !enableProperty.CanWrite)
            {
                error = "无法找到 HybridCLR.Editor.SettingsUtil.Enable。";
                return false;
            }

            try
            {
                enableProperty.SetValue(null, true, null);
                return true;
            }
            catch (Exception exception)
            {
                error = "启用 HybridCLR 设置失败: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryConfigureHybridClrSettings(List<string> messages, out string error)
        {
            error = string.Empty;
            Type settingsType = Type.GetType("HybridCLR.Editor.Settings.HybridCLRSettings, HybridCLR.Editor");
            MethodInfo loadMethod = settingsType?.GetMethod("LoadOrCreate", BindingFlags.Public | BindingFlags.Static);
            MethodInfo saveMethod = settingsType?.GetMethod("Save", BindingFlags.Public | BindingFlags.Static);
            if (settingsType == null || loadMethod == null || saveMethod == null)
            {
                error = "无法读取 HybridCLRSettings。";
                return false;
            }

            try
            {
                object settings = loadMethod.Invoke(null, null);
                if (settings == null)
                {
                    error = "HybridCLRSettings.LoadOrCreate 返回为空。";
                    return false;
                }

                SetHybridClrBoolField(settingsType, settings, "enable", true);
                EnsureHybridClrStringArrayField(
                    settingsType,
                    settings,
                    "hotUpdateAssemblies",
                    DefaultHybridClrHotUpdateAssemblies,
                    replaceWhenEmpty: true);
                EnsureHybridClrStringArrayField(
                    settingsType,
                    settings,
                    "patchAOTAssemblies",
                    DefaultHybridClrPatchAotAssemblies,
                    replaceWhenEmpty: true);

                saveMethod.Invoke(null, null);
                messages.Add("已写入 HybridCLRSettings 默认热更程序集与 AOT 配置。");
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = "写入 HybridCLRSettings 失败: " + exception.GetBaseException().Message;
                return false;
            }
            catch (Exception exception)
            {
                error = "写入 HybridCLRSettings 失败: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryCreateHybridClrInstaller(out object installer, out Type installerType, out string error)
        {
            installer = null;
            error = string.Empty;
            installerType = Type.GetType("HybridCLR.Editor.Installer.InstallerController, HybridCLR.Editor");
            if (installerType == null)
            {
                error = "无法找到 HybridCLR.Editor.Installer.InstallerController。";
                return false;
            }

            try
            {
                installer = Activator.CreateInstance(installerType);
                return installer != null;
            }
            catch (Exception exception)
            {
                error = "创建 HybridCLR InstallerController 失败: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool IsHybridClrInstalledAndCurrent(object installer, Type installerType)
        {
            if (installer == null || installerType == null)
            {
                return false;
            }

            MethodInfo hasInstalledMethod = installerType.GetMethod("HasInstalledHybridCLR",
                BindingFlags.Public | BindingFlags.Instance);
            bool hasInstalled = hasInstalledMethod != null && (bool)hasInstalledMethod.Invoke(installer, null);
            if (!hasInstalled)
            {
                return false;
            }

            PropertyInfo packageVersionProperty = installerType.GetProperty("PackageVersion",
                BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo installedVersionProperty = installerType.GetProperty("InstalledLibil2cppVersion",
                BindingFlags.Public | BindingFlags.Instance);
            string packageVersion = packageVersionProperty?.GetValue(installer, null) as string;
            string installedVersion = installedVersionProperty?.GetValue(installer, null) as string;

            if (string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(installedVersion))
            {
                return hasInstalled;
            }

            return string.Equals(packageVersion.Trim(), installedVersion.Trim(), StringComparison.Ordinal);
        }

        private static bool TryInstallHybridClr(Type installerType, List<string> messages, out string error)
        {
            error = string.Empty;
            try
            {
                object installer = Activator.CreateInstance(installerType);
                MethodInfo installMethod = installerType.GetMethod("InstallDefaultHybridCLR",
                    BindingFlags.Public | BindingFlags.Instance);
                if (installer == null || installMethod == null)
                {
                    error = "无法调用 HybridCLR 默认安装流程。";
                    return false;
                }

                messages.Add("检测到 HybridCLR 尚未安装或版本不匹配，开始自动执行 Install。");
                installMethod.Invoke(installer, null);

                object verifyInstaller = Activator.CreateInstance(installerType);
                if (!IsHybridClrInstalledAndCurrent(verifyInstaller, installerType))
                {
                    error = "HybridCLR Install 执行后仍未通过安装校验。";
                    return false;
                }

                messages.Add("HybridCLR Install 已完成。");
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = "HybridCLR Install 失败: " + exception.GetBaseException().Message;
                return false;
            }
            catch (Exception exception)
            {
                error = "HybridCLR Install 失败: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryGenerateHybridClr(List<string> messages, out string error)
        {
            error = string.Empty;
            Type prebuildCommandType = Type.GetType("HybridCLR.Editor.Commands.PrebuildCommand, HybridCLR.Editor");
            MethodInfo generateAllMethod = prebuildCommandType?.GetMethod("GenerateAll",
                BindingFlags.Public | BindingFlags.Static);
            if (generateAllMethod == null)
            {
                error = "无法找到 HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll。";
                return false;
            }

            try
            {
                messages.Add("开始执行 HybridCLR Generate/All。");
                generateAllMethod.Invoke(null, null);
                messages.Add("HybridCLR Generate/All 执行完成。");
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = "HybridCLR Generate/All 失败: " + exception.GetBaseException().Message;
                return false;
            }
            catch (Exception exception)
            {
                error = "HybridCLR Generate/All 失败: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void SetHybridClrBoolField(Type settingsType, object settings, string fieldName, bool value)
        {
            FieldInfo field = settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(settings, value);
            }
        }

        private static void EnsureHybridClrStringArrayField(
            Type settingsType,
            object settings,
            string fieldName,
            IReadOnlyList<string> requiredValues,
            bool replaceWhenEmpty)
        {
            FieldInfo field = settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(string[]))
            {
                return;
            }

            string[] current = field.GetValue(settings) as string[];
            List<string> merged = current == null
                ? new List<string>()
                : current.Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

            if (merged.Count == 0 && replaceWhenEmpty)
            {
                merged = requiredValues
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            else
            {
                for (int i = 0; i < requiredValues.Count; i++)
                {
                    string value = requiredValues[i];
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    string trimmed = value.Trim();
                    if (!merged.Contains(trimmed))
                    {
                        merged.Add(trimmed);
                    }
                }
            }

            field.SetValue(settings, merged.ToArray());
        }
    }
}
