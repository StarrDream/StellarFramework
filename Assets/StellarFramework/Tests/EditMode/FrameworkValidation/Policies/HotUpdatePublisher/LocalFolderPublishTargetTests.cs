using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class LocalFolderPublishTargetTests
    {
        private TargetFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new TargetFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture?.Dispose();
        }

        [Test]
        public void UploadAsync_CopiesFileAndVerifyChecksLengthAndSha256()
        {
            HotUpdatePublishFile file = _fixture.CreateSource("bundles/content.bundle", "bundle payload");

            HotUpdatePublishTargetFileInfo uploaded = _fixture.Target.UploadAsync(file, CancellationToken.None).GetAwaiter().GetResult();
            _fixture.Target.VerifyAsync(new[] { file }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(uploaded.RelativePath, Is.EqualTo("bundles/content.bundle"));
            Assert.That(uploaded.Length, Is.EqualTo(file.Length));
            Assert.That(uploaded.Sha256, Is.EqualTo(file.Sha256));
            Assert.That(_fixture.Target.ExistsAsync(file.RelativePath, CancellationToken.None).GetAwaiter().GetResult(), Is.True);
        }

        [Test]
        public void ProfileConstructorUsesConfiguredLocalFolderRootAndRemoteRoot()
        {
            HotUpdateEnvironmentProfile profile = TargetFixture.CreateProfile();
            profile.LocalFolderRoot = _fixture.LocalRoot;

            var target = new LocalFolderPublishTarget(profile);

            Assert.That(target.TargetRoot, Is.EqualTo(Path.GetFullPath(Path.Combine(_fixture.LocalRoot, "hotupdate/development"))));
        }

        [Test]
        public void UploadAsync_SamePathAndSameHash_IsIdempotent()
        {
            HotUpdatePublishFile file = _fixture.CreateSource("releases/1.0.0/bundle", "same content");
            _fixture.Target.UploadAsync(file, CancellationToken.None).GetAwaiter().GetResult();

            HotUpdatePublishTargetFileInfo duplicate = _fixture.Target.UploadAsync(file, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(duplicate.Sha256, Is.EqualTo(file.Sha256));
            Assert.That(File.ReadAllText(_fixture.ResolveTarget(file.RelativePath)), Is.EqualTo("same content"));
        }

        [Test]
        public void UploadAsync_SamePathDifferentHash_FailsWithoutOverwritingExistingFile()
        {
            HotUpdatePublishFile original = _fixture.CreateSource("releases/1.0.0/bundle", "original");
            _fixture.Target.UploadAsync(original, CancellationToken.None).GetAwaiter().GetResult();
            HotUpdatePublishFile conflicting = _fixture.CreateSource("releases/1.0.0/bundle", "changed");

            IOException exception = AssertTaskThrows<IOException>(() =>
                _fixture.Target.UploadAsync(conflicting, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain("never overwritten"));
            Assert.That(File.ReadAllText(_fixture.ResolveTarget(original.RelativePath)), Is.EqualTo("original"));
        }

        [Test]
        public void ExistsAndGetInfo_RejectPathTraversal()
        {
            Assert.Throws<ArgumentException>(() => _fixture.Target.ExistsAsync("../outside.txt", CancellationToken.None));
            Assert.Throws<ArgumentException>(() => _fixture.Target.GetInfoAsync("C:/outside.txt", CancellationToken.None));
        }

        [Test]
        public void UploadAsync_SourceMutationAfterSnapshot_FailsBeforePublish()
        {
            HotUpdatePublishFile file = _fixture.CreateSource("releases/1.0.0/bundle", "before");
            File.WriteAllText(file.SourcePath, "after");

            IOException exception = AssertTaskThrows<IOException>(() =>
                _fixture.Target.UploadAsync(file, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain("source file"));
            Assert.That(_fixture.Target.ExistsAsync(file.RelativePath, CancellationToken.None).GetAwaiter().GetResult(), Is.False);
        }

        [Test]
        public void VerifyAsync_TamperedRemoteFile_FailsIntegrityCheck()
        {
            HotUpdatePublishFile file = _fixture.CreateSource("releases/1.0.0/manifest.json", "manifest");
            _fixture.Target.UploadAsync(file, CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(_fixture.ResolveTarget(file.RelativePath), "tampered");

            IOException exception = AssertTaskThrows<IOException>(() =>
                _fixture.Target.VerifyAsync(new[] { file }, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain("target file").Or.Contain("existing"));
        }

        [Test]
        public void PublishVersionAsync_UsesAtomicPointerAndRejectsStaleWriter()
        {
            _fixture.Target.PublishVersionAsync(new HotUpdateVersionPublishRequest
            {
                PointerRelativePath = "PackageVersion",
                PackageVersion = "1.0.1",
                ExpectedCurrentPackageVersion = string.Empty
            }, CancellationToken.None).GetAwaiter().GetResult();

            IOException staleWriter = AssertTaskThrows<IOException>(() =>
                _fixture.Target.PublishVersionAsync(new HotUpdateVersionPublishRequest
                {
                    PointerRelativePath = "PackageVersion",
                    PackageVersion = "1.0.2",
                    ExpectedCurrentPackageVersion = string.Empty
                }, CancellationToken.None));

            Assert.That(staleWriter.Message, Does.Contain("Version pointer changed"));
            Assert.That(File.ReadAllText(_fixture.ResolveTarget("PackageVersion")).Trim(), Is.EqualTo("1.0.1"));
        }

        [Test]
        public void RollbackAsync_ChangesOnlyPointerWhenExpectedVersionMatches()
        {
            _fixture.PublishVersion("1.0.1", string.Empty).GetAwaiter().GetResult();
            _fixture.PublishVersion("1.0.2", "1.0.1").GetAwaiter().GetResult();

            _fixture.Target.RollbackAsync(new HotUpdateVersionPublishRequest
            {
                PointerRelativePath = "PackageVersion",
                PackageVersion = "1.0.1",
                ExpectedCurrentPackageVersion = "1.0.2"
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(File.ReadAllText(_fixture.ResolveTarget("PackageVersion")).Trim(), Is.EqualTo("1.0.1"));
        }

        [Test]
        public void PublishVersionAsync_ConcurrentWritersWithSameExpectedVersionOnlyAllowOneWinner()
        {
            _fixture.PublishVersion("1.0.0", string.Empty).GetAwaiter().GetResult();
            Task<bool> first = Task.Run(() => _fixture.TryPublishVersion("1.0.1", "1.0.0"));
            Task<bool> second = Task.Run(() => _fixture.TryPublishVersion("1.0.2", "1.0.0"));
            Task.WaitAll(first, second);

            Assert.That(first.Result ^ second.Result, Is.True);
            string currentVersion = File.ReadAllText(_fixture.ResolveTarget("PackageVersion")).Trim();
            Assert.That(new[] { "1.0.1", "1.0.2" }, Does.Contain(currentVersion));
        }

        [Test]
        public void Constructor_RequiresConfiguredLocalFolderProfileAndValidRoot()
        {
            HotUpdateEnvironmentProfile profile = TargetFixture.CreateProfile();
            profile.PublishTarget = "OtherTarget";

            Assert.Throws<ArgumentException>(() => new LocalFolderPublishTarget(profile, _fixture.LocalRoot));
            Assert.Throws<ArgumentException>(() => new LocalFolderPublishTarget(TargetFixture.CreateProfile(), string.Empty));
        }

        private sealed class TargetFixture : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "StellarLocalFolderPublishTarget", Guid.NewGuid().ToString("N"));
            public string LocalRoot => Path.Combine(Root, "share");
            public readonly LocalFolderPublishTarget Target;

            public TargetFixture()
            {
                Directory.CreateDirectory(Path.Combine(Root, "sources"));
                Target = new LocalFolderPublishTarget(CreateProfile(), LocalRoot);
            }

            public HotUpdatePublishFile CreateSource(string relativePath, string content)
            {
                string path = Path.Combine(Root, "sources", Guid.NewGuid().ToString("N") + ".bin");
                File.WriteAllText(path, content, new UTF8Encoding(false));
                return HotUpdatePublishFile.FromFile(relativePath, path);
            }

            public string ResolveTarget(string relativePath)
            {
                return Path.Combine(Target.TargetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            public Task PublishVersion(string version, string expected)
            {
                return Target.PublishVersionAsync(new HotUpdateVersionPublishRequest
                {
                    PointerRelativePath = "PackageVersion",
                    PackageVersion = version,
                    ExpectedCurrentPackageVersion = expected
                }, CancellationToken.None);
            }

            public bool TryPublishVersion(string version, string expected)
            {
                try
                {
                    PublishVersion(version, expected).GetAwaiter().GetResult();
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            public void Dispose()
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }

            public static HotUpdateEnvironmentProfile CreateProfile()
            {
                return new HotUpdateEnvironmentProfile
                {
                    EnvironmentId = "Development",
                    MainHostServer = "http://localhost:8080",
                    FallbackHostServer = string.Empty,
                    RemoteRoot = "hotupdate/development",
                    PublishTarget = "LocalFolder",
                    CredentialProfileName = string.Empty
                };
            }
        }

        private static TException AssertTaskThrows<TException>(Func<Task> operation)
            where TException : Exception
        {
            try
            {
                operation().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Assert.That(exception, Is.TypeOf<TException>());
                return (TException)exception;
            }

            Assert.Fail($"Expected {typeof(TException).Name}, but the operation completed successfully.");
            return null;
        }
    }
}
