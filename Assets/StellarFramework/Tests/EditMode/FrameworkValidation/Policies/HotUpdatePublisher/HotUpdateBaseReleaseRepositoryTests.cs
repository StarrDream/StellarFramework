using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEditor;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateBaseReleaseRepositoryTests
    {
        private string _testRoot;
        private string _repositoryRoot;
        private string _metadataPath;
        private HotUpdateBaseReleaseRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "StellarFramework-HotUpdateBaseReleaseTests-" + Guid.NewGuid().ToString("N"));
            _repositoryRoot = Path.Combine(_testRoot, "BaseReleases");
            Directory.CreateDirectory(Path.Combine(_testRoot, "Sources"));
            _metadataPath = Path.Combine(_testRoot, "Sources", "mscorlib.dll");
            File.WriteAllBytes(_metadataPath, Encoding.ASCII.GetBytes("aot metadata bytes"));
            _repository = new HotUpdateBaseReleaseRepository(_repositoryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_testRoot) && Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }

        [Test]
        public void CreateCopiesMetadataAndLoadListAndResolveReturnTheRecordedFiles()
        {
            HotUpdateBaseReleaseCreateRequest request = CreateRequest();

            HotUpdateBaseRelease created = _repository.Create(request);
            HotUpdateBaseRelease loaded = _repository.Load(BuildTarget.Android, "1.0.0");
            HotUpdateBaseRelease[] releases = _repository.List(BuildTarget.Android).ToArray();
            HotUpdateBaseReleaseValidationResult validation = _repository.Validate(
                loaded,
                CreateRequirements(request));
            string[] metadataPaths = _repository.GetAotMetadataPaths(
                loaded,
                CreateRequirements(request)).ToArray();

            Assert.That(created.SchemaVersion, Is.EqualTo(1));
            Assert.That(loaded.Platform, Is.EqualTo(BuildTarget.Android));
            Assert.That(loaded.BaseAppVersion, Is.EqualTo("1.0.0"));
            Assert.That(loaded.Architecture, Is.EqualTo("ARM64"));
            Assert.That(loaded.HybridCLRVersion, Is.EqualTo(request.HybridCLRVersion));
            Assert.That(loaded.YooAssetVersion, Is.EqualTo("2.3.19"));
            Assert.That(loaded.ScriptingBackend, Is.EqualTo(ScriptingImplementation.IL2CPP));
            CollectionAssert.AreEqual(new[] { "mscorlib.dll" }, loaded.AotMetadata);
            CollectionAssert.AreEqual(new[] { ComputeSha256(_metadataPath) }, loaded.AotHashes);
            Assert.That(releases.Length, Is.EqualTo(1));
            Assert.That(validation.IsValid, Is.True, Issues(validation));
            Assert.That(metadataPaths.Length, Is.EqualTo(1));
            Assert.That(metadataPaths[0], Does.Contain(Path.Combine("AotMetadata", "mscorlib.dll")));
            CollectionAssert.AreEqual(File.ReadAllBytes(_metadataPath), File.ReadAllBytes(metadataPaths[0]));
        }

        [Test]
        public void ExistingBaseAppVersionCannotBeOverwritten()
        {
            _repository.Create(CreateRequest());

            Assert.Throws<IOException>(() => _repository.Create(CreateRequest()));

            HotUpdateBaseRelease retained = _repository.Load(BuildTarget.Android, "1.0.0");
            CollectionAssert.AreEqual(new[] { ComputeSha256(_metadataPath) }, retained.AotHashes);
            Assert.That(Directory.GetDirectories(Path.Combine(_repositoryRoot, "Android"))
                .Count(path => Path.GetFileName(path).StartsWith(".staging-", StringComparison.Ordinal)), Is.Zero);
        }

        [Test]
        public void ValidateReportsEveryTargetCompatibilityMismatch()
        {
            HotUpdateBaseRelease release = _repository.Create(CreateRequest());
            HotUpdateBaseReleaseRequirements requirements = CreateRequirements(CreateRequest());
            requirements.BaseAppVersion = "1.0.1";
            requirements.Platform = BuildTarget.StandaloneWindows64;
            requirements.Architecture = "x86_64";
            requirements.UnityVersion = "2022.3.99f1";
            requirements.HybridCLRVersion = "other-hybridclr";
            requirements.YooAssetVersion = "2.4.0";
            requirements.ScriptingBackend = ScriptingImplementation.Mono2x;

            HotUpdateBaseReleaseValidationResult validation = _repository.Validate(release, requirements);

            Assert.That(validation.IsValid, Is.False);
            CollectionAssert.AreEquivalent(new[]
            {
                HotUpdateBaseReleaseValidationIssueCode.BaseAppVersionMismatch,
                HotUpdateBaseReleaseValidationIssueCode.PlatformMismatch,
                HotUpdateBaseReleaseValidationIssueCode.ArchitectureMismatch,
                HotUpdateBaseReleaseValidationIssueCode.UnityVersionMismatch,
                HotUpdateBaseReleaseValidationIssueCode.HybridCLRVersionMismatch,
                HotUpdateBaseReleaseValidationIssueCode.YooAssetVersionMismatch,
                HotUpdateBaseReleaseValidationIssueCode.ScriptingBackendMismatch
            }, validation.Issues.Select(issue => issue.Code));
        }

        [Test]
        public void ValidateReloadsAuthoritativeRecordInsteadOfTrustingMutableCallerData()
        {
            HotUpdateBaseRelease selected = _repository.Create(CreateRequest());
            selected.AotMetadata[0] = "../../different.dll";

            HotUpdateBaseReleaseValidationResult validation = _repository.Validate(
                selected,
                CreateRequirements(CreateRequest()));

            Assert.That(validation.IsValid, Is.True, Issues(validation));
            CollectionAssert.AreEqual(new[] { "mscorlib.dll" }, validation.Release.AotMetadata);
        }

        [Test]
        public void ValidateDetectsMetadataTamperingBySha256()
        {
            HotUpdateBaseRelease release = _repository.Create(CreateRequest());
            string storedMetadata = Path.Combine(
                _repositoryRoot, "Android", "1.0.0", "AotMetadata", "mscorlib.dll");
            File.WriteAllBytes(storedMetadata, Encoding.ASCII.GetBytes("tampered metadata"));

            HotUpdateBaseReleaseValidationResult validation = _repository.Validate(
                release,
                CreateRequirements(CreateRequest()));

            Assert.That(validation.IsValid, Is.False);
            CollectionAssert.Contains(
                validation.Issues.Select(issue => issue.Code),
                HotUpdateBaseReleaseValidationIssueCode.MetadataHashMismatch);
        }

        [Test]
        public void LoadAndValidateRequiresAnExplicitRecordedBaseRelease()
        {
            HotUpdateBaseReleaseRequirements requirements = CreateRequirements(CreateRequest());

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
                _repository.LoadAndValidate(BuildTarget.Android, "1.0.0", requirements));

            Assert.That(exception.Message, Does.Contain("Select a recorded BaseRelease"));
            Assert.That(Directory.Exists(Path.Combine(_testRoot, "HybridCLRData")), Is.False);
        }

        [Test]
        public void CreateRejectsPathTraversalVersionsAndMissingMetadata()
        {
            HotUpdateBaseReleaseCreateRequest unsafeVersion = CreateRequest();
            unsafeVersion.BaseAppVersion = "../outside";
            Assert.Throws<ArgumentException>(() => _repository.Create(unsafeVersion));

            HotUpdateBaseReleaseCreateRequest paddedVersion = CreateRequest();
            paddedVersion.BaseAppVersion = " 1.0.1 ";
            Assert.Throws<ArgumentException>(() => _repository.Create(paddedVersion));

            HotUpdateBaseReleaseCreateRequest noMetadata = CreateRequest();
            noMetadata.BaseAppVersion = "1.0.1";
            noMetadata.AotMetadataSourcePaths = Array.Empty<string>();
            Assert.Throws<ArgumentException>(() => _repository.Create(noMetadata));
            Assert.That(Directory.Exists(Path.Combine(_repositoryRoot, "Android", "1.0.1")), Is.False);
        }

        [Test]
        public void PublishContextSelectsOnlyAValidatedRecordedBaseRelease()
        {
            HotUpdateBaseReleaseCreateRequest request = CreateRequest();
            _repository.Create(request);
            var context = new HotUpdatePublishContext();

            HotUpdateBaseRelease selected = context.SelectBaseRelease(
                _repository,
                CreateRequirements(request));

            Assert.That(context.SelectedBaseRelease, Is.SameAs(selected));
            Assert.That(context.BaseAppVersion, Is.EqualTo("1.0.0"));
            Assert.That(context.Platform, Is.EqualTo(BuildTarget.Android));
        }

        private HotUpdateBaseReleaseCreateRequest CreateRequest()
        {
            return new HotUpdateBaseReleaseCreateRequest
            {
                BaseAppVersion = "1.0.0",
                Platform = BuildTarget.Android,
                Architecture = "ARM64",
                UnityVersion = "2022.3.62f3c1",
                HybridCLRVersion = "4feac30cb2e105992986c737f7f54992b8300e1a",
                YooAssetVersion = "2.3.19",
                ScriptingBackend = ScriptingImplementation.IL2CPP,
                GitCommit = "0123456789abcdef0123456789abcdef01234567",
                AotMetadataSourcePaths = new[] { _metadataPath }
            };
        }

        private static HotUpdateBaseReleaseRequirements CreateRequirements(
            HotUpdateBaseReleaseCreateRequest request)
        {
            return new HotUpdateBaseReleaseRequirements
            {
                BaseAppVersion = request.BaseAppVersion,
                Platform = request.Platform,
                Architecture = request.Architecture,
                UnityVersion = request.UnityVersion,
                HybridCLRVersion = request.HybridCLRVersion,
                YooAssetVersion = request.YooAssetVersion,
                ScriptingBackend = request.ScriptingBackend
            };
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string Issues(HotUpdateBaseReleaseValidationResult validation)
        {
            return string.Join("; ", validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}"));
        }
    }
}
