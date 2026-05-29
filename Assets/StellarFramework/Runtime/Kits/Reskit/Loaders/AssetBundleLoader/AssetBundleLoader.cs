using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using StellarFramework.Res.AB;

namespace StellarFramework.Res
{
    public class AssetBundleLoader : ResLoader
    {
        public override string LoaderName => "AssetBundle";

        protected override ResData LoadRealSync(string path)
        {
            if (AssetBundleManager.Instance == null)
            {
                LogKit.LogError("[AssetBundleLoader] Sync load failed: AssetBundleManager instance is null. Make sure the framework singletons are initialized before requesting AB assets.");
                return null;
            }

            Object asset = AssetBundleManager.Instance?.LoadAssetSync(path);
            if (asset != null)
            {
                return new ResData { Asset = asset };
            }

            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
        {
            if (AssetBundleManager.Instance == null)
            {
                LogKit.LogError("[AssetBundleLoader] Async load failed: AssetBundleManager instance is null. Initialize the framework and AB pipeline before requesting AB assets.");
                return null;
            }

            Object asset = await AssetBundleManager.Instance.LoadAssetAsync(path, cancellationToken);
            if (asset != null)
            {
                return new ResData { Asset = asset };
            }

            return null;
        }

        protected override void UnloadReal(ResData data)
        {
            AssetBundleManager.Instance?.UnloadAsset(data.Path);
        }

        public override void RecycleToPool()
        {
            Pool.PoolKit.Recycle<AssetBundleLoader>(this);
        }
    }
}
