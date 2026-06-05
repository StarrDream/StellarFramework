using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
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
        public void HotUpdatePlayableSceneRunnerHasNoMissingScripts()
        {
            EditorSceneManager.OpenScene(
                "Assets/StellarFramework/Samples/KitSamples/Scenes/HotUpdateKit_Playable.unity",
                OpenSceneMode.Single);

            GameObject runner = GameObject.Find("Example_HotUpdateKit_Runner");
            Assert.That(runner, Is.Not.Null);

            Component[] components = runner.GetComponents<Component>();
            Assert.That(components, Has.None.Null,
                "Example_HotUpdateKit_Runner contains a missing script. HybridCLR AA startup should be attached as a valid component.");
        }

        [Test]
        public void HotUpdateSceneBuilderUsesTemplateBackedHybridClrAaStartupScene()
        {
            string builder = File.ReadAllText(ToAbsoluteAssetPath(
                "Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs"));
            string template = File.ReadAllText(ToAbsoluteAssetPath(
                "Assets/StellarFramework/Samples/KitSamples/Editor/SampleTemplates/KitSamples/HotUpdateKit_Playable.unity.txt"));

            Assert.That(builder, Does.Contain("MaterializeTemplateFolder("));
            Assert.That(builder, Does.Contain("KitSampleTemplateFolder"));
            Assert.That(template, Does.Contain("m_Name: Example_HotUpdateKit_Runner"));
            Assert.That(template, Does.Contain("settingsOverride:"));
            Assert.That(template, Does.Contain("runOnStart:"));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
