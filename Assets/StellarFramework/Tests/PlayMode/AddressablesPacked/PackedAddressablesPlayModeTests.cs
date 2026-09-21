using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Res;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    /// <summary>
    /// Packed Addressables 必须在真实 Play Mode 中验证。
    /// EditMode 只负责 Fast Mode / Authoring 配置检查，不用 AssetDatabase provider 模拟 Bundle runtime。
    /// </summary>
    public sealed class PackedAddressablesPlayModeTests
    {
        private const int TimeoutMs = 60000;
        private const string TestPrefabAddress =
            "Assets/StellarFramework/Tests/Fixtures/Addressables/AddressablesTestPrefab.prefab";

        [UnityTest]
        [Timeout(TimeoutMs + 5000)]
        public IEnumerator PackedModeLoadsPrefabThroughResKit()
        {
            yield return RunPackedModeLoadsPrefabThroughResKit().ToCoroutine();
        }

        private static async UniTask RunPackedModeLoadsPrefabThroughResKit()
        {
            AddressableAssetSettings addressableSettings =
                AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(addressableSettings, Is.Not.Null);

            int previousBuilderIndex = addressableSettings.ActivePlayModeDataBuilderIndex;
            int packedBuilderIndex = addressableSettings.DataBuilders.FindIndex(
                builder => builder is BuildScriptPackedPlayMode);
            Assert.That(
                packedBuilderIndex,
                Is.GreaterThanOrEqualTo(0),
                "Addressables Packed Play Mode builder is missing.");

            IResLoader loader = null;
            try
            {
                addressableSettings.ActivePlayModeDataBuilderIndex = packedBuilderIndex;
                var packedBuilder =
                    addressableSettings.GetDataBuilder(packedBuilderIndex) as BuildScriptPackedPlayMode;
                Assert.That(packedBuilder, Is.Not.Null);

                AddressablesPlayModeBuildResult buildResult =
                    packedBuilder.BuildData<AddressablesPlayModeBuildResult>(
                        new AddressablesDataBuilderInput(addressableSettings));
                Assert.That(buildResult, Is.Not.Null);
                Assert.That(
                    buildResult.Error,
                    Is.Null.Or.Empty,
                    "Build Addressables Player Content before running the packed runtime gate.");

                // Explicitly install the ResKit backend so this test verifies only
                // Addressables asset loading/release, not any content-update workflow.
                AddressablesResKitInstaller.Install();

                using (var timeout = new CancellationTokenSource())
                {
                    timeout.CancelAfter(TimeoutMs);
                    CancellationToken cancellationToken = timeout.Token;

                    loader = StellarFramework.Res.ResKit.Allocate(
                        ResLoaderRequest.Custom("Addressables", "PackedAddressablesPlayModeTests"));
                    Assert.That(loader, Is.Not.Null);

                    GameObject prefab = await loader.LoadAsync<GameObject>(
                        TestPrefabAddress,
                        cancellationToken);
                    Assert.That(prefab, Is.Not.Null);
                    Assert.That(prefab.name, Is.EqualTo("AddressablesTestPrefab"));
                }
            }
            finally
            {
                if (loader != null)
                {
                    loader.ReleaseAll();
                    StellarFramework.Res.ResKit.Recycle(loader);
                }

                addressableSettings.ActivePlayModeDataBuilderIndex = previousBuilderIndex;
                var previousBuilder = addressableSettings.GetDataBuilder(previousBuilderIndex);
                if (previousBuilder != null &&
                    previousBuilder.CanBuildData<AddressablesPlayModeBuildResult>())
                {
                    previousBuilder.BuildData<AddressablesPlayModeBuildResult>(
                        new AddressablesDataBuilderInput(addressableSettings));
                }
            }
        }

    }
}
