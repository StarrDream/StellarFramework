using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class BootstrapInstallerPolicyTests
    {
        [Test]
        public void BootstrapInstallerPersistsWorkflowStateInSessionState()
        {
            string installer = ReadAssetText("Assets/StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapInstaller.cs");

            Assert.That(installer, Does.Contain("SessionState"));
            Assert.That(installer, Does.Contain("ExtractPayload"));
            Assert.That(installer, Does.Contain("InstallDependencies"));
            Assert.That(installer, Does.Contain("ImportPayload"));
            Assert.That(installer, Does.Contain("CleanupBootstrapAssets"));
            Assert.That(installer, Does.Contain("Complete"));
            Assert.That(installer, Does.Contain("Failed"));
        }

        [Test]
        public void BootstrapWindowUsesSharedInstallerServiceInsteadOfPerWindowState()
        {
            string window = ReadAssetText("Assets/StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapWindow.cs");

            Assert.That(window, Does.Not.Contain("new StellarFrameworkBootstrapInstaller()"));
            Assert.That(window, Does.Contain("StellarFrameworkBootstrapInstaller"));
            Assert.That(window, Does.Contain("StartSinglePackageInstall"));
            Assert.That(window, Does.Contain("Messages"));
            Assert.That(window, Does.Contain("Errors"));
        }

        [Test]
        public void BootstrapCleanupClosesInstallerWindowBeforeDeletingBootstrapAssets()
        {
            string utility = ReadAssetText("Assets/StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapPackageUtility.cs");

            Assert.That(utility, Does.Contain("CloseAllOpenWindows"));
            Assert.That(utility, Does.Contain("Resources.FindObjectsOfTypeAll"));
            Assert.That(utility, Does.Contain("StellarFrameworkBootstrapWindow"));
            Assert.That(utility, Does.Contain("AssetDatabase.DeleteAsset(BootstrapAssetRoot)"));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
