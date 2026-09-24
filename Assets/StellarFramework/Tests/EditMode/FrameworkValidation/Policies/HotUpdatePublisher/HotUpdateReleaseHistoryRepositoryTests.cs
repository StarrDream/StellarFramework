using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEditor;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateReleaseHistoryRepositoryTests
    {
        private string _root;
        private HotUpdateReleaseHistoryRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "stellar-release-history-" + Guid.NewGuid().ToString("N"));
            _repository = new HotUpdateReleaseHistoryRepository(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ActivatedRecordPersistsClassificationAndFileHashes()
        {
            HotUpdateReleaseRecord record = Record("release-001", "2026.09.24.001");
            _repository.SaveActivated(record);

            HotUpdateReleaseRecord loaded = _repository.Load(record.ReleaseId);
            Assert.That(loaded.Status, Is.EqualTo(HotUpdateReleaseRecordStatus.Active));
            Assert.That(loaded.Files, Has.Length.EqualTo(2));
            Assert.That(loaded.Files[1].RelativePath, Is.EqualTo("Bundles/a.bundle"));
            Assert.That(loaded.Files[0].Sha256, Is.EqualTo(new string('a', 64)));
            Assert.That(loaded.ChangeClassification.Safety, Is.EqualTo("GREEN"));
            Assert.That(_repository.List(), Has.Count.EqualTo(1));
            Assert.That(Directory.GetFiles(Path.Combine(_root, "Events"), "*.json").Length, Is.EqualTo(1));
        }

        [Test]
        public void NewActiveReleaseSupersedesPriorActiveForSamePackageEnvironmentAndPlatform()
        {
            _repository.SaveActivated(Record("release-001", "2026.09.24.001"));
            _repository.SaveActivated(Record("release-002", "2026.09.24.002"));

            Assert.That(_repository.Load("release-001").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.Superseded));
            Assert.That(_repository.Load("release-002").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.Active));
            Assert.That(_repository.List().Select(item => item.Status).ToArray(),
                Is.EquivalentTo(new[] { HotUpdateReleaseRecordStatus.Superseded, HotUpdateReleaseRecordStatus.Active }));
        }

        [Test]
        public void DuplicateReleaseIdIsRejectedWithoutReplacingExistingRecord()
        {
            _repository.SaveActivated(Record("release-001", "2026.09.24.001"));
            HotUpdateReleaseRecord duplicate = Record("release-001", "2026.09.24.999");

            Assert.Throws<IOException>(() => _repository.SaveActivated(duplicate));
            Assert.That(_repository.Load("release-001").PackageVersion, Is.EqualTo("2026.09.24.001"));
        }

        [Test]
        public void StatusTransitionPersistsSeparateHistoryEvent()
        {
            _repository.SaveActivated(Record("release-001", "2026.09.24.001"));

            _repository.SetStatus("release-001", HotUpdateReleaseRecordStatus.RolledBack,
                new HotUpdateReleaseHistoryEvent
                {
                    EventType = "Rollback",
                    FromVersion = "2026.09.24.001",
                    ToVersion = "2026.09.24.000",
                    Diagnostic = "Verified historical package and switched pointer."
                });

            Assert.That(_repository.Load("release-001").Status, Is.EqualTo(HotUpdateReleaseRecordStatus.RolledBack));
            string[] events = Directory.GetFiles(Path.Combine(_root, "Events"), "*.json");
            Assert.That(events, Has.Length.EqualTo(2));
            bool rollbackEventFound = false;
            for (int index = 0; index < events.Length; index++)
                rollbackEventFound |= File.ReadAllText(events[index]).Contains("Rollback");
            Assert.That(rollbackEventFound, Is.True);
        }

        [Test]
        public void ReleaseIdPathTraversalIsRejected()
        {
            Assert.Throws<ArgumentException>(() => _repository.Load("../outside"));
        }

        private static HotUpdateReleaseRecord Record(string releaseId, string version)
        {
            return new HotUpdateReleaseRecord
            {
                ReleaseId = releaseId,
                PackageName = "GameContent",
                PackageVersion = version,
                BaseAppVersion = "1.0.0",
                Platform = BuildTarget.Android,
                Environment = "Production",
                GitCommit = "deadbeef",
                GitBranch = "main",
                GitDirty = false,
                HotUpdateDllSha256 = new string('d', 64),
                BundleCount = 1,
                ChangeClassification = new HotUpdateReleaseChangeClassification
                {
                    GreenCount = 2,
                    Safety = "GREEN"
                },
                Files = new[]
                {
                    new HotUpdateReleaseFileRecord { RelativePath = "manifest.json", Length = 123, Sha256 = new string('a', 64) },
                    new HotUpdateReleaseFileRecord { RelativePath = "Bundles/a.bundle", Length = 456, Sha256 = new string('b', 64) }
                },
                ManifestFiles = new[] { "manifest.hash", "manifest.json", "manifest.version", "PackageVersion" },
                TotalBytes = 579,
                GateResult = "FAST_PASS",
                ServerRoot = "https://cdn.example.test/GameContent/Android/Production",
                CreatedAtUtc = DateTime.UtcNow,
                Status = HotUpdateReleaseRecordStatus.Active
            };
        }
    }
}
