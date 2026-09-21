using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UIKit 资源加载策略。
    /// UIKit 只依赖该抽象，不关心底层使用 Resources、ResKit、Addressables、YooAsset 或项目自定义方案。
    /// </summary>
    public interface IUILoadStrategy
    {
        /// <summary>当前策略是否支持同步加载。</summary>
        bool SupportSyncLoad { get; }

        /// <summary>同步加载 UIRoot prefab。</summary>
        GameObject LoadUIRoot();
        /// <summary>异步加载 UIRoot prefab。</summary>
        UniTask<GameObject> LoadUIRootAsync(CancellationToken cancellationToken = default);

        /// <summary>同步加载指定 Panel prefab。</summary>
        GameObject LoadPanelPrefab(string panelName);
        /// <summary>异步加载指定 Panel prefab。</summary>
        UniTask<GameObject> LoadPanelPrefabAsync(string panelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 释放某个 Panel prefab 对应的加载侧引用/句柄。
        /// UIKit 在 destroyOnClose 面板销毁时调用。
        /// </summary>
        void UnloadPanelPrefab(string panelName);
        /// <summary>释放该加载策略持有的全部资源句柄和缓存。</summary>
        void ReleaseAll();
    }

    /// <summary>
    /// UIKit 加载与路径设置。
    /// </summary>
    [CreateAssetMenu(fileName = "UIKitSettings", menuName = "StellarFramework/UIKit Settings")]
    public sealed class UIKitSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "UIKitSettings";

        [Header("Loading")]
        [SerializeField] private string defaultLoadStrategyKey = "Resources";
        // 保留原 defaultLoadBackend 的序列化字段名，确保已存在的 UIKitSettings 资源可被 ResKit 适配器读取。
        [SerializeField] private int defaultLoadBackend;
        [SerializeField] private string customLoaderKey = string.Empty;
        [SerializeField] private bool allowSyncLoad = true;

        [Header("Paths")]
        [SerializeField] private string uiRootPath = "UIPanel/UIRoot";
        [SerializeField] private string panelPathFormat = "UIPanel/{0}";

        public string DefaultLoadStrategyKey => string.IsNullOrWhiteSpace(defaultLoadStrategyKey)
            ? "Resources"
            : defaultLoadStrategyKey.Trim();
        public int LegacyResLoadBackendValue => defaultLoadBackend;
        public string CustomLoaderKey => customLoaderKey;
        public bool AllowSyncLoad => allowSyncLoad;
        public string UIRootPath => uiRootPath;
        public string PanelPathFormat => panelPathFormat;

        /// <summary>
        /// 从 Resources 加载设置；不存在时创建只存在于内存中的默认实例。
        /// </summary>
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

        /// <summary>
        /// 根据 Panel 名称套用 PanelPathFormat，生成加载路径。
        /// </summary>
        public string BuildPanelPath(string panelName)
        {
            string format = string.IsNullOrWhiteSpace(panelPathFormat) ? "UIPanel/{0}" : panelPathFormat.Trim();
            return string.Format(format, panelName);
        }
    }
}
