using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using YooAsset.Editor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Creates a separate business package while preserving all existing YooAsset packages.</summary>
    internal static class YooAssetPublisherFirstUseSetup
    {
        private const string PackageNamePrefsSuffix = ".packageName";
        private const string PackageGroupName = "HotUpdateRuntimePayload";
        private const string GeneratedRoot = "Assets/HotUpdatePublisherConsumerE2E/Generated";
        private const string ConsumerBehaviorPath = "Assets/HotUpdatePublisherConsumerE2E/Content/HotUpdateBehavior.txt";

        private static readonly Regex SafePackageName = new Regex(
            @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("Tools/StellarFramework/HotUpdate Publisher/Configure Recommended YooAsset Collector")]
        private static void ConfigureRecommendedCollector()
        {
            string packageName = ReadSelectedPackageName();
            if (!IsSafeBusinessPackageName(packageName))
            {
                UnityEngine.Debug.LogError(
                    "[HotUpdatePublisher] Enter a valid business Package name first. Verification-only package names are rejected.");
                return;
            }

            string consumerBehaviorAbsolutePath = ToAbsoluteAssetPath(ConsumerBehaviorPath);
            Directory.CreateDirectory(Path.GetDirectoryName(consumerBehaviorAbsolutePath));
            if (!File.Exists(consumerBehaviorAbsolutePath))
            {
                File.WriteAllText(consumerBehaviorAbsolutePath,
                    "publisher-consumer-behavior=v1\n");
                AssetDatabase.ImportAsset(ConsumerBehaviorPath, ImportAssetOptions.ForceUpdate);
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
                UnityEngine.Debug.Log(
                    $"[HotUpdatePublisher] Business package '{packageName}' already exists. Existing configuration was preserved; review it in the Collector window.");
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
                return;
            }

            package = new AssetBundleCollectorPackage
            {
                PackageName = packageName,
                PackageDesc = "HotUpdate Publisher business package. Not the Verification package.",
                EnableAddressable = false,
                SupportExtensionless = true,
                LocationToLower = false,
                IncludeAssetGUID = false,
                AutoCollectShaders = false,
                IgnoreRuleName = nameof(NormalIgnoreRule)
            };

            var group = new AssetBundleCollectorGroup
            {
                GroupName = PackageGroupName,
                GroupDesc = "Publisher-generated HybridCLR payload and consumer content",
                ActiveRuleName = nameof(EnableGroup)
            };
            package.Groups.Add(group);

            AddCollector(group, ConsumerBehaviorPath);
            AddCollector(group, GeneratedRoot + "/Manifest/HotUpdateManifest.json");
            AddCollector(group, GeneratedRoot + "/Code/HotUpdate.dll.bytes");
            AddCollector(group, GeneratedRoot + "/Metadata/mscorlib.dll.bytes");
            AddCollector(group, GeneratedRoot + "/Metadata/System.dll.bytes");
            AddCollector(group, GeneratedRoot + "/Metadata/System.Core.dll.bytes");
            AddCollector(group, GeneratedRoot + "/Metadata/UnityEngine.CoreModule.dll.bytes");

            setting.Packages.Add(package);
            AssetBundleCollectorSettingData.SaveFile();

            UnityEngine.Debug.Log(
                $"[HotUpdatePublisher] Created independent business package '{packageName}' with one consumer content asset and six Publisher output paths. Existing Verification configuration was preserved and is not used by this package.");
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
                "HotUpdatePublisherConsumerE2E").Trim();
            return string.IsNullOrWhiteSpace(packageName)
                ? "HotUpdatePublisherConsumerE2E"
                : packageName;
        }

        private static bool IsSafeBusinessPackageName(string packageName)
        {
            return !string.IsNullOrWhiteSpace(packageName) &&
                   SafePackageName.IsMatch(packageName) &&
                   packageName.IndexOf("verification", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
