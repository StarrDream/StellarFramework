using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class VerificationSurfacePolicyTests
    {
        [Test]
        public void VerificationAreaExistsOutsideFrameworkPayload()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/README.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/ValidationArchitecture.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Editor/ReleaseVerificationHubModule.cs")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Scenes/FrameworkValidation_Playable.unity")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Example_FrameworkValidation/FrameworkValidationRunner.cs")), Is.True);
        }

        [Test]
        public void ValidationArchitectureAndEvidenceLedgerAreLinked()
        {
            string architecture = ReadAssetText("Assets/StellarFrameworkVerification/ValidationArchitecture.md");
            string rootReadme = ReadAssetText("README.md");
            string testsGuide = ReadAssetText("Assets/StellarFramework/Tests/Tests-说明文档-Guide.md");
            string matrix = ReadAssetText("Assets/StellarFramework/KitCatalog/KitExportValidationMatrix.md");

            Assert.That(architecture, Does.Contain("StellarFramework 验证架构与发布验收规范"));
            Assert.That(architecture, Does.Contain("Kit Behavior"));
            Assert.That(architecture, Does.Contain("Performance"));
            Assert.That(architecture, Does.Contain("Framework Policy"));
            Assert.That(architecture, Does.Contain("Integration"));
            Assert.That(architecture, Does.Contain("Release"));
            Assert.That(rootReadme, Does.Contain("ValidationArchitecture.md"));
            Assert.That(testsGuide, Does.Contain("ValidationArchitecture.md"));
            Assert.That(matrix, Does.Contain("Evidence Ledger"));
        }

        [Test]
        public void VerificationAreaIsMaintainerOnlyAndExcludedFromDistribution()
        {
            string verificationReadme = ReadAssetText("Assets/StellarFrameworkVerification/README.md");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(verificationReadme, Does.Contain("Maintainer-only"));
            Assert.That(verificationReadme, Does.Contain("Not distributed"));
            Assert.That(catalog, Does.Not.Contain("StellarFrameworkVerification"));
            Assert.That(publisher, Does.Contain("Assets/StellarFrameworkVerification"));
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
