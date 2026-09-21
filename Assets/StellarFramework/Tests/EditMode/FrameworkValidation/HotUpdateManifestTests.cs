using NUnit.Framework;
using StellarFramework.HybridCLR;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HotUpdateManifestTests
    {
        [Test]
        public void ValidManifestPassesValidation()
        {
            HotUpdateManifest manifest = CreateValidManifest();
            HotUpdateManifestValidationReport report = manifest.Validate(strictAssemblyIntegrity: true);

            Assert.That(report.IsValid, Is.True, string.Join(" | ", report.Errors));
        }

        [Test]
        public void StrictValidationRequiresSha256()
        {
            HotUpdateManifest manifest = CreateValidManifest();
            manifest.hotUpdateAssemblySha256 = string.Empty;

            HotUpdateManifestValidationReport report = manifest.Validate(strictAssemblyIntegrity: true);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("requires hotUpdateAssemblySha256"));
        }

        [Test]
        public void JsonRoundTripPreservesCodeUpdateContract()
        {
            HotUpdateManifest expected = CreateValidManifest();
            string json = expected.ToJson(prettyPrint: true);

            HotUpdateManifest actual = HotUpdateManifest.FromJson(json);

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.hotUpdateAssemblyKey, Is.EqualTo(expected.hotUpdateAssemblyKey));
            Assert.That(actual.hotUpdateAssemblySha256, Is.EqualTo(expected.hotUpdateAssemblySha256));
            Assert.That(actual.hotUpdateEntryClass, Is.EqualTo(expected.hotUpdateEntryClass));
            Assert.That(actual.hotUpdateEntryMethod, Is.EqualTo(expected.hotUpdateEntryMethod));
            CollectionAssert.AreEqual(expected.aotMetadataKeys, actual.aotMetadataKeys);
        }

        [Test]
        public void DefaultSettingsUseResKitManifestAssetInsteadOfNetworkFallbacks()
        {
            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport report = settings.Validate(strictProduction: true);

            Assert.That(report.IsValid, Is.True, string.Join(" | ", report.Errors));
            Assert.That(settings.ResourceLoaderKey, Is.EqualTo("YooAsset"));
            Assert.That(settings.HotUpdateManifestKey,
                Is.EqualTo("Assets/GameHotUpdate/Manifest/HotUpdateManifest.json"));
        }

        [Test]
        public void RuntimeSettingsFallbackManifestIsAuthoringOnlyAndHasNoFakeSha()
        {
            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateManifest manifest = HotUpdateManifest.FromRuntimeSettings(settings);

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.hotUpdateAssemblyKey, Is.EqualTo(settings.HotUpdateAssemblyKey));
            Assert.That(manifest.hotUpdateAssemblySha256, Is.Empty);
            CollectionAssert.AreEqual(settings.AotMetadataKeys, manifest.aotMetadataKeys);
        }

        private static HotUpdateManifest CreateValidManifest()
        {
            return new HotUpdateManifest
            {
                version = 1,
                buildTarget = "StandaloneWindows64",
                hotUpdateAssemblyKey = "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes",
                hotUpdateAssemblySha256 =
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                hotUpdateEntryClass = "HotUpdate.HotUpdateMain",
                hotUpdateEntryMethod = "Main",
                aotMetadataKeys = new System.Collections.Generic.List<string>
                {
                    "Assets/GameHotUpdate/Metadata/mscorlib.dll.bytes"
                }
            };
        }
    }
}
