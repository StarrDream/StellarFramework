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
            AsyncOperationHandle handle = default;
            try
            {
                var genericHandle = Addressables.LoadAssetAsync<Object>(path);
                Object result = genericHandle.WaitForCompletion();
                handle = genericHandle;

                if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
                {
                    return new ResData { Asset = result, Data = handle };
                }

                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            catch (Exception e)
            {
                LogKit.LogError($"[AddressableLoader] 同步加载异常: {path}\n{e.Message}");
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
#endif
            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
        {
#if UNITY_ADDRESSABLES
            AsyncOperationHandle<Object> genericHandle = default;
            try
            {
                genericHandle = Addressables.LoadAssetAsync<Object>(path);
                Object result = await genericHandle.ToUniTask(
                    cancellationToken: cancellationToken,
                    autoReleaseWhenCanceled: false);

                if (genericHandle.Status == AsyncOperationStatus.Succeeded && result != null)
                {
                    return new ResData { Asset = result, Data = genericHandle };
                }

                if (genericHandle.IsValid())
                {
                    Addressables.Release(genericHandle);
                }
            }
            catch (OperationCanceledException)
            {
                if (genericHandle.IsValid())
                {
                    Addressables.Release(genericHandle);
                }

                throw;
            }
            catch (Exception e)
            {
                LogKit.LogError($"[AddressableLoader] 异步加载异常: {path}\n{e.Message}");
                if (genericHandle.IsValid())
                {
                    Addressables.Release(genericHandle);
                }
            }
#endif
            await UniTask.CompletedTask;
            return null;
        }

        protected override void UnloadReal(ResData data)
        {
#if UNITY_ADDRESSABLES
            if (data.Data is AsyncOperationHandle handle && handle.IsValid())
            {
                Addressables.Release(handle);
            }
            else
            {
                Addressables.Release(data.Asset);
            }
#endif
        }

        public override void RecycleToPool()
        {
            Pool.PoolKit.Recycle<AddressableLoader>(this);
        }
    }
}
