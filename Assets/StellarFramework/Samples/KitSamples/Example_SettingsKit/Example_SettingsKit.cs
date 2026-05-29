using System.Collections.Generic;
using StellarFramework.Audio;
using StellarFramework.Res;
using StellarFramework.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace StellarFramework.Examples
{
    /// <summary>
    /// SettingsKit 综合使用示例。
    ///
    /// 场景: Scenes/SettingsKit_Playable.unity
    /// 操作: F10 显示/隐藏设置面板，1/2 播放测试音效，F5 保存，F9 重置。
    /// 前置: 样例构建器会生成预览物体、默认设置页和 Example Extensions 扩展页。
    /// 通过标准: 修改设置会立即影响预览，保存后再次运行能恢复上次设置。
    /// </summary>
    public class Example_SettingsKit : MonoBehaviour
    {
        [Header("Optional Runtime Adapters")]
        [SerializeField]
        private AudioMixer _mainMixer;

        [Header("Preview Targets")]
        [SerializeField]
        private Light _previewLight;

        [SerializeField]
        private Renderer _previewRenderer;

        [SerializeField]
        private SettingsMenuOverlay _overlay;

        private readonly Dictionary<string, string> _languageLabels = new Dictionary<string, string>
        {
            { "zh-CN", "简体中文" },
            { "en-US", "English" },
            { "ja-JP", "日语" }
        };

        private bool _showSubtitles = true;
        private float _cameraSensitivity = 1f;
        private float _screenShake = 1f;
        private bool _showDamageNumbers = true;
        private float _hudScale = 1f;
        private string _crosshairStyle = "dot";
        private string _accentTheme = "stellar_blue";
        private string _currentLanguage = "zh-CN";
        private string _currentLanguageLabel = "简体中文";
        private string _jumpBinding = "Space";
        private string _dashBinding = "LeftShift";
        private Color _accentColor = new Color(0.27f, 0.55f, 0.92f);
        private Vector3 _previewBasePosition;

        private void Start()
        {
            if (_overlay == null)
            {
                _overlay = GetComponent<SettingsMenuOverlay>();
                if (_overlay == null)
                {
                    _overlay = gameObject.AddComponent<SettingsMenuOverlay>();
                }
            }

            if (_previewRenderer != null)
            {
                _previewBasePosition = _previewRenderer.transform.position;
            }

            if (_overlay != null)
            {
                _overlay.title = "SettingsKit Sample";
                _overlay.toggleKey = KeyCode.F10;
                _overlay.visibleOnStart = true;
                _overlay.windowRect = new Rect(36f, 36f, 860f, 620f);
            }

            ConfigureAudio();
            InstallSettings();
            RefreshPreviewState();
        }

        private void OnDestroy()
        {
            SettingsKit.SettingChanged -= HandleSettingChanged;
        }

        private void Update()
        {
            UpdatePreviewObject();

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                AudioKit.PlaySound("Audio/SFX/UI_Click", SoundPriority.Normal);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) && _previewRenderer != null)
            {
                AudioKit.PlaySound3D("Audio/SFX/Explosion", _previewRenderer.transform.position, SoundPriority.High);
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (SettingsKit.Save(out string error))
                {
                    Debug.Log("[Example_SettingsKit] 设置已保存。");
                }
                else
                {
                    Debug.LogError(error);
                }
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                SettingsKit.ResetAll();
            }
        }

        private void OnGUI()
        {
            DrawStatusPanel();
            DrawCrosshairPreview();
            DrawSubtitlePreview();
            DrawDamageNumberPreview();
        }

        private void ConfigureAudio()
        {
            if (_mainMixer == null)
            {
                return;
            }

            AudioKit.Init(_mainMixer, new ExampleAudioFallbackLoader<ResourceLoader>());
            AudioKit.PlayMusic("Audio/BGM/MainTheme", 0.5f);
        }

        private void InstallSettings()
        {
            SettingsKit.SettingChanged -= HandleSettingChanged;
            SettingsKit.SettingChanged += HandleSettingChanged;

            SettingsKit.ConfigureStorage(new PlayerPrefsSettingsStorage("StellarFramework.Sample.SettingsKit."));
            SettingsKit.InstallDefaultProviders(new DefaultSettingsInstallOptions
            {
                IncludeGameplay = true,
                IncludeAudio = _mainMixer != null,
                IncludeGraphics = true,
                IncludeInput = true,
                IncludeLanguage = true,
                AudioAdapter = _mainMixer != null ? new AudioKitSettingsAdapter() : null,
                GraphicsAdapter = new UnityGraphicsSettingsAdapter(),
                LanguageAdapter = new SimpleLanguageSettingsAdapter(BuildLanguageOptions(), "zh-CN", ApplyLanguage),
                InputAdapter = new SimpleInputBindingAdapter(ExampleSettingsExtensionsProvider.BuildInputSpecs(), ApplyBinding),
                AdditionalProviders = new ISettingsPageProvider[]
                {
                    new ExampleSettingsExtensionsProvider(
                        ApplyDamageNumbers,
                        ApplyHudScale,
                        ApplyCrosshairStyle,
                        ApplyAccentTheme)
                }
            });
            SettingsKit.Init();
        }

        private void RefreshPreviewState()
        {
            _showSubtitles = SettingsKit.GetValue(SettingsKeys.GameplayShowSubtitles, true);
            _cameraSensitivity = SettingsKit.GetValue(SettingsKeys.GameplayCameraSensitivity, 1f);
            _screenShake = SettingsKit.GetValue(SettingsKeys.GameplayScreenShake, 1f);

            ApplyLanguage(SettingsKit.GetValue(SettingsKeys.LanguageCurrent, "zh-CN"));
            ApplyBinding(ExampleSettingsKeys.InputJump, SettingsKit.GetValue(ExampleSettingsKeys.InputJump, "Space"));
            ApplyBinding(ExampleSettingsKeys.InputDash, SettingsKit.GetValue(ExampleSettingsKeys.InputDash, "LeftShift"));
            ApplyHudScale(SettingsKit.GetValue(ExampleSettingsKeys.ExtensionHudScale, 1f));
            ApplyCrosshairStyle(SettingsKit.GetValue(ExampleSettingsKeys.ExtensionCrosshairStyle, "dot"));
            ApplyDamageNumbers(SettingsKit.GetValue(ExampleSettingsKeys.ExtensionDamageNumbers, true));
            ApplyAccentTheme(SettingsKit.GetValue(ExampleSettingsKeys.ExtensionAccentTheme, "stellar_blue"));
        }

        private void HandleSettingChanged(SettingEntry entry)
        {
            switch (entry.Definition.Key)
            {
                case SettingsKeys.GameplayShowSubtitles:
                    _showSubtitles = entry.GetValue<bool>();
                    break;
                case SettingsKeys.GameplayCameraSensitivity:
                    _cameraSensitivity = entry.GetValue<float>();
                    break;
                case SettingsKeys.GameplayScreenShake:
                    _screenShake = entry.GetValue<float>();
                    break;
            }
        }

        private void ApplyLanguage(string value)
        {
            _currentLanguage = string.IsNullOrEmpty(value) ? "zh-CN" : value;
            _currentLanguageLabel = _languageLabels.TryGetValue(_currentLanguage, out string label)
                ? label
                : _currentLanguage;
        }

        private void ApplyBinding(string key, string value)
        {
            if (key == ExampleSettingsKeys.InputJump)
            {
                _jumpBinding = value;
            }
            else if (key == ExampleSettingsKeys.InputDash)
            {
                _dashBinding = value;
            }
        }

        private void ApplyDamageNumbers(bool value)
        {
            _showDamageNumbers = value;
        }

        private void ApplyHudScale(float value)
        {
            _hudScale = Mathf.Clamp(value, 0.8f, 1.4f);
        }

        private void ApplyCrosshairStyle(string value)
        {
            _crosshairStyle = string.IsNullOrEmpty(value) ? "dot" : value;
        }

        private void ApplyAccentTheme(string value)
        {
            _accentTheme = string.IsNullOrEmpty(value) ? "stellar_blue" : value;
            _accentColor = ResolveAccentColor(_accentTheme);

            if (_previewRenderer != null)
            {
                Material material = _previewRenderer.material;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", _accentColor);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", _accentColor);
                }
            }

            if (_previewLight != null)
            {
                _previewLight.color = Color.Lerp(Color.white, _accentColor, 0.35f);
            }
        }

        private void UpdatePreviewObject()
        {
            if (_previewRenderer == null)
            {
                return;
            }

            Transform previewTransform = _previewRenderer.transform;
            previewTransform.Rotate(0f, 20f * _cameraSensitivity * Time.deltaTime, 0f, Space.World);
            previewTransform.position = _previewBasePosition + Vector3.up * (Mathf.Sin(Time.time * 3.5f) * 0.12f * _screenShake);

            float scale = Mathf.Lerp(0.9f, 1.35f, Mathf.InverseLerp(0.8f, 1.4f, _hudScale));
            previewTransform.localScale = Vector3.one * scale;
        }

        private void DrawStatusPanel()
        {
            Rect area = new Rect(Screen.width - 330f, 16f, 314f, 252f);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("SettingsKit 预览");
            GUILayout.Space(6f);
            GUILayout.Label($"语言: {_currentLanguageLabel}");
            GUILayout.Label($"跳跃: {_jumpBinding}");
            GUILayout.Label($"冲刺: {_dashBinding}");
            GUILayout.Label($"字幕: {FormatToggle(_showSubtitles)}");
            GUILayout.Label($"镜头灵敏度: {_cameraSensitivity:0.00}");
            GUILayout.Label($"屏幕震动: {_screenShake:0.00}");
            GUILayout.Label($"HUD 缩放: {_hudScale:0.00}");
            GUILayout.Label($"准星: {_crosshairStyle}");
            GUILayout.Label($"主题色: {_accentTheme}");
            GUILayout.Space(6f);
            GUILayout.Label("F10 显示/隐藏设置菜单");
            GUILayout.Label("F5 保存 / F9 重置全部");
            GUILayout.Label("1 播放 UI 点击音效 / 2 播放爆炸音效");

            GUILayout.EndArea();
        }

        private void DrawCrosshairPreview()
        {
            float size = 10f * _hudScale;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;

            Color oldColor = GUI.color;
            GUI.color = _accentColor;

            switch (_crosshairStyle)
            {
                case "ring":
                    DrawRect(centerX - size, centerY - 1f, size * 2f, 2f);
                    DrawRect(centerX - 1f, centerY - size, 2f, size * 2f);
                    DrawRect(centerX - size * 0.6f, centerY - size * 0.6f, 2f, 2f);
                    DrawRect(centerX + size * 0.6f - 2f, centerY - size * 0.6f, 2f, 2f);
                    DrawRect(centerX - size * 0.6f, centerY + size * 0.6f - 2f, 2f, 2f);
                    DrawRect(centerX + size * 0.6f - 2f, centerY + size * 0.6f - 2f, 2f, 2f);
                    break;

                case "angle":
                    DrawRect(centerX - size - 8f, centerY - size - 8f, 10f, 2f);
                    DrawRect(centerX - size - 8f, centerY - size - 8f, 2f, 10f);
                    DrawRect(centerX + size - 2f, centerY - size - 8f, 10f, 2f);
                    DrawRect(centerX + size + 6f, centerY - size - 8f, 2f, 10f);
                    DrawRect(centerX - size - 8f, centerY + size + 6f, 10f, 2f);
                    DrawRect(centerX - size - 8f, centerY + size - 2f, 2f, 10f);
                    DrawRect(centerX + size - 2f, centerY + size + 6f, 10f, 2f);
                    DrawRect(centerX + size + 6f, centerY + size - 2f, 2f, 10f);
                    break;

                default:
                    DrawRect(centerX - 1.5f, centerY - 1.5f, 3f, 3f);
                    DrawRect(centerX - size - 6f, centerY - 1f, 8f, 2f);
                    DrawRect(centerX + size - 2f, centerY - 1f, 8f, 2f);
                    DrawRect(centerX - 1f, centerY - size - 6f, 2f, 8f);
                    DrawRect(centerX - 1f, centerY + size - 2f, 2f, 8f);
                    break;
            }

            GUI.color = oldColor;
        }

        private void DrawSubtitlePreview()
        {
            if (!_showSubtitles)
            {
                return;
            }

            Rect area = new Rect(Screen.width * 0.5f - 260f, Screen.height - 110f, 520f, 54f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(
                new Rect(area.x + 14f, area.y + 16f, area.width - 28f, 24f),
                $"字幕预览 ({_currentLanguage})");
        }

        private void DrawDamageNumberPreview()
        {
            if (!_showDamageNumbers)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = _accentColor;
            float yOffset = Mathf.Sin(Time.time * 3f) * 6f;
            GUI.Label(
                new Rect(Screen.width * 0.5f + 18f, Screen.height * 0.5f - 52f - yOffset, 100f, 24f),
                "+128");
            GUI.color = oldColor;
        }

        private static void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }

        private static Color ResolveAccentColor(string accentTheme)
        {
            switch (accentTheme)
            {
                case "amber":
                    return new Color(0.96f, 0.68f, 0.24f);
                case "mint":
                    return new Color(0.33f, 0.84f, 0.67f);
                default:
                    return new Color(0.27f, 0.55f, 0.92f);
            }
        }

        private static IReadOnlyList<SettingChoiceOption> BuildLanguageOptions()
        {
            return new[]
            {
                new SettingChoiceOption("zh-CN", "简体中文"),
                new SettingChoiceOption("en-US", "English"),
                new SettingChoiceOption("ja-JP", "日语")
            };
        }

        private static string FormatToggle(bool value)
        {
            return value ? "开启" : "关闭";
        }
    }
}
