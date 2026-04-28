using System;
using System.Collections.Generic;

namespace StellarFramework.Settings
{
    public enum SettingValueKind
    {
        Bool,
        Float,
        Int,
        String,
        Choice
    }

    public sealed class SettingsPageDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }

        public SettingsPageDefinition(string id, string displayName, string description, int order = 0)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Order = order;
        }
    }

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

    public interface ISettingApplyStrategy
    {
        string StrategyName { get; }
        bool TryApply(SettingDefinition definition, object value, out string error);
    }

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

    public interface ISettingsStorage
    {
        bool TryLoad(string key, out string rawValue);
        void Save(string key, string rawValue);
        void Delete(string key);
        void Flush();
    }

    public interface ISettingsPageProvider
    {
        string ProviderName { get; }
        void Register(SettingsRegistry registry);
    }

    public interface IAudioSettingsAdapter
    {
        float MusicVolume { get; set; }
        float SoundVolume { get; set; }
        bool MusicOn { get; set; }
        bool SoundOn { get; set; }
    }

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

    public interface ILanguageSettingsAdapter
    {
        IReadOnlyList<SettingChoiceOption> GetLanguageOptions();
        string GetCurrentLanguageValue();
        bool ApplyLanguage(string value, out string error);
    }

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

    public interface IInputBindingAdapter
    {
        IReadOnlyList<InputBindingSettingSpec> GetBindingSpecs();
        bool ApplyBinding(string settingKey, string value, out string error);
    }
}
