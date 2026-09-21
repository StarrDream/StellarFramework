namespace StellarFramework.Audio
{
    /// <summary>
    /// 音频模块常量定义
    /// </summary>
    public static class AudioDefines
    {
        /// <summary>PlayerPrefs: BGM volume.</summary>
        public const string PREFS_MusicVolume = "Audio_MusicVolume";
        /// <summary>PlayerPrefs: SFX volume.</summary>
        public const string PREFS_SoundVolume = "Audio_SoundVolume";
        /// <summary>PlayerPrefs: BGM enabled.</summary>
        public const string PREFS_MusicOn = "Audio_MusicOn";
        /// <summary>PlayerPrefs: SFX enabled.</summary>
        public const string PREFS_SoundOn = "Audio_SoundOn";

        /// <summary>AudioMixer 暴露的 BGM 音量参数名。</summary>
        public const string MIXER_PARAM_BGM_VOLUME = "BGMVolume";
        /// <summary>AudioMixer 暴露的 SFX 音量参数名。</summary>
        public const string MIXER_PARAM_SFX_VOLUME = "SFXVolume";

        /// <summary>默认 BGM MixerGroup 名称。</summary>
        public const string MIXER_GROUP_BGM = "BGM";
        /// <summary>默认 SFX MixerGroup 名称。</summary>
        public const string MIXER_GROUP_SFX = "SFX";

        /// <summary>
        /// 同时存活的最大 SFX Voice 数，用于限制 AudioSource 爆发和混音开销。
        /// </summary>
        public const int MAX_SOUND_VOICES = 64;
    }
}