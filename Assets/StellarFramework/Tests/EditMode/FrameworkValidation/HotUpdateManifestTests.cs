using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.HotUpdate;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.Tests.HotUpdate
{
    public sealed class HotUpdateManifestTests
    {
        [Test]
        public void ValidManifestPassesValidation()
        {
            HotUpdateManifest manifest = CreateValidManifest();

            HotUpdateManifestValidationReport report = manifest.Validate();

            Assert.That(report.IsValid, Is.True, string.Join(" | ", report.Errors));
        }

        [Test]
        public void ManifestValidationRejectsInvalidSha()
        {
            HotUpdateManifest manifest = CreateValidManifest();
            manifest.hotUpdateAssemblySha256 = "1234";

            HotUpdateManifestValidationReport report = manifest.Validate();

            Assert.That(report.IsValid, Is.False);
            CollectionAssert.Contains(report.Errors, "hotUpdateAssemblySha256 must be a 64-character SHA256 hex string when provided.");
        }

        [Test]
        public void ManifestValidationRejectsMissingCoreFields()
        {
            HotUpdateManifest manifest = new HotUpdateManifest();

            HotUpdateManifestValidationReport report = manifest.Validate();

            Assert.That(report.IsValid, Is.False);
            CollectionAssert.Contains(report.Errors, "hotUpdateAssemblyKey is empty.");
            CollectionAssert.Contains(report.Errors, "hotUpdateEntryClass is empty.");
            CollectionAssert.Contains(report.Errors, "hotUpdateEntryMethod is empty.");
            CollectionAssert.Contains(report.Errors, "aotMetadataKeys are empty.");
        }

        [Test]
        public void ManifestRoundTripsThroughJson()
        {
            HotUpdateManifest manifest = CreateValidManifest();

            string json = manifest.ToJson(true);
            HotUpdateManifest parsed = HotUpdateManifest.FromJson(json);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.hotUpdateAssemblyKey, Is.EqualTo(manifest.hotUpdateAssemblyKey));
            Assert.That(parsed.hotUpdateAssemblySha256, Is.EqualTo(manifest.hotUpdateAssemblySha256));
            CollectionAssert.AreEqual(manifest.aotMetadataKeys, parsed.aotMetadataKeys);
        }

        [Test]
        public void ManifestJsonWithUtf8BomParses()
        {
            HotUpdateManifest manifest = CreateValidManifest();

            string json = "\ufeff" + manifest.ToJson(true);
            HotUpdateManifest parsed = HotUpdateManifest.FromJson(json);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.hotUpdateAssemblyKey, Is.EqualTo(manifest.hotUpdateAssemblyKey));
        }

        [Test]
        public void ResourcesFallbackBuildsManifestFromSettings()
        {
            ResKitRuntimeSettings settings = ResKitRuntimeSettings.LoadOrCreateDefault();

            HotUpdateManifest manifest = HotUpdateManifest.FromRuntimeSettings(settings);

            Assert.That(manifest.hotUpdateAssemblyKey, Is.EqualTo(settings.HotUpdateAssemblyKey));
            Assert.That(manifest.hotUpdateAssemblySha256, Is.EqualTo(settings.HotUpdateAssemblySha256));
            Assert.That(manifest.hotUpdateEntryClass, Is.EqualTo(settings.HotUpdateEntryClass));
            Assert.That(manifest.hotUpdateEntryMethod, Is.EqualTo(settings.HotUpdateEntryMethod));
            CollectionAssert.AreEqual(settings.AotMetadataKeys, manifest.aotMetadataKeys);
        }

        [Test]
        public void SourceChainReturnsFirstSuccessfulManifestAndRecordsFailures()
        {
            HotUpdateManifest expected = CreateValidManifest();
            IHotUpdateManifestSource[] sources =
            {
                new StubManifestSource("broken", HotUpdateManifestLoadResult.Fail("broken", "Missing manifest")),
                new StubManifestSource("good", HotUpdateManifestLoadResult.Ok(expected, "good"))
            };

            HotUpdateManifestLoadResult result = HotUpdateManifestSourceChain.LoadAsync(
                sources,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Manifest, Is.SameAs(expected));
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("Missing manifest"));
        }

        private static HotUpdateManifest CreateValidManifest()
        {
            return new HotUpdateManifest
            {
                version = 1,
                buildTarget = "StandaloneWindows64",
                hotUpdateAssemblyKey = "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes",
                hotUpdateAssemblySha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                hotUpdateEntryClass = "HotUpdate.HotUpdateMain",
                hotUpdateEntryMethod = "Main",
                aotMetadataKeys = new List<string>
                {
                    "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes",
                    "Assets/GameHotUpdate/Metadata/System.dll.bytes"
                }
            };
        }

        private sealed class StubManifestSource : IHotUpdateManifestSource
        {
            private readonly HotUpdateManifestLoadResult _result;

            public StubManifestSource(string description, HotUpdateManifestLoadResult result)
            {
                Description = description;
                _result = result;
            }

            public string Description { get; }

            public UniTask<HotUpdateManifestLoadResult> LoadAsync(CancellationToken cancellationToken)
            {
                return UniTask.FromResult(_result);
            }
        }
    }
}
