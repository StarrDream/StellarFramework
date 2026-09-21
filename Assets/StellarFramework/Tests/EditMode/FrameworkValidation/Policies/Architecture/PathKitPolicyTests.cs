using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class PathKitPolicyTests
    {
        [Test]
        public void PathKitCoreAndAdapterKeepTheStandaloneDependencyBoundary()
        {
            string coreRoot = Absolute("Assets/StellarFramework/Runtime/Kits/PathKit");
            string coreAsmdef = Read("Assets/StellarFramework/Runtime/Kits/PathKit/StellarFramework.PathKit.Core.asmdef");
            string adapterAsmdef = Read("Assets/StellarFramework/Runtime/Kits/PathKit/Adapters/GridKit/StellarFramework.PathKit.GridKitAdapter.asmdef");
            Assert.That(Directory.Exists(coreRoot), Is.True);
            Assert.That(coreAsmdef, Does.Contain("\"references\": []"));
            Assert.That(coreAsmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(adapterAsmdef, Does.Contain("StellarFramework.PathKit.Core"));
            Assert.That(adapterAsmdef, Does.Contain("StellarFramework.GridKit.Core"));
            foreach (string sourcePath in Directory.GetFiles(Path.Combine(coreRoot, "Identity"), "*.cs")) AssertCoreSource(sourcePath);
            foreach (string sourcePath in Directory.GetFiles(Path.Combine(coreRoot, "Graph"), "*.cs")) AssertCoreSource(sourcePath);
            foreach (string sourcePath in Directory.GetFiles(Path.Combine(coreRoot, "Search"), "*.cs")) AssertCoreSource(sourcePath);
            foreach (string sourcePath in Directory.GetFiles(Path.Combine(coreRoot, "Internal"), "*.cs")) AssertCoreSource(sourcePath);
        }

        [Test]
        public void PathKitCatalogGuidesAndBenchmarksAreRegistered()
        {
            string catalog = Read("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            Assert.That(catalog, Does.Contain("\"id\": \"pathkit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"pathkit.gridkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-PathKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-PathKit-GridKitAdapter.unitypackage"));
            string usageGuide = Read("Assets/StellarFramework/FrameworkDoc/02-Kits/PathKit/PathKit-路径搜索-说明文档-Guide.md");
            string sourceGuide = Read("Assets/StellarFramework/FrameworkDoc/02-Kits/PathKit/PathKit-路径搜索-源码文档-Guide.md");
            Assert.That(usageGuide, Does.Contain("Quick Start"));
            Assert.That(usageGuide, Does.Contain("PathSearchStatus.None"));
            Assert.That(usageGuide, Does.Contain("FindPath 不会返回 None"));
            Assert.That(sourceGuide, Does.Contain("Closed record"));
            Assert.That(sourceGuide, Does.Contain("PathSearchStatus.None = 0"));
            Assert.That(Read("Assets/StellarFramework/FrameworkDoc/02-Kits/PathKit/PathKit-GridKit适配器-Guide.md"), Does.Contain("NoCornerCut"));
            Assert.That(File.Exists(Absolute("Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Performance/PathKit/PathKitBenchmarkTests.cs")), Is.True);
        }

        [Test]
        public void PathKitExporterAddsCoreAndAdapterEntryPoints()
        {
            string publisher = Read("Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");
            Assert.That(publisher, Does.Contain("ExportPathKitPackage"));
            Assert.That(publisher, Does.Contain("ExportPathKitGridKitAdapterPackage"));
            Assert.That(publisher, Does.Not.Contain("ExportPathKitSamplePackage"));
            Assert.That(publisher, Does.Contain("excludedSourcePaths"));
        }

        private static void AssertCoreSource(string sourcePath)
        {
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
            Assert.That(source, Does.Not.Contain("GridKit"), sourcePath);
            Assert.That(source, Does.Not.Contain("Addressables"), sourcePath);
            Assert.That(source, Does.Not.Contain("HybridCLR"), sourcePath);
            Assert.That(source, Does.Not.Contain("UniTask"), sourcePath);
            Assert.That(source, Does.Not.Contain("Newtonsoft"), sourcePath);
            Assert.That(source, Does.Not.Contain("MonoBehaviour"), sourcePath);
        }

        private static string Read(string assetPath) => File.ReadAllText(Absolute(assetPath));

        private static string Absolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
