using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
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
        private IResLoader _loader;
        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private bool _isUsingSourceA = true;
        private string _currentBgmPath;
        private FactoryObjectPool<AudioSource> _sfxPool;


        private ResLoaderType _currentResLoaderType;

        // 记录 Source 状态
        private class ActiveSoundInfo
        {
            public AudioSource Source;
            public Transform FollowTarget;
            public SoundPriority Priority; //优先级
        }

        private readonly List<ActiveSoundInfo> _activeSounds = new List<ActiveSoundInfo>(64);
        private CancellationTokenSource _managerCTS = new CancellationTokenSource(); // 用于管理所有延迟任务

        private float _musicVolume = 1.0f;
        private float _soundVolume = 1.0f;
        private bool _isMusicOn = true;
        private bool _isSoundOn = true;

        public void Init(ResLoaderType loaderType = ResLoaderType.Resources)
        {
            _currentResLoaderType = loaderType;
            switch (loaderType)
            {
                case ResLoaderType.Resources:
                    _loader = ResKit.Allocate<ResourceLoader>();
                    break;
                case ResLoaderType.Addressable:
                    _loader = ResKit.Allocate<AddressableLoader>();
                    break;
            }

            InitializeBGM();
            InitializeSFXPool();
            LoadSettings();
        }


        private void Update()
        {
            // 倒序遍历，安全移除无效项
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                var info = _activeSounds[i];

                if (info.Source == null)
                {
                    _activeSounds.RemoveAt(i);
                    continue;
                }

                // 跟随逻辑
                if (info.FollowTarget != null)
                {
                    // 检查目标是否假死
                    if (info.FollowTarget.gameObject == null)
                    {
                        info.FollowTarget = null; // 目标没了就停留在原地
                    }
                    else
                    {
                        info.Source.transform.position = info.FollowTarget.position;
                    }
                }
            }
        }

        protected override void OnDestroy()
        {
            // 取消所有正在进行的回收任务，防止场景销毁后回调报错
            _managerCTS.Cancel();
            _managerCTS.Dispose();

            if (_loader != null)
            {
                ResKit.Recycle(_loader);
                _loader = null;
            }

            base.OnDestroy();
        }

        private void InitializeBGM()
        {
            _bgmSourceA = CreateSource("BGM_Track_A");
            _bgmSourceB = CreateSource("BGM_Track_B");
            _bgmSourceA.loop = true;
            _bgmSourceB.loop = true;
            _bgmSourceA.spatialBlend = 0f;
            _bgmSourceB.spatialBlend = 0f;
        }

        private void InitializeSFXPool()
        {
            _sfxPool = new FactoryObjectPool<AudioSource>(
                factoryMethod: () => CreateSource($"SFX_Pool_Item"),
                resetMethod: (source) =>
                {
                    if (source != null)
                    {
                        source.Stop();
                        source.clip = null;
                        source.transform.SetParent(transform);
                        source.transform.localPosition = Vector3.zero;
                        source.spatialBlend = 0f;
                        source.volume = 1f;
                        source.pitch = 1f;
                        source.minDistance = 1.0f;
                        source.maxDistance = 500.0f;
                        source.priority = 128; // 恢复默认 Unity 优先级
                    }
                },
                destroyMethod: (source) =>
                {
                    if (source != null) Destroy(source.gameObject);
                },
                maxCount: AudioDefines.MAX_SOUND_VOICES
            );
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            return go.AddComponent<AudioSource>();
        }

        // ================= SFX 核心逻辑 =================

        /// <summary>
        /// 播放音效 (内部实现)
        /// </summary>
        public void PlaySoundInternal(string path, Vector3 position, Transform attachTarget, bool is3D, SoundPriority priority = SoundPriority.Normal)
        {
            if (!_isSoundOn) return;
            if (string.IsNullOrEmpty(path)) return;

            // 检查池子容量
            if (_activeSounds.Count >= AudioDefines.MAX_SOUND_VOICES)
            {
                // 尝试剔除低优先级音效
                if (!TryEvictLowPrioritySound(priority))
                {
                    // 如果所有正在播放的音效优先级都 >= 当前请求，则丢弃当前请求
                    LogKit.LogWarning($"[AudioManager] 音效池已满且无法剔除，丢弃: {path} (Priority: {priority})");
                    return;
                }
            }

            var cachedData = ResMgr.GetCache(path, _currentResLoaderType);

            if (cachedData != null && cachedData.Asset is AudioClip cachedClip)
            {
                PlayClipNow(cachedClip, position, attachTarget, is3D, priority);
            }
            else
            {
                // 异步加载
                PlaySoundAsync(path, position, attachTarget, is3D, priority).Forget();
            }
        }

        /// <summary>
        /// 尝试剔除一个低优先级的音效
        /// </summary>
        private bool TryEvictLowPrioritySound(SoundPriority newPriority)
        {
            ActiveSoundInfo candidate = null;
            SoundPriority minPriority = SoundPriority.Critical;

            // 寻找优先级最低的音效
            foreach (var info in _activeSounds)
            {
                if (info.Priority < minPriority)
                {
                    minPriority = info.Priority;
                    candidate = info;
                }
            }

            // 只有当找到的最低优先级 < 新请求的优先级时，才剔除
            // (同级不剔除，先来后到)
            if (candidate != null && minPriority < newPriority)
            {
                if (candidate.Source != null)
                {
                    candidate.Source.Stop();
                    // 立即回收
                    _sfxPool.Recycle(candidate.Source);
                }

                _activeSounds.Remove(candidate);
                return true;
            }

            return false;
        }

        private void PlayClipNow(AudioClip clip, Vector3 position, Transform attachTarget, bool is3D, SoundPriority priority)
        {
            var source = _sfxPool.Allocate();

            var info = new ActiveSoundInfo
            {
                Source = source,
                FollowTarget = attachTarget,
                Priority = priority
            };

            _activeSounds.Add(info);

            source.clip = clip;
            source.volume = _soundVolume;
            source.spatialBlend = is3D ? 1.0f : 0.0f;

            if (is3D)
            {
                source.minDistance = 1.0f;
                source.maxDistance = 20.0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.transform.position = attachTarget != null ? attachTarget.position : position;
            }

            source.Play();

            // 延迟回收
            RecycleSourceDelay(info, clip.length).Forget();
        }

        private async UniTaskVoid PlaySoundAsync(string path, Vector3 position, Transform attachTarget, bool is3D, SoundPriority priority)
        {
            // 绑定 Manager 的 CTS，防止 Manager 销毁后还在加载
            var clip = await _loader.LoadAsync<AudioClip>(path).AttachExternalCancellation(_managerCTS.Token);
            if (this == null || !_isSoundOn || clip == null) return;
            PlayClipNow(clip, position, attachTarget, is3D, priority);
        }

        private async UniTaskVoid RecycleSourceDelay(ActiveSoundInfo info, float delay)
        {
            try
            {
                // 音效通常受 TimeScale 影响，所以 ignoreTimeScale = false
                // 绑定 _managerCTS，当 AudioManager 销毁时取消所有等待
                await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: false, cancellationToken: _managerCTS.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (info.Source != null)
            {
                // 检查是否已经被剔除策略回收了
                if (_activeSounds.Contains(info))
                {
                    _activeSounds.Remove(info);
                    _sfxPool.Recycle(info.Source);
                }
            }
        }

        // ================= BGM 逻辑 =================
        public async void PlayMusic(string path, float fadeDuration)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (_currentBgmPath == path) return;

            _currentBgmPath = path;
            var clip = await _loader.LoadAsync<AudioClip>(path).AttachExternalCancellation(_managerCTS.Token);
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
                if (_isMusicOn) targetSource.volume = Mathf.Lerp(0, _musicVolume, t);
                if (currentSource.isPlaying) currentSource.volume = Mathf.Lerp(_musicVolume, 0, t);
                await UniTask.Yield(PlayerLoopTiming.Update, _managerCTS.Token);
            }

            targetSource.volume = _isMusicOn ? _musicVolume : 0;
            currentSource.Stop();
            _isUsingSourceA = !_isUsingSourceA;
        }

        public void StopMusic()
        {
            _currentBgmPath = null;
            if (_bgmSourceA) _bgmSourceA.Stop();
            if (_bgmSourceB) _bgmSourceB.Stop();
        }

        private void LoadSettings()
        {
            _musicVolume = PlayerPrefs.GetFloat(AudioDefines.PREFS_MusicVolume, 1.0f);
            _soundVolume = PlayerPrefs.GetFloat(AudioDefines.PREFS_SoundVolume, 1.0f);
            _isMusicOn = PlayerPrefs.GetInt(AudioDefines.PREFS_MusicOn, 1) == 1;
            _isSoundOn = PlayerPrefs.GetInt(AudioDefines.PREFS_SoundOn, 1) == 1;
            UpdateBGMVolume();
        }

        public void SetMusicVolume(float v)
        {
            _musicVolume = v;
            PlayerPrefs.SetFloat(AudioDefines.PREFS_MusicVolume, v);
            UpdateBGMVolume();
        }

        public void SetSoundVolume(float v)
        {
            _soundVolume = v;
            PlayerPrefs.SetFloat(AudioDefines.PREFS_SoundVolume, v);
            foreach (var info in _activeSounds)
                if (info.Source != null)
                    info.Source.volume = v;
        }

        public void SetMusicOn(bool isOn)
        {
            _isMusicOn = isOn;
            PlayerPrefs.SetInt(AudioDefines.PREFS_MusicOn, isOn ? 1 : 0);
            UpdateBGMVolume();
        }

        public void SetSoundOn(bool isOn)
        {
            _isSoundOn = isOn;
            PlayerPrefs.SetInt(AudioDefines.PREFS_SoundOn, isOn ? 1 : 0);
            if (!isOn)
            {
                // 关闭音效时，停止所有正在播放的 SFX
                foreach (var info in _activeSounds)
                {
                    if (info.Source != null) info.Source.Stop();
                }
            }
        }

        private void UpdateBGMVolume()
        {
            var active = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
            if (active != null) active.volume = _isMusicOn ? _musicVolume : 0;
        }
    }
}