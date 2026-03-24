using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Cysharp.Threading.Tasks;
using StellarFramework.Pool;
using System.Threading;

namespace StellarFramework.Audio
{
    /// <summary>
    /// 音效优先级
    /// </summary>
    public enum SoundPriority
    {
        Low = 0, // 环境音、脚步声、远处的爆炸
        Normal = 1, // 普通攻击、UI点击
        High = 2, // 技能释放、受击、关键提示音
        Critical = 3 // Boss技能、剧情对白、玩家死亡
    }

    [RequireComponent(typeof(AudioListener))]
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private IAudioLoader _audioLoader; // 策略接口

        private AudioMixer _mixer;
        private AudioMixerGroup _bgmGroup;
        private AudioMixerGroup _sfxGroup;

        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private bool _isUsingSourceA = true;
        private string _currentBgmPath;

        private FactoryObjectPool<AudioSource> _sfxPool;

        private class ActiveSoundInfo
        {
            public AudioSource Source;
            public Transform FollowTarget;
            public SoundPriority Priority;
        }

        private readonly List<ActiveSoundInfo> _activeSounds = new List<ActiveSoundInfo>(AudioDefines.MAX_SOUND_VOICES);
        private readonly CancellationTokenSource _managerCTS = new CancellationTokenSource();

        private float _musicVolume = 1.0f;
        private float _soundVolume = 1.0f;
        private bool _isMusicOn = true;
        private bool _isSoundOn = true;

        public void Init(AudioMixer mixer, IAudioLoader loader)
        {
            if (mixer == null)
            {
                Debug.LogError("[AudioManager] 初始化失败: Mixer 为空");
                return;
            }

            if (loader == null)
            {
                Debug.LogError("[AudioManager] 初始化失败: IAudioLoader 为空");
                return;
            }

            _mixer = mixer;
            _audioLoader = loader;

            var bgmGroups = _mixer.FindMatchingGroups(AudioDefines.MIXER_GROUP_BGM);
            var sfxGroups = _mixer.FindMatchingGroups(AudioDefines.MIXER_GROUP_SFX);

            if (bgmGroups.Length == 0 || sfxGroups.Length == 0)
            {
                Debug.LogError("[AudioManager] 初始化失败: AudioMixer 中缺少 BGM 或 SFX 组配置");
                return;
            }

            _bgmGroup = bgmGroups[0];
            _sfxGroup = sfxGroups[0];

            InitializeBGM();
            InitializeSFXPool();
            LoadSettings();
        }

        private void Update()
        {
            // 倒序遍历，安全移除无效项并执行 Update 轮询回收
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                var info = _activeSounds[i];
                if (info.Source == null)
                {
                    _activeSounds.RemoveAt(i);
                    continue;
                }

                // 核心优化：通过 isPlaying 判定回收，废弃容易引发状态混乱的 UniTask.Delay
                if (!info.Source.isPlaying)
                {
                    _sfxPool.Recycle(info.Source);
                    _activeSounds.RemoveAt(i);
                    continue;
                }

                // 跟随逻辑
                if (info.FollowTarget != null)
                {
                    info.Source.transform.position = info.FollowTarget.position;
                }
            }
        }

        protected override void OnDestroy()
        {
            _managerCTS.Cancel();
            _managerCTS.Dispose();

            if (_audioLoader != null)
            {
                _audioLoader.Release();
                _audioLoader = null;
            }

            base.OnDestroy();
        }

        private void InitializeBGM()
        {
            _bgmSourceA = CreateSource("BGM_Track_A", _bgmGroup);
            _bgmSourceB = CreateSource("BGM_Track_B", _bgmGroup);
            _bgmSourceA.loop = true;
            _bgmSourceB.loop = true;
            _bgmSourceA.spatialBlend = 0f;
            _bgmSourceB.spatialBlend = 0f;
        }

        private void InitializeSFXPool()
        {
            _sfxPool = new FactoryObjectPool<AudioSource>(
                factoryMethod: () => CreateSource("SFX_Pool_Item", _sfxGroup),
                allocateMethod: source => { source.gameObject.SetActive(true); },
                recycleMethod: source =>
                {
                    source.Stop();
                    source.clip = null;
                    source.transform.SetParent(transform);
                    source.transform.localPosition = Vector3.zero;
                    source.spatialBlend = 0f;
                    source.volume = 1f;
                    source.pitch = 1f;
                    source.gameObject.SetActive(false);
                },
                destroyMethod: source =>
                {
                    if (source != null) Destroy(source.gameObject);
                },
                maxCount: AudioDefines.MAX_SOUND_VOICES
            );
        }

        private AudioSource CreateSource(string name, AudioMixerGroup group)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            return source;
        }

        // ================= SFX 核心逻辑 =================

        public void PlaySoundInternal(string path, Vector3 position, Transform attachTarget, bool is3D,
            SoundPriority priority)
        {
            if (!_isSoundOn || string.IsNullOrEmpty(path)) return;

            if (_activeSounds.Count >= AudioDefines.MAX_SOUND_VOICES)
            {
                if (!TryEvictLowPrioritySound(priority))
                {
                    Debug.LogWarning($"[AudioManager] 音效池已满且无法剔除，丢弃: {path} (Priority: {priority})");
                    return;
                }
            }

            PlaySoundAsync(path, position, attachTarget, is3D, priority).Forget();
        }

        private bool TryEvictLowPrioritySound(SoundPriority newPriority)
        {
            ActiveSoundInfo candidate = null;
            SoundPriority minPriority = SoundPriority.Critical;

            foreach (var info in _activeSounds)
            {
                if (info.Priority < minPriority)
                {
                    minPriority = info.Priority;
                    candidate = info;
                }
            }

            if (candidate != null && minPriority < newPriority)
            {
                if (candidate.Source != null)
                {
                    candidate.Source.Stop();
                    _sfxPool.Recycle(candidate.Source);
                }

                _activeSounds.Remove(candidate);
                return true;
            }

            return false;
        }

        private async UniTaskVoid PlaySoundAsync(string path, Vector3 position, Transform attachTarget, bool is3D,
            SoundPriority priority)
        {
            // 通过策略接口加载资源
            var clip = await _audioLoader.LoadAudioAsync(path, _managerCTS.Token);

            if (this == null || !_isSoundOn || clip == null) return;

            var source = _sfxPool.Allocate();
            var info = new ActiveSoundInfo
            {
                Source = source,
                FollowTarget = attachTarget,
                Priority = priority
            };

            _activeSounds.Add(info);

            source.clip = clip;
            source.spatialBlend = is3D ? 1.0f : 0.0f;

            if (is3D)
            {
                source.minDistance = 1.0f;
                source.maxDistance = 20.0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.transform.position = attachTarget != null ? attachTarget.position : position;
            }

            source.Play();
        }

        // ================= BGM 逻辑 =================

        public async void PlayMusic(string path, float fadeDuration)
        {
            if (string.IsNullOrEmpty(path) || _currentBgmPath == path) return;

            _currentBgmPath = path;

            // 通过策略接口加载资源
            var clip = await _audioLoader.LoadAudioAsync(path, _managerCTS.Token);

            if (clip == null || this == null) return;

            var targetSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;
            var currentSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;

            targetSource.clip = clip;
            targetSource.volume = 0;
            targetSource.Play();

            float timer = 0f;
            while (timer < fadeDuration)
            {
                if (this == null) return;
                timer += Time.unscaledDeltaTime;
                float t = timer / fadeDuration;

                targetSource.volume = Mathf.Lerp(0, 1f, t);
                if (currentSource.isPlaying) currentSource.volume = Mathf.Lerp(1f, 0, t);

                await UniTask.Yield(PlayerLoopTiming.Update, _managerCTS.Token);
            }

            targetSource.volume = 1f;
            currentSource.Stop();
            _isUsingSourceA = !_isUsingSourceA;
        }

        public void StopMusic()
        {
            _currentBgmPath = null;
            if (_bgmSourceA) _bgmSourceA.Stop();
            if (_bgmSourceB) _bgmSourceB.Stop();
        }

        // ================= Settings 逻辑 (接入 AudioMixer) =================

        private void LoadSettings()
        {
            _musicVolume = PlayerPrefs.GetFloat(AudioDefines.PREFS_MusicVolume, 1.0f);
            _soundVolume = PlayerPrefs.GetFloat(AudioDefines.PREFS_SoundVolume, 1.0f);
            _isMusicOn = PlayerPrefs.GetInt(AudioDefines.PREFS_MusicOn, 1) == 1;
            _isSoundOn = PlayerPrefs.GetInt(AudioDefines.PREFS_SoundOn, 1) == 1;

            ApplyMixerVolume(AudioDefines.MIXER_PARAM_BGM_VOLUME, _isMusicOn ? _musicVolume : 0f);
            ApplyMixerVolume(AudioDefines.MIXER_PARAM_SFX_VOLUME, _isSoundOn ? _soundVolume : 0f);
        }

        public void SetMusicVolume(float v)
        {
            _musicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(AudioDefines.PREFS_MusicVolume, _musicVolume);
            ApplyMixerVolume(AudioDefines.MIXER_PARAM_BGM_VOLUME, _isMusicOn ? _musicVolume : 0f);
        }

        public void SetSoundVolume(float v)
        {
            _soundVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(AudioDefines.PREFS_SoundVolume, _soundVolume);
            ApplyMixerVolume(AudioDefines.MIXER_PARAM_SFX_VOLUME, _isSoundOn ? _soundVolume : 0f);
        }

        public void SetMusicOn(bool isOn)
        {
            _isMusicOn = isOn;
            PlayerPrefs.SetInt(AudioDefines.PREFS_MusicOn, isOn ? 1 : 0);
            ApplyMixerVolume(AudioDefines.MIXER_PARAM_BGM_VOLUME, _isMusicOn ? _musicVolume : 0f);
        }

        public void SetSoundOn(bool isOn)
        {
            _isSoundOn = isOn;
            PlayerPrefs.SetInt(AudioDefines.PREFS_SoundOn, isOn ? 1 : 0);
            ApplyMixerVolume(AudioDefines.MIXER_PARAM_SFX_VOLUME, _isSoundOn ? _soundVolume : 0f);

            if (!isOn)
            {
                foreach (var info in _activeSounds)
                {
                    if (info.Source != null) info.Source.Stop();
                }
            }
        }

        /// <summary>
        /// 将 0~1 的线性音量转换为 AudioMixer 的对数分贝值 (-80dB ~ 0dB)
        /// </summary>
        private void ApplyMixerVolume(string paramName, float linearVolume)
        {
            if (_mixer == null) return;

            float dbVolume = linearVolume <= 0.0001f ? -80f : Mathf.Log10(linearVolume) * 20f;
            _mixer.SetFloat(paramName, dbVolume);
        }
    }
}