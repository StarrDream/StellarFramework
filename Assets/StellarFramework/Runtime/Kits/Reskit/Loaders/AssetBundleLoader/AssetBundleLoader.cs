using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.Res.AB;

namespace StellarFramework.Res
{
    public class AssetBundleLoader : ResLoader
    {
        public override ResLoaderType LoaderType => ResLoaderType.AssetBundle;

        protected override ResData LoadRealSync(string path)
        {
            var asset = AssetBundleManager.Instance.LoadAssetSync(path);
            if (asset != null)
            {
                return new ResData
                {
                    Path = path,
                    Asset = asset,
                    Type = ResLoaderType.AssetBundle,
                    Data = null
                };
            }

            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
            var asset = await AssetBundleManager.Instance.LoadAssetAsync(path);
            if (asset != null)
            {
                return new ResData
                {
                    Path = path,
                    Asset = asset,
                    Type = ResLoaderType.AssetBundle,
                    Data = null
                };
            }

            return null;
        }
    }
}