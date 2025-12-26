namespace StellarFramework.Audio
{
    /// <summary>
    /// 音频模块常量定义
    /// </summary>
    public class AudioDefines
    {
        // PlayerPrefs Key
        public const string PREFS_MusicVolume = "Audio_MusicVolume";
        public const string PREFS_SoundVolume = "Audio_SoundVolume";
        public const string PREFS_MusicOn = "Audio_MusicOn";
        public const string PREFS_SoundOn = "Audio_SoundOn";

        // 同屏最大音效并发数
        // 防止战斗激烈时创建几百个 AudioSource 导致 CPU 耗时过高或爆音
        public const int MAX_SOUND_VOICES = 64;
    }
}