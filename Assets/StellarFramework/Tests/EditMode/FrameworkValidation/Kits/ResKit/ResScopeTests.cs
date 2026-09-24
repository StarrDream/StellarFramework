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
    public sealed class ResScopeTests
    {
        private const string LoaderKey = "ResScopeTests";
        private const int TimeoutMs = 5000;

        [TearDown]
        public void TearDown()
        {
            ResKit.UnregisterCustomLoader(LoaderKey);
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator DisposeReleasesOwnedAssetsAndRecyclesLoaderOnce()
        {
            return RunDisposeReleasesOwnedAssetsAndRecyclesLoaderOnce().ToCoroutine();
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator DisposeCancelsPendingLoad()
        {
            return RunDisposeCancelsPendingLoad().ToCoroutine();
        }

        [Test]
        public void DisposedScopeRejectsFurtherUseAndDisposeIsIdempotent()
        {
            var loader = new ImmediateScopeTestLoader();
            ResKit.RegisterLoader(LoaderKey, _ => loader);

            ResScope scope = ResKit.CreateCustomScope(LoaderKey, "DisposedScope");
            scope.Dispose();
            scope.Dispose();

            Assert.That(scope.IsDisposed, Is.True);
            Assert.That(loader.RecycleCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => _ = scope.Loader);
            Assert.Throws<ObjectDisposedException>(() => scope.Release("unused"));
        }

        private static async UniTask RunDisposeReleasesOwnedAssetsAndRecyclesLoaderOnce()
        {
            var loader = new ImmediateScopeTestLoader();
            ResKit.RegisterLoader(LoaderKey, _ => loader);

            GameObject asset;
            using (ResScope scope = ResKit.CreateCustomScope(LoaderKey, "Inventory"))
            {
                asset = await scope.LoadAsync<GameObject>("Assets/Test/Scope.asset");
                Assert.That(asset, Is.Not.Null);
                Assert.That(scope.IsDisposed, Is.False);
                Assert.That(loader.UnloadCount, Is.EqualTo(0));
            }

            Assert.That(loader.RecycleCount, Is.EqualTo(1));
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
            Assert.That(asset == null, Is.True, "Scope disposal should release the final owned Unity object.");
        }

        private static async UniTask RunDisposeCancelsPendingLoad()
        {
            var loader = new BlockingScopeTestLoader();
            ResKit.RegisterLoader(LoaderKey, _ => loader);

            ResScope scope = ResKit.CreateCustomScope(LoaderKey, "PendingOwner");
            UniTask<UnityEngine.Object> pending =
                scope.LoadAsync<UnityEngine.Object>("Assets/Test/PendingScope.asset");

            Assert.That(
                loader.PhysicalLoadStarted,
                Is.True,
                "The fake physical load must be active before disposing the scope.");

            scope.Dispose();

            bool cancelled = false;
            try
            {
                await pending;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.That(cancelled, Is.True);
            Assert.That(
                loader.PhysicalCancellationObserved,
                Is.True,
                "Disposing the only waiting scope must cancel the shared physical load.");
            Assert.That(loader.RecycleCount, Is.EqualTo(1));
        }

        private sealed class ImmediateScopeTestLoader : ResLoader
        {
            public override string LoaderName => "ResScopeImmediateTest";
            public int UnloadCount { get; private set; }
            public int RecycleCount { get; private set; }

            protected override ResData LoadRealSync(string path)
            {
                return CreateData();
            }

            protected override UniTask<ResData> LoadRealAsync(
                string path,
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(CreateData());
            }

            protected override void UnloadReal(ResData data)
            {
                UnloadCount++;
                if (data?.Asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(data.Asset);
                }
            }

            public override void RecycleToPool()
            {
                RecycleCount++;
                OnRecycled();
            }

            private static ResData CreateData()
            {
                return new ResData
                {
                    Asset = new GameObject("ResScopeTestAsset")
                };
            }
        }

        private sealed class BlockingScopeTestLoader : ResLoader
        {
            public override string LoaderName => "ResScopeBlockingTest";
            public int RecycleCount { get; private set; }
            public bool PhysicalLoadStarted { get; private set; }
            public bool PhysicalCancellationObserved { get; private set; }

            protected override ResData LoadRealSync(string path)
            {
                return null;
            }

            protected override async UniTask<ResData> LoadRealAsync(
                string path,
                CancellationToken cancellationToken)
            {
                PhysicalLoadStarted = true;

                try
                {
                    await UniTask.WaitUntilCanceled(
                        cancellationToken,
                        PlayerLoopTiming.Update,
                        completeImmediately: true);
                    cancellationToken.ThrowIfCancellationRequested();
                    return null;
                }
                catch (OperationCanceledException)
                {
                    PhysicalCancellationObserved = true;
                    throw;
                }
            }

            protected override void UnloadReal(ResData data)
            {
            }

            public override void RecycleToPool()
            {
                RecycleCount++;
                OnRecycled();
            }
        }
    }
}
