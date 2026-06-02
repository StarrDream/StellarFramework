using NUnit.Framework;

namespace StellarFrameworkInstaller.Tests
{
    public sealed class StellarFrameworkDefineSymbolsUtilityTests
    {
        [Test]
        public void MergeDefineSymbolsAppendsMissingSymbols()
        {
            string merged = StellarFrameworkDefineSymbolsUtility.MergeDefineSymbols(
                "ENABLE_LOG",
                "UNITY_ADDRESSABLES",
                "HYBRIDCLR_ENABLE");

            Assert.That(merged, Does.Contain("ENABLE_LOG"));
            Assert.That(merged, Does.Contain("UNITY_ADDRESSABLES"));
            Assert.That(merged, Does.Contain("HYBRIDCLR_ENABLE"));
        }

        [Test]
        public void MergeDefineSymbolsDoesNotDuplicateSymbols()
        {
            string merged = StellarFrameworkDefineSymbolsUtility.MergeDefineSymbols(
                "ENABLE_LOG;UNITY_ADDRESSABLES",
                "UNITY_ADDRESSABLES");

            Assert.That(merged.Split(';'), Has.Exactly(1).EqualTo("UNITY_ADDRESSABLES"));
        }

        [Test]
        public void MergeDefineSymbolsIgnoresEmptySymbols()
        {
            string merged = StellarFrameworkDefineSymbolsUtility.MergeDefineSymbols("ENABLE_LOG", "", null, " ");
            Assert.That(merged, Is.EqualTo("ENABLE_LOG"));
        }
    }
}
