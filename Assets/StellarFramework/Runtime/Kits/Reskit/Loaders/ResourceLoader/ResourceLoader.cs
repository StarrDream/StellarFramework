using UnityEngine;
using Cysharp.Threading.Tasks;

namespace StellarFramework.Res
{
    public class ResourceLoader : ResLoader
    {
        public override string LoaderName => "Resources";

        protected override ResData LoadRealSync(string path)
        {
            Object asset = Resources.Load(path);
            if (asset != null) return new ResData { Asset = asset };
            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
            ResourceRequest req = Resources.LoadAsync(path);
            await req;
            if (req.asset != null) return new ResData { Asset = req.asset };
            return null;
        }

        protected override void UnloadReal(ResData data)
        {
            if (!(data.Asset is GameObject) && !(data.Asset is Component))
            {
                Resources.UnloadAsset(data.Asset);
            }
            else
            {
                ResMgr.TriggerResourcesUnload();
            }
        }
    }
}