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
    public sealed class ResLoaderCancellationTests
    {
        private const int TimeoutMs = 5000;

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator SamePathPendingLoadHonorsWaitingCallerCancellation()
        {
            return RunSamePathPendingLoadHonorsWaitingCallerCancellation().ToCoroutine();
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator SolePendingLoadPropagatesCallerCancellation()
        {
            return RunSolePendingLoadPropagatesCallerCancellation().ToCoroutine();
        }

        private static async UniTask RunSolePendingLoadPropagatesCallerCancellation()
        {
            var loader = new CancellationAwareBlockingLoader();

            using (var cancelled = new CancellationTokenSource())
            {
                UniTask<UnityEngine.Object> pending =
                    loader.LoadAsync<UnityEngine.Object>("Assets/Test/SolePending.asset", cancelled.Token);

                Assert.That(
                    loader.PhysicalLoadStarted,
                    Is.True,
                    "The fake physical load must have started synchronously before cancellation is requested.");

                cancelled.Cancel();

                bool cancellationObserved = false;
                try
                {
                    await pending;
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved = true;
                }

                Assert.That(
                    cancellationObserved,
                    Is.True,
                    "The first/sole caller cancellation must propagate as OperationCanceledException, not null.");

                Assert.That(
                    loader.PhysicalCancellationObserved,
                    Is.True,
                    "When the sole waiter cancels, ResMgr must cancel the shared physical load as well.");
            }

            loader.ReleaseAll();
        }

        private static async UniTask RunSamePathPendingLoadHonorsWaitingCallerCancellation()
        {
            var loader = new BlockingTestLoader();
            UniTask<UnityEngine.Object> first =
                loader.LoadAsync<UnityEngine.Object>("Assets/Test/Pending.asset");

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();

                bool cancellationObserved = false;
                try
                {
                    await loader.LoadAsync<UnityEngine.Object>(
                        "Assets/Test/Pending.asset",
                        cancelled.Token);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved = true;
                }

                Assert.That(
                    cancellationObserved,
                    Is.True,
                    "A caller joining an existing same-path load must be able to cancel its own wait.");
            }

            loader.CompletePending();
            UnityEngine.Object firstResult = await first;
            Assert.That(firstResult, Is.Not.Null);
            loader.ReleaseAll();
            UnityEngine.Object.DestroyImmediate(firstResult);
        }

        private sealed class BlockingTestLoader : ResLoader
        {
            private readonly UniTaskCompletionSource<ResData> _source =
                new UniTaskCompletionSource<ResData>();

            public override string LoaderName => "BlockingTest";

            public void CompletePending()
            {
                var asset = new GameObject("ResLoaderCancellationDummy");
                _source.TrySetResult(new ResData
                {
                    Asset = asset
                });
            }

            protected override ResData LoadRealSync(string path)
            {
                return null;
            }

            protected override UniTask<ResData> LoadRealAsync(
                string path,
                CancellationToken cancellationToken)
            {
                return _source.Task;
            }

            protected override void UnloadReal(ResData data)
            {
            }
        }

        private sealed class CancellationAwareBlockingLoader : ResLoader
        {
            public bool PhysicalLoadStarted { get; private set; }
            public bool PhysicalCancellationObserved { get; private set; }

            public override string LoaderName => "CancellationAwareBlockingTest";

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
                    // This fake is validating ResMgr's cancellation contract, not UniTask's
                    // Editor PlayerLoop integration. Complete immediately from the token
                    // callback so the test cannot deadlock while Unity Test Runner owns the
                    // EditMode update loop.
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
        }
    }
}
