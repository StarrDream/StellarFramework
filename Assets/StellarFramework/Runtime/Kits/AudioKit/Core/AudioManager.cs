using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Pool;
using UnityEngine;
using UnityEngine.Audio;

namespace StellarFramework.Audio
{
    /// <summary>
    /// 音效优先级
    /// </summary>
    public enum SoundPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    [RequireComponent(typeof(AudioListener))]
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private IAudioLoader _audioLoader;
        private AudioMixer _mixer;
        private AudioMixerGroup _bgmGroup;
        private AudioMixerGroup _sfxGroup;

        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private bool _isUsingSourceA = true;
        private string _currentBgmPath;
        private string _requestedBgmPath;

        private FactoryObjectPool<AudioSource> _sfxPool;

        private sealed class ActiveSoundInfo
        {
            public AudioSource Source;
            public Transform FollowTarget;
            public SoundPriority Priority;
        }

        private readonly List<ActiveSoundInfo> _activeSounds = new List<ActiveSoundInfo>(AudioDefines.MAX_SOUND_VOICES);
        private readonly CancellationTokenSource _managerCTS = new CancellationTokenSource();

        private CancellationTokenSource _bgmSwitchCTS;
        private bool _isInitialized;

        private float _musicVolume = 1.0f;
        private float _soundVolume = 1.0f;
        private bool _isMusicOn = true;
        private bool _isSoundOn = true;

        public void Init(AudioMixer mixer, IAudioLoader loader)
        {
            if (mixer == null)
            {
                Debug.LogError($"[AudioManager] 初始化失败: mixer 为空, TriggerObject={gameObject.name}");
                return;
            }

            if (loader == null)
            {
                Debug.LogError($"[AudioManager] 初始化失败: loader 为空, TriggerObject={gameObject.name}");
                return;
            }

            if (_isInitialized)
            {
                if (ReferenceEquals(_mixer, mixer) && ReferenceEquals(_audioLoader, loader))
                {
                    Debug.LogWarning(
                        $"[AudioManager] 检测到重复初始化，已忽略, TriggerObject={gameObject.name}, Mixer={mixer.name}");
                    return;
                }

                ShutdownRuntimeState();
            }

            _mixer = mixer;
            _audioLoader = loader;

            AudioMixerGroup[] bgmGroups = _mixer.FindMatchingGroups(AudioDefines.MIXER_GROUP_BGM);
            AudioMixerGroup[] sfxGroups = _mixer.FindMatchingGroups(AudioDefines.MIXER_GROUP_SFX);

            if (bgmGroups == null || bgmGroups.Length == 0 || sfxGroups == null || sfxGroups.Length == 0)
            {
                Debug.LogError(
                    $"[AudioManager] 初始化失败: AudioMixer 缺少 BGM 或 SFX Group, TriggerObject={gameObject.name}, Mixer={mixer.name}");
                return;
            }

            _bgmGroup = bgmGroups[0];
            _sfxGroup = sfxGroups[0];

            InitializeBGM();
            InitializeSFXPool();
            LoadSettings();

            _isInitialized = true;
            Debug.Log($"[AudioManager] 初始化完成, Mixer={mixer.name}, TriggerObject={gameObject.name}");
        }

        private void Update()
        {
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                ActiveSoundInfo info = _activeSounds[i];
                if (info == null || info.Source == null)
                {
                    _activeSounds.RemoveAt(i);
                    continue;
                }

                if (!info.Source.isPlaying)
                {
                    _sfxPool?.Recycle(info.Source);
                    _activeSounds.RemoveAt(i);
                    continue;
                }

                if (info.FollowTarget != null)
                {
                    info.Source.transform.position = info.FollowTarget.position;
                }
            }
        }

        protected override void OnDestroy()
        {
            ShutdownRuntimeState();

            if (!_managerCTS.IsCancellationRequested)
            {
                _managerCTS.Cancel();
            }

            _managerCTS.Dispose();
            base.OnDestroy();
        }

        private void ShutdownRuntimeState()
        {
            StopMusic();
            CancelBgmSwitch();

            if (_audioLoader != null)
            {
                _audioLoader.Release();
                _audioLoader = null;
            }

            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                ActiveSoundInfo info = _activeSounds[i];
                if (info?.Source != null)
                {
                    info.Source.Stop();
                    _sfxPool?.Recycle(info.Source);
                }
            }

            _activeSounds.Clear();

            if (_bgmSourceA != null)
            {
                _bgmSourceA.Stop();
            }

            if (_bgmSourceB != null)
            {
                _bgmSourceB.Stop();
            }

            _currentBgmPath = null;
            _requestedBgmPath = null;
            _isInitialized = false;
        }

        private void InitializeBGM()
        {
            if (_bgmSourceA == null)
            {
                _bgmSourceA = CreateSource("BGM_Track_A", _bgmGroup);
            }

            if (_bgmSourceB == null)
            {
                _bgmSourceB = CreateSource("BGM_Track_B", _bgmGroup);
            }

            _bgmSourceA.outputAudioMixerGroup = _bgmGroup;
            _bgmSourceB.outputAudioMixerGroup = _bgmGroup;
            _bgmSourceA.loop = true;
            _bgmSourceB.loop = true;
            _bgmSourceA.spatialBlend = 0f;
            _bgmSourceB.spatialBlend = 0f;
        }

        private void InitializeSFXPool()
        {
            _sfxPool = new FactoryObjectPool<AudioSource>(
                factoryMethod: () => CreateSource("SFX_Pool_Item", _sfxGroup),
                allocateMethod: source =>
                {
                    if (source != null)
                    {
                        source.outputAudioMixerGroup = _sfxGroup;
                        source.gameObject.SetActive(true);
                    }
                },
                recycleMethod: source =>
                {
                    if (source == null)
                    {
                        return;
                    }

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
                    if (source != null)
                    {
                        Destroy(source.gameObject);
                    }
                },
                maxCount: AudioDefines.MAX_SOUND_VOICES
            );
        }

        private AudioSource CreateSource(string name, AudioMixerGroup group)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            return source;
        }

        // ================= SFX =================

        public void PlaySoundInternal(string path, Vector3 position, Transform attachTarget, bool is3D,
            SoundPriority priority)
        {
            if (!_isInitialized)
            {
                Debug.LogError($"[AudioManager] 播放音效失败: 系统未初始化, Path={path}, TriggerObject={gameObject.name}");
                return;
            }

            if (!_isSoundOn || string.IsNullOrEmpty(path))
            {
                return;
            }

            if (_activeSounds.Count >= AudioDefines.MAX_SOUND_VOICES)
            {
                if (!TryEvictLowPrioritySound(priority))
                {
                    Debug.LogWarning(
                        $"[AudioManager] 音效池已满且无法剔除，丢弃音效, Path={path}, Priority={priority}, ActiveCount={_activeSounds.Count}");
                    return;
                }
            }

            PlaySoundAsync(path, position, attachTarget, is3D, priority).Forget();
        }

        private bool TryEvictLowPrioritySound(SoundPriority newPriority)
        {
            ActiveSoundInfo candidate = null;
            SoundPriority minPriority = SoundPriority.Critical;

            for (int i = 0; i < _activeSounds.Count; i++)
            {
                ActiveSoundInfo info = _activeSounds[i];
                if (info == null)
                {
                    continue;
                }

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
                    _sfxPool?.Recycle(candidate.Source);
                }

                _activeSounds.Remove(candidate);
                return true;
            }

            return false;
        }

        private async UniTaskVoid PlaySoundAsync(string path, Vector3 position, Transform attachTarget, bool is3D,
            SoundPriority priority)
        {
            try
            {
                AudioClip clip = await _audioLoader.LoadAudioAsync(path, _managerCTS.Token);
                if (this == null || !_isInitialized || !_isSoundOn || clip == null)
                {
                    return;
                }

                AudioSource source = _sfxPool.Allocate();
                if (source == null)
                {
                    Debug.LogError(
                        $"[AudioManager] 播放音效失败: 从对象池分配 AudioSource 为空, Path={path}, TriggerObject={gameObject.name}");
                    return;
                }

                ActiveSoundInfo info = new ActiveSoundInfo
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
            catch (OperationCanceledException) when (_managerCTS.IsCancellationRequested)
            {
            }
        }

        // ================= BGM =================

        public void PlayMusic(string path, float fadeDuration)
        {
            if (!_isInitialized)
            {
                Debug.LogError($"[AudioManager] 播放 BGM 失败: 系统未初始化, Path={path}, TriggerObject={gameObject.name}");
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[AudioManager] 播放 BGM 失败: path 为空, TriggerObject={gameObject.name}");
                return;
            }

            if (_currentBgmPath == path || _requestedBgmPath == path)
            {
                return;
            }

            CancelBgmSwitch();

            _requestedBgmPath = path;
            CancellationTokenSource switchCts = CancellationTokenSource.CreateLinkedTokenSource(_managerCTS.Token);
            _bgmSwitchCTS = switchCts;
            PlayMusicAsync(path, Mathf.Max(0f, fadeDuration), switchCts).Forget();
        }

        private async UniTaskVoid PlayMusicAsync(string path, float fadeDuration, CancellationTokenSource switchCts)
        {
            CancellationToken token = switchCts.Token;

            try
            {
                AudioClip clip = await _audioLoader.LoadAudioAsync(path, token);
                if (clip == null || this == null || !_isInitialized || token.IsCancellationRequested)
                {
                    if (ReferenceEquals(_bgmSwitchCTS, switchCts))
                    {
                        _requestedBgmPath = null;
                    }

                    return;
                }

                AudioSource targetSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;
                AudioSource currentSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;

                if (targetSource == null || currentSource == null)
                {
                    Debug.LogError(
                        $"[AudioManager] 切换 BGM 失败: BGM Source 丢失, Path={path}, TriggerObject={gameObject.name}");

                    if (ReferenceEquals(_bgmSwitchCTS, switchCts))
                    {
                        _requestedBgmPath = null;
                    }

                    return;
                }

                targetSource.clip = clip;
                targetSource.volume = 0f;
                targetSource.Play();
                _currentBgmPath = path;
                _requestedBgmPath = path;

                if (fadeDuration <= 0f)
                {
                    currentSource.Stop();
                    targetSource.volume = 1f;
                    _isUsingSourceA = !_isUsingSourceA;
                    return;
                }

                float timer = 0f;
                while (timer < fadeDuration)
                {
                    if (this == null || !_isInitialized || token.IsCancellationRequested)
                    {
                        return;
                    }

                    timer += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(timer / fadeDuration);
                    targetSource.volume = Mathf.Lerp(0f, 1f, t);

                    if (currentSource.isPlaying)
                    {
                        currentSource.volume = Mathf.Lerp(1f, 0f, t);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                targetSource.volume = 1f;
                currentSource.Stop();
                _isUsingSourceA = !_isUsingSourceA;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(_bgmSwitchCTS, switchCts))
                {
                    if (_currentBgmPath != path)
                    {
                        _requestedBgmPath = null;
                    }

                    switchCts.Dispose();
                    _bgmSwitchCTS = null;
                }
            }
        }

        public void StopMusic()
        {
            CancelBgmSwitch();
            _currentBgmPath = null;
            _requestedBgmPath = null;

            if (_bgmSourceA != null)
            {
                _bgmSourceA.Stop();
                _bgmSourceA.clip = null;
                _bgmSourceA.volume = 1f;
            }

            if (_bgmSourceB != null)
            {
                _bgmSourceB.Stop();
                _bgmSourceB.clip = null;
                _bgmSourceB.volume = 1f;
            }
        }

        // ================= Settings =================

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
                for (int i = _activeSounds.Count - 1; i >= 0; i--)
                {
                    ActiveSoundInfo info = _activeSounds[i];
                    if (info?.Source != null)
                    {
                        info.Source.Stop();
                        _sfxPool?.Recycle(info.Source);
                    }
                }

                _activeSounds.Clear();
            }
        }

        /// <summary>
        /// 将 0~1 线性音量转换为 AudioMixer 的对数分贝值
        /// </summary>
        private void ApplyMixerVolume(string paramName, float linearVolume)
        {
            if (_mixer == null)
            {
                return;
            }

            float dbVolume = linearVolume <= 0.0001f ? -80f : Mathf.Log10(linearVolume) * 20f;
            _mixer.SetFloat(paramName, dbVolume);
        }

        private void CancelBgmSwitch()
        {
            if (_bgmSwitchCTS == null)
            {
                return;
            }

            if (!_bgmSwitchCTS.IsCancellationRequested)
            {
                _bgmSwitchCTS.Cancel();
            }
        }
    }
}
