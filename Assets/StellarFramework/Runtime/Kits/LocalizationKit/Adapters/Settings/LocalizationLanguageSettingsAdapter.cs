using System;
using System.Collections.Generic;
using StellarFramework.Settings;

namespace StellarFramework.Localization.SettingsAdapter
{
    /// <summary>
    /// SettingsKit 语言选择项与 Localization Locale 的映射。
    /// </summary>
    public readonly struct LocalizationLanguageOption
    {
        /// <summary>对应 Locale。</summary>
        public LocaleId Locale { get; }
        /// <summary>设置 UI 展示名称，例如“中文”或“English”。</summary>
        public string Label { get; }
        /// <summary>可选说明文本。</summary>
        public string Description { get; }

        /// <summary>创建语言选项。</summary>
        public LocalizationLanguageOption(LocaleId locale, string label, string description = "")
        {
            if (!locale.IsValid)
                throw new ArgumentException("Locale must be valid.", nameof(locale));

            Locale = locale;
            Label = string.IsNullOrEmpty(label) ? locale.Value : label;
            Description = description ?? string.Empty;
        }
    }

    /// <summary>
    /// 把 LocalizationService 暴露为 SettingsKit 的语言设置 Adapter。
    /// </summary>
    /// <remarks>
    /// 不传 options 时按 Catalog 全部 Locale 自动生成选项，Label 默认等于 Locale 值；
    /// 正式产品通常应显式传入“中文 / English”等用户可读标签。
    /// </remarks>
    public sealed class LocalizationLanguageSettingsAdapter : ILanguageSettingsAdapter
    {
        private readonly LocalizationService _service;
        private readonly SettingChoiceOption[] _options;

        /// <summary>
        /// 创建 Adapter。自定义 options 中的 Locale 必须存在于 Catalog，且不可重复。
        /// </summary>
        public LocalizationLanguageSettingsAdapter(
            LocalizationService service,
            IReadOnlyList<LocalizationLanguageOption> options = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _options = options == null || options.Count == 0
                ? BuildDefaultOptions(service.Catalog)
                : BuildConfiguredOptions(service.Catalog, options);
        }

        /// <summary>返回 SettingsKit 可展示的语言选项。</summary>
        public IReadOnlyList<SettingChoiceOption> GetLanguageOptions() => _options;

        /// <summary>返回当前 Locale 的稳定字符串值。</summary>
        public string GetCurrentLanguageValue() => _service.CurrentLocale.Value;

        /// <summary>将 SettingsKit 选中的字符串值应用到 LocalizationService。</summary>
        public bool ApplyLanguage(string value, out string error)
        {
            if (!LocaleId.TryCreate(value, out LocaleId locale, out error))
                return false;
            return _service.SetLocale(locale, out error);
        }

        private static SettingChoiceOption[] BuildDefaultOptions(LocalizationCatalog catalog)
        {
            var result = new SettingChoiceOption[catalog.Count];
            for (int i = 0; i < catalog.Count; i++)
            {
                LocaleId locale = catalog.GetTable(i).Locale;
                result[i] = new SettingChoiceOption(locale.Value, locale.Value);
            }
            return result;
        }

        private static SettingChoiceOption[] BuildConfiguredOptions(
            LocalizationCatalog catalog,
            IReadOnlyList<LocalizationLanguageOption> options)
        {
            var result = new SettingChoiceOption[options.Count];
            var seen = new HashSet<LocaleId>();
            for (int i = 0; i < options.Count; i++)
            {
                LocalizationLanguageOption option = options[i];
                if (!catalog.Contains(option.Locale))
                    throw new ArgumentException(
                        "Configured language option locale '" + option.Locale +
                        "' does not exist in LocalizationCatalog.",
                        nameof(options));
                if (!seen.Add(option.Locale))
                    throw new ArgumentException(
                        "Configured language option locale '" + option.Locale + "' is duplicated.",
                        nameof(options));

                result[i] = new SettingChoiceOption(
                    option.Locale.Value,
                    option.Label,
                    option.Description);
            }
            return result;
        }
    }
}
