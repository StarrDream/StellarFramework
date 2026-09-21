using NUnit.Framework;
using StellarFramework.Editor.Modules;
using StellarFramework.Res;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace StellarFramework.Tests.ResKit
{
    public sealed class AddressablesBuildToolTests
    {
        [Test]
        public void AddressablesInstallerRegistersPureResKitBackend()
        {
            AddressablesResKitInstaller.Install();

            using (ResScope scope = StellarFramework.Res.ResKit.CreateCustomScope(
                       AddressablesResKitInstaller.LoaderKey,
                       "AddressablesBuildToolTests"))
            {
                Assert.That(scope.Loader, Is.TypeOf<AddressableLoader>());
                var loader = (AddressableLoader)scope.Loader;
                Assert.That(loader.LoaderName, Is.EqualTo("Addressables"));
            }
        }

        [Test]
        public void LocalBuiltInPolicyDisablesRemoteCatalogAndRemoteGroupPaths()
        {
            AddressablesLocalConfigurationReport apply =
                AddressablesBuildToolLogic.ApplyLocalBuiltInDefaults();
            Assert.That(apply.Success, Is.True, string.Join(" | ", apply.Errors));

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.BuildRemoteCatalog, Is.False);

            AddressablesLocalConfigurationReport inspect =
                AddressablesBuildToolLogic.InspectLocalConfiguration();
            Assert.That(inspect.Success, Is.True, string.Join(" | ", inspect.Errors));
        }

        [Test]
        public void AddressablesToolDoesNotOwnHybridClrOrContentUpdateWorkflow()
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    UnityEngine.Application.dataPath,
                    "StellarFramework/Editor/StellarToolsHub/Modules/Addressables/AddressablesBuildToolModule.cs"));

            Assert.That(source, Does.Not.Contain("HybridCLRHotUpdateAssetExporter"));
            Assert.That(source, Does.Not.Contain("HotUpdateSettings"));
            Assert.That(source, Does.Not.Contain("RemoteHotUpdate"));
            Assert.That(source, Does.Not.Contain("HotUpdateManifest"));
        }
    }
}
