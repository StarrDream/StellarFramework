using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using StellarFramework.Audio;
using StellarFramework.Res;

namespace StellarFramework.Examples
{
    /// <summary>
    /// AudioKit 综合使用场景演示
    /// </summary>
    public class Example_AudioKit : MonoBehaviour
    {
        [Header("核心配置")]
        [Tooltip("必须挂载配置好 BGM 和 SFX Group 并暴露了对应 Volume 参数的 AudioMixer")]
        [SerializeField]
        private AudioMixer _mainMixer;

        [Header("测试引用")]
        [Tooltip("用于测试 3D 音效跟随的目标物体")]
        [SerializeField]
        private Transform _movingTarget;

        private void Start()
        {
            if (_mainMixer == null)
            {
                Debug.LogError($"[Example_AudioKit] 初始化失败: 缺失 AudioMixer 引用，触发对象: {gameObject.name}");
                return;
            }

            // 示例场景优先尝试加载资源；若资源缺失，则回退到程序化生成的测试音频。
            AudioKit.Init(_mainMixer, new ExampleAudioFallbackLoader<ResourceLoader>());

            AudioKit.PlayMusic("Audio/BGM/MainTheme");
            Debug.Log("[Example_AudioKit] AudioKit 初始化完成，已开始播放默认 BGM。");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                AudioKit.PlaySound("Audio/SFX/UI_Click", SoundPriority.Normal);
                Debug.Log("[Example_AudioKit] 播放 2D UI 音效");
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                AudioKit.PlaySound3D("Audio/SFX/Explosion", transform.position, SoundPriority.High);
                Debug.Log("[Example_AudioKit] 播放 3D 爆炸音效 (固定位置)");
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (_movingTarget != null)
                {
                    AudioKit.PlaySound3D("Audio/SFX/Footstep", _movingTarget, SoundPriority.Low);
                    Debug.Log("[Example_AudioKit] 播放 3D 跟随音效 (目标移动中)");
                }
                else
                {
                    Debug.LogError("[Example_AudioKit] 播放跟随音效失败: _movingTarget 为空，当前状态: 无法获取跟随目标");
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                AudioKit.PlayMusic("Audio/BGM/BattleTheme", 2.0f);
                Debug.Log("[Example_AudioKit] 切换 BGM，执行 2.0s 淡入淡出");
            }

            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                bool currentSoundState = AudioKit.SoundOn;
                AudioKit.SoundOn = !currentSoundState;
                Debug.Log($"[Example_AudioKit] 切换音效静音状态，当前状态: {AudioKit.SoundOn}");
            }

            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                float nextVolume = AudioKit.MusicVolume - 0.2f;
                if (nextVolume < 0f)
                {
                    nextVolume = 1.0f;
                }

                AudioKit.MusicVolume = nextVolume;
                Debug.Log($"[Example_AudioKit] 调节 BGM 音量，当前线性音量: {AudioKit.MusicVolume}");
            }
        }
    }

    internal sealed class ExampleAudioFallbackLoader<TLoader> : IAudioLoader where TLoader : ResLoader, new()
    {
        private IResLoader _resLoader = ResKit.Allocate<TLoader>();
        private readonly Dictionary<string, AudioClip> _generatedClips = new Dictionary<string, AudioClip>();

        public async UniTask<AudioClip> LoadAudioAsync(string path, CancellationToken cancellationToken)
        {
            AudioClip clip = null;

            if (_resLoader != null)
            {
                clip = await _resLoader.LoadAsync<AudioClip>(path, cancellationToken);
            }

            if (clip != null)
            {
                return clip;
            }

            if (_generatedClips.TryGetValue(path, out AudioClip generated) && generated != null)
            {
                return generated;
            }

            AudioClip fallback = ExampleAudioToneFactory.Create(path);
            if (fallback != null)
            {
                _generatedClips[path] = fallback;
            }

            return fallback;
        }

        public void Release()
        {
            if (_resLoader != null)
            {
                ResKit.Recycle(_resLoader);
                _resLoader = null;
            }

            foreach (KeyValuePair<string, AudioClip> pair in _generatedClips)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(pair.Value);
                }
                else
                {
                    Object.DestroyImmediate(pair.Value);
                }
            }

            _generatedClips.Clear();
        }
    }

    internal static class ExampleAudioToneFactory
    {
        public static AudioClip Create(string path)
        {
            switch (path)
            {
                case "Audio/BGM/MainTheme":
                    return CreateClip("MainTheme_Generated", 261.63f, 1.6f, 0.22f);
                case "Audio/BGM/BattleTheme":
                    return CreateClip("BattleTheme_Generated", 329.63f, 1.2f, 0.24f);
                case "Audio/SFX/UI_Click":
                    return CreateClip("UIClick_Generated", 880f, 0.10f, 0.28f);
                case "Audio/SFX/Explosion":
                    return CreateClip("Explosion_Generated", 110f, 0.45f, 0.32f);
                case "Audio/SFX/Footstep":
                    return CreateClip("Footstep_Generated", 196f, 0.18f, 0.18f);
                default:
                    return null;
            }
        }

        private static AudioClip CreateClip(string clipName, float frequency, float durationSeconds, float amplitude)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - (t / durationSeconds));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * amplitude;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
