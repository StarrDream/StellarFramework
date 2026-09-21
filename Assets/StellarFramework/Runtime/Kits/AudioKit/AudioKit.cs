using UnityEngine;
using UnityEngine.Audio;

namespace StellarFramework.Audio
{
    /// <summary>
    /// AudioKit 静态门面。
    /// 负责向业务层提供 BGM、2D/3D SFX 和音量开关入口，资源加载由 IAudioLoader 隔离。
    /// </summary>
    public static class AudioKit
    {
        /// <summary>
        /// 初始化音频系统，使用 Unity Resources 默认加载策略。
        /// 应在首次播放音频前调用。
        /// </summary>
        /// <param name="mixer">配置好的混音器</param>
        public static void Init(AudioMixer mixer)
        {
            if (mixer == null)
            {
                Debug.LogError("[AudioKit] 初始化失败: 传入的 AudioMixer 为空");
                return;
            }

            AudioManager.Instance.Init(mixer, new ResourcesAudioLoader());
        }

        /// <summary>
        /// 初始化音频系统并注入自定义资源加载策略。
        /// </summary>
        /// <param name="mixer">配置好的混音器</param>
        /// <param name="customLoader">自定义音频资源加载器，例如 ResKit/Addressables/YooAsset Adapter。</param>
        public static void Init(AudioMixer mixer, IAudioLoader customLoader)
        {
            if (mixer == null)
            {
                Debug.LogError("[AudioKit] 初始化失败: 传入的 AudioMixer 为空");
                return;
            }

            if (customLoader == null)
            {
                Debug.LogError("[AudioKit] 初始化失败: 传入的 customLoader 为空");
                return;
            }

            AudioManager.Instance.Init(mixer, customLoader);
        }

        /// <summary>
        /// 播放 BGM。若已有音乐正在播放，由 AudioManager 按 fadeDuration 处理切换。
        /// </summary>
        public static void PlayMusic(string path, float fadeDuration = 0.5f)
        {
            AudioManager.Instance.PlayMusic(path, fadeDuration);
        }

        /// <summary>停止当前 BGM。</summary>
        public static void StopMusic()
        {
            AudioManager.Instance.StopMusic();
        }

        /// <summary>播放不带空间衰减的 2D 音效。</summary>
        public static void PlaySound(string path, SoundPriority priority = SoundPriority.Normal)
        {
            AudioManager.Instance.PlaySoundInternal(path, Vector3.zero, null, false, priority);
        }

        /// <summary>在固定世界坐标播放 3D 音效。</summary>
        public static void PlaySound3D(string path, Vector3 position, SoundPriority priority = SoundPriority.Normal)
        {
            AudioManager.Instance.PlaySoundInternal(path, position, null, true, priority);
        }

        /// <summary>
        /// 播放跟随指定 Transform 的 3D 音效。
        /// </summary>
        public static void PlaySound3D(string path, Transform target, SoundPriority priority = SoundPriority.Normal)
        {
            if (target == null)
            {
                Debug.LogError("[AudioKit] PlaySound3D 失败: 跟随目标 target 为空");
                return;
            }

            AudioManager.Instance.PlaySoundInternal(path, target.position, target, true, priority);
        }

        /// <summary>BGM 音量，范围由 AudioManager/Mixer 实现约束并写入 PlayerPrefs。</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(AudioDefines.PREFS_MusicVolume, 1.0f);
            set => AudioManager.Instance.SetMusicVolume(value);
        }

        /// <summary>SFX 音量。</summary>
        public static float SoundVolume
        {
            get => PlayerPrefs.GetFloat(AudioDefines.PREFS_SoundVolume, 1.0f);
            set => AudioManager.Instance.SetSoundVolume(value);
        }

        /// <summary>BGM 总开关。</summary>
        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(AudioDefines.PREFS_MusicOn, 1) == 1;
            set => AudioManager.Instance.SetMusicOn(value);
        }

        /// <summary>SFX 总开关。</summary>
        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(AudioDefines.PREFS_SoundOn, 1) == 1;
            set => AudioManager.Instance.SetSoundOn(value);
        }
    }
}
