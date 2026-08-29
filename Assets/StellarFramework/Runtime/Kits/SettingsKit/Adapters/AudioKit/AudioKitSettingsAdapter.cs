using StellarFramework.Audio;

namespace StellarFramework.Settings
{
    /// <summary>
    /// 将 SettingsKit 的音频设置接入可选的 AudioKit。
    /// </summary>
    public sealed class AudioKitSettingsAdapter : IAudioSettingsAdapter
    {
        public float MusicVolume
        {
            get => AudioKit.MusicVolume;
            set => AudioKit.MusicVolume = value;
        }

        public float SoundVolume
        {
            get => AudioKit.SoundVolume;
            set => AudioKit.SoundVolume = value;
        }

        public bool MusicOn
        {
            get => AudioKit.MusicOn;
            set => AudioKit.MusicOn = value;
        }

        public bool SoundOn
        {
            get => AudioKit.SoundOn;
            set => AudioKit.SoundOn = value;
        }
    }
}
