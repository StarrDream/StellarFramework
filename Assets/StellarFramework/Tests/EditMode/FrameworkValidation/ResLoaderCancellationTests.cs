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
        [UnityTest]
        public IEnumerator SamePathPendingLoadHonorsWaitingCallerCancellation()
        {
            return RunSamePathPendingLoadHonorsWaitingCallerCancellation().ToCoroutine();
        }

        [UnityTest]
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
                await UniTask.Yield();
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
            public override string LoaderName => "CancellationAwareBlockingTest";

            protected override ResData LoadRealSync(string path)
            {
                return null;
            }

            protected override async UniTask<ResData> LoadRealAsync(
                string path,
                CancellationToken cancellationToken)
            {
                await UniTask.WaitUntilCanceled(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }

            protected override void UnloadReal(ResData data)
            {
            }
        }
    }
}
