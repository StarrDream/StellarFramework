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
            Assert.That(sample, Does.Not.Contain("TimeKit"));
            Assert.That(sample, Does.Not.Contain("Addressables"));
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
