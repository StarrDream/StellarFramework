namespace StellarFramework.Audio
{
    /// <summary>
    /// 音频模块常量定义
    /// </summary>
    public static class AudioDefines
    {
        // PlayerPrefs Key
        public const string PREFS_MusicVolume = "Audio_MusicVolume";
        public const string PREFS_SoundVolume = "Audio_SoundVolume";
        public const string PREFS_MusicOn = "Audio_MusicOn";
        public const string PREFS_SoundOn = "Audio_SoundOn";

        // AudioMixer 暴露的参数名称 (需在编辑器中将对应组的 Volume 暴露并重命名)
        public const string MIXER_PARAM_BGM_VOLUME = "BGMVolume";
        public const string MIXER_PARAM_SFX_VOLUME = "SFXVolume";

        // AudioMixer 组名称
        public const string MIXER_GROUP_BGM = "BGM";
        public const string MIXER_GROUP_SFX = "SFX";

        // 同屏最大音效并发数
        // 防止战斗激烈时创建几百个 AudioSource 导致 CPU 耗时过高或爆音
        public const int MAX_SOUND_VOICES = 64;
    }
}