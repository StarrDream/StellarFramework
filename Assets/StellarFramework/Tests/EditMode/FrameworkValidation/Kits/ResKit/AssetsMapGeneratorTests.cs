using System.IO;
using System.Reflection;
using NUnit.Framework;
using StellarFramework.Editor;
using StellarFramework.Generated;
using StellarFramework.HybridCLR;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class AssetsMapGeneratorTests
    {
        [Test]
        public void GeneratedMapContainsCanonicalRuntimeKeysAndExcludesInfrastructure()
        {
            AssetsMapGenerator.GenerateIfNeeded();
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                AssetsMapGenerator.OutputAssetPath.Replace('/', Path.DirectorySeparatorChar)));

            Assert.That(source, Does.Contain("public static class AssetsMap"));
            Assert.That(source, Does.Contain("Assets/GameHotUpdate/Manifest/HotUpdateManifest.json"));
            Assert.That(source, Does.Contain("Assets/Resources/HotUpdateSettings.asset"));
            Assert.That(source, Does.Not.Contain("Assets/AddressableAssetsData/"));
            Assert.That(source, Does.Not.Contain("/Editor/"));
            Assert.That(source, Does.Not.Contain("InitTestScene"));
        }

        [Test]
        public void GeneratedConstantsExposeExpectedStrongNames()
        {
            Assert.That(
                AssetsMap.GameHotUpdate.Manifest.HotUpdateManifest,
                Is.EqualTo("Assets/GameHotUpdate/Manifest/HotUpdateManifest.json"));
            Assert.That(
                AssetsMap.Resources.HotUpdateSettings,
                Is.EqualTo("Assets/Resources/HotUpdateSettings.asset"));
        }

        [Test]
        public void ResourcesBackendAcceptsCanonicalAssetsMapPath()
        {
            using (ResScope scope = StellarFramework.Res.ResKit.CreateScope(ResLoadBackend.Resources, "AssetsMapTest"))
            {
                HotUpdateSettings settings = scope.Load<HotUpdateSettings>(AssetsMap.Resources.HotUpdateSettings);
                Assert.That(settings, Is.Not.Null);
            }
        }

        [Test]
        public void RegenerationIsStableWhenProjectAssetsDidNotChange()
        {
            AssetsMapGenerator.GenerateIfNeeded();
            Assert.That(AssetsMapGenerator.GenerateIfNeeded(), Is.False);
        }

        [Test]
        public void AutoGenerationIgnoresCodeAndEditorChanges()
        {
            Assert.That(AssetsMapGenerator.MayAffectGeneratedMap(
                "Assets/StellarFramework/Editor/Foo.cs"), Is.False);
            Assert.That(AssetsMapGenerator.MayAffectGeneratedMap(
                "Assets/StellarFramework/Runtime/Foo.cs"), Is.False);
            Assert.That(AssetsMapGenerator.MayAffectGeneratedMap(
                "Assets/Game/Prefabs/Hero.prefab"), Is.True);
        }

        [Test]
        public void GeneratedMemberNeverMatchesItsEnclosingFolderTypeName()
        {
            MethodInfo buildSource = typeof(AssetsMapGenerator).GetMethod(
                "BuildSource",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(buildSource, Is.Not.Null);
            string source = (string)buildSource.Invoke(
                null,
                new object[]
                {
                    new[] { "Assets/UIKitAdaptationDeviceDemo/UIKitAdaptationDeviceDemo.unity" }
                });

            Assert.That(source, Does.Contain("public static class UIKitAdaptationDeviceDemo"));
            Assert.That(
                source,
                Does.Contain("public const string UIKitAdaptationDeviceDemo_unity = \"Assets/UIKitAdaptationDeviceDemo/UIKitAdaptationDeviceDemo.unity\";"));
            Assert.That(
                source,
                Does.Not.Contain("public const string UIKitAdaptationDeviceDemo ="));
        }
    }
}
