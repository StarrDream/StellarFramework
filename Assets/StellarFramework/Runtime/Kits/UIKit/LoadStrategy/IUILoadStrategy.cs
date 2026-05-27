using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UI 加载策略接口
    /// 我只定义 UIKit 真正关心的加载能力，不让 UIKit 知道底层到底使用 Resources、AB、AA 还是业务自定义加载器
    /// </summary>
    public interface IUILoadStrategy
    {
        bool SupportSyncLoad { get; }

        GameObject LoadUIRoot();
        UniTask<GameObject> LoadUIRootAsync(CancellationToken cancellationToken = default);

        GameObject LoadPanelPrefab(string panelName);
        UniTask<GameObject> LoadPanelPrefabAsync(string panelName, CancellationToken cancellationToken = default);

        void UnloadPanelPrefab(string panelName);
        void ReleaseAll();
    }

    [CreateAssetMenu(fileName = "UIKitSettings", menuName = "StellarFramework/UIKit Settings")]
    public sealed class UIKitSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "UIKitSettings";

        [Header("Loading")]
        [SerializeField] private ResLoadBackend defaultLoadBackend = ResLoadBackend.Default;
        [SerializeField] private string customLoaderKey = string.Empty;
        [SerializeField] private bool allowSyncLoad = true;

        [Header("Paths")]
        [SerializeField] private string uiRootPath = "UIPanel/UIRoot";
        [SerializeField] private string panelPathFormat = "UIPanel/{0}";

        public ResLoadBackend DefaultLoadBackend => defaultLoadBackend;
        public string CustomLoaderKey => customLoaderKey;
        public bool AllowSyncLoad => allowSyncLoad;
        public string UIRootPath => uiRootPath;
        public string PanelPathFormat => panelPathFormat;

        public static UIKitSettings LoadOrCreateDefault(string resourcesPath = DefaultResourcesPath)
        {
            UIKitSettings settings = null;
            if (!string.IsNullOrWhiteSpace(resourcesPath))
            {
                settings = Resources.Load<UIKitSettings>(resourcesPath);
            }

            if (settings != null)
            {
                return settings;
            }

            settings = CreateInstance<UIKitSettings>();
            settings.name = "UIKitSettings_RuntimeDefault";
            return settings;
        }

        public string BuildPanelPath(string panelName)
        {
            string format = string.IsNullOrWhiteSpace(panelPathFormat) ? "UIPanel/{0}" : panelPathFormat.Trim();
            return string.Format(format, panelName);
        }
    }
}
