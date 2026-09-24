using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateImmutablePublishStageHandlerTests
    {
        private string _root;
        private HotUpdatePublishContext _context;
        private RecordingPublishTarget _target;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "StellarImmutablePublishTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "PackageManifest.bin"), "binary manifest");
            File.WriteAllText(Path.Combine(_root, "PackageManifest.json"), "json manifest");
            File.WriteAllText(Path.Combine(_root, "PackageManifest.hash"), "manifest hash");
            File.WriteAllText(Path.Combine(_root, "PackageVersion"), "2.0.0");
            Directory.CreateDirectory(Path.Combine(_root, "bundles"));
            File.WriteAllText(Path.Combine(_root, "bundles", "content.bundle"), "bundle bytes");

            _target = new RecordingPublishTarget();
            _context = new HotUpdatePublishContext
            {
                PackageVersion = "2.0.0",
                ExpectedCurrentPackageVersion = "1.9.0",
                PublishTargetAdapter = _target,
                YooAssetBuildOutput = new YooAssetBuildOutput
                {
                    PackageVersion = "2.0.0",
                    OutputDirectory = _root,
                    BundleCount = 1,
                    TotalBytes = 64,
                    ManifestFiles = new[] { "PackageManifest.bin", "PackageManifest.json", "PackageManifest.hash", "PackageVersion" }
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void PrepareUpload_SnapshotsEveryFileExceptMutablePackageVersionPointer()
        {
            HotUpdatePublishStepResult result = new HotUpdatePrepareUploadStageHandler()
                .ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True);
            Assert.That(_context.PublishFiles.Count, Is.EqualTo(4));
            Assert.That(_context.PublishFiles, Has.None.Property("RelativePath").EqualTo("PackageVersion"));
            Assert.That(_context.VersionPublishRequest.PointerRelativePath, Is.EqualTo("PackageVersion"));
            Assert.That(_context.VersionPublishRequest.PackageVersion, Is.EqualTo("2.0.0"));
            Assert.That(_context.VersionPublishRequest.ExpectedCurrentPackageVersion, Is.EqualTo("1.9.0"));
        }

        [Test]
        public void PublishStages_UploadThenVerifyAllThenRangeThenPublishVersion()
        {
            PrepareUpload();
            var rangeVerifier = new RecordingRemoteVerifier(_target.Events);

            Assert.That(new HotUpdateUploadFilesStageHandler().ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult().Success, Is.True);
            Assert.That(new HotUpdateRemoteVerificationStageHandler(rangeVerifier).ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult().Success, Is.True);
            Assert.That(new HotUpdatePublishVersionStageHandler().ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult().Success, Is.True);

            int targetVerifyIndex = _target.Events.IndexOf("target-verify");
            int rangeIndex = _target.Events.IndexOf("range-verify");
            int versionIndex = _target.Events.IndexOf("publish-version");
            Assert.That(targetVerifyIndex, Is.GreaterThanOrEqualTo(_context.PublishFiles.Count));
            Assert.That(rangeIndex, Is.GreaterThan(targetVerifyIndex));
            Assert.That(versionIndex, Is.EqualTo(_target.Events.Count - 1));
            Assert.That(_target.Events[versionIndex], Is.EqualTo("publish-version"));
        }

        [Test]
        public void PublishVersionStage_CannotRunBeforeSuccessfulRemoteChecks()
        {
            PrepareUpload();
            HotUpdatePublishStepResult early = new HotUpdatePublishVersionStageHandler()
                .ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(early.Success, Is.False);
            Assert.That(_target.Events, Does.Not.Contain("publish-version"));

            new HotUpdateUploadFilesStageHandler().ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult();
            Assert.Throws<IOException>(() => new HotUpdateRemoteVerificationStageHandler(new ThrowingRemoteVerifier())
                .ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult());
            HotUpdatePublishStepResult afterRemoteFailure = new HotUpdatePublishVersionStageHandler()
                .ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(afterRemoteFailure.Success, Is.False);
            Assert.That(_target.Events, Does.Not.Contain("publish-version"));
        }

        private void PrepareUpload()
        {
            new HotUpdatePrepareUploadStageHandler().ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult();
        }

        private sealed class RecordingPublishTarget : IHotUpdatePublishTarget
        {
            public readonly List<string> Events = new List<string>();

            public Task<HotUpdatePublishTargetFileInfo> UploadAsync(HotUpdatePublishFile file, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Events.Add("upload:" + file.RelativePath);
                return Task.FromResult(new HotUpdatePublishTargetFileInfo(file.RelativePath, file.Length, file.Sha256));
            }

            public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult(false);
            public Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult<HotUpdatePublishTargetFileInfo>(null);

            public Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Events.Add("target-verify");
                return Task.CompletedTask;
            }

            public Task PublishVersionAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken)
            {
                Events.Add("publish-version");
                return Task.CompletedTask;
            }

            public Task RollbackAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class RecordingRemoteVerifier : IHotUpdatePrePublishRemoteVerifier
        {
            private readonly List<string> _events;
            public RecordingRemoteVerifier(List<string> events) { _events = events; }

            public Task VerifyAsync(HotUpdatePublishContext context, IReadOnlyList<HotUpdatePublishFile> immutableFiles, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(context.ImmutableUploadCompleted, Is.True);
                Assert.That(immutableFiles.Count, Is.EqualTo(context.PublishFiles.Count));
                _events.Add("range-verify");
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingRemoteVerifier : IHotUpdatePrePublishRemoteVerifier
        {
            public Task VerifyAsync(HotUpdatePublishContext context, IReadOnlyList<HotUpdatePublishFile> immutableFiles, CancellationToken cancellationToken)
            {
                throw new IOException("Remote GET/Range probe failed.");
            }
        }
    }
}
