# AudioKit 使用手册

## 1. 设计理念 (Why)

**AudioKit** 是一个基于 `AudioMixer` 的高性能音频管理系统，解决了 Unity 原生 AudioSource 管理混乱、并发过多导致爆音的问题。

### 核心特性
*   **硬件级混音**：全面接入 `AudioMixer`，利用底层 DSP 优化音量控制，拒绝遍历修改 `AudioSource.volume` 带来的 CPU 损耗。
*   **BGM 轨道管理**：双轨道设计，支持无缝自动淡入淡出 (`CrossFade`)。
*   **SFX 轮询对象池**：内置基于 `FactoryObjectPool` 的 AudioSource 对象池，采用 `Update` 轮询机制精准回收，彻底根除异步延迟回收带来的状态混乱。
*   **优先级剔除**：限制最大并发数（默认 64），当池满时，高优先级音效 (`Critical`) 会自动挤掉低优先级音效 (`Low`)。
*   **加载解耦 (泛型策略模式)**：基于 `IAudioLoader` 接口与泛型注入，音频资源的加载与核心逻辑完全剥离，支持无缝接入 Resources、Addressables 或 YooAsset 等任意资源管理方案。

---

## 2. 快速上手 (Quick Start)

### 2.1 初始化 (必须)
在游戏启动入口处，必须传入配置好的 `AudioMixer` 实例。同时通过**泛型**指定资源加载策略：

**方式 A：使用内置的 ResKit 加载 (默认)**
```csharp
public AudioMixer MainMixer;

void Start()
{
    // 初始化 AudioKit，注入混音器，并通过泛型指定使用 ResourceLoader
    AudioKit.Init<ResourceLoader>(MainMixer); 
    
    // 如果使用 Addressables，则改为：
    // AudioKit.Init<AddressableLoader>(MainMixer);
}
```

**方式 B：接入第三方资源管理 (如 YooAsset)**
```csharp
void Start()
{
    // 1. 实现 IAudioLoader 接口
    IAudioLoader myYooAssetLoader = new MyYooAssetAudioLoader();
    
    // 2. 注入自定义加载策略
    AudioKit.Init(MainMixer, myYooAssetLoader);
}
```
*(注：Mixer 中必须包含名为 `BGM` 和 `SFX` 的 Group，并将其音量参数暴露为 `BGMVolume` 和 `SFXVolume`。您可以通过 Tools Hub 中的 `AudioKit 音频中心` 自动诊断并修复 Mixer 配置)*

### 2.2 播放音乐 (BGM)
```csharp
// 播放 (自动淡入，默认 0.5s)
AudioKit.PlayMusic("BGM/Battle_Theme");

// 停止
AudioKit.StopMusic();
```

### 2.3 播放音效 (SFX)
```csharp
// 2D 音效 (UI)
AudioKit.PlaySound("SFX/Button_Click", SoundPriority.Normal);

// 3D 音效 (指定位置)
AudioKit.PlaySound3D("SFX/Explosion", transform.position, SoundPriority.High);

// 3D 音效 (跟随物体，如脚步声)
AudioKit.PlaySound3D("SFX/Footstep", transform, SoundPriority.Low);
```

### 2.4 全局设置
```csharp
AudioKit.MusicVolume = 0.5f; // 自动映射为 Mixer 的对数分贝
AudioKit.SoundOn = false;    // 静音 SFX 并打断当前所有音效
```

---

## 3. 优先级系统 (Priority)

在 `AudioManager` 内部，音效分为 4 个等级：
1.  `Low`: 环境音、脚步声。
2.  `Normal`: 普通攻击、UI。
3.  `High`: 技能、受击。
4.  `Critical`: 剧情对白、Boss 技能。

当同时播放的音效超过 64 个时，系统会优先停止 `Low` 级别的音效来腾出空位，确保关键反馈不丢失。