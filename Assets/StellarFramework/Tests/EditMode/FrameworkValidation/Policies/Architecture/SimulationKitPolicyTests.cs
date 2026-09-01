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
        public void SimulationKitCatalogAndSampleClosureAreExplicit()
        {
            string catalog = Read("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string sampleAsmdef = Read(
                "Assets/StellarFramework/Samples/KitSamples/Example_SimulationKit/StellarFramework.Samples.SimulationKit.asmdef");
            string sample = Read("Assets/StellarFramework/Samples/KitSamples/Example_SimulationKit/Example_SimulationKit.cs");
            string mutationResult = Read(
                "Assets/StellarFramework/Runtime/Kits/SimulationKit/SimulationMutationResult.cs");
            string usageGuide = Read(
                "Assets/StellarFramework/Runtime/Kits/SimulationKit/SimulationKit-批量模拟调度-说明文档-Guide.md");
            string sourceGuide = Read(
                "Assets/StellarFramework/Runtime/Kits/SimulationKit/SimulationKit-批量模拟调度-源码文档-Guide.md");
            string benchmark = Read(
                "Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Performance/SimulationKit/SimulationKitBenchmarkTests.cs");
            string scene = Read("Assets/StellarFramework/Samples/KitSamples/Scenes/SimulationKit_Playable.unity");
            string template = Read(
                "Assets/StellarFramework/Samples/KitSamples/Editor/SampleTemplates/KitSamples/SimulationKit_Playable.unity.txt");

            Assert.That(catalog, Does.Contain("\"id\": \"simulationkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-SimulationKit.unitypackage"));
            Assert.That(catalog, Does.Contain("\"id\": \"samples.simulationkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-Sample-SimulationKit.unitypackage"));
            Assert.That(catalog, Does.Contain("\"requiredProfileIds\": [\"simulationkit\"]"));
            Assert.That(sampleAsmdef, Does.Contain("StellarFramework.SimulationKit.Core"));
            Assert.That(sampleAsmdef, Does.Contain("\"autoReferenced\": false"));
            Assert.That(sample, Does.Contain("Reset Burst"));
            Assert.That(sample, Does.Contain("Reset Staggered"));
            Assert.That(sample, Does.Contain("Budget 16"));
            Assert.That(sample, Does.Contain("DrainCurrentTick"));
            Assert.That(sample, Does.Contain("FrameStep"));
            Assert.That(sample, Does.Contain("Frame Step (Collect once)"));
            Assert.That(sample, Does.Contain("Game Tick"));
            Assert.That(sample, Does.Contain("Manual Drain (same tick)"));
            Assert.That(sample, Does.Not.Contain("while (result.HasBacklog)"));
            Assert.That(sample, Does.Not.Contain("TimeKit"));
            Assert.That(sample, Does.Not.Contain("Addressables"));
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
            Assert.That(scene, Does.Contain("m_Name: Example_SimulationKit"));
            Assert.That(scene, Does.Not.Contain("m_Script: {fileID: 0}"));
            Assert.That(template, Does.Contain("m_Name: Example_SimulationKit"));
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
