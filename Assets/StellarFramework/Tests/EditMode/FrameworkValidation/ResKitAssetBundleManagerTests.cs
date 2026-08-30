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

        [Test]
        public void AssetBundlePipelineRetainsAndWarmsShadersForPlayerBuilds()
        {
            string builder = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/AssetBundle/AssetBundleToolModule.cs");
            string manager = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleManager.cs");

            Assert.That(builder, Does.Contain("EnsureAlwaysIncludedShaders"));
            Assert.That(builder, Does.Contain("m_AlwaysIncludedShaders"));
            Assert.That(builder, Does.Contain("ShaderVariantCollectionAssetPath"));
            Assert.That(builder, Does.Contain("EnsureShaderVariantCollection"));
            Assert.That(manager, Does.Contain("LoadBundleRecursiveSync(SHADER_BUNDLE_NAME"));
            Assert.That(manager, Does.Contain("LoadBundleRecursiveAsync(SHADER_BUNDLE_NAME"));
            Assert.That(manager, Does.Contain("ShaderVariantCollection"));
            Assert.That(manager, Does.Contain("材质可能显示为紫色"));
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
