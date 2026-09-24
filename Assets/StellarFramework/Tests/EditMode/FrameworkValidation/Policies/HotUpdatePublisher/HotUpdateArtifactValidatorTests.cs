using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using StellarFramework.HybridCLR;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateArtifactValidatorTests
    {
        private ArtifactFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new ArtifactFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture?.Dispose();
        }

        [Test]
        public void Validate_CompleteArtifactsMatchingBaseRelease_ReturnsValidReport()
        {
            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.True, string.Join(Environment.NewLine, report.Errors));
            Assert.That(report.Errors, Is.Empty);
        }

        [Test]
        public void Validate_WrongManifestTarget_ReportsPlatformMismatch()
        {
            _fixture.WriteManifest(manifest => manifest.buildTarget = BuildTarget.Android.ToString());

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("does not match"));
        }

        [Test]
        public void Validate_DllBytesDifferFromManifestSha_ReportsIntegrityFailure()
        {
            File.WriteAllBytes(_fixture.DllPath, new byte[] { 1, 2, 3, 4 });

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("SHA256 mismatch"));
        }

        [Test]
        public void Validate_EntryClassMissing_ReportsEntryFailure()
        {
            _fixture.WriteManifest(manifest => manifest.hotUpdateEntryClass = "HotUpdate.DoesNotExist");

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("entry class"));
        }

        [Test]
        public void Validate_EntryMethodMissing_ReportsEntryFailure()
        {
            _fixture.WriteManifest(manifest => manifest.hotUpdateEntryMethod = "DoesNotExist");

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("entry method"));
        }

        [Test]
        public void Validate_UnsafePackageVersion_ReportsInvalidVersion()
        {
            _fixture.Context.PackageVersion = "../outside";

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("path-safe identifier"));
        }

        [Test]
        public void Validate_ManifestWithoutRequiredFields_ReportsInvalidManifest()
        {
            File.WriteAllText(_fixture.ManifestPath, "{}");

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("hotUpdateAssemblyKey is empty"));
        }

        [Test]
        public void Validate_MissingYooAssetBuildRecord_ReportsIncompleteBuild()
        {
            _fixture.Context.YooAssetBuildOutput = null;

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("did not produce a completed output record"));
        }

        [Test]
        public void Validate_AotMetadataDiffersFromSelectedBaseRelease_ReportsMismatch()
        {
            File.WriteAllBytes(_fixture.AotPath, new byte[] { 9, 8, 7 });

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("against selected BaseRelease"));
        }

        [Test]
        public void Validate_YooAssetRecordWithMissingManifestFile_ReportsIncompleteBuild()
        {
            _fixture.Context.YooAssetBuildOutput.ManifestFiles = new[]
            {
                "PackageManifest.bin", "PackageManifest.json", "PackageManifest.hash", "PackageVersion",
                "missing.manifest"
            };

            HotUpdateArtifactValidationReport report = _fixture.Validator.Validate(_fixture.Context);

            Assert.That(report.IsValid, Is.False);
            Assert.That(string.Join("\n", report.Errors), Does.Contain("missing.manifest"));
        }

        [Test]
        public void ValidateArtifactsStage_MapsReportErrorsToTypedPipelineFailure()
        {
            _fixture.WriteManifest(manifest => manifest.buildTarget = BuildTarget.Android.ToString());
            var handler = new HotUpdateArtifactValidationStageHandler(_fixture.Validator);

            HotUpdatePublishStepResult result = handler.ExecuteAsync(_fixture.Context, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(handler.Stage, Is.EqualTo(HotUpdatePublishStage.ValidateArtifacts));
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(HotUpdatePublishErrorCode.ArtifactValidationFailed));
            Assert.That(result.Error, Does.Contain("does not match"));
        }

        public static class TestHotUpdateEntry
        {
            public static void Main() { }
        }

        private sealed class ArtifactFixture : IDisposable
        {
            private readonly string _root = Path.Combine(Path.GetTempPath(), "StellarHotUpdateArtifactValidator", Guid.NewGuid().ToString("N"));
            private readonly string _baseReleaseRoot;
            private readonly string _sourceMetadataPath;
            private readonly string _packageOutputPath;

            public readonly string AssetsRoot;
            public readonly string ManifestPath;
            public readonly string DllPath;
            public readonly string AotPath;
            public readonly HotUpdatePublishContext Context;
            public readonly HotUpdateArtifactValidator Validator;

            public ArtifactFixture()
            {
                AssetsRoot = Path.Combine(_root, "Assets");
                _baseReleaseRoot = Path.Combine(_root, "BaseReleases");
                _sourceMetadataPath = Path.Combine(_root, "Source", "mscorlib.dll");
                _packageOutputPath = Path.Combine(_root, "YooAsset", "Windows");
                Directory.CreateDirectory(Path.GetDirectoryName(_sourceMetadataPath));
                Directory.CreateDirectory(Path.Combine(AssetsRoot, "GameHotUpdate", "Manifest"));
                Directory.CreateDirectory(Path.Combine(AssetsRoot, "GameHotUpdate", "Assemblies"));
                Directory.CreateDirectory(Path.Combine(AssetsRoot, "GameHotUpdate", "Metadata"));
                Directory.CreateDirectory(_packageOutputPath);

                byte[] metadataBytes = { 1, 3, 3, 7 };
                File.WriteAllBytes(_sourceMetadataPath, metadataBytes);
                AotPath = Path.Combine(AssetsRoot, "GameHotUpdate", "Metadata", "mscorlib.dll.bytes");
                File.WriteAllBytes(AotPath, metadataBytes);

                var repository = new HotUpdateBaseReleaseRepository(_baseReleaseRoot);
                repository.Create(new HotUpdateBaseReleaseCreateRequest
                {
                    BaseAppVersion = "1.0.0",
                    Platform = BuildTarget.StandaloneWindows64,
                    Architecture = "x86_64",
                    UnityVersion = "2022.3.62f3c1",
                    HybridCLRVersion = "test-revision",
                    YooAssetVersion = "2.3.19",
                    ScriptingBackend = ScriptingImplementation.IL2CPP,
                    GitCommit = "test-commit",
                    AotMetadataSourcePaths = new[] { _sourceMetadataPath }
                });
                var requirements = new HotUpdateBaseReleaseRequirements
                {
                    BaseAppVersion = "1.0.0",
                    Platform = BuildTarget.StandaloneWindows64,
                    Architecture = "x86_64",
                    UnityVersion = "2022.3.62f3c1",
                    HybridCLRVersion = "test-revision",
                    YooAssetVersion = "2.3.19",
                    ScriptingBackend = ScriptingImplementation.IL2CPP
                };

                ManifestPath = Path.Combine(AssetsRoot, "GameHotUpdate", "Manifest", "HotUpdateManifest.json");
                DllPath = Path.Combine(AssetsRoot, "GameHotUpdate", "Assemblies", "HotUpdate.dll.bytes");
                byte[] assemblyBytes = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
                File.WriteAllBytes(DllPath, assemblyBytes);

                string dllAssetPath = "Assets/GameHotUpdate/Assemblies/HotUpdate.dll.bytes";
                WriteManifest(manifest =>
                {
                    manifest.buildTarget = BuildTarget.StandaloneWindows64.ToString();
                    manifest.hotUpdateAssemblyKey = dllAssetPath;
                    manifest.hotUpdateAssemblySha256 = ComputeSha256(assemblyBytes);
                    manifest.hotUpdateEntryClass = typeof(TestHotUpdateEntry).FullName;
                    manifest.hotUpdateEntryMethod = nameof(TestHotUpdateEntry.Main);
                    manifest.aotMetadataKeys.Add("Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes");
                });

                string[] manifestFiles = { "PackageManifest.bin", "PackageManifest.json", "PackageManifest.hash", "PackageVersion" };
                foreach (string fileName in manifestFiles)
                    File.WriteAllText(Path.Combine(_packageOutputPath, fileName), "fixture");

                Context = new HotUpdatePublishContext
                {
                    Platform = BuildTarget.StandaloneWindows64,
                    PackageVersion = "1.0.1",
                    HybridCLRBuildOutput = new HybridCLRBuildOutput
                    {
                        ManifestAssetPath = "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json",
                        HotUpdateDllAssetPath = dllAssetPath,
                        HotUpdateDllSha256 = ComputeSha256(assemblyBytes),
                        AotMetadataAssetPaths = new[] { "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes" }
                    },
                    YooAssetBuildOutput = new YooAssetBuildOutput
                    {
                        PackageVersion = "1.0.1",
                        OutputDirectory = _packageOutputPath,
                        BundleCount = 1,
                        TotalBytes = 128,
                        ManifestFiles = manifestFiles
                    }
                };
                Context.SelectBaseRelease(repository, requirements);
                Validator = new HotUpdateArtifactValidator(repository, AssetsRoot);
            }

            public void WriteManifest(Action<HotUpdateManifest> update)
            {
                HotUpdateManifest manifest = File.Exists(ManifestPath)
                    ? JsonUtility.FromJson<HotUpdateManifest>(File.ReadAllText(ManifestPath))
                    : new HotUpdateManifest();
                update(manifest);
                File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            }

            public void Dispose()
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }

            private static string ComputeSha256(byte[] bytes)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    var builder = new StringBuilder(64);
                    foreach (byte value in sha256.ComputeHash(bytes)) builder.Append(value.ToString("x2"));
                    return builder.ToString();
                }
            }
        }
    }
}
