using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HotUpdatePublisherWorkflowAssemblyTests
    {
        private string _temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(Path.GetTempPath(), "StellarPublisherAssemblyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
        }

        [Test]
        public void Create_ComposesFullPipelineAndDryRunWithoutMutationStages()
        {
            var context = new HotUpdatePublishContext { PublishTarget = "LocalFolder" };

            HotUpdatePublisherWorkflow workflow = HotUpdatePublisherWorkflowAssembly.Create(
                new PassingGitSnapshotProvider(),
                context,
                new HotUpdateChangeClassifier(new EmptyChangeSource(), new EmptyChangeFactsProvider()),
                new FakeBuildAdapter(),
                new HotUpdateArtifactValidator(new HotUpdateBaseReleaseRepository(Path.Combine(_temporaryRoot, "BaseReleases"))),
                new PassingFastGateRunner(),
                null,
                new FakePublishTarget(),
                new FakeRemoteVerifier(),
                new HotUpdateReleaseHistoryRepository(Path.Combine(_temporaryRoot, "History")));

            Assert.That(workflow.StageHandlers.Count, Is.EqualTo(12));
            Assert.That(workflow.FullStages.Count, Is.EqualTo(12));
            Assert.That(workflow.DryRunStages.Count, Is.EqualTo(8));
            Assert.That(ContainsStage(workflow.DryRunStages, HotUpdatePublishStage.UploadFiles), Is.False);
            Assert.That(ContainsStage(workflow.DryRunStages, HotUpdatePublishStage.VerifyRemote), Is.False);
            Assert.That(ContainsStage(workflow.DryRunStages, HotUpdatePublishStage.PublishVersion), Is.False);
            Assert.That(ContainsStage(workflow.DryRunStages, HotUpdatePublishStage.Finalize), Is.False);
            Assert.That(context.PublishTargetAdapter, Is.Not.Null);
        }

        [Test]
        public void Create_RequiresConfiguredTargetIdentifierBeforeAssembling()
        {
            var context = new HotUpdatePublishContext();

            Assert.Throws<ArgumentException>(() => HotUpdatePublisherWorkflowAssembly.Create(
                new PassingGitSnapshotProvider(),
                context,
                new HotUpdateChangeClassifier(new EmptyChangeSource(), new EmptyChangeFactsProvider()),
                new FakeBuildAdapter(),
                new HotUpdateArtifactValidator(new HotUpdateBaseReleaseRepository(Path.Combine(_temporaryRoot, "BaseReleases"))),
                new PassingFastGateRunner(),
                null,
                new FakePublishTarget(),
                new FakeRemoteVerifier(),
                new HotUpdateReleaseHistoryRepository(Path.Combine(_temporaryRoot, "History"))));
        }

        private static bool ContainsStage(IReadOnlyList<HotUpdatePublishStage> stages, HotUpdatePublishStage expected)
        {
            for (int index = 0; index < stages.Count; index++)
                if (stages[index] == expected) return true;
            return false;
        }

        private sealed class EmptyChangeSource : IHotUpdateWorkspaceChangeSource
        {
            public IReadOnlyList<HotUpdateWorkspaceChange> ReadChanges() => Array.Empty<HotUpdateWorkspaceChange>();
        }

        private sealed class EmptyChangeFactsProvider : IHotUpdateChangeFactsProvider
        {
            public HotUpdateChangeFacts Resolve(HotUpdateWorkspaceChange change) => null;
            public IReadOnlyList<HotUpdateDependencyBoundaryViolation> FindDependencyBoundaryViolations() =>
                Array.Empty<HotUpdateDependencyBoundaryViolation>();
        }

        private sealed class PassingGitSnapshotProvider : IHotUpdateGitSnapshotProvider
        {
            public HotUpdateGitSnapshot ReadSnapshot() => new HotUpdateGitSnapshot("test", "test-commit", false);
        }

        private sealed class FakeBuildAdapter : IHotUpdateBuildAdapter
        {
            public Task<HotUpdatePublishStepResult> CompileHotUpdateAsync(HotUpdatePublishContext context, CancellationToken cancellationToken) => Succeeded();
            public Task<HotUpdatePublishStepResult> ExportHybridCLRAssetsAsync(HotUpdatePublishContext context, CancellationToken cancellationToken) => Succeeded();
            public Task<HotUpdatePublishStepResult> BuildYooAssetAsync(HotUpdatePublishContext context, CancellationToken cancellationToken) => Succeeded();
            private static Task<HotUpdatePublishStepResult> Succeeded() => Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }

        private sealed class PassingFastGateRunner : IHotUpdateFastReleaseGateRunner
        {
            public Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken) =>
                Task.FromResult(new HotUpdateReleaseGateRun { Passed = true });
        }

        private sealed class FakePublishTarget : IHotUpdatePublishTarget
        {
            public Task<HotUpdatePublishTargetFileInfo> UploadAsync(HotUpdatePublishFile file, CancellationToken cancellationToken) => Task.FromResult<HotUpdatePublishTargetFileInfo>(null);
            public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult(false);
            public Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult<HotUpdatePublishTargetFileInfo>(null);
            public Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task PublishVersionAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task RollbackAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeRemoteVerifier : IHotUpdatePrePublishRemoteVerifier
        {
            public Task VerifyAsync(HotUpdatePublishContext context, IReadOnlyList<HotUpdatePublishFile> immutableFiles, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
