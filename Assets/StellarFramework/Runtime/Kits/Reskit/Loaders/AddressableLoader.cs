using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using Object = UnityEngine.Object;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace StellarFramework.Res
{
    public class AddressableLoader : ResLoader
    {
        public override ResLoaderType LoaderType => ResLoaderType.Addressable;

        protected override ResData LoadRealSync(string path)
        {
#if UNITY_ADDRESSABLES
            // 使用无泛型 Handle 避免装箱类型不匹配
            AsyncOperationHandle handle = default;
            try
            {
                // 注意：这里先用泛型加载，然后转为无泛型 Handle
                var genericHandle = Addressables.LoadAssetAsync<Object>(path);
                var result = genericHandle.WaitForCompletion();
                handle = genericHandle; // 隐式转换

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return new ResData
                    {
                        Path = path,
                        Asset = result,
                        Type = ResLoaderType.Addressable,
                        Data = handle // 存入的是 struct AsyncOperationHandle
                    };
                }
                else
                {
                    LogKit.LogError($"[AddressableLoader] 同步加载失败: {path} | Status: {handle.Status}");
                    if (handle.IsValid()) Addressables.Release(handle);
                }
            }
            catch (Exception e)
            {
                LogKit.LogWarning($"[AddressableLoader] 同步加载异常: {path}\n{e.Message}");
                if (handle.IsValid()) Addressables.Release(handle);
            }
#else
            LogKit.LogError("[AddressableLoader] 请先安装 Addressables 包并定义 UNITY_ADDRESSABLES 宏");
#endif
            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
#if UNITY_ADDRESSABLES
            AsyncOperationHandle handle = default;
            try 
            {
                var genericHandle = Addressables.LoadAssetAsync<Object>(path);
                await genericHandle.Task;
                handle = genericHandle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return new ResData
                    {
                        Path = path,
                        Asset = genericHandle.Result,
                        Type = ResLoaderType.Addressable,
                        Data = handle // 存入的是 struct AsyncOperationHandle
                    };
                }
                else
                {
                    LogKit.LogError($"[AddressableLoader] 异步加载失败: {path} | Status: {handle.Status}");
                    if (handle.IsValid()) Addressables.Release(handle);
                }
            }
            catch (Exception e)
            {
                LogKit.LogWarning($"[AddressableLoader] 异步加载异常: {path}\n{e.Message}");
                if (handle.IsValid()) Addressables.Release(handle);
            }
#endif
            return null;
        }
    }
}