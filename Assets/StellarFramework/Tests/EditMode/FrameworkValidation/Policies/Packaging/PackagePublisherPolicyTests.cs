using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class PackagePublisherPolicyTests
    {
        [Test]
        public void PackagePublisherDefinesSinglePackageExportMenu()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("ExportSinglePackageInstaller"));
            Assert.That(source, Does.Not.Contain("[MenuItem(\"StellarFramework/Packages"));
            Assert.That(source, Does.Contain("StellarFramework.unitypackage"));
        }

        [Test]
        public void SourceProjectPackageManifestHasNoAuthorMachinePathDependencies()
        {
            string manifest = ReadAssetText("Packages/manifest.json");
            string packageLock = ReadAssetText("Packages/packages-lock.json");

            Assert.That(manifest, Does.Not.Contain("\"file:"));
            Assert.That(packageLock, Does.Not.Contain("\"source\": \"local\""));
        }

        [Test]
        public void PackagePublisherStillKeepsBasePayloadExclusions()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("Assets/StellarFramework/Runtime/Kits/HotUpdateKit"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AddressableLoader"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Tests"));
        }

        [TestCase("Assets/StellarFrameworkVerification/README.md")]
        [TestCase("Assets/StellarFrameworkBackup/Test.cs")]
        [TestCase("Assets/StellarFramework2/Test.cs")]
        public void BasePackageRejectsSiblingFrameworkPrefixes(string assetPath)
        {
            // Regression: raw StartsWith("Assets/StellarFramework") also matches sibling roots.
            Assert.That(InvokePublisherBool("IsIncludedInBasePackage", assetPath), Is.False);
        }

        [Test]
        public void BasePackageAcceptsFrameworkChildPath()
        {
            Assert.That(
                InvokePublisherBool(
                    "IsIncludedInBasePackage",
                    "Assets/StellarFramework/Runtime/Kits/GridKit/GridOccupancy.cs"),
                Is.True);
            Assert.That(
                InvokePublisherBool(
                    "IsIncludedInBasePackage",
                    "Assets\\StellarFramework\\Runtime\\Kits\\GridKit\\GridOccupancy.cs"),
                Is.True);
        }

        [TestCase("Assets/StellarFrameworkVerification/README.md")]
        [TestCase("Assets/StellarFrameworkBackup/Test.cs")]
        [TestCase("Assets/GameHotUpdateBackup/Test.cs")]
        public void FullPayloadRejectsSiblingRootPrefixes(string assetPath)
        {
            // The framework and GameHotUpdate roots are directory boundaries, not text prefixes.
            Assert.That(InvokePublisherBool("IsIncludedInFullPayload", assetPath), Is.False);
        }

        [Test]
        public void FullPayloadAcceptsFrameworkAndGameHotUpdateChildPaths()
        {
            Assert.That(
                InvokePublisherBool(
                    "IsIncludedInFullPayload",
                    "Assets/StellarFramework/Runtime/Kits/GridKit/GridOccupancy.cs"),
                Is.True);
            Assert.That(
                InvokePublisherBool(
                    "IsIncludedInFullPayload",
                    "Assets/GameHotUpdate/Source/HotUpdateMain.cs"),
                Is.True);
        }

        [Test]
        public void BaseFrameworkAssetSelectionExcludesVerificationSibling()
        {
            string[] assets = InvokePublisherPaths("GetBaseFrameworkAssetPaths");

            Assert.That(assets, Is.Not.Empty);
            Assert.That(
                assets.Any(path => IsUnderDirectory(path, "Assets/StellarFrameworkVerification")),
                Is.False);
        }

        [Test]
        public void FullFrameworkAssetSelectionExcludesVerificationSibling()
        {
            string[] assets = InvokePublisherPaths("GetFullFrameworkAssetPaths");

            Assert.That(assets, Is.Not.Empty);
            Assert.That(
                assets.Any(path => IsUnderDirectory(path, "Assets/StellarFrameworkVerification")),
                Is.False);
        }

        [Test]
        public void PackagePublisherExcludesPackagingToolsFromFullPayload()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Editor/KitPackageBootstrap"));
            Assert.That(source, Does.Contain("FullPayloadExcludedPrefixes"));
        }

        [Test]
        public void PackagePublisherExcludesGeneratedSamplesFromPayloads()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Scenes"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Generated"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/ArchitectureDemo/Scene"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/ArchitectureDemo/Resources"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Resources/Audio"));
        }

        [Test]
        public void PackagePublisherExcludesGeneratedSampleScenesFromBasePayload()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/KitSamples/Scenes"));
            Assert.That(source, Does.Contain("Assets/StellarFramework/Samples/ArchitectureDemo/Scene"));
        }

        [Test]
        public void PackagePublisherWritesChineseSinglePackageGuide()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("StellarFramework-Package-Dependencies.md"));
            Assert.That(source, Does.Contain("只需要导入"));
            Assert.That(source, Does.Contain("一键安装"));
            Assert.That(source, Does.Contain("com.cysharp.unitask"));
            Assert.That(source, Does.Contain("com.unity.addressables"));
        }

        [Test]
        public void PackagePublisherCleansLegacySplitPackageArtifacts()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("StellarFramework-Bootstrap.unitypackage"));
            Assert.That(source, Does.Contain("StellarFramework-Base.unitypackage"));
            Assert.That(source, Does.Contain("StellarFramework-FullHotUpdate.unitypackage"));
            Assert.That(source, Does.Contain("File.Delete"));
        }

        [Test]
        public void PackagePublisherKeepsExportArtifactsOutsideAssetsTree()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("BuildArtifacts"));
            Assert.That(source, Does.Contain("private const string ExportRoot = \"BuildArtifacts/StellarFramework\""));
            Assert.That(source, Does.Contain("LegacyAssetExportRoot"));
        }

        [Test]
        public void PackagePublisherExportsFilesWithoutRecursingThroughParentFolders()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain(".Where(path => !AssetDatabase.IsValidFolder(path))"));
        }

        [Test]
        public void BootstrapPackageFilesExistAndDescribeSinglePackageInstallFlow()
        {
            string bootstrapAsmdef = ReadAssetText(
                "Assets/StellarFrameworkBootstrap/Editor/StellarFramework.Bootstrap.Editor.asmdef");
            string bootstrapWindow = ReadAssetText(
                "Assets/StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapWindow.cs");
            string bootstrapInstaller = ReadAssetText(
                "Assets/StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapInstaller.cs");
            string bootstrapUtility = ReadAssetText(
                "Assets/StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapPackageUtility.cs");

            Assert.That(bootstrapAsmdef, Does.Contain("\"references\": []"));
            Assert.That(bootstrapAsmdef, Does.Not.Contain("UniTask"));
            Assert.That(bootstrapAsmdef, Does.Not.Contain("Newtonsoft"));
            Assert.That(bootstrapWindow, Does.Contain("StellarFramework/安装/单包安装器"));
            Assert.That(bootstrapWindow, Does.Contain("一键安装 StellarFramework"));
            Assert.That(bootstrapWindow, Does.Not.Contain("安装基础框架"));
            Assert.That(bootstrapWindow, Does.Not.Contain("安装完整热更新框架"));
            Assert.That(bootstrapInstaller, Does.Contain("com.cysharp.unitask"));
            Assert.That(bootstrapInstaller, Does.Contain("com.unity.nuget.newtonsoft-json"));
            Assert.That(bootstrapInstaller, Does.Contain("com.unity.addressables"));
            Assert.That(bootstrapInstaller, Does.Contain("com.code-philosophy.hybridclr"));
            Assert.That(bootstrapInstaller, Does.Contain("ImportPackage"));
            Assert.That(bootstrapInstaller, Does.Not.Contain("OpenFilePanel"));
            Assert.That(bootstrapInstaller, Does.Contain("EnsureDefaultAddressablesSettings"));
            Assert.That(bootstrapInstaller, Does.Contain("EnsureLogKitDefine"));
            Assert.That(bootstrapInstaller, Does.Contain("RequestOpenToolsHub"));
            Assert.That(bootstrapInstaller, Does.Contain("RequestCleanupBootstrapArtifacts"));
            Assert.That(bootstrapUtility, Does.Contain("Payloads"));
            Assert.That(bootstrapUtility, Does.Contain("AddressableAssetSettingsDefaultObject"));
            Assert.That(bootstrapUtility, Does.Contain("GetSettings"));
            Assert.That(bootstrapUtility, Does.Contain("StellarFramework/Tools Hub"));
            Assert.That(bootstrapUtility, Does.Contain("SessionState"));
            Assert.That(bootstrapUtility, Does.Contain("Assets/StellarFrameworkBootstrap"));
            Assert.That(bootstrapUtility, Does.Contain("DeleteAsset"));
            Assert.That(bootstrapUtility, Does.Contain("StellarFrameworkPackagePublisher.cs"));
            Assert.That(bootstrapUtility, Does.Contain("IsFrameworkDevelopmentProject"));
            Assert.That(bootstrapUtility, Does.Contain("ENABLE_LOG"));
            Assert.That(bootstrapUtility, Does.Contain("TryAddDefineForSelectedBuildTarget"));
            Assert.That(bootstrapUtility, Does.Contain("UNITY_2021_2_OR_NEWER"));
            Assert.That(bootstrapUtility, Does.Contain("NamedBuildTarget.FromBuildTargetGroup"));
            Assert.That(bootstrapUtility, Does.Contain("GetScriptingDefineSymbols(namedBuildTarget)"));
            Assert.That(bootstrapUtility, Does.Contain("SetScriptingDefineSymbols(namedBuildTarget, merged)"));
        }

        [Test]
        public void BootstrapReadmeUsesChineseSinglePackageInstructions()
        {
            string readme = ReadAssetText("Assets/StellarFrameworkBootstrap/README.md");

            Assert.That(readme, Does.Contain("只需导入一个包"));
            Assert.That(readme, Does.Contain("单包安装器"));
            Assert.That(readme, Does.Contain("一键安装"));
            Assert.That(readme, Does.Contain("Unity 2022.3 LTS"));
            Assert.That(readme, Does.Contain("Unity 6000.x"));
            Assert.That(readme, Does.Contain("Built-in、URP、HDRP"));
            Assert.That(readme, Does.Contain("构建样例"));
        }

        [Test]
        public void PackageGuideDocumentsUnityAndRenderPipelineCompatibility()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("Unity 2022.3 LTS"));
            Assert.That(source, Does.Contain("Unity 6000.x"));
            Assert.That(source, Does.Contain("Built-in、URP 或 HDRP"));
            Assert.That(source, Does.Contain("RenderPipelineCompatibility"));
            Assert.That(source, Does.Contain("SampleTemplates"));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static Type GetPublisherType()
        {
            Type publisherType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "StellarFramework.Editor.Modules.StellarFrameworkPackagePublisher", false))
                .FirstOrDefault(type => type != null);

            Assert.That(publisherType, Is.Not.Null);
            return publisherType;
        }

        private static bool InvokePublisherBool(string methodName, string assetPath)
        {
            MethodInfo method = GetPublisherType().GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"Publisher method '{methodName}' was not found.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(method.GetParameters().Length, Is.EqualTo(1));
            Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(string)));

            return (bool)method.Invoke(null, new object[] { assetPath });
        }

        private static string[] InvokePublisherPaths(string methodName)
        {
            MethodInfo method = GetPublisherType().GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"Publisher method '{methodName}' was not found.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(string[])));
            Assert.That(method.GetParameters().Length, Is.EqualTo(0));

            return (string[])method.Invoke(null, null);
        }

        private static bool IsUnderDirectory(string assetPath, string directoryPath)
        {
            string normalizedAssetPath = assetPath.Replace('\\', '/');
            string normalizedDirectoryPath = directoryPath.Replace('\\', '/').TrimEnd('/');
            return normalizedAssetPath == normalizedDirectoryPath ||
                   normalizedAssetPath.StartsWith(normalizedDirectoryPath + "/", StringComparison.Ordinal);
        }
    }
}
