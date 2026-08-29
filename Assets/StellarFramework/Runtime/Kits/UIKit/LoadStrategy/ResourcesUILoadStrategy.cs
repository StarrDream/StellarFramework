using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UIKit Core 自带的 Resources 加载策略，不依赖任何资源 Kit。
    /// </summary>
    public sealed class ResourcesUILoadStrategy : IUILoadStrategy
    {
        private readonly UIKitSettings _settings;

        public ResourcesUILoadStrategy(UIKitSettings settings = null)
        {
            _settings = settings != null ? settings : UIKitSettings.LoadOrCreateDefault();
        }

        public bool SupportSyncLoad => _settings.AllowSyncLoad;

        public GameObject LoadUIRoot()
        {
            return Resources.Load<GameObject>(_settings.UIRootPath);
        }

        public async UniTask<GameObject> LoadUIRootAsync(CancellationToken cancellationToken = default)
        {
            return await LoadAsync(_settings.UIRootPath, cancellationToken);
        }

        public GameObject LoadPanelPrefab(string panelName)
        {
            return string.IsNullOrWhiteSpace(panelName)
                ? null
                : Resources.Load<GameObject>(_settings.BuildPanelPath(panelName));
        }

        public async UniTask<GameObject> LoadPanelPrefabAsync(string panelName,
            CancellationToken cancellationToken = default)
        {
            return string.IsNullOrWhiteSpace(panelName)
                ? null
                : await LoadAsync(_settings.BuildPanelPath(panelName), cancellationToken);
        }

        public void UnloadPanelPrefab(string panelName)
        {
        }

        public void ReleaseAll()
        {
        }

        private static async UniTask<GameObject> LoadAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            ResourceRequest request = Resources.LoadAsync<GameObject>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);
            return request.asset as GameObject;
        }
    }
}
