using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.HotUpdate;
using StellarFramework.Res;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace StellarFramework.Tests.ResKit
{
    public sealed class AddressablesHotUpdateRuntimeTests
    {
        private const string TestPrefabAddress =
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab";

        [Test]
        public void RuntimeSettingsProvidesAddressablesTestKey()
        {
            ResKitRuntimeSettings assetSettings =
                Resources.Load<ResKitRuntimeSettings>(ResKitRuntimeSettings.DefaultResourcesPath);
            Assert.That(assetSettings, Is.Not.Null,
                "Resources/ResKitRuntimeSettings.asset should load as a ResKitRuntimeSettings asset.");

            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();

            CollectionAssert.Contains(settings.BuildAddressablesDefaultUpdateKeys(), "hotupdate");
            Assert.That(settings.Validate().IsValid, Is.True);
        }

        [UnityTest]
        public IEnumerator AddressablesHotUpdateCanInitializeDownloadAndLoadPrefab()
        {
#if UNITY_ADDRESSABLES
            yield return RunAddressablesHotUpdateCanInitializeDownloadAndLoadPrefab().ToCoroutine();
#else
            Assert.Ignore("Addressables package is not installed or UNITY_ADDRESSABLES is not enabled.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AddressablesCanLoadHybridClrDllBytesAndMetadata()
        {
#if UNITY_ADDRESSABLES
            yield return RunAddressablesCanLoadHybridClrDllBytesAndMetadata().ToCoroutine();
#else
            Assert.Ignore("Addressables package is not installed or UNITY_ADDRESSABLES is not enabled.");
            yield break;
#endif
        }

        [UnityTest]
        [Explicit("Runs the full HybridCLR AA startup path. This may require an IL2CPP player-like environment.")]
        public IEnumerator HybridClrAaRunnerCanEnterHotUpdate()
        {
#if UNITY_ADDRESSABLES && HYBRIDCLR_ENABLE
            yield return RunHybridClrAaRunnerCanEnterHotUpdate().ToCoroutine();
#else
            Assert.Ignore("Requires Addressables and HYBRIDCLR_ENABLE.");
            yield break;
#endif
        }

#if UNITY_ADDRESSABLES
        private static async UniTask RunAddressablesHotUpdateCanInitializeDownloadAndLoadPrefab()
        {
            PrepareSingletonFactoryForEditMode();

            AddressableHotUpdateManager manager = AddressableHotUpdateManager.Instance;
            Assert.That(manager, Is.Not.Null);

            AddressableOperationResult init = await manager.InitializeAsync();
            Assert.That(init.Success, Is.True, init.Error);

            object[] keys = { TestPrefabAddress };
            UpdateCheckResult check = await manager.CheckCatalogUpdatesAsync(
                keys,
                updateCatalogs: true);
            Assert.That(check.IsSuccess, Is.True, check.Error);

            AddressableDownloadResult download = await manager.DownloadDependenciesAsync(
                keys,
                (AddressableDownloadProgress progress) => { });
            Assert.That(download.Success, Is.True, download.Error);

            IResLoader loader = StellarFramework.Res.ResKit.Allocate(ResLoaderRequest.Custom(
                "Addressables",
                "AddressablesHotUpdateRuntimeTests"));
            Assert.That(loader, Is.Not.Null);

            try
            {
                GameObject prefab = await loader.LoadAsync<GameObject>(TestPrefabAddress);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.name, Does.Contain("TestSphere_AA"));
            }
            finally
            {
                loader.ReleaseAll();
                StellarFramework.Res.ResKit.Recycle(loader);
            }
        }

        private static async UniTask RunAddressablesCanLoadHybridClrDllBytesAndMetadata()
        {
            PrepareSingletonFactoryForEditMode();

            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport validation = settings.Validate();
            Assert.That(validation.IsValid, Is.True, string.Join(" | ", validation.Errors));
            HotUpdateManifestLoadResult manifestResult = await HotUpdateManifestSourceChain.LoadAsync(
                HotUpdateManifestSourceChain.BuildDefaultSources(settings),
                default);
            Assert.That(manifestResult.Success, Is.True, manifestResult.Error);
            HotUpdateManifest manifest = manifestResult.Manifest;
            Assert.That(manifest, Is.Not.Null);

            AddressableHotUpdateManager manager = AddressableHotUpdateManager.Instance;
            AddressableOperationResult init = await manager.InitializeAsync();
            Assert.That(init.Success, Is.True, init.Error);

            List<object> keys = manifest.BuildDownloadKeys();
            CollectionAssert.Contains(keys, manifest.hotUpdateAssemblyKey);

            UpdateCheckResult check = await manager.CheckCatalogUpdatesAsync(
                keys,
                settings.AddressablesUpdateCatalogsOnCheck);
            Assert.That(check.IsSuccess, Is.True, check.Error);

            AddressableDownloadResult download = await manager.DownloadDependenciesAsync(keys);
            Assert.That(download.Success, Is.True, download.Error);

            IResLoader loader = StellarFramework.Res.ResKit.Allocate(ResLoaderRequest.Custom(
                "Addressables",
                "HybridCLRDllBytesRuntimeTests"));
            Assert.That(loader, Is.Not.Null);

            try
            {
                TextAsset hotUpdateDll = await loader.LoadAsync<TextAsset>(manifest.hotUpdateAssemblyKey);
                Assert.That(hotUpdateDll, Is.Not.Null);
                Assert.That(hotUpdateDll.bytes, Is.Not.Null.And.Not.Empty);
                Assert.That(ComputeSha256(hotUpdateDll.bytes), Is.EqualTo(manifest.hotUpdateAssemblySha256));

                foreach (string metadataKey in manifest.aotMetadataKeys)
                {
                    TextAsset metadata = await loader.LoadAsync<TextAsset>(metadataKey);
                    Assert.That(metadata, Is.Not.Null, metadataKey);
                    Assert.That(metadata.bytes, Is.Not.Null.And.Not.Empty, metadataKey);
                }
            }
            finally
            {
                loader.ReleaseAll();
                StellarFramework.Res.ResKit.Recycle(loader);
            }
        }

#if HYBRIDCLR_ENABLE
        private static async UniTask RunHybridClrAaRunnerCanEnterHotUpdate()
        {
            PrepareSingletonFactoryForEditMode();

            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport validation = settings.Validate();
            Assert.That(validation.IsValid, Is.True, string.Join(" | ", validation.Errors));

            HybridCLRAAHotUpdateResult result = await HotUpdateKit.RunCodeHotUpdateAsync(settings);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.State, Is.EqualTo(HybridCLRAAHotUpdateRunnerState.EnteredHotUpdate));
        }
#endif

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void PrepareSingletonFactoryForEditMode()
        {
            MethodInfo init = typeof(SingletonFactory).GetMethod(
                "Init",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(init, Is.Not.Null, "SingletonFactory.Init was not found.");
            init.Invoke(null, null);

            System.Type registerType = System.Type.GetType(
                "StellarFramework.Generated.SingletonRegister, StellarFramework.Generated.SingletonRegister");
            Assert.That(registerType, Is.Not.Null, "Generated SingletonRegister was not found.");

            MethodInfo registerAll = registerType.GetMethod(
                "RegisterAll",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(registerAll, Is.Not.Null, "SingletonRegister.RegisterAll was not found.");
            registerAll.Invoke(null, null);

            AddressablesResKitInstaller.Install();
            AddressablesHotUpdateInstaller.Install();
            EnsureAddressablesHotUpdateStrategyInstalled();
        }

        private static void EnsureAddressablesHotUpdateStrategyInstalled()
        {
            const string strategyTypeName =
                "StellarFramework.Res.AddressablesPackageHotUpdateStrategy, StellarFramework.ResKit.Addressables";

            System.Type strategyType = System.Type.GetType(strategyTypeName);
            Assert.That(strategyType, Is.Not.Null, "Addressables hot update strategy type was not found.");

            IResourceHotUpdateStrategy strategy = HotUpdateKit.ResourceStrategy;
            if (strategy == null || strategy.GetType() != strategyType)
            {
                object instance = System.Activator.CreateInstance(strategyType, true);
                Assert.That(instance, Is.Not.Null, "Failed to create Addressables hot update strategy instance.");
                HotUpdateKit.SetResourceStrategy((IResourceHotUpdateStrategy)instance);
            }

            Assert.That(HotUpdateKit.ResourceStrategy.GetType(), Is.EqualTo(strategyType));
        }
#endif
    }
}
