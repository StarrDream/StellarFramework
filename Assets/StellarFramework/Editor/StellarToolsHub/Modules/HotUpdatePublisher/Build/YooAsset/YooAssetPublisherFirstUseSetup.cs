using System;
using System.IO;
using System.Text.RegularExpressions;
using StellarFramework.Res.CodeUpdate.HybridCLR;
using UnityEditor;
using YooAsset.Editor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Creates a business package containing Publisher outputs while preserving existing packages.</summary>
    internal static class YooAssetPublisherFirstUseSetup
    {
        private const string PackageNamePrefsSuffix = ".packageName";
        private const string AssetOutputRootPrefsSuffix = ".assetOutputRoot";
        private const string PackageGroupName = "HotUpdateRuntimePayload";
        private const string GeneratedRoot = "Assets/HotUpdate/Generated";

        private static readonly Regex SafePackageName = new Regex(
            @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("Tools/StellarFramework/热更发布器/创建 YooAsset 资源收集配置")]
        private static void ConfigureRecommendedCollector()
        {
            string packageName = ReadSelectedPackageName();
            if (!IsSafeBusinessPackageName(packageName))
            {
                UnityEngine.Debug.LogError(
                    "[热更发布器] 请先填写有效的业务资源包名。验证用途的资源包不能用于发布。");
                return;
            }

            string generatedRoot = ReadHotUpdateAssetOutputRoot();
            if (!IsSafeAssetRoot(generatedRoot))
            {
                UnityEngine.Debug.LogError(
                    $"[热更发布器] 热更文件生成目录“{generatedRoot}”必须位于 Assets/ 下。本次没有更改 YooAsset 配置。");
                return;
            }

            string[] metadataPaths;
            try
            {
                HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
                metadataPaths = HotUpdateAotMetadataSelection.GetGeneratedAssetPaths(
                    generatedRoot, settings.AotMetadataKeys);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is ArgumentException)
            {
                UnityEngine.Debug.LogError(
                    "[热更发布器] HotUpdateSettings 中的 AOT 元数据选择无效：" + exception.Message);
                return;
            }

            AssetBundleCollectorSetting setting = AssetBundleCollectorSettingData.Setting;
            AssetBundleCollectorPackage package = null;
            for (int index = 0; index < setting.Packages.Count; index++)
            {
                if (string.Equals(setting.Packages[index].PackageName, packageName, StringComparison.Ordinal))
                {
                    package = setting.Packages[index];
                    break;
                }
            }

            if (package != null)
            {
                if (!package.EnableAddressable)
                {
                    // The runtime consumer loads resources by address. YooAsset emits empty
                    // addresses when this package option is disabled, even when collectors
                    // use AddressByFileName.
                    package.EnableAddressable = true;
                    AssetBundleCollectorSettingData.SaveFile();
                    UnityEngine.Debug.Log(
                        $"[热更发布器] 已为业务资源包“{packageName}”启用 Addressable，确保资源收集配置可以生成运行时地址。");
                }

                UnityEngine.Debug.Log(
                    $"[热更发布器] 业务资源包“{packageName}”已存在。原有分组和收集项已保留；请在资源收集器中确认热更输出路径。");
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
                return;
            }

            package = new AssetBundleCollectorPackage
            {
                PackageName = packageName,
                PackageDesc = "业务项目热更资源包；仅收集此处配置的热更文件。",
                EnableAddressable = true,
                SupportExtensionless = true,
                LocationToLower = false,
                IncludeAssetGUID = false,
                AutoCollectShaders = false,
                IgnoreRuleName = nameof(NormalIgnoreRule)
            };

            var group = new AssetBundleCollectorGroup
            {
                GroupName = PackageGroupName,
                GroupDesc = "由热更发布器生成的代码、清单和 AOT 元数据",
                ActiveRuleName = nameof(EnableGroup)
            };
            package.Groups.Add(group);

            AddCollector(group, generatedRoot + "/Manifest/HotUpdateManifest.json");
            AddCollector(group, generatedRoot + "/Code/HotUpdate.dll.bytes");
            foreach (string metadataPath in metadataPaths)
                AddCollector(group, metadataPath);

            setting.Packages.Add(package);
            AssetBundleCollectorSettingData.SaveFile();

            UnityEngine.Debug.Log(
                $"[热更发布器] 已创建业务资源包“{packageName}”，并添加代码清单、热更 DLL 和 AOT 元数据的收集路径。原有验证配置已保留。");
            EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
        }

        private static void AddCollector(AssetBundleCollectorGroup group, string assetPath)
        {
            group.Collectors.Add(new AssetBundleCollector
            {
                CollectPath = assetPath,
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = nameof(AddressByFileName),
                PackRuleName = nameof(PackSeparately),
                FilterRuleName = nameof(CollectAll)
            });
        }

        private static string ReadSelectedPackageName()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string suffix = projectRoot.Replace('\\', '/');
            string packageName = EditorPrefs.GetString(
                "StellarFramework.HotUpdatePublisher." + suffix + PackageNamePrefsSuffix,
                "DefaultPackage").Trim();
            return string.IsNullOrWhiteSpace(packageName)
                ? "DefaultPackage"
                : packageName;
        }

        private static string ReadHotUpdateAssetOutputRoot()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string suffix = projectRoot.Replace('\\', '/');
            string root = UnityEditor.EditorPrefs.GetString(
                "StellarFramework.HotUpdatePublisher." + suffix + AssetOutputRootPrefsSuffix,
                GeneratedRoot);
            return (root ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsSafeAssetRoot(string assetRoot)
        {
            if (string.IsNullOrWhiteSpace(assetRoot) ||
                !assetRoot.StartsWith("Assets/", StringComparison.Ordinal) ||
                assetRoot.Contains(":") || assetRoot.Contains("%"))
                return false;

            string[] segments = assetRoot.Split('/');
            for (int index = 0; index < segments.Length; index++)
                if (string.IsNullOrWhiteSpace(segments[index]) || segments[index] == "." || segments[index] == "..")
                    return false;

            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string assetsRoot = Path.GetFullPath(UnityEngine.Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(projectRoot,
                assetRoot.Replace('/', Path.DirectorySeparatorChar)))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeBusinessPackageName(string packageName)
        {
            return !string.IsNullOrWhiteSpace(packageName) &&
                   SafePackageName.IsMatch(packageName) &&
                   packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) < 0;
        }

    }
}
