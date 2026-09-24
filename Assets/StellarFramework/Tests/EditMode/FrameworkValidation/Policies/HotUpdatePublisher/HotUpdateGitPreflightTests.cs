using System;
using System.Threading;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateGitPreflightTests
    {
        [Test]
        public void DetachedHeadIsRecordedExplicitly()
        {
            var snapshot = new HotUpdateGitSnapshot("HEAD", new string('d', 40), false);

            Assert.That(snapshot.Branch, Is.EqualTo("(detached)"));
        }

        [Test]
        public void CleanRepositoryCapturesBranchCommitAndDirtyState()
        {
            var context = new HotUpdatePublishContext { Environment = "Production" };
            var handler = new HotUpdateGitPreflightStageHandler(new FakeGitSnapshotProvider("main", new string('a', 40), false));

            HotUpdatePublishStepResult result = handler.ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True);
            Assert.That(context.GitBranch, Is.EqualTo("main"));
            Assert.That(context.GitCommit, Is.EqualTo(new string('a', 40)));
            Assert.That(context.GitDirty, Is.False);
        }

        [Test]
        public void DirtyProductionRepositoryIsRejectedAfterCapturingProvenance()
        {
            var context = new HotUpdatePublishContext { Environment = "pRoDuCtIoN" };
            var handler = new HotUpdateGitPreflightStageHandler(new FakeGitSnapshotProvider("release", new string('b', 40), true));

            HotUpdatePublishStepResult result = handler.ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.StageFailed));
            Assert.That(context.GitBranch, Is.EqualTo("release"));
            Assert.That(context.GitCommit, Is.EqualTo(new string('b', 40)));
            Assert.That(context.GitDirty, Is.True);
        }

        [TestCase("Development")]
        [TestCase("Staging")]
        public void DirtyNonProductionRepositoryIsAllowedWithProvenanceWarning(string environment)
        {
            var context = new HotUpdatePublishContext { Environment = environment };
            var handler = new HotUpdateGitPreflightStageHandler(new FakeGitSnapshotProvider("feature/work", new string('c', 40), true));

            HotUpdatePublishStepResult result = handler.ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings.Count, Is.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("dirty"));
            Assert.That(context.GitDirty, Is.True);
        }

        [Test]
        public void UnknownEnvironmentIsRejectedFailClosed()
        {
            var context = new HotUpdatePublishContext { Environment = "Preview" };
            var handler = new HotUpdateGitPreflightStageHandler(new FakeGitSnapshotProvider("main", new string('e', 40), false));

            HotUpdatePublishStepResult result = handler.ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("Unsupported publishing environment"));
        }

        private sealed class FakeGitSnapshotProvider : IHotUpdateGitSnapshotProvider
        {
            private readonly HotUpdateGitSnapshot _snapshot;
            public FakeGitSnapshotProvider(string branch, string commit, bool dirty)
            {
                _snapshot = new HotUpdateGitSnapshot(branch, commit, dirty);
            }
            public HotUpdateGitSnapshot ReadSnapshot() => _snapshot;
        }
    }
}
