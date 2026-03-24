using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.Res.AB;

namespace StellarFramework.Res
{
    public class AssetBundleLoader : ResLoader
    {
        public override string LoaderName => "AssetBundle";

        protected override ResData LoadRealSync(string path)
        {
            var asset = AssetBundleManager.Instance?.LoadAssetSync(path);
            if (asset != null) return new ResData { Asset = asset };
            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
            var asset = await AssetBundleManager.Instance.LoadAssetAsync(path);
            if (asset != null) return new ResData { Asset = asset };
            return null;
        }

        protected override void UnloadReal(ResData data)
        {
            AssetBundleManager.Instance?.UnloadAsset(data.Path);
        }
    }
}