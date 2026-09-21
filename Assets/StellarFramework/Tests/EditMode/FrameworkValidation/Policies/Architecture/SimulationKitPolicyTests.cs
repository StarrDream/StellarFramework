using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class SimulationKitPolicyTests
    {
        [Test]
        public void SimulationKitCoreKeepsZeroDependencyFoundationBoundary()
        {
            string root = Absolute("Assets/StellarFramework/Runtime/Kits/SimulationKit");
            string asmdef = Read("Assets/StellarFramework/Runtime/Kits/SimulationKit/StellarFramework.SimulationKit.Core.asmdef");
            Assert.That(Directory.Exists(root), Is.True);
            Assert.That(asmdef, Does.Contain("\"references\": []"));
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"));

            foreach (string sourcePath in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("TimeKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("SpatialKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("ResKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("Addressables"), sourcePath);
                Assert.That(source, Does.Not.Contain("HybridCLR"), sourcePath);
                Assert.That(source, Does.Not.Contain("UniTask"), sourcePath);
                Assert.That(source, Does.Not.Contain("Newtonsoft"), sourcePath);
                Assert.That(source, Does.Not.Contain("MonoBehaviour"), sourcePath);
                Assert.That(source, Does.Not.Contain("IEnumerable"), sourcePath);
            }
        }

        [Test]
        public void SimulationKitCatalogDocsAndBenchmarksAreExplicit()
        {
            string catalog = Read("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string mutationResult = Read(
                "Assets/StellarFramework/Runtime/Kits/SimulationKit/SimulationMutationResult.cs");
            string usageGuide = Read(
                "Assets/StellarFramework/FrameworkDoc/02-Kits/SimulationKit/SimulationKit-批量模拟调度-说明文档-Guide.md");
            string sourceGuide = Read(
                "Assets/StellarFramework/FrameworkDoc/02-Kits/SimulationKit/SimulationKit-批量模拟调度-源码文档-Guide.md");
            string benchmark = Read(
                "Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Performance/SimulationKit/SimulationKitBenchmarkTests.cs");

            Assert.That(catalog, Does.Contain("\"id\": \"simulationkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-SimulationKit.unitypackage"));
            Assert.That(mutationResult, Does.Contain("已注册 Entry 的业务调度状态"));
            Assert.That(mutationResult, Does.Contain("记录已观察到的时间"));
            Assert.That(usageGuide, Does.Contain("单次 `CollectDue()` 调用的 Count Budget"));
            Assert.That(usageGuide, Does.Contain("Frame-Spreading"));
            Assert.That(usageGuide, Does.Contain("Explicit Flush"));
            Assert.That(usageGuide, Does.Contain("HasBacklog` 表示当前 `nowTick`"));
            Assert.That(usageGuide, Does.Contain("不要在这里 while-drain"));
            Assert.That(sourceGuide, Does.Contain("Dispatch Budget 与 Frame Budget"));
            Assert.That(sourceGuide, Does.Contain("HasBacklog 与同 Tick 重复 Collect"));
            Assert.That(sourceGuide, Does.Contain("根判断保持 O(1)"));
            Assert.That(benchmark, Does.Contain("ExplicitBacklogDrainThroughput"));
            Assert.That(benchmark, Does.Contain("explicit backlog drain throughput"));
            Assert.That(File.Exists(Absolute(
                "Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Performance/SimulationKit/SimulationKitBenchmarkTests.cs")), Is.True);
        }

        private static string Read(string assetPath) => File.ReadAllText(Absolute(assetPath));

        private static string Absolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
