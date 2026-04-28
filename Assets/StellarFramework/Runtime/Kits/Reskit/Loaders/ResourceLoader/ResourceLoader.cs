using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Res
{
    public class ResourceLoader : ResLoader
    {
        public override string LoaderName => "Resources";

        protected override ResData LoadRealSync(string path)
        {
            Object asset = Resources.Load(path);
            if (asset != null)
            {
                return new ResData { Asset = asset };
            }

            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
        {
            ResourceRequest req = Resources.LoadAsync(path);
            Object asset = await req.ToUniTask(cancellationToken: cancellationToken);
            if (asset != null)
            {
                return new ResData { Asset = asset };
            }

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

        public override void RecycleToPool()
        {
            Pool.PoolKit.Recycle<ResourceLoader>(this);
        }
    }
}
