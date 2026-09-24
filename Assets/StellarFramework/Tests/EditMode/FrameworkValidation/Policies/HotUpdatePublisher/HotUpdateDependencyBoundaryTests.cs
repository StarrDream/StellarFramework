using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateDependencyBoundaryTests
    {
        [Test]
        public void HotUpdateMayDependOnBaseApp()
        {
            HotUpdateAssemblyDefinition[] definitions =
            {
                new HotUpdateAssemblyDefinition(
                    "Game.Base", "Assets/_Project/Base/Game.Base.asmdef", "base-guid",
                    new[] { "UnityEngine.CoreModule" }),
                new HotUpdateAssemblyDefinition(
                    "HotUpdate", "Assets/_Project/HotUpdate/HotUpdate.asmdef", "hot-guid",
                    new[] { "Game.Base" })
            };

            Assert.That(HotUpdateDependencyBoundaryPolicy.FindViolations(definitions), Is.Empty);
        }

        [Test]
        public void BaseAppMayNotDependOnHotUpdateByAssemblyName()
        {
            HotUpdateAssemblyDefinition[] definitions =
            {
                new HotUpdateAssemblyDefinition(
                    "Game.Base", "Assets/_Project/Base/Game.Base.asmdef", "base-guid",
                    new[] { "HotUpdate" }),
                new HotUpdateAssemblyDefinition(
                    "HotUpdate", "Assets/_Project/HotUpdate/HotUpdate.asmdef", "hot-guid",
                    new string[0])
            };

            var violations = HotUpdateDependencyBoundaryPolicy.FindViolations(definitions);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].SourceAssembly, Is.EqualTo("Game.Base"));
            Assert.That(violations[0].TargetAssembly, Is.EqualTo("HotUpdate"));
            Assert.That(violations[0].Message, Does.Contain("must not depend"));
        }

        [Test]
        public void BaseAppMayNotDependOnHotUpdateByAsmdefGuid()
        {
            HotUpdateAssemblyDefinition[] definitions =
            {
                new HotUpdateAssemblyDefinition(
                    "Game.Base", "Assets/_Project/Base/Game.Base.asmdef", "base-guid",
                    new[] { "GUID:HOT-GUID" }),
                new HotUpdateAssemblyDefinition(
                    "Game.HotUpdate", "Assets/_Project/HotUpdate/Game.HotUpdate.asmdef", "hot-guid",
                    new string[0])
            };

            Assert.That(HotUpdateDependencyBoundaryPolicy.FindViolations(definitions).Count, Is.EqualTo(1));
        }

        [Test]
        public void EditorOnlyAssemblyReferenceDoesNotCreatePlayerBoundaryViolation()
        {
            HotUpdateAssemblyDefinition[] definitions =
            {
                new HotUpdateAssemblyDefinition(
                    "Game.Editor", "Assets/Editor/Game.Editor.asmdef", "editor-guid",
                    new[] { "HotUpdate" }),
                new HotUpdateAssemblyDefinition(
                    "HotUpdate", "Assets/_Project/HotUpdate/Game.HotUpdate.asmdef", "hot-guid",
                    new string[0])
            };

            Assert.That(HotUpdateDependencyBoundaryPolicy.FindViolations(definitions), Is.Empty);
        }
    }
}
