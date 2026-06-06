using System.Reflection;
using NUnit.Framework;
using StellarFramework.Res;
using StellarFramework.Res.AB;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class ResKitAssetBundleManagerTests
    {
        [Test]
        public void ResolveBasePathUsesConfiguredAssetBundleRootPath()
        {
            MethodInfo method = typeof(AssetBundleManager).GetMethod(
                "ResolveBasePath",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            ResKitRuntimeSettings settings = ScriptableObject.CreateInstance<ResKitRuntimeSettings>();
            SetPrivateField(settings, "assetBundleRootPath", "CustomBundles");

            string path = (string)method.Invoke(null, new object[] { settings });

            Assert.That(path, Is.EqualTo(Application.streamingAssetsPath + "/CustomBundles"));
        }

        [Test]
        public void ResolveBasePathFallsBackToDefaultAssetBundlesFolder()
        {
            MethodInfo method = typeof(AssetBundleManager).GetMethod(
                "ResolveBasePath",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            ResKitRuntimeSettings settings = ScriptableObject.CreateInstance<ResKitRuntimeSettings>();

            string path = (string)method.Invoke(null, new object[] { settings });

            Assert.That(path, Is.EqualTo(Application.streamingAssetsPath + "/AssetBundles"));
        }

        [Test]
        public void MissingManifestErrorExplainsHowToBuildAssetBundles()
        {
            MethodInfo method = typeof(AssetBundleManager).GetMethod(
                "BuildMissingManifestError",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            string message = (string)method.Invoke(null, new object[]
            {
                "StreamingAssets/AssetBundles/Windows/Windows",
                "StreamingAssets/AssetBundles/Windows/AssetBundleManifest"
            });

            Assert.That(message, Does.Contain("Manifest 文件不存在"));
            Assert.That(message, Does.Contain("初始化AB"));
            Assert.That(message, Does.Contain("增量构建"));
            Assert.That(message, Does.Contain("StreamingAssets/AssetBundles/Windows/Windows"));
        }

        [Test]
        public void ManifestLoaderChecksFileExistsBeforeLoadingLocalBundles()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleManager.cs");

            Assert.That(source, Does.Contain("TryResolveExistingBundlePath"));
            Assert.That(source, Does.Contain("File.Exists(primaryPath)"));
            Assert.That(source, Does.Contain("File.Exists(fallbackPath)"));
            Assert.That(source, Does.Not.Contain("AssetBundle.LoadFromFile(altPath)"));
        }

        private static void SetPrivateField<T>(ResKitRuntimeSettings settings, string fieldName, T value)
        {
            FieldInfo field = typeof(ResKitRuntimeSettings).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(settings, value);
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return System.IO.File.ReadAllText(System.IO.Path.Combine(
                projectRoot,
                assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        }
    }
}
