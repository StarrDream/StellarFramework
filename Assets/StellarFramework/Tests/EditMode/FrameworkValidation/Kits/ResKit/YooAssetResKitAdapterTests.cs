using NUnit.Framework;
using StellarFramework.Res;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class YooAssetResKitAdapterTests
    {
        [TearDown]
        public void TearDown()
        {
            YooAssetResKitInstaller.Uninstall();
        }

        [Test]
        public void InstallRegistersYooAssetLoaderForDefaultPackage()
        {
            YooAssetResKitInstaller.Install();

            using (ResScope scope =
                   ResKit.CreateCustomScope(YooAssetResKitInstaller.LoaderKey, "DefaultPackageTest"))
            {
                Assert.That(scope.Loader, Is.TypeOf<YooAssetLoader>());

                var loader = (YooAssetLoader)scope.Loader;
                Assert.That(loader.PackageName, Is.EqualTo(YooAssetResKitInstaller.DefaultPackageName));
                Assert.That(loader.LoaderName, Is.EqualTo("YooAsset:DefaultPackage"));
            }
        }

        [Test]
        public void InstallCanBindCustomPackageName()
        {
            YooAssetResKitInstaller.Install("Gameplay");

            using (ResScope scope =
                   ResKit.CreateCustomScope(YooAssetResKitInstaller.LoaderKey, "GameplayTest"))
            {
                var loader = (YooAssetLoader)scope.Loader;
                Assert.That(loader.PackageName, Is.EqualTo("Gameplay"));
                Assert.That(loader.LoaderName, Is.EqualTo("YooAsset:Gameplay"));
            }
        }

        [Test]
        public void DifferentPackagesUseDifferentSharedCacheNamespaces()
        {
            YooAssetResKitInstaller.Install("PackageA");
            using ResScope packageA =
                ResKit.CreateCustomScope(YooAssetResKitInstaller.LoaderKey, "A");
            string loaderA = packageA.Loader is YooAssetLoader a ? a.LoaderName : string.Empty;

            YooAssetResKitInstaller.Install("PackageB");
            using ResScope packageB =
                ResKit.CreateCustomScope(YooAssetResKitInstaller.LoaderKey, "B");
            string loaderB = packageB.Loader is YooAssetLoader b ? b.LoaderName : string.Empty;

            Assert.That(loaderA, Is.EqualTo("YooAsset:PackageA"));
            Assert.That(loaderB, Is.EqualTo("YooAsset:PackageB"));
            Assert.That(loaderA, Is.Not.EqualTo(loaderB));
        }
    }
}
