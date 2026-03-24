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
        [Header("核心配置")] [Tooltip("必须挂载配置好 BGM 和 SFX Group 并暴露了对应 Volume 参数的 AudioMixer")] [SerializeField]
        private AudioMixer _mainMixer;

        [Header("测试引用")] [Tooltip("用于测试 3D 音效跟随的目标物体")] [SerializeField]
        private Transform _movingTarget;

        private void Start()
        {
            if (_mainMixer == null)
            {
                Debug.LogError($"[Example_AudioKit] 初始化失败: 缺失 AudioMixer 引用，触发对象: {gameObject.name}");
                return;
            }

            // 架构重构：使用泛型指定加载器类型，替代原有的枚举传参
            // 如果要使用 Addressables，改为 AudioKit.Init<AddressableLoader>(_mainMixer);
            AudioKit.Init<ResourceLoader>(_mainMixer);

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
                    Debug.LogError($"[Example_AudioKit] 播放跟随音效失败: _movingTarget 为空，当前状态: 无法获取跟随目标");
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
                if (nextVolume < 0f) nextVolume = 1.0f;
                AudioKit.MusicVolume = nextVolume;
                Debug.Log($"[Example_AudioKit] 调节 BGM 音量，当前线性音量: {AudioKit.MusicVolume}");
            }
        }
    }
}