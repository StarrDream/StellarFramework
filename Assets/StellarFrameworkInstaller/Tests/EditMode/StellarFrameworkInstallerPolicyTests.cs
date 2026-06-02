using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFrameworkInstaller.Tests
{
    public sealed class StellarFrameworkInstallerPolicyTests
    {
        [Test]
        public void InstallerAssemblyDoesNotReferenceFrameworkOrOptionalPackages()
        {
            string asmdef = ReadAssetText("Assets/StellarFrameworkInstaller/Editor/StellarFramework.Installer.Editor.asmdef");

            Assert.That(asmdef, Does.Not.Contain("StellarFramework."));
            Assert.That(asmdef, Does.Not.Contain("UniTask"));
            Assert.That(asmdef, Does.Not.Contain("Newtonsoft"));
            Assert.That(asmdef, Does.Not.Contain("Unity.Addressables"));
            Assert.That(asmdef, Does.Not.Contain("HybridCLR"));
        }

        [Test]
        public void InstallerWindowDefinesMenuAndTwoInstallActions()
        {
            string source =
                ReadAssetText("Assets/StellarFrameworkInstaller/Editor/StellarFrameworkInstallerWindow.cs")
                + ReadAssetText("Assets/StellarFrameworkInstaller/Editor/StellarFrameworkInstallerConstants.cs");

            Assert.That(source, Does.Contain("StellarFramework/Installer"));
            Assert.That(source, Does.Contain("安装基础框架"));
            Assert.That(source, Does.Contain("安装 AA + HybridCLR 热更新能力"));
        }

        [Test]
        public void InstallerDocumentationAndPayloadFoldersExist()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkInstaller/Docs/StellarFrameworkInstaller-设计文档-Guide.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkInstaller/Payloads/README.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkInstaller/OfflinePackages/README.md")), Is.True);

            string designDoc = ReadAssetText("Assets/StellarFrameworkInstaller/Docs/StellarFrameworkInstaller-设计文档-Guide.md");
            Assert.That(designDoc, Does.Contain("StellarFrameworkInstaller.unitypackage"));
            Assert.That(designDoc, Does.Contain("安装基础框架"));
            Assert.That(designDoc, Does.Contain("安装 AA + HybridCLR 热更新能力"));
        }

        [Test]
        public void OptionalPackageSetupUsesReflectionInsteadOfDirectEditorReferences()
        {
            string source =
                ReadAssetText("Assets/StellarFrameworkInstaller/Editor/StellarFrameworkAddressablesReflectionBridge.cs")
                + ReadAssetText("Assets/StellarFrameworkInstaller/Editor/StellarFrameworkPostCoreReflectionBridge.cs");

            Assert.That(source, Does.Contain("FindType(\"UnityEditor.AddressableAssets"));
            Assert.That(source, Does.Contain("FindType(\"StellarFramework.Res.ResKitRuntimeSettings"));
            Assert.That(source, Does.Not.Contain("using UnityEditor.AddressableAssets"));
            Assert.That(source, Does.Not.Contain("using StellarFramework."));
        }

        [Test]
        public void FrameworkDocsDoNotTreatInstallerAsAFrameworkModule()
        {
            string frameworkReadme = ReadAssetText("Assets/StellarFramework/README.md");
            string toolsHubGuide = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md");

            Assert.That(frameworkReadme, Does.Not.Contain("StellarFrameworkInstaller.unitypackage"));
            Assert.That(frameworkReadme, Does.Not.Contain("StellarFramework -> Installer"));
            Assert.That(toolsHubGuide, Does.Not.Contain("StellarFrameworkInstaller.unitypackage"));
            Assert.That(toolsHubGuide, Does.Not.Contain("StellarFramework -> Installer"));
        }

        [Test]
        public void ExportedInstallerPackageDoesNotIncludeDevelopmentTests()
        {
            string source = ReadAssetText("Assets/StellarFrameworkInstaller/Editor/StellarFrameworkInstallerPackageBuilder.cs");

            Assert.That(source, Does.Contain("Build Payloads And Installer Package"));
            Assert.That(source, Does.Contain("Assets/StellarFrameworkInstaller/Editor"));
            Assert.That(source, Does.Contain("Assets/StellarFrameworkInstaller/Docs"));
            Assert.That(source, Does.Contain("Assets/StellarFrameworkInstaller/Payloads"));
            Assert.That(source, Does.Contain("Assets/StellarFrameworkInstaller/OfflinePackages"));
            Assert.That(source, Does.Not.Contain("Assets/StellarFrameworkInstaller/Tests"));
        }

        private static string ReadAssetText(string assetPath)
        {
            return File.ReadAllText(ToAbsoluteAssetPath(assetPath));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
