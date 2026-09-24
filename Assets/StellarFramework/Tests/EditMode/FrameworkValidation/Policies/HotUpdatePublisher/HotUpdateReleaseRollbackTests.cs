using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEditor;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateReleaseRollbackTests
    {
        private string _root;
        private HotUpdateReleaseHistoryRepository _history;
        private StubTarget _target;
        private StubRemoteVerifier _verifier;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "stellar-release-rollback-" + Guid.NewGuid().ToString("N"));
            _history = new HotUpdateReleaseHistoryRepository(_root);
            _target = new StubTarget();
            _verifier = new StubRemoteVerifier(_target);
            _history.SaveActivated(Record("release-old", "2026.09.24.001"));
            _history.SaveActivated(Record("release-current", "2026.09.24.002"));
            _target.Files["manifest.json"] = new HotUpdatePublishTargetFileInfo("manifest.json", 100, new string('a', 64));
            _target.Files["Bundles/a.bundle"] = new HotUpdatePublishTargetFileInfo("Bundles/a.bundle", 300000, new string('b', 64));
            _target.CurrentVersion = "2026.09.24.002";
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void RollbackVerifiesRemoteArtifactsThenChangesPointerAndRecordsEventWithoutUploading()
        {
            _verifier.BeforeVerify = () => _target.Order.Add("remote-verify");
            HotUpdateRollbackResult result = Service().RollbackAsync("release-current", "release-old")
                .GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.PointerChanged, Is.True);
            Assert.That(_target.CurrentVersion, Is.EqualTo("2026.09.24.001"));
            CollectionAssert.AreEqual(new[]
            {
                "get:manifest.json", "get:Bundles/a.bundle", "publish-version", "remote-verify"
            }, _target.Order);
            Assert.That(_target.UploadCount, Is.Zero);
            Assert.That(_target.VerifyCount, Is.Zero);
            Assert.That(_target.LastVersionRequest.ExpectedCurrentPackageVersion, Is.EqualTo("2026.09.24.002"));
            Assert.That(_history.Load("release-old").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.Active));
            Assert.That(_history.Load("release-current").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.RolledBack));
            Assert.That(ContainsEvent("Rollback"), Is.True);
        }

        [Test]
        public void MissingOrTamperedHistoricalFileBlocksPointerChange()
        {
            _target.Files["Bundles/a.bundle"] = new HotUpdatePublishTargetFileInfo("Bundles/a.bundle", 300000, new string('c', 64));

            HotUpdateRollbackResult result = Service().RollbackAsync("release-current", "release-old")
                .GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.PointerChanged, Is.False);
            Assert.That(result.Error, Does.Contain("SHA256"));
            Assert.That(_target.CurrentVersion, Is.EqualTo("2026.09.24.002"));
            Assert.That(_target.LastVersionRequest, Is.Null);
            Assert.That(_verifier.CallCount, Is.Zero);
            Assert.That(_target.UploadCount, Is.Zero);
        }

        [Test]
        public void RollbackVerificationFailureIsRecordedAndReportedAfterPointerChange()
        {
            _verifier.Exception = new IOException("Historical Manifest GET returned HTTP 500.");

            HotUpdateRollbackResult result = Service().RollbackAsync("release-current", "release-old")
                .GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.PointerChanged, Is.True);
            Assert.That(result.Error, Does.Contain("pointer changed"));
            Assert.That(_history.Load("release-current").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.RolledBack));
            Assert.That(_history.Load("release-old").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.RollbackUnverified));
            Assert.That(ContainsEvent("RollbackVerificationFailed"), Is.True);
        }

        [Test]
        public void WrongEnvironmentOrAlreadyActiveTargetIsRejectedBeforeRemoteReads()
        {
            // Seed the current active release into a separate history, then point at an incompatible Staging record.
            string otherRoot = Path.Combine(_root, "other");
            var otherHistory = new HotUpdateReleaseHistoryRepository(otherRoot);
            otherHistory.SaveActivated(Record("release-current", "2026.09.24.002"));
            otherHistory.SaveActivated(Record("other-old", "2026.09.24.001", "Staging"));

            HotUpdateRollbackResult result = new HotUpdateReleaseRollbackService(otherHistory, _target, _verifier)
                .RollbackAsync("release-current", "other-old").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.PointerChanged, Is.False);
            Assert.That(_target.Order, Is.Empty);
        }

        private HotUpdateReleaseRollbackService Service() => new HotUpdateReleaseRollbackService(_history, _target, _verifier);

        private bool ContainsEvent(string eventType)
        {
            string[] paths = Directory.GetFiles(Path.Combine(_root, "Events"), "*.json");
            for (int index = 0; index < paths.Length; index++)
                if (File.ReadAllText(paths[index]).Contains(eventType)) return true;
            return false;
        }

        private static HotUpdateReleaseRecord Record(string releaseId, string version, string environment = "Production")
        {
            return new HotUpdateReleaseRecord
            {
                ReleaseId = releaseId,
                PackageName = "GameContent",
                PackageVersion = version,
                BaseAppVersion = "1.0.0",
                Platform = BuildTarget.Android,
                Environment = environment,
                GitCommit = "deadbeef",
                GitBranch = "main",
                HotUpdateDllSha256 = new string('d', 64),
                BundleCount = 1,
                ChangeClassification = new HotUpdateReleaseChangeClassification { GreenCount = 1, Safety = "GREEN" },
                Files = new[]
                {
                    new HotUpdateReleaseFileRecord { RelativePath = "manifest.json", Length = 100, Sha256 = new string('a', 64) },
                    new HotUpdateReleaseFileRecord { RelativePath = "Bundles/a.bundle", Length = 300000, Sha256 = new string('b', 64) }
                },
                ManifestFiles = new[] { "manifest.hash", "manifest.json", "manifest.version", "PackageVersion" },
                TotalBytes = 300100,
                GateResult = "FAST_PASS",
                ServerRoot = "https://cdn.example.test/GameContent/Android/Production",
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        private sealed class StubTarget : IHotUpdatePublishTarget
        {
            public readonly Dictionary<string, HotUpdatePublishTargetFileInfo> Files = new Dictionary<string, HotUpdatePublishTargetFileInfo>();
            public readonly List<string> Order = new List<string>();
            public string CurrentVersion;
            public int UploadCount { get; private set; }
            public int VerifyCount { get; private set; }
            public HotUpdateVersionPublishRequest LastVersionRequest { get; private set; }

            public Task<HotUpdatePublishTargetFileInfo> UploadAsync(HotUpdatePublishFile file, CancellationToken cancellationToken)
            {
                UploadCount++;
                throw new InvalidOperationException("Rollback must never upload historical files.");
            }
            public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult(Files.ContainsKey(relativePath));
            public Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(string relativePath, CancellationToken cancellationToken)
            {
                Order.Add("get:" + relativePath);
                Files.TryGetValue(relativePath, out HotUpdatePublishTargetFileInfo result);
                return Task.FromResult(result);
            }
            public Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken)
            {
                VerifyCount++;
                throw new InvalidOperationException("Rollback verification must use the remote runtime verifier.");
            }
            public Task PublishVersionAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken)
            {
                Order.Add("publish-version");
                if (!string.Equals(CurrentVersion, request.ExpectedCurrentPackageVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException("Version pointer compare-and-swap failed.");
                LastVersionRequest = request;
                CurrentVersion = request.PackageVersion;
                return Task.CompletedTask;
            }
            public Task RollbackAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("The historical rollback path must publish its target version directly.");
        }

        private sealed class StubRemoteVerifier : IHotUpdateHistoricalReleaseRemoteVerifier
        {
            private readonly StubTarget _target;
            public StubRemoteVerifier(StubTarget target) => _target = target;
            public Action BeforeVerify { get; set; }
            public Exception Exception { get; set; }
            public int CallCount { get; private set; }

            public Task VerifyRollbackAsync(HotUpdateReleaseRecord release, CancellationToken cancellationToken)
            {
                CallCount++;
                BeforeVerify?.Invoke();
                if (Exception != null) throw Exception;
                Assert.That(_target.CurrentVersion, Is.EqualTo(release.PackageVersion));
                return Task.CompletedTask;
            }
        }
    }
}
