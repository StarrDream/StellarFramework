using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using StellarFramework.Audio;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// AudioKit 的 Hub 入口模块
    /// 职责: 将 AudioKit 注册到 StellarFramework Tools Hub，提供混音器的自动化诊断与配置引导
    /// </summary>
    [StellarTool("AudioKit 音频中心", "框架核心", 5)]
    public class AudioKitHubModule : ToolModule
    {
        public override string Icon => "d_AudioMixerController Icon";
        public override string Description => "AudioKit 混音器配置与诊断工具。一键检测并引导创建符合规范的 AudioMixer。";

        private AudioMixer _targetMixer;
        private bool _hasBGMGroup;
        private bool _hasSFXGroup;
        private bool _hasBGMParam;
        private bool _hasSFXParam;

        private const string PREFS_MIXER_PATH = "Stellar_AudioKit_Editor_MixerPath";

        public override void OnEnable()
        {
            // 尝试从本地缓存加载上次使用的 Mixer
            string path = EditorPrefs.GetString(PREFS_MIXER_PATH, "");
            if (!string.IsNullOrEmpty(path))
            {
                _targetMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            }

            // 若缓存失效，自动全工程扫描
            if (_targetMixer == null)
            {
                AutoFindMixer(silent: true);
            }

            ValidateMixer();
        }

        public override void OnGUI()
        {
            Section("混音器配置 (AudioMixer)");

            EditorGUI.BeginChangeCheck();
            _targetMixer = (AudioMixer)EditorGUILayout.ObjectField("主混音器", _targetMixer, typeof(AudioMixer), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (_targetMixer != null)
                {
                    EditorPrefs.SetString(PREFS_MIXER_PATH, AssetDatabase.GetAssetPath(_targetMixer));
                }
                else
                {
                    EditorPrefs.DeleteKey(PREFS_MIXER_PATH);
                }

                ValidateMixer();
            }

            if (_targetMixer == null)
            {
                EditorGUILayout.HelpBox("未分配 AudioMixer。AudioKit 需要一个配置好的混音器才能正常工作。", MessageType.Warning);
                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton("自动查找工程中的 Mixer", GUILayout.Height(30)))
                    {
                        AutoFindMixer(silent: false);
                    }

                    if (GUILayout.Button("新建 AudioMixer", GUILayout.Height(30)))
                    {
                        CreateNewMixer();
                    }
                }

                return;
            }

            Section("规范诊断结果");
            DrawDiagnostic($"BGM 混音组 (Group: {AudioDefines.MIXER_GROUP_BGM})", _hasBGMGroup, "缺少对应名称的 Group");
            DrawDiagnostic($"SFX 混音组 (Group: {AudioDefines.MIXER_GROUP_SFX})", _hasSFXGroup, "缺少对应名称的 Group");
            DrawDiagnostic($"BGM 暴露参数 ({AudioDefines.MIXER_PARAM_BGM_VOLUME})", _hasBGMParam, "未暴露该音量参数");
            DrawDiagnostic($"SFX 暴露参数 ({AudioDefines.MIXER_PARAM_SFX_VOLUME})", _hasSFXParam, "未暴露该音量参数");

            bool allGood = _hasBGMGroup && _hasSFXGroup && _hasBGMParam && _hasSFXParam;

            if (allGood)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("当前 AudioMixer 配置完美，符合 AudioKit 运行规范！", MessageType.Info);
            }
            else
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("混音器配置不完整，请按照以下步骤手动修复：\n\n" +
                                        $"1. 双击打开该 AudioMixer。\n" +
                                        $"2. 在 Groups 面板中，添加两个子组，分别命名为 '{AudioDefines.MIXER_GROUP_BGM}' 和 '{AudioDefines.MIXER_GROUP_SFX}'。\n" +
                                        $"3. 选中 {AudioDefines.MIXER_GROUP_BGM} 组，在 Inspector 中右键 'Volume' 属性，选择 'Expose Volume to script'。\n" +
                                        $"4. 在右上角 Exposed Parameters 面板中，将刚暴露的参数重命名为 '{AudioDefines.MIXER_PARAM_BGM_VOLUME}'。\n" +
                                        $"5. 对 {AudioDefines.MIXER_GROUP_SFX} 组重复步骤 3 和 4，重命名为 '{AudioDefines.MIXER_PARAM_SFX_VOLUME}'。",
                    MessageType.Error);

                if (DangerButton("我已修复，重新验证", GUILayout.Height(30)))
                {
                    ValidateMixer();
                }
            }
        }

        private void ValidateMixer()
        {
            _hasBGMGroup = false;
            _hasSFXGroup = false;
            _hasBGMParam = false;
            _hasSFXParam = false;

            if (_targetMixer == null) return;

            // 验证 Group
            var bgmGroups = _targetMixer.FindMatchingGroups(AudioDefines.MIXER_GROUP_BGM);
            if (bgmGroups != null && bgmGroups.Length > 0 && bgmGroups[0].name == AudioDefines.MIXER_GROUP_BGM)
                _hasBGMGroup = true;

            var sfxGroups = _targetMixer.FindMatchingGroups(AudioDefines.MIXER_GROUP_SFX);
            if (sfxGroups != null && sfxGroups.Length > 0 && sfxGroups[0].name == AudioDefines.MIXER_GROUP_SFX)
                _hasSFXGroup = true;

            // 验证暴露参数 (通过尝试 GetFloat 判定)
            if (_targetMixer.GetFloat(AudioDefines.MIXER_PARAM_BGM_VOLUME, out _))
                _hasBGMParam = true;

            if (_targetMixer.GetFloat(AudioDefines.MIXER_PARAM_SFX_VOLUME, out _))
                _hasSFXParam = true;
        }

        private void DrawDiagnostic(string label, bool isValid, string errorMsg)
        {
            using (new GUILayout.HorizontalScope())
            {
                // 采用 Hub 统一的样式风格
                GUILayout.Label(isValid ? "[通过]" : "[缺失]", isValid ? EditorStyles.boldLabel : Window.DangerButtonStyle,
                    GUILayout.Width(50));
                GUILayout.Label(label, GUILayout.Width(220));
                if (!isValid)
                {
                    GUILayout.Label(errorMsg, EditorStyles.miniLabel);
                }
            }

            GUILayout.Space(4);
        }

        private void AutoFindMixer(bool silent)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioMixer");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _targetMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                EditorPrefs.SetString(PREFS_MIXER_PATH, path);
                ValidateMixer();

                if (!silent && Window != null)
                    Window.ShowNotification(new GUIContent("已自动找到混音器"));
            }
            else
            {
                if (!silent && Window != null)
                    Window.ShowNotification(new GUIContent("工程中未找到任何 AudioMixer"));
            }
        }

        private void CreateNewMixer()
        {
            // 调用 Unity 原生菜单创建 Mixer，避免使用脆弱的内部反射 API
            EditorApplication.ExecuteMenuItem("Assets/Create/Audio Mixer");
            if (Window != null)
            {
                Window.ShowNotification(new GUIContent("请为新创建的 Mixer 命名，然后将其拖入上方的槽位中"));
            }
        }
    }
}