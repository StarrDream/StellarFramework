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

        private static void SetPrivateField<T>(ResKitRuntimeSettings settings, string fieldName, T value)
        {
            FieldInfo field = typeof(ResKitRuntimeSettings).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(settings, value);
        }
    }
}
