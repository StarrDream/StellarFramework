using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Localization.UnityUGUI
{
    /// <summary>
    /// 把 LocalizationContext 的文本键绑定到 UGUI Text。
    /// Enable 时绑定并刷新，Disable 时解除 LocaleChanged 订阅。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedTextView : MonoBehaviour
    {
        [SerializeField] private LocalizationContext _context;
        [SerializeField] private Text _target;
        [SerializeField] private string _key;
        [SerializeField, HideInInspector] private string _bindingId;
        [SerializeField] private bool _autoResolveContext = true;
        private bool _isBound;

        /// <summary>当前文本 Key。</summary>
        public string Key => _key ?? string.Empty;
        /// <summary>Editor Scanner 首次绑定时生成的稳定身份，不因层级改名、重排或 Reparent 自动改变。</summary>
        public string BindingId => _bindingId ?? string.Empty;
        /// <summary>目标 UGUI Text。</summary>
        public Text Target => _target;
        /// <summary>使用的 LocalizationContext。</summary>
        public LocalizationContext Context => _context;
        /// <summary>未显式配置 Context 时，是否从父级自动解析。</summary>
        public bool AutoResolveContext => _autoResolveContext;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!Bind(out string error))
                Debug.LogError("[LocalizedTextView] " + error, this);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Unbind();
        }

        /// <summary>
        /// 重新配置 Context、Text 与 Key。
        /// 当前已绑定时会安全解绑并按新配置重新绑定。
        /// </summary>
        public void Configure(LocalizationContext context, Text target, string key)
        {
            bool rebind = _isBound;
            if (rebind) Unbind();

            _context = context;
            _target = target;
            _key = key;

            if (rebind && !Bind(out string error))
                Debug.LogError("[LocalizedTextView] " + error, this);
        }

        /// <summary>
        /// Editor Scanner/工具链使用的稳定绑定配置。
        /// BindingId 只在首次生成、显式 Fork 或冲突修复时变化；普通扫描不能根据层级重算。
        /// </summary>
        public void ConfigureBinding(string bindingId, Text target, string key, LocalizationContext context = null)
        {
            bool rebind = _isBound;
            if (rebind) Unbind();

            _bindingId = bindingId == null ? string.Empty : bindingId.Trim();
            _target = target;
            _key = key == null ? string.Empty : key.Trim();
            _context = context;

            if (rebind && !Bind(out string error))
                Debug.LogError("[LocalizedTextView] " + error, this);
        }

        /// <summary>
        /// 立即刷新文本并订阅 LocaleChanged。
        /// 重复 Bind 不会重复订阅。
        /// </summary>
        public bool Bind(out string error)
        {
            if (_isBound)
                return Refresh(out error);

            if (!Refresh(out error))
                return false;

            _context.LocaleChanged += HandleLocaleChanged;
            _isBound = true;
            return true;
        }

        /// <summary>解除 LocaleChanged 订阅。</summary>
        public void Unbind()
        {
            if (!_isBound) return;
            if (_context != null)
                _context.LocaleChanged -= HandleLocaleChanged;
            _isBound = false;
        }

        /// <summary>
        /// 替换 Key 并立即刷新目标文本。
        /// </summary>
        public bool SetKey(string key, out string error)
        {
            _key = key;
            return Refresh(out error);
        }

        /// <summary>
        /// 使用当前 Context/Key 立即刷新 Text。
        /// 查找失败时不写入错误占位文本，而是返回 false 交给调用方处理。
        /// </summary>
        public bool Refresh(out string error)
        {
            if (_context == null && _autoResolveContext)
            {
                _context = GetComponentInParent<LocalizationContext>(true);
            }
            if (_context == null)
            {
                error = "LocalizationContext is not assigned.";
                return false;
            }
            if (_target == null)
            {
                error = "UGUI Text target is not assigned.";
                return false;
            }
            if (!_context.TryInitialize(out error)) return false;
            if (!LocalizationKey.TryCreate(_key, out LocalizationKey key, out error)) return false;

            LocalizationLookupResult lookup = _context.Service.Lookup(key);
            if (!lookup.Success)
            {
                error = "Lookup failed with status " + lookup.Status + " for key '" + key + "'.";
                return false;
            }

            _target.text = lookup.Value;
            error = null;
            return true;
        }

        private void HandleLocaleChanged(object sender, LocalizationChangedEventArgs args)
        {
            if (!Refresh(out string error))
                Debug.LogError("[LocalizedTextView] " + error, this);
        }
    }
}
