using UnityEngine;
using Cysharp.Threading.Tasks;
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
            LogKit.LogError($"[AddressableLoader] Addressables 不支持同步加载: {path}");
            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
#if UNITY_ADDRESSABLES
            AsyncOperationHandle<Object> handle = default;
            try 
            {
                // 1. 创建 Handle (不自动释放)
                handle = Addressables.LoadAssetAsync<Object>(path);
                
                // 2. 等待完成
                await handle.Task;
                
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    // 3. 封装数据，关键：保存 Handle
                    return new ResData
                    {
                        Path = path,
                        Asset = handle.Result,
                        Type = ResLoaderType.Addressable,
                        Data = handle // <--- 存入 Handle
                    };
                }
                else
                {
                    LogKit.LogError($"[AddressableLoader] 加载失败: {path} | Status: {handle.Status}");
                    // 失败时释放 Handle
                    if (handle.IsValid()) Addressables.Release(handle);
                }
            }
            catch (System.Exception e)
            {
                LogKit.LogError($"[AddressableLoader] 异常: {path}\n{e}");
                //  发生异常时必须释放 Handle，防止内存泄漏
                if (handle.IsValid()) Addressables.Release(handle);
            }
#endif
            return null;
        }
    }
}