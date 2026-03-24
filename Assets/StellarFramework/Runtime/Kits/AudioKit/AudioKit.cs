using UnityEngine;
using UnityEngine.Audio;
using StellarFramework.Res;

namespace StellarFramework.Audio
{
    /// <summary>
    /// AudioKit 静态门面
    /// 提供极其简单的 API 供业务层调用
    /// </summary>
    public static class AudioKit
    {
        /// <summary>
        /// 初始化音频系统 (使用默认的 ResKit 加载策略)
        /// 必须在游戏启动时调用，注入 AudioMixer 以启用硬件级混音
        /// </summary>
        /// <typeparam name="TLoader">指定的 ResLoader 类型 (如 ResourceLoader, AddressableLoader)</typeparam>
        /// <param name="mixer">配置好的混音器</param>
        public static void Init<TLoader>(AudioMixer mixer) where TLoader : ResLoader, new()
        {
            if (mixer == null)
            {
                Debug.LogError("[AudioKit] 初始化失败: 传入的 AudioMixer 为空");
                return;
            }

            IAudioLoader defaultLoader = new DefaultResKitAudioLoader<TLoader>();
            AudioManager.Instance.Init(mixer, defaultLoader);
        }

        /// <summary>
        /// 初始化音频系统 (使用自定义的加载策略)
        /// </summary>
        /// <param name="mixer">配置好的混音器</param>
        /// <param name="customLoader">自定义的音频资源加载器 (如 YooAssetLoader)</param>
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

        // --- BGM ---
        public static void PlayMusic(string path, float fadeDuration = 0.5f)
        {
            AudioManager.Instance.PlayMusic(path, fadeDuration);
        }

        public static void StopMusic()
        {
            AudioManager.Instance.StopMusic();
        }

        // --- SFX (2D) ---
        public static void PlaySound(string path, SoundPriority priority = SoundPriority.Normal)
        {
            AudioManager.Instance.PlaySoundInternal(path, Vector3.zero, null, false, priority);
        }

        // --- SFX (3D) ---
        public static void PlaySound3D(string path, Vector3 position, SoundPriority priority = SoundPriority.Normal)
        {
            AudioManager.Instance.PlaySoundInternal(path, position, null, true, priority);
        }

        public static void PlaySound3D(string path, Transform target, SoundPriority priority = SoundPriority.Normal)
        {
            if (target == null)
            {
                Debug.LogError("[AudioKit] PlaySound3D 失败: 跟随目标 target 为空");
                return;
            }

            AudioManager.Instance.PlaySoundInternal(path, target.position, target, true, priority);
        }

        // --- Settings ---
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(AudioDefines.PREFS_MusicVolume, 1.0f);
            set => AudioManager.Instance.SetMusicVolume(value);
        }

        public static float SoundVolume
        {
            get => PlayerPrefs.GetFloat(AudioDefines.PREFS_SoundVolume, 1.0f);
            set => AudioManager.Instance.SetSoundVolume(value);
        }

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(AudioDefines.PREFS_MusicOn, 1) == 1;
            set => AudioManager.Instance.SetMusicOn(value);
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(AudioDefines.PREFS_SoundOn, 1) == 1;
            set => AudioManager.Instance.SetSoundOn(value);
        }
    }
}