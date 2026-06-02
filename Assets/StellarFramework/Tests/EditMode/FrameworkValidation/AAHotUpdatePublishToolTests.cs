using System.IO;
using NUnit.Framework;
using StellarFramework.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class AAHotUpdatePublishToolTests
    {
        private static readonly string TempRoot =
            Path.Combine(Application.dataPath, "Temp", "AAHotUpdatePublishToolTests");

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TempRoot))
            {
                Directory.Delete(TempRoot, true);
            }

            string metaPath = TempRoot + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        [Test]
        public void ExpandPathTokensUsesProjectAndBuildTarget()
        {
            string result = AAHotUpdatePublishLogic.ExpandPathTokens(
                "[ProjectRoot]/Publish/[BuildTarget]",
                BuildTarget.StandaloneWindows64);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            Assert.That(result, Is.EqualTo(projectRoot + "/Publish/StandaloneWindows64"));
        }

        [Test]
        public void BuildRuntimeManifestPathOrUrlReturnsEmptyForLocalBuiltIn()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateLocalBuiltInDefault();

            string url = AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(url, Is.EqualTo(string.Empty));
        }

        [Test]
        public void BuildRuntimeManifestPathOrUrlUsesConfiguredHttpRemoteLoadPath()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            config.RemoteLoadPathOrUrl = "https://cdn.example.com/hotupdate/[BuildTarget]";

            string url = AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(url, Is.EqualTo("https://cdn.example.com/hotupdate/StandaloneWindows64/HotUpdateManifest.json"));
        }

        [Test]
        public void BuildRemoteBuildPathFallsBackToRemotePublishDirectory()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            config.RemoteBuildDirectory = "";
            config.RemotePublishDirectory = "D:/HotUpdate/[BuildTarget]";

            string path = AAWorkflowPathUtility.BuildRemoteBuildPath(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(path.Replace('\\', '/'), Does.EndWith("/HotUpdate/StandaloneWindows64"));
        }

        [Test]
        public void ValidatePublishDirectoryRequiresManifestCatalogHashAndBundle()
        {
            string publishDir = Path.Combine(TempRoot, "Publish");
            Directory.CreateDirectory(publishDir);
            File.WriteAllText(Path.Combine(publishDir, "HotUpdateManifest.json"), "{}");
            File.WriteAllText(Path.Combine(publishDir, "catalog_0.1.json"), "{}");
            File.WriteAllText(Path.Combine(publishDir, "catalog_0.1.hash"), "hash");
            File.WriteAllBytes(Path.Combine(publishDir, "content.bundle"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(publishDir, "content.bundle.meta"), "ignored");

            AAHotUpdatePublishValidationReport report =
                AAHotUpdatePublishLogic.ValidatePublishDirectory(publishDir);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }

        [Test]
        public void CopyDirectorySkipsMetaFilesAndCleansStaleFiles()
        {
            string sourceDir = Path.Combine(TempRoot, "Source");
            string destinationDir = Path.Combine(TempRoot, "Destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);
            File.WriteAllText(Path.Combine(sourceDir, "HotUpdateManifest.json"), "{}");
            File.WriteAllText(Path.Combine(sourceDir, "content.bundle"), "bundle");
            File.WriteAllText(Path.Combine(sourceDir, "content.bundle.meta"), "meta");
            File.WriteAllText(Path.Combine(destinationDir, "stale.bundle"), "stale");

            int copied = AAHotUpdatePublishLogic.CopyDirectory(
                sourceDir,
                destinationDir,
                cleanDestination: true,
                copyMetaFiles: false);

            Assert.That(copied, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(destinationDir, "HotUpdateManifest.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(destinationDir, "content.bundle")), Is.True);
            Assert.That(File.Exists(Path.Combine(destinationDir, "content.bundle.meta")), Is.False);
            Assert.That(File.Exists(Path.Combine(destinationDir, "stale.bundle")), Is.False);
        }

        [Test]
        public void AreSameDirectoryNormalizesSlashAndTrailingSeparator()
        {
            string left = Path.Combine(TempRoot, "Publish");
            string right = left.Replace('\\', '/') + "/";

            Assert.That(AAHotUpdatePublishLogic.AreSameDirectory(left, right), Is.True);
        }

        [Test]
        public void WorkflowDefaultsSeparateLocalBuiltInAndRemoteHotUpdate()
        {
            AAWorkflowConfigSet configSet = AAWorkflowConfigSet.CreateDefault();

            Assert.That(configSet.Configs.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(configSet.Configs[0].Mode, Is.EqualTo(AAWorkflowMode.LocalBuiltIn));
            Assert.That(configSet.Configs[0].EnableRemoteCatalog, Is.False);
            Assert.That(configSet.Configs[0].AllowStreamingAssetsFallback, Is.True);
            Assert.That(configSet.Configs[1].Mode, Is.EqualTo(AAWorkflowMode.RemoteHotUpdate));
            Assert.That(configSet.Configs[1].EnableRemoteCatalog, Is.True);
            Assert.That(configSet.Configs[1].AllowStreamingAssetsFallback, Is.False);
        }

        [Test]
        public void WorkflowManifestSourceUsesStreamingAssetsForLocalBuiltIn()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateLocalBuiltInDefault();

            string display = AAWorkflowPathUtility.BuildManifestDisplayPath(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(display, Does.StartWith("StreamingAssets:"));
            Assert.That(display, Does.EndWith("/aa/HotUpdateManifest.json"));
            Assert.That(AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(config, BuildTarget.StandaloneWindows64),
                Is.EqualTo(string.Empty));
        }

        [Test]
        public void WorkflowManifestSourceUsesRemoteFileUriForRemoteHotUpdate()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            config.RemoteLoadPathOrUrl = "";
            config.RemotePublishDirectory = "D:/HotUpdate/[BuildTarget]";

            string url = AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(url, Is.EqualTo("file:///D:/HotUpdate/StandaloneWindows64/HotUpdateManifest.json"));
        }

        [Test]
        public void WorkflowManifestSourceUsesConfiguredHttpUrlForRemoteHotUpdate()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            config.RemoteLoadPathOrUrl = "https://cdn.example.com/aa/[BuildTarget]";

            string url = AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(url, Is.EqualTo("https://cdn.example.com/aa/StandaloneWindows64/HotUpdateManifest.json"));
        }

        [Test]
        public void FileManifestPathConvertsToExpectedFileUri()
        {
            string fileUri = AAWorkflowPathUtility.ToFileUri(
                "D:/HotUpdate/StandaloneWindows64/HotUpdateManifest.json");

            Assert.That(
                fileUri,
                Is.EqualTo("file:///D:/HotUpdate/StandaloneWindows64/HotUpdateManifest.json"));
        }

        [Test]
        public void BuildPackagingStatusDescribesLocalBuiltInModeForPlayerBuild()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateLocalBuiltInDefault();

            AAWorkflowPackagingStatus status =
                AAWorkflowPackagingStatus.Build(config, BuildTarget.StandaloneWindows64);

            Assert.That(status.ModeLabel, Is.EqualTo("本地内置 AA"));
            Assert.That(status.BadgeText, Is.EqualTo("Player 将从包内 StreamingAssets 加载"));
            Assert.That(status.ManifestDisplayPath, Does.StartWith("StreamingAssets:"));
            Assert.That(status.ManifestDisplayPath, Does.EndWith("/aa/HotUpdateManifest.json"));
            Assert.That(status.RemoteCatalogLabel, Is.EqualTo("关闭"));
            Assert.That(status.StreamingAssetsFallbackLabel, Is.EqualTo("开启"));
            Assert.That(status.IsRemoteHotUpdate, Is.False);
        }

        [Test]
        public void BuildPackagingStatusDescribesRemoteHotUpdateModeForPlayerBuild()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            config.RemoteLoadPathOrUrl = "https://cdn.example.com/hotupdate/[BuildTarget]";
            config.EnableRemoteCatalog = true;
            config.AllowStreamingAssetsFallback = false;

            AAWorkflowPackagingStatus status =
                AAWorkflowPackagingStatus.Build(config, BuildTarget.StandaloneWindows64);

            Assert.That(status.ModeLabel, Is.EqualTo("远端热更 AA"));
            Assert.That(status.BadgeText, Is.EqualTo("Player 将优先读取远端 Manifest"));
            Assert.That(
                status.ManifestDisplayPath,
                Is.EqualTo("https://cdn.example.com/hotupdate/StandaloneWindows64/HotUpdateManifest.json"));
            Assert.That(status.RemoteCatalogLabel, Is.EqualTo("开启"));
            Assert.That(status.StreamingAssetsFallbackLabel, Is.EqualTo("关闭"));
            Assert.That(status.IsRemoteHotUpdate, Is.True);
        }

        [Test]
        public void ResolvePlayerStreamingAssetsRequiresPlayerDataFolder()
        {
            AAWorkflowConfig config = AAWorkflowConfig.CreateLocalBuiltInDefault();
            config.TestPlayerRootDirectory = Path.Combine(TempRoot, "FakePlayer");
            string dataRoot = Path.Combine(config.TestPlayerRootDirectory, "FakeGame_Data");
            Directory.CreateDirectory(Path.Combine(dataRoot, "StreamingAssets"));

            string destination = AAWorkflowPathUtility.ResolveTestPlayerStreamingAssetsAaDirectory(
                config,
                BuildTarget.StandaloneWindows64);

            Assert.That(destination.Replace('\\', '/'),
                Does.EndWith("/FakeGame_Data/StreamingAssets/aa"));
            Assert.That(AAWorkflowPathUtility.IsSafeTestPlayerStreamingAssetsAaDirectory(
                destination,
                BuildTarget.StandaloneWindows64), Is.True);
            Assert.That(AAWorkflowPathUtility.IsSafeTestPlayerStreamingAssetsAaDirectory(
                Application.dataPath,
                BuildTarget.StandaloneWindows64), Is.False);
        }

        [Test]
        public void LocalBuiltInValidationAcceptsAddressablesLocalOutputWithoutHash()
        {
            string publishDir = Path.Combine(TempRoot, "LocalBuiltIn");
            Directory.CreateDirectory(Path.Combine(publishDir, "Windows", "StandaloneWindows64"));
            File.WriteAllText(
                Path.Combine(publishDir, "HotUpdateManifest.json"),
                "{\"hotUpdateAssemblyKey\":\"Assets/GameHotUpdate/Code/HotUpdate.dll.bytes\"," +
                "\"hotUpdateAssemblySha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"," +
                "\"hotUpdateEntryClass\":\"HotUpdate.HotUpdateMain\"," +
                "\"hotUpdateEntryMethod\":\"Main\"," +
                "\"aotMetadataKeys\":[\"Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes\"]}");
            File.WriteAllText(Path.Combine(publishDir, "Windows", "catalog.json"), "{}");
            File.WriteAllText(Path.Combine(publishDir, "Windows", "settings.json"), "{}");
            File.WriteAllBytes(Path.Combine(publishDir, "Windows", "StandaloneWindows64", "content.bundle"),
                new byte[] { 1 });

            AAHotUpdatePublishValidationReport report =
                AAWorkflowValidator.ValidatePublishDirectory(
                    publishDir,
                    hotUpdateDllBytesPath: null,
                    requireCatalogHash: false,
                    requireSettingsJson: true);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }

        [Test]
        public void SyncManifestToPublishDirectoryOverwritesStaleRemoteManifest()
        {
            string sourceManifest = Path.Combine(TempRoot, "HotUpdateManifest.json");
            string publishDir = Path.Combine(TempRoot, "RemotePublish");
            Directory.CreateDirectory(publishDir);
            File.WriteAllText(sourceManifest, "{\"hotUpdateAssemblySha256\":\"new\"}");
            File.WriteAllText(Path.Combine(publishDir, "HotUpdateManifest.json"),
                "{\"hotUpdateAssemblySha256\":\"old\"}");

            bool copied = AAWorkflowPublishService.SyncManifestToPublishDirectory(
                sourceManifest,
                publishDir,
                out string message);

            Assert.That(copied, Is.True, message);
            Assert.That(File.ReadAllText(Path.Combine(publishDir, "HotUpdateManifest.json")),
                Does.Contain("\"new\""));
        }

        [Test]
        public void LegacyLocalBuiltInDirectoryCleanupIsLimitedToProjectStreamingAssets()
        {
            string legacy = AAWorkflowPathUtility.GetLegacyLocalBuiltInDirectory(BuildTarget.StandaloneWindows64);

            Assert.That(legacy.Replace('\\', '/'),
                Does.EndWith("/Assets/StreamingAssets/aa/StandaloneWindows64"));
            Assert.That(
                AAWorkflowPathUtility.IsSafeLegacyLocalBuiltInDirectory(
                    legacy,
                    BuildTarget.StandaloneWindows64),
                Is.True);
            Assert.That(
                AAWorkflowPathUtility.IsSafeLegacyLocalBuiltInDirectory(
                    Path.Combine(TempRoot, "StandaloneWindows64"),
                    BuildTarget.StandaloneWindows64),
                Is.False);
        }

        [Test]
        public void ValidatePublishDirectoryCanDetectManifestShaMismatchAgainstDllBytes()
        {
            string publishDir = Path.Combine(TempRoot, "PublishMismatch");
            Directory.CreateDirectory(publishDir);
            File.WriteAllText(Path.Combine(publishDir, "HotUpdateManifest.json"),
                "{\"hotUpdateAssemblySha256\":\"0000\"}");
            File.WriteAllText(Path.Combine(publishDir, "catalog_0.1.json"), "{}");
            File.WriteAllText(Path.Combine(publishDir, "catalog_0.1.hash"), "hash");
            File.WriteAllBytes(Path.Combine(publishDir, "content.bundle"), new byte[] { 1 });
            string dllBytes = Path.Combine(TempRoot, "HotUpdate.dll.bytes");
            File.WriteAllBytes(dllBytes, new byte[] { 1, 2, 3 });

            AAHotUpdatePublishValidationReport report =
                AAWorkflowValidator.ValidatePublishDirectory(publishDir, dllBytes);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("SHA256"));
        }

        [Test]
        public void AddressablesDefaultGroupsStayNewUserFriendly()
        {
            string settingsPath = Path.Combine(Application.dataPath, "AddressableAssetsData", "AddressableAssetSettings.asset");
            string localGroupPath = Path.Combine(
                Application.dataPath,
                "AddressableAssetsData",
                "AssetGroups",
                "StellarFramework Local Resources.asset");
            string hotUpdateGroupPath = Path.Combine(
                Application.dataPath,
                "AddressableAssetsData",
                "AssetGroups",
                "StellarFramework Hot Update Code.asset");

            string settingsText = File.ReadAllText(settingsPath);
            string localGroupText = File.ReadAllText(localGroupPath);
            string hotUpdateGroupText = File.ReadAllText(hotUpdateGroupPath);

            Assert.That(settingsText, Does.Contain("40583c01e3b8a214489a211798224db7"));
            Assert.That(settingsText, Does.Contain("69fc24f53cb35704ebf0239f71d4631c"));
            Assert.That(settingsText, Does.Contain("0190aaae5b2862b4dad9c9d9b7610bf6"));
            Assert.That(settingsText, Does.Not.Contain("2cf6afe149a47604dade66daaa324121"));
            Assert.That(settingsText, Does.Not.Contain("8197f129b0badfe47aae357c9b42a322"));
            Assert.That(settingsText, Does.Not.Contain("900bdd5cc90c8964abb9c6e10d5f7a7b"));

            Assert.That(localGroupText, Does.Contain("m_GroupName: StellarFramework Local Resources"));
            Assert.That(localGroupText, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab"));
            Assert.That(localGroupText, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab"));
            Assert.That(localGroupText, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab"));

            Assert.That(hotUpdateGroupText, Does.Contain("m_GroupName: StellarFramework Hot Update Code"));
            Assert.That(hotUpdateGroupText, Does.Contain("Assets/GameHotUpdate/Code/HotUpdate.dll.bytes"));
            Assert.That(hotUpdateGroupText, Does.Contain("Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes"));
        }
    }
}
