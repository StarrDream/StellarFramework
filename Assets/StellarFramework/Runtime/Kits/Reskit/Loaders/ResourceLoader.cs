using UnityEngine;
using Cysharp.Threading.Tasks;

namespace StellarFramework.Res
{
    public class ResourceLoader : ResLoader
    {
        public override ResLoaderType LoaderType => ResLoaderType.Resources;

        protected override ResData LoadRealSync(string path)
        {
            var asset = Resources.Load(path);
            if (asset != null)
            {
                return new ResData
                {
                    Path = path,
                    Asset = asset,
                    Type = ResLoaderType.Resources,
                    Data = null
                };
            }

            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
            var req = Resources.LoadAsync(path);
            await req;

            if (req.asset != null)
            {
                return new ResData
                {
                    Path = path,
                    Asset = req.asset,
                    Type = ResLoaderType.Resources,
                    Data = null
                };
            }

            return null;
        }
    }
}