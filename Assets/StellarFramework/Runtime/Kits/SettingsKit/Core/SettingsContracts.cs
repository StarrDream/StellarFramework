using System;
using System.Collections.Generic;

namespace StellarFramework.Settings
{
    /// <summary>
    /// 设置值的标准数据类型。
    /// </summary>
    public enum SettingValueKind
    {
        Bool,
        Float,
        Int,
        String,
        Choice
    }

    /// <summary>
    /// 设置页元数据。
    /// </summary>
    public sealed class SettingsPageDefinition
    {
        /// <summary>稳定页 ID，用于注册和查询。</summary>
        public string Id { get; }
        /// <summary>面向用户的页名称。</summary>
        public string DisplayName { get; }
        /// <summary>可选说明。</summary>
        public string Description { get; }
        /// <summary>页排序权重；越小越靠前。</summary>
        public int Order { get; }

        public SettingsPageDefinition(string id, string displayName, string description, int order = 0)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Order = order;
        }
    }

    /// <summary>
    /// Choice 类型设置的一项可选值。
    /// Value 是持久化稳定值，Label 是 UI 展示文本。
    /// </summary>
    public sealed class SettingChoiceOption
    {
        public string Value { get; }
        public string Label { get; }
        public string Description { get; }

        public SettingChoiceOption(string value, string label, string description = "")
        {
            Value = value ?? string.Empty;
            Label = string.IsNullOrEmpty(label) ? Value : label;
            Description = description ?? string.Empty;
        }
    }

    /// <summary>
    /// 把 SettingsKit 中的值应用到真实运行时系统的策略接口。
    /// </summary>
    /// <remarks>
    /// Core 只管理设置状态，不直接依赖 AudioKit、Localization、Input 或具体图形实现；
    /// 这些副作用通过 ApplyStrategy / Adapter 注入。
    /// </remarks>
    public interface ISettingApplyStrategy
    {
        string StrategyName { get; }
        bool TryApply(SettingDefinition definition, object value, out string error);
    }

    /// <summary>
    /// 不产生运行时副作用的 Apply 策略。
    /// 适合纯偏好数据或由业务层自行读取的设置。
    /// </summary>
    public sealed class NoopSettingApplyStrategy : ISettingApplyStrategy
    {
        public static readonly NoopSettingApplyStrategy Instance = new NoopSettingApplyStrategy();

        public string StrategyName => "Noop";

        public bool TryApply(SettingDefinition definition, object value, out string error)
        {
            error = null;
            return true;
        }
    }

    /// <summary>
    /// 用委托快速适配外部系统的 Apply 策略。
    /// 委托返回 null/empty 表示成功，非空字符串表示错误。
    /// </summary>
    public sealed class DelegateSettingApplyStrategy : ISettingApplyStrategy
    {
        private readonly Func<SettingDefinition, object, string> _applyFunc;

        public string StrategyName { get; }

        public DelegateSettingApplyStrategy(string strategyName, Func<SettingDefinition, object, string> applyFunc)
        {
            StrategyName = string.IsNullOrEmpty(strategyName) ? "Delegate" : strategyName;
            _applyFunc = applyFunc ?? throw new ArgumentNullException(nameof(applyFunc));
        }

        public bool TryApply(SettingDefinition definition, object value, out string error)
        {
            error = _applyFunc(definition, value);
            return string.IsNullOrEmpty(error);
        }
    }

    /// <summary>
    /// SettingsKit 的持久化抽象。
    /// Storage 只处理字符串读写，类型转换由 SettingDefinition 负责。
    /// </summary>
    public interface ISettingsStorage
    {
        bool TryLoad(string key, out string rawValue);
        void Save(string key, string rawValue);
        void Delete(string key);
        void Flush();
    }

    /// <summary>
    /// 设置页/设置项注册来源。
    /// </summary>
    public interface ISettingsPageProvider
    {
        string ProviderName { get; }
        void Register(SettingsRegistry registry);
    }

    /// <summary>
    /// SettingsKit 与音频实现之间的最小适配接口。
    /// </summary>
    public interface IAudioSettingsAdapter
    {
        float MusicVolume { get; set; }
        float SoundVolume { get; set; }
        bool MusicOn { get; set; }
        bool SoundOn { get; set; }
    }

    /// <summary>
    /// SettingsKit 与 Unity/项目图形设置之间的适配接口。
    /// </summary>
    public interface IGraphicsSettingsAdapter
    {
        IReadOnlyList<SettingChoiceOption> GetResolutionOptions();
        string GetCurrentResolutionValue();
        bool ApplyResolution(string value, out string error);

        IReadOnlyList<SettingChoiceOption> GetQualityOptions();
        string GetCurrentQualityValue();
        bool ApplyQuality(string value, out string error);

        bool IsFullscreen { get; }
        bool ApplyFullscreen(bool value, out string error);

        bool IsVSyncEnabled { get; }
        bool ApplyVSync(bool value, out string error);

        IReadOnlyList<SettingChoiceOption> GetTargetFrameRateOptions();
        string GetCurrentTargetFrameRateValue();
        bool ApplyTargetFrameRate(string value, out string error);
    }

    /// <summary>
    /// SettingsKit 与本地化系统之间的语言设置适配接口。
    /// </summary>
    public interface ILanguageSettingsAdapter
    {
        IReadOnlyList<SettingChoiceOption> GetLanguageOptions();
        string GetCurrentLanguageValue();
        bool ApplyLanguage(string value, out string error);
    }

    /// <summary>
    /// 一项输入绑定设置的定义。
    /// </summary>
    public sealed class InputBindingSettingSpec
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string DefaultValue { get; }
        public IReadOnlyList<SettingChoiceOption> Options { get; }
        public int Order { get; }

        public InputBindingSettingSpec(string key, string displayName, string description, string defaultValue,
            IReadOnlyList<SettingChoiceOption> options, int order = 0)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            DefaultValue = defaultValue ?? string.Empty;
            Options = options ?? Array.Empty<SettingChoiceOption>();
            Order = order;
        }
    }

    /// <summary>
    /// SettingsKit 与具体输入系统之间的适配接口。
    /// </summary>
    public interface IInputBindingAdapter
    {
        IReadOnlyList<InputBindingSettingSpec> GetBindingSpecs();
        bool ApplyBinding(string settingKey, string value, out string error);
    }
}
