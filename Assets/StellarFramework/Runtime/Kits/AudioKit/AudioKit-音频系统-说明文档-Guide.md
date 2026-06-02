# AudioKit / 音频系统说明文档

AudioKit 管理 BGM、音效、3D 音效、音量和开关。业务层只调用 `AudioKit`，资源来源可以是 ResKit，也可以是自定义 `IAudioLoader`。

## 入口 API

- `AudioKit.Init<TLoader>(AudioMixer mixer)`：用 ResKit loader 初始化音频。
- `AudioKit.Init(AudioMixer mixer, IAudioLoader customLoader)`：使用自定义音频加载器。
- `AudioKit.PlayMusic(path, fadeDuration)`：播放 BGM。
- `AudioKit.StopMusic()`：停止 BGM。
- `AudioKit.PlaySound(path, priority)`：播放 2D 音效。
- `AudioKit.PlaySound3D(path, position, priority)`：在世界坐标播放 3D 音效。
- `AudioKit.MusicVolume`、`SoundVolume`、`MusicOn`、`SoundOn`：音量和开关。

## 使用模板

```csharp
using StellarFramework.Audio;
using StellarFramework.Res;
using UnityEngine;
using UnityEngine.Audio;

public sealed class AudioEntry : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;

    private void Awake()
    {
        AudioKit.Init<ResourceLoader>(mixer);
        AudioKit.MusicVolume = 0.8f;
        AudioKit.SoundVolume = 1f;
    }

    public void PlayClick()
    {
        AudioKit.PlaySound("Audio/UI/Click");
    }
}
```

## ToolsHub 关联

- `AudioKit 音频中心` 用于检查 Mixer、分组和示例音频资源。
- `SettingsKit 设置中心` 可通过 adapter 把音量设置应用到 AudioKit。

## 常见问题

- 没声音：确认调用了 `AudioKit.Init`，Mixer 暴露参数名称正确，资源路径能被 loader 加载。
- 音效被抢占：检查 `SoundPriority` 和 AudioManager 的通道策略。
- 音量 UI 不生效：确认 SettingsKit 的 `AudioKitSettingsAdapter` 已接入。
