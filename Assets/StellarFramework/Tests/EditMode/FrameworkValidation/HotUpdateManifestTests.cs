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
        public void StrictManifestValidationRejectsMissingSha()
        {
            HotUpdateManifest manifest = CreateValidManifest();
            manifest.hotUpdateAssemblySha256 = string.Empty;

            HotUpdateManifestValidationReport report = manifest.Validate(strictAssemblyIntegrity: true);

            Assert.That(report.IsValid, Is.False);
            CollectionAssert.Contains(report.Errors,
                "Production hot update requires hotUpdateAssemblySha256. Re-export dll.bytes and regenerate HotUpdateManifest.json.");
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
            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();

            HotUpdateManifest manifest = HotUpdateManifest.FromRuntimeSettings(settings);

            Assert.That(manifest.hotUpdateAssemblyKey, Is.EqualTo(settings.HotUpdateAssemblyKey));
            Assert.That(manifest.hotUpdateAssemblySha256, Is.EqualTo(settings.HotUpdateAssemblySha256));
            Assert.That(manifest.hotUpdateEntryClass, Is.EqualTo(settings.HotUpdateEntryClass));
            Assert.That(manifest.hotUpdateEntryMethod, Is.EqualTo(settings.HotUpdateEntryMethod));
            CollectionAssert.AreEqual(settings.AotMetadataKeys, manifest.aotMetadataKeys);
        }

        [Test]
        public void StrictSettingsValidationRejectsMissingShaAndResourcesOnlyFallback()
        {
            HotUpdateSettings settings = ScriptableObject.CreateInstance<HotUpdateSettings>();
            SetPrivateField(settings, "hotUpdateAssemblySha256", string.Empty);
            SetPrivateField(settings, "hotUpdateManifestPathOrUrl", string.Empty);
            SetPrivateField(settings, "hotUpdateManifestFallbackToStreamingAssets", false);
            SetPrivateField(settings, "hotUpdateManifestFallbackToResources", true);

            HotUpdateSettingsValidationReport report = settings.Validate(strictProduction: true);

            Assert.That(report.IsValid, Is.False);
            CollectionAssert.Contains(report.Errors,
                "Production hot update requires HotUpdateManifestPathOrUrl or StreamingAssets fallback. Resources-only fallback is not allowed.");
            CollectionAssert.Contains(report.Errors,
                "Production hot update requires HotUpdateAssemblySha256. Re-export dll.bytes so the framework can verify the hot update DLL.");
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

        [Test]
        public void StrictSourceChainUsesExplicitSourceOnly()
        {
            HotUpdateSettings settings = ScriptableObject.CreateInstance<HotUpdateSettings>();
            SetPrivateField(settings, "hotUpdateManifestPathOrUrl", "https://example.com/hotupdate/HotUpdateManifest.json");
            SetPrivateField(settings, "hotUpdateManifestFallbackToStreamingAssets", true);
            SetPrivateField(settings, "hotUpdateManifestFallbackToResources", true);

            var sources = HotUpdateManifestSourceChain.BuildDefaultSources(settings, strictProduction: true);

            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0], Is.TypeOf<HttpHotUpdateManifestSource>());
        }

        [Test]
        public void StrictSourceChainSkipsResourcesFallbackEvenWithoutExplicitSource()
        {
            HotUpdateSettings settings = ScriptableObject.CreateInstance<HotUpdateSettings>();
            SetPrivateField(settings, "hotUpdateManifestPathOrUrl", string.Empty);
            SetPrivateField(settings, "hotUpdateManifestFallbackToStreamingAssets", true);
            SetPrivateField(settings, "hotUpdateManifestFallbackToResources", true);

            var sources = HotUpdateManifestSourceChain.BuildDefaultSources(settings, strictProduction: true);

            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0], Is.TypeOf<StreamingAssetsHotUpdateManifestSource>());
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

        private static void SetPrivateField<T>(HotUpdateSettings settings, string fieldName, T value)
        {
            var field = typeof(HotUpdateSettings).GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(settings, value);
        }
    }
}
