using System.Collections.Generic;

namespace StellarFramework.Settings
{
    public static class SettingsPageIds
    {
        public const string Gameplay = "gameplay";
        public const string Audio = "audio";
        public const string Graphics = "graphics";
        public const string Input = "input";
        public const string Language = "language";
        public const string Extension = "extension";
    }

    public static class SettingsKeys
    {
        public const string GameplayShowSubtitles = "gameplay.show_subtitles";
        public const string GameplayCameraSensitivity = "gameplay.camera_sensitivity";
        public const string GameplayScreenShake = "gameplay.screen_shake";

        public const string AudioMusicOn = "audio.music_on";
        public const string AudioMusicVolume = "audio.music_volume";
        public const string AudioSoundOn = "audio.sound_on";
        public const string AudioSoundVolume = "audio.sound_volume";

        public const string GraphicsFullscreen = "graphics.fullscreen";
        public const string GraphicsVSync = "graphics.vsync";
        public const string GraphicsQuality = "graphics.quality";
        public const string GraphicsResolution = "graphics.resolution";
        public const string GraphicsTargetFrameRate = "graphics.target_frame_rate";

        public const string LanguageCurrent = "language.current";
    }

    public sealed class DefaultSettingsInstallOptions
    {
        public bool IncludeGameplay { get; set; } = true;
        public bool IncludeAudio { get; set; } = true;
        public bool IncludeGraphics { get; set; } = true;
        public bool IncludeInput { get; set; } = true;
        public bool IncludeLanguage { get; set; } = true;

        public IAudioSettingsAdapter AudioAdapter { get; set; }
        public IGraphicsSettingsAdapter GraphicsAdapter { get; set; }
        public IInputBindingAdapter InputAdapter { get; set; }
        public ILanguageSettingsAdapter LanguageAdapter { get; set; }
        public IReadOnlyList<ISettingsPageProvider> AdditionalProviders { get; set; }
    }

    public static class DefaultSettingsInstaller
    {
        public static void Install(SettingsManager manager, DefaultSettingsInstallOptions options)
        {
            if (manager == null)
            {
                return;
            }

            if (options.IncludeGameplay)
            {
                manager.RegisterProvider(new GameplaySettingsPageProvider());
            }

            if (options.IncludeAudio && options.AudioAdapter != null)
            {
                manager.RegisterProvider(new AudioSettingsPageProvider(options.AudioAdapter));
            }

            if (options.IncludeGraphics && options.GraphicsAdapter != null)
            {
                manager.RegisterProvider(new GraphicsSettingsPageProvider(options.GraphicsAdapter));
            }

            if (options.IncludeInput && options.InputAdapter != null)
            {
                manager.RegisterProvider(new InputSettingsPageProvider(options.InputAdapter));
            }

            if (options.IncludeLanguage && options.LanguageAdapter != null)
            {
                manager.RegisterProvider(new LanguageSettingsPageProvider(options.LanguageAdapter));
            }

            if (options.AdditionalProviders == null)
            {
                return;
            }

            for (int i = 0; i < options.AdditionalProviders.Count; i++)
            {
                ISettingsPageProvider provider = options.AdditionalProviders[i];
                if (provider != null)
                {
                    manager.RegisterProvider(provider);
                }
            }
        }
    }

    public sealed class GameplaySettingsPageProvider : ISettingsPageProvider
    {
        public string ProviderName => "Gameplay";

        public void Register(SettingsRegistry registry)
        {
            registry.RegisterPage(new SettingsPageDefinition(
                SettingsPageIds.Gameplay,
                "Gameplay / 游戏设置",
                "Subtitles, camera sensitivity, and other gameplay-facing local preferences.",
                0));

            registry.RegisterSetting(new BoolSettingDefinition(
                SettingsKeys.GameplayShowSubtitles,
                SettingsPageIds.Gameplay,
                "Subtitles / 字幕显示",
                "Toggle gameplay and cutscene subtitles.",
                true,
                order: 0));

            registry.RegisterSetting(new FloatSettingDefinition(
                SettingsKeys.GameplayCameraSensitivity,
                SettingsPageIds.Gameplay,
                "Camera Sensitivity / 镜头灵敏度",
                "Global multiplier for camera turning speed.",
                1f,
                0.2f,
                2f,
                step: 0.05f,
                order: 1));

            registry.RegisterSetting(new FloatSettingDefinition(
                SettingsKeys.GameplayScreenShake,
                SettingsPageIds.Gameplay,
                "Screen Shake / 屏幕震动",
                "Controls the overall intensity of hit and explosion feedback.",
                1f,
                0f,
                1f,
                step: 0.05f,
                order: 2));
        }
    }

    public sealed class AudioSettingsPageProvider : ISettingsPageProvider
    {
        private readonly IAudioSettingsAdapter _adapter;

        public string ProviderName => "Audio";

        public AudioSettingsPageProvider(IAudioSettingsAdapter adapter)
        {
            _adapter = adapter;
        }

        public void Register(SettingsRegistry registry)
        {
            registry.RegisterPage(new SettingsPageDefinition(
                SettingsPageIds.Audio,
                "Audio / 声音设置",
                "Unified entry for music, SFX, and mute behavior.",
                10));

            registry.RegisterSetting(new BoolSettingDefinition(
                SettingsKeys.AudioMusicOn,
                SettingsPageIds.Audio,
                "Music Enabled / 音乐开关",
                "Stops background music output immediately when disabled.",
                _adapter.MusicOn,
                applyImmediately: true,
                order: 0,
                applyStrategy: new DelegateSettingApplyStrategy("Audio.MusicOn", (_, value) =>
                {
                    _adapter.MusicOn = (bool)value;
                    return null;
                })));

            registry.RegisterSetting(new FloatSettingDefinition(
                SettingsKeys.AudioMusicVolume,
                SettingsPageIds.Audio,
                "Music Volume / 音乐音量",
                "Controls BGM volume.",
                _adapter.MusicVolume,
                0f,
                1f,
                step: 0.05f,
                applyImmediately: true,
                order: 1,
                applyStrategy: new DelegateSettingApplyStrategy("Audio.MusicVolume", (_, value) =>
                {
                    _adapter.MusicVolume = (float)value;
                    return null;
                })));

            registry.RegisterSetting(new BoolSettingDefinition(
                SettingsKeys.AudioSoundOn,
                SettingsPageIds.Audio,
                "SFX Enabled / 音效开关",
                "Stops currently playing sound effects immediately when disabled.",
                _adapter.SoundOn,
                applyImmediately: true,
                order: 2,
                applyStrategy: new DelegateSettingApplyStrategy("Audio.SoundOn", (_, value) =>
                {
                    _adapter.SoundOn = (bool)value;
                    return null;
                })));

            registry.RegisterSetting(new FloatSettingDefinition(
                SettingsKeys.AudioSoundVolume,
                SettingsPageIds.Audio,
                "SFX Volume / 音效音量",
                "Controls sound effect volume.",
                _adapter.SoundVolume,
                0f,
                1f,
                step: 0.05f,
                applyImmediately: true,
                order: 3,
                applyStrategy: new DelegateSettingApplyStrategy("Audio.SoundVolume", (_, value) =>
                {
                    _adapter.SoundVolume = (float)value;
                    return null;
                })));
        }
    }

    public sealed class GraphicsSettingsPageProvider : ISettingsPageProvider
    {
        private readonly IGraphicsSettingsAdapter _adapter;

        public string ProviderName => "Graphics";

        public GraphicsSettingsPageProvider(IGraphicsSettingsAdapter adapter)
        {
            _adapter = adapter;
        }

        public void Register(SettingsRegistry registry)
        {
            registry.RegisterPage(new SettingsPageDefinition(
                SettingsPageIds.Graphics,
                "Graphics / 画面设置",
                "Resolution, fullscreen, framerate, and quality level.",
                20));

            registry.RegisterSetting(new BoolSettingDefinition(
                SettingsKeys.GraphicsFullscreen,
                SettingsPageIds.Graphics,
                "Fullscreen / 全屏模式",
                "Toggle between fullscreen and windowed mode.",
                _adapter.IsFullscreen,
                order: 0,
                applyStrategy: new DelegateSettingApplyStrategy("Graphics.Fullscreen", (_, value) =>
                {
                    return _adapter.ApplyFullscreen((bool)value, out string error) ? null : error;
                })));

            registry.RegisterSetting(new BoolSettingDefinition(
                SettingsKeys.GraphicsVSync,
                SettingsPageIds.Graphics,
                "VSync / 垂直同步",
                "Reduces tearing but can increase input latency.",
                _adapter.IsVSyncEnabled,
                order: 1,
                applyStrategy: new DelegateSettingApplyStrategy("Graphics.VSync", (_, value) =>
                {
                    return _adapter.ApplyVSync((bool)value, out string error) ? null : error;
                })));

            registry.RegisterSetting(new ChoiceSettingDefinition(
                SettingsKeys.GraphicsQuality,
                SettingsPageIds.Graphics,
                "Quality / 画质等级",
                "Switches Unity QualitySettings levels.",
                _adapter.GetCurrentQualityValue(),
                _adapter.GetQualityOptions(),
                order: 2,
                applyStrategy: new DelegateSettingApplyStrategy("Graphics.Quality", (_, value) =>
                {
                    return _adapter.ApplyQuality(value.ToString(), out string error) ? null : error;
                })));

            registry.RegisterSetting(new ChoiceSettingDefinition(
                SettingsKeys.GraphicsResolution,
                SettingsPageIds.Graphics,
                "Resolution / 分辨率",
                "Switches the current display resolution.",
                _adapter.GetCurrentResolutionValue(),
                _adapter.GetResolutionOptions(),
                order: 3,
                applyStrategy: new DelegateSettingApplyStrategy("Graphics.Resolution", (_, value) =>
                {
                    return _adapter.ApplyResolution(value.ToString(), out string error) ? null : error;
                })));

            registry.RegisterSetting(new ChoiceSettingDefinition(
                SettingsKeys.GraphicsTargetFrameRate,
                SettingsPageIds.Graphics,
                "Target FPS / 目标帧率",
                "Updates Application.targetFrameRate.",
                _adapter.GetCurrentTargetFrameRateValue(),
                _adapter.GetTargetFrameRateOptions(),
                order: 4,
                applyStrategy: new DelegateSettingApplyStrategy("Graphics.TargetFrameRate", (_, value) =>
                {
                    return _adapter.ApplyTargetFrameRate(value.ToString(), out string error) ? null : error;
                })));
        }
    }

    public sealed class LanguageSettingsPageProvider : ISettingsPageProvider
    {
        private readonly ILanguageSettingsAdapter _adapter;

        public string ProviderName => "Language";

        public LanguageSettingsPageProvider(ILanguageSettingsAdapter adapter)
        {
            _adapter = adapter;
        }

        public void Register(SettingsRegistry registry)
        {
            registry.RegisterPage(new SettingsPageDefinition(
                SettingsPageIds.Language,
                "Language / 语言设置",
                "Works with any localization pipeline through an adapter.",
                40));

            registry.RegisterSetting(new ChoiceSettingDefinition(
                SettingsKeys.LanguageCurrent,
                SettingsPageIds.Language,
                "Current Language / 当前语言",
                "Switches UI text and localized content.",
                _adapter.GetCurrentLanguageValue(),
                _adapter.GetLanguageOptions(),
                applyImmediately: true,
                order: 0,
                applyStrategy: new DelegateSettingApplyStrategy("Language.Current", (_, value) =>
                {
                    return _adapter.ApplyLanguage(value.ToString(), out string error) ? null : error;
                })));
        }
    }

    public sealed class InputSettingsPageProvider : ISettingsPageProvider
    {
        private readonly IInputBindingAdapter _adapter;

        public string ProviderName => "Input";

        public InputSettingsPageProvider(IInputBindingAdapter adapter)
        {
            _adapter = adapter;
        }

        public void Register(SettingsRegistry registry)
        {
            registry.RegisterPage(new SettingsPageDefinition(
                SettingsPageIds.Input,
                "Input / 键位设置",
                "Connects classic Input, Input System, or any third-party solution through an adapter.",
                30));

            IReadOnlyList<InputBindingSettingSpec> specs = _adapter.GetBindingSpecs();
            for (int i = 0; i < specs.Count; i++)
            {
                InputBindingSettingSpec spec = specs[i];
                registry.RegisterSetting(new ChoiceSettingDefinition(
                    spec.Key,
                    SettingsPageIds.Input,
                    spec.DisplayName,
                    spec.Description,
                    spec.DefaultValue,
                    spec.Options,
                    applyImmediately: true,
                    order: spec.Order,
                    applyStrategy: new DelegateSettingApplyStrategy("Input.Binding", (definition, value) =>
                    {
                        return _adapter.ApplyBinding(definition.Key, value.ToString(), out string error) ? null : error;
                    })));
            }
        }
    }
}
