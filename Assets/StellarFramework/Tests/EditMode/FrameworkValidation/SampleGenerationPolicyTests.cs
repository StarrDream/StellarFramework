using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class SampleGenerationPolicyTests
    {
        [Test]
        public void ExamplePlayableSceneBuilderAlsoGeneratesArchitectureDemoAssets()
        {
            string builder = ReadAssetText("Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs");

            Assert.That(builder, Does.Contain("ArchitectureDemo"));
            Assert.That(builder, Does.Contain("Panel_Main.prefab"));
            Assert.That(builder, Does.Contain("BuildArchitectureDemoFromTemplate"));
            Assert.That(builder, Does.Contain("ArchitectureDemoTemplateFolder"));
        }

        [Test]
        public void ExamplePlayableSceneBuilderNoLongerCreatesScenesViaEditorSceneManagerNewScene()
        {
            string builder = ReadAssetText("Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs");

            Assert.That(builder, Does.Not.Contain("EditorSceneManager.NewScene("));
            Assert.That(builder, Does.Contain("BuildAllSamples"));
            Assert.That(builder, Does.Contain("SampleTemplates"));
            Assert.That(builder, Does.Not.Contain("BuildResKitAssetBundle();"));
        }

        [Test]
        public void ExamplePlayableSceneBuilderUsesRenderPipelineCompatibilityForGeneratedMaterials()
        {
            string builder = ReadAssetText("Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs");

            Assert.That(builder, Does.Contain("RenderPipelineCompatibility"));
            Assert.That(builder, Does.Not.Contain("Shader.Find(\"Universal Render Pipeline/Lit\")"));
        }

        [Test]
        public void RenderPipelineCompatibilityTreatsNonSrpProjectsAsBuiltIn()
        {
            string source = ReadAssetText("Assets/StellarFramework/Runtime/Extensions/RenderPipelineCompatibility.cs");

            Assert.That(source, Does.Contain("FrameworkRenderPipelineFamily.BuiltIn"));
            Assert.That(source, Does.Contain("GraphicsSettings.currentRenderPipeline"));
            Assert.That(source, Does.Contain("QualitySettings.renderPipeline"));
        }

        [Test]
        public void MaterialConverterUsesPipelineCompatibilityAndBuiltInFallbackMessaging()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/URPMaterialConverterWindow.cs");

            Assert.That(source, Does.Contain("RenderPipelineCompatibility"));
            Assert.That(source, Does.Contain("FrameworkRenderPipelineFamily.BuiltIn"));
            Assert.That(source, Does.Contain("当前项目使用 Built-in 管线"));
            Assert.That(source, Does.Not.Contain("Shader.Find(\"Universal Render Pipeline/Lit\")"));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
