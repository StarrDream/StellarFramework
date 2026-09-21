using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using System.IO;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public class ResourceLoader : ResLoader
    {
        public override string LoaderName => "Resources";

        protected override ResData LoadRealSync(string path)
        {
            Object asset = Resources.Load(NormalizeResourcesPath(path));
            if (asset != null)
            {
                return new ResData { Asset = asset };
            }

            return null;
        }

        protected override async UniTask<ResData> LoadRealAsync(string path, CancellationToken cancellationToken)
        {
            ResourceRequest req = Resources.LoadAsync(NormalizeResourcesPath(path));
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

        /// <summary>
        /// Accept both the traditional Resources-relative key and a canonical Assets/... path.
        /// This lets generated AssetsMap constants remain backend-neutral.
        /// </summary>
        internal static string NormalizeResourcesPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            const string marker = "/Resources/";
            int resourcesIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex < 0)
            {
                return normalized;
            }

            string relative = normalized.Substring(resourcesIndex + marker.Length);
            string extension = Path.GetExtension(relative);
            return string.IsNullOrEmpty(extension)
                ? relative
                : relative.Substring(0, relative.Length - extension.Length);
        }
    }
}
