using NUnit.Framework;

namespace StellarFrameworkInstaller.Tests
{
    public sealed class StellarFrameworkPackageManifestUtilityTests
    {
        [Test]
        public void ManifestContainsPackageDetectsRegistryPackage()
        {
            const string manifest = "{\"dependencies\":{\"com.unity.addressables\":\"1.22.3\"}}";
            Assert.That(StellarFrameworkPackageManifestUtility.ManifestContainsPackage(manifest, "com.unity.addressables"), Is.True);
        }

        [Test]
        public void ManifestContainsPackageDetectsGitPackage()
        {
            const string manifest = "{\"dependencies\":{\"com.code-philosophy.hybridclr\":\"https://github.com/focus-creative-games/hybridclr_unity.git\"}}";
            Assert.That(StellarFrameworkPackageManifestUtility.ManifestContainsPackage(manifest, "com.code-philosophy.hybridclr"), Is.True);
        }

        [Test]
        public void BuildPackageSourceUsesVersionForRegistryPackages()
        {
            Assert.That(
                StellarFrameworkPackageManifestUtility.BuildPackageSource("com.unity.addressables", "1.22.3", ""),
                Is.EqualTo("com.unity.addressables@1.22.3"));
        }

        [Test]
        public void BuildPackageSourceUsesGitUrlWhenProvided()
        {
            Assert.That(
                StellarFrameworkPackageManifestUtility.BuildPackageSource("com.cysharp.unitask", "1.0.0", "https://example.com/repo.git"),
                Is.EqualTo("https://example.com/repo.git"));
        }
    }
}
