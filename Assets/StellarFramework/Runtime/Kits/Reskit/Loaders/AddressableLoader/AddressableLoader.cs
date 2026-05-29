using System;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace StellarFramework.Res
{
    public class AddressableLoader : ResLoader
    {
        public override string LoaderName => "Addressables";

        protected override ResData LoadRealSync(string path)
        {
#if UNITY_ADDRESSABLES
            LogKit.LogError(
                $"[AddressableLoader] Sync load is disabled for the production Addressables backend. Use LoadAsync<T>. Path={path}. If this is UI or hot-update content, keep it on the async path.");
#else
            LogKit.LogError("[AddressableLoader] Addressables is unavailable. Install Addressables and enable UNITY_ADDRESSABLES.");
#endif
            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
        {
            return await LoadRealAsyncTyped<Object>(path, cancellationToken);
        }

        protected override async UniTask<ResData> LoadRealAsyncTyped<T>(string path, CancellationToken cancellationToken)
        {
#if UNITY_ADDRESSABLES
            if (string.IsNullOrEmpty(path))
            {
                LogKit.LogError("[AddressableLoader] Async load failed: path is empty.");
                return null;
            }

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                LogKit.LogWarning(
                    $"[AddressableLoader] Recommended address format is full Assets/... path for AB/AA compatibility. CurrentPath={path}");
            }

            AsyncOperationHandle rawHandle = default;
            AsyncOperationHandle<T> typedHandle = default;
            bool hasHandle = false;
            try
            {
                typedHandle = Addressables.LoadAssetAsync<T>(path);
                rawHandle = typedHandle;
                hasHandle = true;

                T result = await typedHandle.ToUniTask(
                    cancellationToken: cancellationToken,
                    autoReleaseWhenCanceled: false);

                if (typedHandle.Status == AsyncOperationStatus.Succeeded && result != null)
                {
                    return new ResData { Asset = result, Data = rawHandle };
                }

                ReleaseHandleIfValid(rawHandle);
                LogKit.LogError($"[AddressableLoader] Async load failed: Path={path}, Type={typeof(T).Name}, Status={typedHandle.Status}");
            }
            catch (OperationCanceledException)
            {
                if (hasHandle)
                {
                    ReleaseHandleIfValid(rawHandle);
                }

                throw;
            }
            catch (Exception e)
            {
                if (hasHandle)
                {
                    ReleaseHandleIfValid(rawHandle);
                }

                LogKit.LogError($"[AddressableLoader] Async load exception: Path={path}, Type={typeof(T).Name}\n{e.Message}");
            }
#else
            await UniTask.CompletedTask;
            LogKit.LogError("[AddressableLoader] Addressables is unavailable. Install Addressables and enable UNITY_ADDRESSABLES.");
#endif
            return null;
        }

        protected override void UnloadReal(ResData data)
        {
#if UNITY_ADDRESSABLES
            if (data == null)
            {
                return;
            }

            if (data.Data is AsyncOperationHandle<Object> objectHandle)
            {
                ReleaseHandleIfValid(objectHandle);
                return;
            }

            if (data.Data is AsyncOperationHandle handle)
            {
                ReleaseHandleIfValid(handle);
                return;
            }

            if (data.Asset != null)
            {
                Addressables.Release(data.Asset);
            }
#endif
        }

        public override void RecycleToPool()
        {
            Pool.PoolKit.Recycle<AddressableLoader>(this);
        }

#if UNITY_ADDRESSABLES
        private static void ReleaseHandleIfValid(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
#endif
    }
}
