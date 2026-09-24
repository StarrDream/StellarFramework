using System.Collections.Generic;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateChangeClassifierTests
    {
        [Test]
        public void HotUpdateSourceAndRemoteContentAreGreenAndRequireFastGate()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                Facts("Assets/_Project/HotUpdate/Gameplay/PlayerController.cs",
                    HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.HotUpdate),
                Facts("Assets/_Project/Content/UI/MainMenu.prefab",
                    HotUpdateChangeAssetKind.Prefab, HotUpdateProjectLayer.RemoteContent),
                Facts("Assets/_Project/Content/Config/Balance.json",
                    HotUpdateChangeAssetKind.Configuration, HotUpdateProjectLayer.RemoteContent)
            });

            Assert.That(result.GreenCount, Is.EqualTo(3));
            Assert.That(result.YellowCount, Is.Zero);
            Assert.That(result.RedCount, Is.Zero);
            Assert.That(result.CanHotPatch, Is.True);
            Assert.That(result.RequiresFastGate, Is.True);
            Assert.That(result.RequiresFullGate, Is.False);
        }

        [Test]
        public void ShaderAotSensitiveSourceAndHotUpdateAsmdefRequireFullGate()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                Facts("Assets/_Project/Content/Shaders/World.shader",
                    HotUpdateChangeAssetKind.Shader, HotUpdateProjectLayer.RemoteContent),
                new HotUpdateChangeFacts(
                    "Assets/_Project/HotUpdate/Gameplay/HotUpdate.asmdef",
                    HotUpdateChangeAssetKind.AssemblyDefinition,
                    HotUpdateProjectLayer.HotUpdate,
                    assemblyName: "HotUpdate",
                    assemblyReferencesChanged: true),
                new HotUpdateChangeFacts(
                    "Assets/_Project/HotUpdate/Gameplay/ReflectionBridge.cs",
                    HotUpdateChangeAssetKind.CSharpSource,
                    HotUpdateProjectLayer.HotUpdate,
                    hasAotSensitiveApiDependency: true)
            });

            Assert.That(result.GreenCount, Is.Zero);
            Assert.That(result.YellowCount, Is.EqualTo(3));
            Assert.That(result.RedCount, Is.Zero);
            Assert.That(result.CanHotPatch, Is.True);
            Assert.That(result.RequiresFastGate, Is.True);
            Assert.That(result.RequiresFullGate, Is.True);
        }

        [Test]
        public void BaseProjectConfigAndNativePluginChangesAreRed()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                Facts("Assets/_Project/Base/Bootstrap/Bootstrapper.cs",
                    HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.BaseApp),
                Facts("Assets/_Project/Base/Bootstrap/Game.Base.asmdef",
                    HotUpdateChangeAssetKind.AssemblyDefinition, HotUpdateProjectLayer.BaseApp),
                Facts("ProjectSettings/ProjectSettings.asset",
                    HotUpdateChangeAssetKind.ProjectConfiguration, HotUpdateProjectLayer.Unknown),
                Facts("Packages/manifest.json",
                    HotUpdateChangeAssetKind.ProjectConfiguration, HotUpdateProjectLayer.Unknown),
                Facts("Packages/com.example.game/Runtime/Bootstrap.cs",
                    HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.Unknown),
                Facts("Assets/Plugins/Android/game-sdk.aar",
                    HotUpdateChangeAssetKind.NativePlugin, HotUpdateProjectLayer.Unknown)
            });

            Assert.That(result.RedCount, Is.EqualTo(6));
            Assert.That(result.CanHotPatch, Is.False);
            Assert.That(result.RequiresFastGate, Is.False);
        }

        [Test]
        public void BuiltInContentAndHotUpdateBehaviourOutsideRemoteRootAreRed()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                Facts("Assets/_Project/BaseContent/Scenes/Startup.unity",
                    HotUpdateChangeAssetKind.Scene, HotUpdateProjectLayer.BuiltInContent),
                new HotUpdateChangeFacts(
                    "Assets/_Project/BaseContent/UI/Startup.prefab",
                    HotUpdateChangeAssetKind.Prefab,
                    HotUpdateProjectLayer.BuiltInContent,
                    containsHotUpdateMonoBehaviour: true)
            });

            Assert.That(result.RedCount, Is.EqualTo(2));
            Assert.That(result.CanHotPatch, Is.False);
            Assert.That(result.Changes[1].Reason, Does.Contain("YooAsset remote-content root"));
        }

        [Test]
        public void HotUpdateBehaviourInRemoteContentIsAllowed()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                new HotUpdateChangeFacts(
                    "Assets/_Project/Content/Gameplay/Enemy.prefab",
                    HotUpdateChangeAssetKind.Prefab,
                    HotUpdateProjectLayer.RemoteContent,
                    containsHotUpdateMonoBehaviour: true)
            });

            Assert.That(result.GreenCount, Is.EqualTo(1));
            Assert.That(result.RedCount, Is.Zero);
            Assert.That(result.CanHotPatch, Is.True);
        }

        [Test]
        public void UnknownAssetTypeRemainsYellowEvenUnderHotUpdateRoot()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                Facts("Assets/_Project/Content/Custom/Unknown.asset",
                    HotUpdateChangeAssetKind.Unknown, HotUpdateProjectLayer.RemoteContent)
            });

            Assert.That(result.YellowCount, Is.EqualTo(1));
            Assert.That(result.RequiresFullGate, Is.True);
            Assert.That(result.Changes[0].Reason, Does.Contain("not declared"));
        }

        [Test]
        public void GitPorcelainParserPreservesSpacesAndRenameSourcePath()
        {
            IReadOnlyList<HotUpdateWorkspaceChange> changes =
                GitHotUpdateWorkspaceChangeSource.ParsePorcelainStatus(
                    " M Assets/_Project/HotUpdate/My Player.cs\0R  Assets/_Project/Content/New.prefab\0Assets/_Project/Content/Old.prefab\0");

            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].Path, Is.EqualTo("Assets/_Project/HotUpdate/My Player.cs"));
            Assert.That(changes[0].Kind, Is.EqualTo(HotUpdateChangeKind.Modified));
            Assert.That(changes[1].Path, Is.EqualTo("Assets/_Project/Content/New.prefab"));
            Assert.That(changes[1].Kind, Is.EqualTo(HotUpdateChangeKind.Renamed));
            Assert.That(changes[2].Path, Is.EqualTo("Assets/_Project/Content/Old.prefab"));
            Assert.That(changes[2].Kind, Is.EqualTo(HotUpdateChangeKind.Deleted));
        }

        [Test]
        public void RedChangeDominatesGreenChanges()
        {
            HotUpdateChangeClassificationResult result = HotUpdateChangeClassifier.Classify(new[]
            {
                Facts("Assets/_Project/HotUpdate/Gameplay/PlayerController.cs",
                    HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.HotUpdate),
                Facts("Packages/manifest.json",
                    HotUpdateChangeAssetKind.ProjectConfiguration, HotUpdateProjectLayer.Unknown)
            });

            Assert.That(result.GreenCount, Is.EqualTo(1));
            Assert.That(result.RedCount, Is.EqualTo(1));
            Assert.That(result.CanHotPatch, Is.False);
            Assert.That(result.RequiresFastGate, Is.False);
        }

        private static HotUpdateChangeFacts Facts(
            string path,
            HotUpdateChangeAssetKind assetKind,
            HotUpdateProjectLayer layer)
        {
            return new HotUpdateChangeFacts(path, assetKind, layer);
        }
    }
}
