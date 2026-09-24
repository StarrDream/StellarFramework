using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEditor;
using YooAsset;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateBuildAdapterTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "StellarFrameworkPublisherTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public void YooAssetBuild_RequiresHybridCLRArtifactsBeforeCallingRunner()
        {
            var runner = new FakeYooAssetBuildRunner(_root, createArtifacts: true);
            var adapter = new YooAssetHotUpdateBuildAdapter(runner);
            var context = CreateContext();

            var result = adapter.BuildYooAssetAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("HybridCLR"));
            Assert.That(runner.CallCount, Is.Zero);
        }

        [Test]
        public void YooAssetBuild_MapsInputsAndRecordsVerifiedOutput()
        {
            var runner = new FakeYooAssetBuildRunner(_root, createArtifacts: true);
            var adapter = new YooAssetHotUpdateBuildAdapter(runner);
            var context = CreateContext();
            context.HybridCLRBuildOutput = new HybridCLRBuildOutput();

            var result = adapter.BuildYooAssetAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(runner.Target, Is.EqualTo(BuildTarget.StandaloneWindows64));
            Assert.That(runner.PackageName, Is.EqualTo("GameContent"));
            Assert.That(runner.PackageVersion, Is.EqualTo("2026.09.24.1"));
            Assert.That(runner.OutputRoot, Is.EqualTo(_root));
            Assert.That(runner.Compression, Is.EqualTo("LZ4"));
            Assert.That(context.YooAssetBuildOutput.BundleCount, Is.EqualTo(1));
            Assert.That(context.YooAssetBuildOutput.TotalBytes, Is.GreaterThan(0));
            Assert.That(context.YooAssetBuildOutput.ManifestFiles, Has.Length.EqualTo(4));
            Assert.That(context.BuildOutput, Is.EqualTo(runner.PackageDirectory));
        }

        [Test]
        public void YooAssetBuild_PropagatesRunnerFailureWithoutMarkingOutput()
        {
            var runner = new FakeYooAssetBuildRunner(_root, createArtifacts: false)
            {
                Failure = "collector rejected production package"
            };
            var adapter = new YooAssetHotUpdateBuildAdapter(runner);
            var context = CreateContext();
            context.HybridCLRBuildOutput = new HybridCLRBuildOutput();

            var result = adapter.BuildYooAssetAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("collector rejected"));
            Assert.That(context.YooAssetBuildOutput, Is.Null);
        }

        [Test]
        public void HybridCLRCompile_RequiresValidatedBaseReleaseAndNeverFallsBackToGeneratedMetadata()
        {
            var compiler = new FakeHybridCLRCompiler();
            var adapter = new HybridCLRHotUpdateBuildAdapter(
                new HotUpdateBaseReleaseRepository(Path.Combine(_root, "BaseReleases")), compiler);
            var context = new HotUpdatePublishContext
            {
                Platform = EditorUserBuildSettings.activeBuildTarget
            };

            HotUpdatePublishStepResult result = adapter.CompileHotUpdateAsync(
                context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("BaseRelease"));
            Assert.That(compiler.CallCount, Is.Zero);
        }

        [Test]
        public void HybridCLRBaseReleaseGeneration_RejectsTargetSwitchBeforeGeneratingAssets()
        {
            var compiler = new FakeHybridCLRCompiler();
            var adapter = new HybridCLRHotUpdateBuildAdapter(
                new HotUpdateBaseReleaseRepository(Path.Combine(_root, "BaseReleases")), compiler);
            var request = new HotUpdateBaseReleaseCreateRequest
            {
                Platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                    ? BuildTarget.StandaloneWindows64
                    : BuildTarget.Android,
                BaseAppVersion = "test"
            };

            Assert.Throws<InvalidOperationException>(() => adapter.CreateBaseRelease(request));
            Assert.That(compiler.CallCount, Is.Zero);
            Assert.That(Directory.Exists(Path.Combine(_root, "BaseReleases")), Is.False);
        }

        private HotUpdatePublishContext CreateContext()
        {
            return new HotUpdatePublishContext
            {
                Platform = BuildTarget.StandaloneWindows64,
                PackageName = "GameContent",
                PackageVersion = "2026.09.24.1",
                YooAssetBuildOutputRoot = _root,
                YooAssetCompression = "LZ4"
            };
        }

        private sealed class FakeYooAssetBuildRunner : IYooAssetBuildRunner
        {
            private readonly string _root;
            private readonly bool _createArtifacts;
            public int CallCount { get; private set; }
            public BuildTarget Target { get; private set; }
            public string PackageName { get; private set; }
            public string PackageVersion { get; private set; }
            public string OutputRoot { get; private set; }
            public string Compression { get; private set; }
            public string PackageDirectory { get; private set; }
            public string Failure { get; set; }

            public FakeYooAssetBuildRunner(string root, bool createArtifacts)
            {
                _root = root;
                _createArtifacts = createArtifacts;
            }

            public YooAssetBuildRunnerResult Build(
                BuildTarget target, string packageName, string packageVersion, string outputRoot, string compression)
            {
                CallCount++;
                Target = target;
                PackageName = packageName;
                PackageVersion = packageVersion;
                OutputRoot = outputRoot;
                Compression = compression;
                PackageDirectory = Path.Combine(_root, target.ToString(), packageName, packageVersion);
                if (!string.IsNullOrEmpty(Failure))
                    return new YooAssetBuildRunnerResult { Success = false, Error = Failure };
                Directory.CreateDirectory(PackageDirectory);
                if (_createArtifacts)
                {
                    File.WriteAllBytes(Path.Combine(PackageDirectory, YooAssetSettingsData.GetPackageHashFileName(packageName, packageVersion)), new byte[] { 1 });
                    File.WriteAllBytes(Path.Combine(PackageDirectory, YooAssetSettingsData.GetPackageVersionFileName(packageName)), new byte[] { 2 });
                    File.WriteAllBytes(Path.Combine(PackageDirectory, YooAssetSettingsData.GetManifestBinaryFileName(packageName, packageVersion)), new byte[] { 3 });
                    File.WriteAllText(Path.Combine(PackageDirectory, YooAssetSettingsData.GetManifestJsonFileName(packageName, packageVersion)), "{}");
                    File.WriteAllBytes(Path.Combine(PackageDirectory, "test.bundle"), new byte[] { 4, 5 });
                }
                return new YooAssetBuildRunnerResult { Success = true, OutputDirectory = PackageDirectory };
            }
        }

        private sealed class FakeHybridCLRCompiler : IHybridCLRAssemblyCompiler
        {
            public int CallCount { get; private set; }
            public void Compile(BuildTarget target, bool developmentBuild) => CallCount++;
        }
    }
}
