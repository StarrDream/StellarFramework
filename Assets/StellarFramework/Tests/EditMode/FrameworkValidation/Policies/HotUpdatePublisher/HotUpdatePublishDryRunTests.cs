using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdatePublishDryRunTests
    {
        [Test]
        public void DryRunBuildsNewAndReusePlanWithoutRemoteMutation()
        {
            string root = CreatePackage();
            try
            {
                string reusePath = Path.Combine(root, "reused.bundle");
                var target = new StubTarget();
                target.Files.Add("reused.bundle", new HotUpdatePublishTargetFileInfo("reused.bundle",
                    new FileInfo(reusePath).Length, Sha256(reusePath)));
                HotUpdatePublishContext context = CreateContext(root, target);

                HotUpdateDryRunResult result = CreateDryRun().RunAsync(context).GetAwaiter().GetResult();

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.NoRemoteMutation, Is.True);
                Assert.That(result.ChangeSafety, Is.EqualTo("GREEN"));
                Assert.That(result.NewCount, Is.EqualTo(1));
                Assert.That(result.ReuseCount, Is.EqualTo(1));
                Assert.That(result.UploadBytes, Is.EqualTo(new FileInfo(Path.Combine(root, "new.bundle")).Length));
                Assert.That(result.TotalBytes, Is.EqualTo(result.UploadBytes + new FileInfo(reusePath).Length));
                CollectionAssert.AreEqual(new[]
                {
                    HotUpdatePublishStage.Preflight, HotUpdatePublishStage.ClassifyChanges,
                    HotUpdatePublishStage.CompileHotUpdate, HotUpdatePublishStage.ExportHybridCLRAssets,
                    HotUpdatePublishStage.BuildYooAsset, HotUpdatePublishStage.ValidateArtifacts,
                    HotUpdatePublishStage.RunReleaseGate, HotUpdatePublishStage.PrepareUpload
                }, result.ExecutedStages);
                Assert.That(target.UploadCount, Is.Zero);
                Assert.That(target.PublishVersionCount, Is.Zero);
                Assert.That(target.VerifyCount, Is.Zero);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ExistingImmutablePathWithDifferentBytesFailsInsteadOfOverwriting()
        {
            string root = CreatePackage();
            try
            {
                var target = new StubTarget();
                target.Files.Add("new.bundle", new HotUpdatePublishTargetFileInfo("new.bundle", 1, "wrong-hash"));

                HotUpdateDryRunResult result = CreateDryRun().RunAsync(CreateContext(root, target)).GetAwaiter().GetResult();

                Assert.That(result.Success, Is.False);
                Assert.That(result.FailedStage, Is.EqualTo(HotUpdatePublishStage.PrepareUpload));
                Assert.That(result.Error, Does.Contain("different bytes"));
                Assert.That(target.UploadCount, Is.Zero);
                Assert.That(target.PublishVersionCount, Is.Zero);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void DryRunRejectsPublishAndVersionStagesAtConstruction()
        {
            Assert.Throws<ArgumentException>(() => new HotUpdatePublishDryRun(new IHotUpdatePublishStageHandler[]
            {
                new StubStage(HotUpdatePublishStage.UploadFiles)
            }));
            Assert.Throws<ArgumentException>(() => new HotUpdatePublishDryRun(new IHotUpdatePublishStageHandler[]
            {
                new StubStage(HotUpdatePublishStage.PublishVersion)
            }));
        }

        private static HotUpdatePublishDryRun CreateDryRun()
        {
            var handlers = new List<IHotUpdatePublishStageHandler>();
            HotUpdatePublishStage[] stages =
            {
                HotUpdatePublishStage.Preflight, HotUpdatePublishStage.ClassifyChanges,
                HotUpdatePublishStage.CompileHotUpdate, HotUpdatePublishStage.ExportHybridCLRAssets,
                HotUpdatePublishStage.BuildYooAsset, HotUpdatePublishStage.ValidateArtifacts
            };
            for (int index = 0; index < stages.Length; index++) handlers.Add(new StubStage(stages[index]));
            handlers.Add(new HotUpdateReleaseGateStageHandler(new PassingFastGateRunner()));
            handlers.Add(new HotUpdatePrepareUploadStageHandler());
            return new HotUpdatePublishDryRun(handlers);
        }

        private static HotUpdatePublishContext CreateContext(string root, StubTarget target)
        {
            return new HotUpdatePublishContext
            {
                PackageVersion = "1.2.3",
                Environment = "Production",
                PublishTargetAdapter = target,
                ChangeClassification = HotUpdateChangeClassifier.Classify(new[]
                {
                    new HotUpdateChangeFacts("Assets/_Project/HotUpdate/Gameplay/Player.cs",
                        HotUpdateChangeAssetKind.CSharpSource, HotUpdateProjectLayer.HotUpdate)
                }),
                YooAssetBuildOutput = new YooAssetBuildOutput
                {
                    PackageVersion = "1.2.3",
                    OutputDirectory = root,
                    ManifestFiles = new[] { "manifest.hash", "manifest.json", "manifest.version", "PackageVersion" }
                }
            };
        }

        private static string CreatePackage()
        {
            string root = Path.Combine(Path.GetTempPath(), "stellar-hotupdate-dry-run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "PackageVersion"), "1.2.3");
            File.WriteAllBytes(Path.Combine(root, "new.bundle"), new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(Path.Combine(root, "reused.bundle"), new byte[] { 9, 8, 7, 6, 5 });
            return root;
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private sealed class StubStage : IHotUpdatePublishStageHandler
        {
            private readonly Action<HotUpdatePublishContext> _onRun;
            public StubStage(HotUpdatePublishStage stage, Action<HotUpdatePublishContext> onRun = null)
            {
                Stage = stage;
                _onRun = onRun;
            }
            public HotUpdatePublishStage Stage { get; }
            public Task<HotUpdatePublishStepResult> ExecuteAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
            {
                _onRun?.Invoke(context);
                return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
            }
        }

        private sealed class PassingFastGateRunner : IHotUpdateFastReleaseGateRunner
        {
            public Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HotUpdateReleaseGateRun
                {
                    Passed = true,
                    EvidencePath = "fast-gate.json"
                });
            }
        }

        private sealed class StubTarget : IHotUpdatePublishTarget
        {
            public readonly Dictionary<string, HotUpdatePublishTargetFileInfo> Files = new Dictionary<string, HotUpdatePublishTargetFileInfo>();
            public int UploadCount { get; private set; }
            public int PublishVersionCount { get; private set; }
            public int VerifyCount { get; private set; }

            public Task<HotUpdatePublishTargetFileInfo> UploadAsync(HotUpdatePublishFile file, CancellationToken cancellationToken)
            {
                UploadCount++;
                throw new InvalidOperationException("A Dry Run must never upload files.");
            }
            public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken) =>
                Task.FromResult(Files.ContainsKey(relativePath));
            public Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(string relativePath, CancellationToken cancellationToken) =>
                Task.FromResult(Files.TryGetValue(relativePath, out HotUpdatePublishTargetFileInfo info) ? info : null);
            public Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken)
            {
                VerifyCount++;
                throw new InvalidOperationException("A Dry Run must not perform post-upload verification.");
            }
            public Task PublishVersionAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken)
            {
                PublishVersionCount++;
                throw new InvalidOperationException("A Dry Run must never change the package version pointer.");
            }
            public Task RollbackAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("A Dry Run must never roll back a package version pointer.");
        }
    }
}
