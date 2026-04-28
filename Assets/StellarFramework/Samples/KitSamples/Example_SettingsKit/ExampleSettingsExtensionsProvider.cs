using System;
using System.Collections.Generic;
using StellarFramework.Settings;

namespace StellarFramework.Examples
{
    internal static class ExampleSettingsKeys
    {
        public const string InputJump = "input.jump";
        public const string InputDash = "input.dash";

        public const string ExtensionHudScale = "extension.hud_scale";
        public const string ExtensionCrosshairStyle = "extension.crosshair_style";
        public const string ExtensionDamageNumbers = "extension.damage_numbers";
        public const string ExtensionAccentTheme = "extension.accent_theme";
    }

    public sealed class ExampleSettingsExtensionsProvider : ISettingsPageProvider
    {
        private readonly Action<bool> _applyDamageNumbers;
        private readonly Action<float> _applyHudScale;
        private readonly Action<string> _applyCrosshairStyle;
        private readonly Action<string> _applyAccentTheme;

        public string ProviderName => "ExampleExtension";

        public ExampleSettingsExtensionsProvider(
            Action<bool> applyDamageNumbers,
            Action<float> applyHudScale,
            Action<string> applyCrosshairStyle,
            Action<string> applyAccentTheme)
        {
            _applyDamageNumbers = applyDamageNumbers;
            _applyHudScale = applyHudScale;
            _applyCrosshairStyle = applyCrosshairStyle;
            _applyAccentTheme = applyAccentTheme;
        }

        public void Register(SettingsRegistry registry)
        {
            registry.RegisterPage(new SettingsPageDefinition(
                SettingsPageIds.Extension,
                "Extensions",
                "Example page that demonstrates horizontal extension through page providers and apply strategies.",
                50));

            registry.RegisterSetting(new FloatSettingDefinition(
                ExampleSettingsKeys.ExtensionHudScale,
                SettingsPageIds.Extension,
                "HUD Scale",
                "Scales the preview HUD and crosshair.",
                1.0f,
                0.8f,
                1.4f,
                step: 0.05f,
                applyImmediately: true,
                order: 0,
                applyStrategy: new DelegateSettingApplyStrategy("Example.HudScale", (_, value) =>
                {
                    _applyHudScale?.Invoke((float)value);
                    return null;
                })));

            registry.RegisterSetting(new ChoiceSettingDefinition(
                ExampleSettingsKeys.ExtensionCrosshairStyle,
                SettingsPageIds.Extension,
                "Crosshair Style",
                "Switches the preview crosshair style without coupling SettingsKit to a UI framework.",
                "dot",
                BuildCrosshairOptions(),
                applyImmediately: true,
                order: 1,
                applyStrategy: new DelegateSettingApplyStrategy("Example.CrosshairStyle", (_, value) =>
                {
                    _applyCrosshairStyle?.Invoke(value?.ToString());
                    return null;
                })));

            registry.RegisterSetting(new BoolSettingDefinition(
                ExampleSettingsKeys.ExtensionDamageNumbers,
                SettingsPageIds.Extension,
                "Damage Numbers",
                "Toggles the floating damage number preview.",
                true,
                applyImmediately: true,
                order: 2,
                applyStrategy: new DelegateSettingApplyStrategy("Example.DamageNumbers", (_, value) =>
                {
                    _applyDamageNumbers?.Invoke((bool)value);
                    return null;
                })));

            registry.RegisterSetting(new ChoiceSettingDefinition(
                ExampleSettingsKeys.ExtensionAccentTheme,
                SettingsPageIds.Extension,
                "Accent Theme",
                "Changes the preview theme color and lighting accent.",
                "stellar_blue",
                BuildAccentThemeOptions(),
                applyImmediately: true,
                order: 3,
                applyStrategy: new DelegateSettingApplyStrategy("Example.AccentTheme", (_, value) =>
                {
                    _applyAccentTheme?.Invoke(value?.ToString());
                    return null;
                })));
        }

        public static IReadOnlyList<InputBindingSettingSpec> BuildInputSpecs()
        {
            return new[]
            {
                new InputBindingSettingSpec(
                    ExampleSettingsKeys.InputJump,
                    "Jump",
                    "Example action binding for jump.",
                    "Space",
                    new[]
                    {
                        new SettingChoiceOption("Space", "Space"),
                        new SettingChoiceOption("Mouse1", "Mouse Right"),
                        new SettingChoiceOption("J", "J")
                    },
                    order: 0),
                new InputBindingSettingSpec(
                    ExampleSettingsKeys.InputDash,
                    "Dash",
                    "Example action binding for dash.",
                    "LeftShift",
                    new[]
                    {
                        new SettingChoiceOption("LeftShift", "Left Shift"),
                        new SettingChoiceOption("LeftCtrl", "Left Ctrl"),
                        new SettingChoiceOption("K", "K")
                    },
                    order: 1)
            };
        }

        private static IReadOnlyList<SettingChoiceOption> BuildCrosshairOptions()
        {
            return new[]
            {
                new SettingChoiceOption("dot", "Dot"),
                new SettingChoiceOption("ring", "Ring"),
                new SettingChoiceOption("angle", "Angle")
            };
        }

        private static IReadOnlyList<SettingChoiceOption> BuildAccentThemeOptions()
        {
            return new[]
            {
                new SettingChoiceOption("stellar_blue", "Stellar Blue"),
                new SettingChoiceOption("amber", "Amber"),
                new SettingChoiceOption("mint", "Mint")
            };
        }
    }
}
