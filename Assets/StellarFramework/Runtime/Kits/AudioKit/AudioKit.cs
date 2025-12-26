using UnityEngine;

namespace StellarFramework.Audio
{
    /// <summary>
    /// AudioKit 静态门面
    /// 提供极其简单的 API 供业务层调用
    /// </summary>
    public static class AudioKit
    {
        // --- BGM ---

        /// <summary>
        /// 播放背景音乐 (自动淡入淡出)
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="fadeDuration">淡入淡出时间(秒)</param>
        public static void PlayMusic(string path, float fadeDuration = 0.5f)
        {
            AudioManager.Instance.PlayMusic(path, fadeDuration);
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        public static void StopMusic()
        {
            AudioManager.Instance.StopMusic();
        }

        // --- SFX (2D) ---

        /// <summary>
        /// 播放 2D 音效 (UI, 全局提示音)
        /// </summary>
        public static void PlaySound(string path)
        {
            AudioManager.Instance.PlaySoundInternal(path, Vector3.zero, null, false);
        }

        // --- SFX (3D) ---

        /// <summary>
        /// 播放 3D 音效 (在指定位置播放)
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="position">世界坐标</param>
        public static void PlaySound3D(string path, Vector3 position)
        {
            AudioManager.Instance.PlaySoundInternal(path, position, null, true);
        }

        /// <summary>
        /// 播放 3D 音效 (跟随物体，如脚步声)
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="target">跟随的目标 Transform</param>
        public static void PlaySound3D(string path, Transform target)
        {
            if (target == null) return;
            // 注意：目前底层简化为在 target 当前位置播放，未实现持续跟随 Update
            AudioManager.Instance.PlaySoundInternal(path, target.position, target, true);
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