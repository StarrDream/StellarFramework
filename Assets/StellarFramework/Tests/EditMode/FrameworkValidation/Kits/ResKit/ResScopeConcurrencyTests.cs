using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Res;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class ResScopeConcurrencyTests
    {
        private const string LoaderKey = "Tests.SharedScope";
        private const string TestPath = "Assets/Test/Shared.asset";
        private const int TimeoutMs = 5000;

        [SetUp]
        public void SetUp()
        {
            SharedBlockingLoader.Reset();
            StellarFramework.Res.ResKit.RegisterLoader(LoaderKey, _ => new SharedBlockingLoader());
        }

        [TearDown]
        public void TearDown()
        {
            StellarFramework.Res.ResKit.UnregisterCustomLoader(LoaderKey);
            SharedBlockingLoader.CleanupAsset();
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator TwoScopesShareOnePhysicalLoadAndUnloadAfterLastOwner()
        {
            return RunTwoScopesShareOnePhysicalLoadAndUnloadAfterLastOwner().ToCoroutine();
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator OneScopeCancellationDoesNotCancelAnotherScopesSharedLoad()
        {
            return RunOneScopeCancellationDoesNotCancelAnotherScopesSharedLoad().ToCoroutine();
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator DisposingLastWaitingScopeCancelsSharedPhysicalLoad()
        {
            return RunDisposingLastWaitingScopeCancelsSharedPhysicalLoad().ToCoroutine();
        }

        private static async UniTask RunTwoScopesShareOnePhysicalLoadAndUnloadAfterLastOwner()
        {
            var scopeA = StellarFramework.Res.ResKit.CreateCustomScope(LoaderKey, "ScopeA");
            var scopeB = StellarFramework.Res.ResKit.CreateCustomScope(LoaderKey, "ScopeB");
            try
            {
                UniTask<UnityEngine.Object> loadA = scopeA.LoadAsync<UnityEngine.Object>(TestPath);
                UniTask<UnityEngine.Object> loadB = scopeB.LoadAsync<UnityEngine.Object>(TestPath);

                Assert.That(SharedBlockingLoader.PhysicalLoadCount, Is.EqualTo(1));
                SharedBlockingLoader.Complete();

                UnityEngine.Object assetA = await loadA;
                UnityEngine.Object assetB = await loadB;
                Assert.That(assetA, Is.Not.Null);
                Assert.That(assetB, Is.SameAs(assetA));

                scopeA.Dispose();
                Assert.That(SharedBlockingLoader.UnloadCount, Is.EqualTo(0),
                    "Disposing one owner must not unload a resource still held by another scope.");

                scopeB.Dispose();
                Assert.That(SharedBlockingLoader.UnloadCount, Is.EqualTo(1));
            }
            finally
            {
                scopeA.Dispose();
                scopeB.Dispose();
            }
        }

        private static async UniTask RunOneScopeCancellationDoesNotCancelAnotherScopesSharedLoad()
        {
            var scopeA = StellarFramework.Res.ResKit.CreateCustomScope(LoaderKey, "CancelledScope");
            var scopeB = StellarFramework.Res.ResKit.CreateCustomScope(LoaderKey, "SurvivingScope");
            using (var callerCancellation = new CancellationTokenSource())
            {
                try
                {
                    UniTask<UnityEngine.Object> cancelledLoad =
                        scopeA.LoadAsync<UnityEngine.Object>(TestPath, callerCancellation.Token);
                    UniTask<UnityEngine.Object> survivingLoad =
                        scopeB.LoadAsync<UnityEngine.Object>(TestPath);

                    callerCancellation.Cancel();
                    bool cancellationObserved = false;
                    try
                    {
                        await cancelledLoad;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationObserved = true;
                    }

                    Assert.That(cancellationObserved, Is.True);
                    Assert.That(SharedBlockingLoader.SharedPhysicalCancellationCount, Is.EqualTo(0),
                        "Cancelling one waiter must not cancel the shared physical load while another waiter remains.");

                    SharedBlockingLoader.Complete();
                    UnityEngine.Object result = await survivingLoad;
                    Assert.That(result, Is.Not.Null);
                    Assert.That(SharedBlockingLoader.PhysicalLoadCount, Is.EqualTo(1));
                }
                finally
                {
                    scopeA.Dispose();
                    scopeB.Dispose();
                }
            }
        }

        private static async UniTask RunDisposingLastWaitingScopeCancelsSharedPhysicalLoad()
        {
            var scope = StellarFramework.Res.ResKit.CreateCustomScope(LoaderKey, "LastScope");
            UniTask<UnityEngine.Object> load = scope.LoadAsync<UnityEngine.Object>(TestPath);

            Assert.That(SharedBlockingLoader.PhysicalLoadCount, Is.EqualTo(1));

            scope.Dispose();
            bool cancelled = false;
            try
            {
                await load;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.That(cancelled, Is.True);
            Assert.That(SharedBlockingLoader.SharedPhysicalCancellationCount, Is.EqualTo(1));
        }

        private sealed class SharedBlockingLoader : ResLoader
        {
            private static UniTaskCompletionSource<ResData> _source;
            private static GameObject _asset;

            public static int PhysicalLoadCount { get; private set; }
            public static int SharedPhysicalCancellationCount { get; private set; }
            public static int UnloadCount { get; private set; }

            public override string LoaderName => "SharedScopePhysicalLoader";

            public static void Reset()
            {
                CleanupAsset();
                _source = new UniTaskCompletionSource<ResData>();
                PhysicalLoadCount = 0;
                SharedPhysicalCancellationCount = 0;
                UnloadCount = 0;
            }

            public static void Complete()
            {
                if (_asset == null) _asset = new GameObject("ResScopeSharedDummy");
                _source.TrySetResult(new ResData { Asset = _asset });
            }

            public static void CleanupAsset()
            {
                if (_asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(_asset);
                    _asset = null;
                }
            }

            protected override ResData LoadRealSync(string path)
            {
                return null;
            }

            protected override async UniTask<ResData> LoadRealAsync(
                string path,
                CancellationToken cancellationToken)
            {
                PhysicalLoadCount++;
                try
                {
                    return await _source.Task.AttachExternalCancellation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    SharedPhysicalCancellationCount++;
                    throw;
                }
            }

            protected override void UnloadReal(ResData data)
            {
                UnloadCount++;
            }

            public override void RecycleToPool()
            {
                ReleaseAll();
            }
        }
    }
}
