using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StellarFramework.Audio;
using UnityEngine;

namespace StellarFramework.Settings
{
    public sealed class AudioKitSettingsAdapter : IAudioSettingsAdapter
    {
        public float MusicVolume
        {
            get => AudioKit.MusicVolume;
            set => AudioKit.MusicVolume = value;
        }

        public float SoundVolume
        {
            get => AudioKit.SoundVolume;
            set => AudioKit.SoundVolume = value;
        }

        public bool MusicOn
        {
            get => AudioKit.MusicOn;
            set => AudioKit.MusicOn = value;
        }

        public bool SoundOn
        {
            get => AudioKit.SoundOn;
            set => AudioKit.SoundOn = value;
        }
    }

    public sealed class UnityGraphicsSettingsAdapter : IGraphicsSettingsAdapter
    {
        private readonly List<SettingChoiceOption> _frameRateOptions = new List<SettingChoiceOption>
        {
            new SettingChoiceOption("30", "30 FPS"),
            new SettingChoiceOption("60", "60 FPS"),
            new SettingChoiceOption("120", "120 FPS"),
            new SettingChoiceOption("unlimited", "Unlimited / 不限制")
        };

        public IReadOnlyList<SettingChoiceOption> GetResolutionOptions()
        {
            return Screen.resolutions
                .Select(resolution => new SettingChoiceOption(
                    $"{resolution.width}x{resolution.height}@{resolution.refreshRate}",
                    $"{resolution.width} x {resolution.height} @ {resolution.refreshRate}Hz"))
                .Distinct(new OptionValueComparer())
                .ToList();
        }

        public string GetCurrentResolutionValue()
        {
            Resolution current = Screen.currentResolution;
            return $"{current.width}x{current.height}@{current.refreshRate}";
        }

        public bool ApplyResolution(string value, out string error)
        {
            if (!TryParseResolution(value, out int width, out int height, out int refreshRate))
            {
                error = $"[UnityGraphicsSettingsAdapter] Invalid resolution value: {value}";
                return false;
            }

            Screen.SetResolution(width, height, Screen.fullScreen, refreshRate);
            error = null;
            return true;
        }

        public IReadOnlyList<SettingChoiceOption> GetQualityOptions()
        {
            string[] names = QualitySettings.names;
            var options = new List<SettingChoiceOption>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                options.Add(new SettingChoiceOption(i.ToString(CultureInfo.InvariantCulture), names[i]));
            }

            return options;
        }

        public string GetCurrentQualityValue()
        {
            return QualitySettings.GetQualityLevel().ToString(CultureInfo.InvariantCulture);
        }

        public bool ApplyQuality(string value, out string error)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int qualityLevel))
            {
                error = $"[UnityGraphicsSettingsAdapter] Invalid quality level: {value}";
                return false;
            }

            qualityLevel = Mathf.Clamp(qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            QualitySettings.SetQualityLevel(qualityLevel, true);
            error = null;
            return true;
        }

        public bool IsFullscreen => Screen.fullScreen;

        public bool ApplyFullscreen(bool value, out string error)
        {
            Screen.fullScreen = value;
            error = null;
            return true;
        }

        public bool IsVSyncEnabled => QualitySettings.vSyncCount > 0;

        public bool ApplyVSync(bool value, out string error)
        {
            QualitySettings.vSyncCount = value ? 1 : 0;
            error = null;
            return true;
        }

        public IReadOnlyList<SettingChoiceOption> GetTargetFrameRateOptions()
        {
            return _frameRateOptions;
        }

        public string GetCurrentTargetFrameRateValue()
        {
            return Application.targetFrameRate <= 0
                ? "unlimited"
                : Application.targetFrameRate.ToString(CultureInfo.InvariantCulture);
        }

        public bool ApplyTargetFrameRate(string value, out string error)
        {
            if (string.Equals(value, "unlimited", StringComparison.OrdinalIgnoreCase))
            {
                Application.targetFrameRate = -1;
                error = null;
                return true;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetFrameRate))
            {
                error = $"[UnityGraphicsSettingsAdapter] Invalid target framerate: {value}";
                return false;
            }

            Application.targetFrameRate = targetFrameRate;
            error = null;
            return true;
        }

        private static bool TryParseResolution(string token, out int width, out int height, out int refreshRate)
        {
            width = 0;
            height = 0;
            refreshRate = 60;

            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            string[] pieces = token.Split('@');
            if (pieces.Length == 0)
            {
                return false;
            }

            string[] sizePieces = pieces[0].Split('x');
            if (sizePieces.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(sizePieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width) ||
                !int.TryParse(sizePieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
            {
                return false;
            }

            if (pieces.Length > 1)
            {
                int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out refreshRate);
                if (refreshRate <= 0)
                {
                    refreshRate = 60;
                }
            }

            return true;
        }

        private sealed class OptionValueComparer : IEqualityComparer<SettingChoiceOption>
        {
            public bool Equals(SettingChoiceOption x, SettingChoiceOption y)
            {
                return string.Equals(x?.Value, y?.Value, StringComparison.Ordinal);
            }

            public int GetHashCode(SettingChoiceOption obj)
            {
                return obj?.Value?.GetHashCode() ?? 0;
            }
        }
    }

    public sealed class SimpleLanguageSettingsAdapter : ILanguageSettingsAdapter
    {
        private readonly IReadOnlyList<SettingChoiceOption> _options;
        private readonly Action<string> _onLanguageChanged;
        private string _currentLanguageValue;

        public SimpleLanguageSettingsAdapter(
            IReadOnlyList<SettingChoiceOption> options,
            string defaultLanguageValue,
            Action<string> onLanguageChanged = null)
        {
            _options = options ?? Array.Empty<SettingChoiceOption>();
            _currentLanguageValue = string.IsNullOrEmpty(defaultLanguageValue) && _options.Count > 0
                ? _options[0].Value
                : defaultLanguageValue;
            _onLanguageChanged = onLanguageChanged;
        }

        public IReadOnlyList<SettingChoiceOption> GetLanguageOptions()
        {
            return _options;
        }

        public string GetCurrentLanguageValue()
        {
            return _currentLanguageValue;
        }

        public bool ApplyLanguage(string value, out string error)
        {
            _currentLanguageValue = value ?? string.Empty;
            _onLanguageChanged?.Invoke(_currentLanguageValue);
            error = null;
            return true;
        }
    }

    public sealed class SimpleInputBindingAdapter : IInputBindingAdapter
    {
        private readonly IReadOnlyList<InputBindingSettingSpec> _specs;
        private readonly Dictionary<string, string> _bindings = new Dictionary<string, string>();
        private readonly Action<string, string> _onBindingChanged;

        public SimpleInputBindingAdapter(
            IReadOnlyList<InputBindingSettingSpec> specs,
            Action<string, string> onBindingChanged = null)
        {
            _specs = specs ?? Array.Empty<InputBindingSettingSpec>();
            _onBindingChanged = onBindingChanged;

            for (int i = 0; i < _specs.Count; i++)
            {
                InputBindingSettingSpec spec = _specs[i];
                _bindings[spec.Key] = spec.DefaultValue;
            }
        }

        public IReadOnlyList<InputBindingSettingSpec> GetBindingSpecs()
        {
            return _specs;
        }

        public bool ApplyBinding(string settingKey, string value, out string error)
        {
            if (!_bindings.ContainsKey(settingKey))
            {
                error = $"[SimpleInputBindingAdapter] Unknown input binding key: {settingKey}";
                return false;
            }

            _bindings[settingKey] = value ?? string.Empty;
            _onBindingChanged?.Invoke(settingKey, _bindings[settingKey]);
            error = null;
            return true;
        }
    }
}
