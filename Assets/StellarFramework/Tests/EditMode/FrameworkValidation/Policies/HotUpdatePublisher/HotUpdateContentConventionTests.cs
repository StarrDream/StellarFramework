using System.IO;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateContentConventionTests
    {
        private const string GuidePath =
            "Assets/StellarFramework/FrameworkDoc/02-Kits/HybridCLRKit/HotUpdate-开发规范-Guide.md";

        [Test]
        public void NewProjectRootsMatchDocumentedConvention()
        {
            Assert.That(HotUpdateDevelopmentConvention.BaseRoot, Is.EqualTo("Assets/_Project/Base"));
            Assert.That(HotUpdateDevelopmentConvention.HotUpdateRoot, Is.EqualTo("Assets/_Project/HotUpdate"));
            Assert.That(HotUpdateDevelopmentConvention.RemoteContentRoot, Is.EqualTo("Assets/_Project/Content"));
            Assert.That(HotUpdateDevelopmentConvention.BuiltInContentRoot, Is.EqualTo("Assets/_Project/BaseContent"));
        }

        [Test]
        public void ExistingHotUpdateLayoutRemainsRecognizedWithoutMigration()
        {
            Assert.That(
                HotUpdateDevelopmentConvention.ClassifyLayer(
                    "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes"),
                Is.EqualTo(HotUpdateProjectLayer.HotUpdate));
            Assert.That(
                HotUpdateDevelopmentConvention.ClassifyLayer(
                    "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes"),
                Is.EqualTo(HotUpdateProjectLayer.HotUpdate));
            Assert.That(
                HotUpdateDevelopmentConvention.ClassifyLayer("Assets/Game/LegacyPrefab.prefab"),
                Is.EqualTo(HotUpdateProjectLayer.Unknown));
        }

        [Test]
        public void BuiltInAndRemoteContentHaveDistinctBoundaries()
        {
            Assert.That(
                HotUpdateDevelopmentConvention.ClassifyLayer(
                    "Assets/_Project/BaseContent/Scenes/Startup.unity"),
                Is.EqualTo(HotUpdateProjectLayer.BuiltInContent));
            Assert.That(
                HotUpdateDevelopmentConvention.ClassifyLayer(
                    "Assets/_Project/Content/Scenes/SeasonalEvent.unity"),
                Is.EqualTo(HotUpdateProjectLayer.RemoteContent));
        }

        [Test]
        public void HotUpdateMonoBehaviourIsRestrictedToRemoteContent()
        {
            Assert.That(
                HotUpdateDevelopmentConvention.CanPlaceHotUpdateMonoBehaviourIn(
                    "Assets/_Project/Content/Actors/Enemy.prefab"),
                Is.True);
            Assert.That(
                HotUpdateDevelopmentConvention.CanPlaceHotUpdateMonoBehaviourIn(
                    "Assets/_Project/BaseContent/Actors/Startup.prefab"),
                Is.False);
            Assert.That(
                HotUpdateDevelopmentConvention.GetHotUpdateBehaviourBoundaryError(
                    "Assets/_Project/BaseContent/Actors/Startup.prefab"),
                Does.Contain("Load the assembly before loading this content"));
        }

        [Test]
        public void GuideDocumentsBoundariesMigrationAndRiskLevels()
        {
            string absolutePath = Path.Combine(Application.dataPath, "StellarFramework/FrameworkDoc/02-Kits/HybridCLRKit/HotUpdate-开发规范-Guide.md");
            Assert.That(File.Exists(absolutePath), Is.True, "The HotUpdate development convention guide must exist.");

            string guide = File.ReadAllText(absolutePath);
            StringAssert.Contains("Assets/_Project/Base", guide);
            StringAssert.Contains("Assets/_Project/HotUpdate", guide);
            StringAssert.Contains("Assets/_Project/Content", guide);
            StringAssert.Contains("Assets/_Project/BaseContent", guide);
            StringAssert.Contains("旧项目", guide);
            StringAssert.Contains("Base App → HotUpdate", guide);
            StringAssert.Contains("YooAsset", guide);
            StringAssert.Contains("Shader", guide);
            StringAssert.Contains("GREEN", guide);
            StringAssert.Contains("YELLOW", guide);
            StringAssert.Contains("RED", guide);
        }
    }
}
