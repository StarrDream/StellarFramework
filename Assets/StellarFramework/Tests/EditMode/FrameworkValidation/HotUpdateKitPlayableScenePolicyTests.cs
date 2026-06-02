using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HotUpdateKitPlayableScenePolicyTests
    {
        [Test]
        public void HotUpdatePlayableSceneContainsHybridClrAaStartupComponent()
        {
            string scene = File.ReadAllText(ToAbsoluteAssetPath(
                "Assets/StellarFramework/Samples/KitSamples/Scenes/HotUpdateKit_Playable.unity"));

            Assert.That(scene, Does.Contain("m_Name: Example_HotUpdateKit_Runner"));
            Assert.That(scene, Does.Contain("settingsOverride:"),
                "HotUpdateKit_Playable should include Example_HybridCLRAAStartup so users can run the Addressables HybridCLR flow from the scene.");
            Assert.That(scene, Does.Contain("runOnStart:"));
        }

        [Test]
        public void HotUpdateSceneBuilderAddsHybridClrAaStartupComponent()
        {
            string builder = File.ReadAllText(ToAbsoluteAssetPath(
                "Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs"));

            Assert.That(builder, Does.Contain("runner.AddComponent<Example_HybridCLRAAStartup>()"));
            Assert.That(builder, Does.Contain("运行 AA 热更"));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
