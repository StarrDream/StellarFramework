using System;
using UnityEngine;

namespace StellarFramework.Localization.UnityUGUI
{
    /// <summary>
    /// Unity 场景中的 LocalizationService 生命周期宿主。
    /// 将 ScriptableObject Catalog 转换为纯 C# LocalizationService，并把 LocaleChanged 暴露给 View。
    /// </summary>
    /// <remarks>
    /// Context 不使用全局静态单例，因此同一项目可以按场景/子系统维护独立本地化上下文。
    /// Awake 会尝试初始化；也可在测试或运行时通过 Configure 后显式 TryInitialize。
    /// </remarks>
    public sealed class LocalizationContext : MonoBehaviour
    {
        [SerializeField] private LocalizationCatalogAsset _catalog;

        private LocalizationService _service;
        private string _lastInitializationError;

        /// <summary>当前绑定的 Catalog 资产。</summary>
        public LocalizationCatalogAsset CatalogAsset => _catalog;
        /// <summary>构建完成的纯 C# LocalizationService；未初始化时为 null。</summary>
        public LocalizationService Service => _service;
        /// <summary>是否已成功构建 Service。</summary>
        public bool IsInitialized => _service != null;
        /// <summary>最近一次初始化失败原因；成功时为空。</summary>
        public string LastInitializationError => _lastInitializationError ?? string.Empty;
        /// <summary>当前 Locale；未初始化时返回 default(LocaleId)。</summary>
        public LocaleId CurrentLocale => _service == null ? default(LocaleId) : _service.CurrentLocale;

        /// <summary>当前语言切换成功后的场景级通知。</summary>
        public event EventHandler<LocalizationChangedEventArgs> LocaleChanged;

        private void Awake()
        {
            if (!TryInitialize(out string error))
                Debug.LogError("[LocalizationContext] " + error, this);
        }

        private void OnDestroy()
        {
            UnsubscribeService();
            _service = null;
        }

        /// <summary>
        /// 替换 Catalog。若 Catalog 发生变化，会解除旧 Service 事件并等待重新初始化。
        /// </summary>
        public void Configure(LocalizationCatalogAsset catalog)
        {
            if (ReferenceEquals(_catalog, catalog) && _service != null) return;
            UnsubscribeService();
            _catalog = catalog;
            _service = null;
            _lastInitializationError = null;
        }

        /// <summary>
        /// 尝试从 Catalog 构建 LocalizationService。
        /// 已初始化时为幂等成功。
        /// </summary>
        public bool TryInitialize(out string error)
        {
            if (_service != null)
            {
                error = null;
                return true;
            }
            if (_catalog == null)
            {
                error = "LocalizationCatalogAsset is not assigned.";
                _lastInitializationError = error;
                return false;
            }
            if (!_catalog.TryBuildService(out _service, out error))
            {
                _lastInitializationError = error;
                return false;
            }

            _lastInitializationError = null;
            _service.LocaleChanged += HandleLocaleChanged;
            return true;
        }

        /// <summary>
        /// 以字符串 Locale 值切换语言。
        /// </summary>
        public bool SetLocale(string localeValue, out string error)
        {
            if (!TryInitialize(out error)) return false;
            if (!LocaleId.TryCreate(localeValue, out LocaleId locale, out error)) return false;
            return _service.SetLocale(locale, out error);
        }

        private void HandleLocaleChanged(object sender, LocalizationChangedEventArgs args)
        {
            LocaleChanged?.Invoke(this, args);
        }

        private void UnsubscribeService()
        {
            if (_service != null)
                _service.LocaleChanged -= HandleLocaleChanged;
        }
    }
}
