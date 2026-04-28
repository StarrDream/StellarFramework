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
            Object asset = AssetBundleManager.Instance?.LoadAssetSync(path);
            if (asset != null)
            {
                return new ResData { Asset = asset };
            }

            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
        {
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
