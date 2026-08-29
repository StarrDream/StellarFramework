# AudioKit / 音频系统源码文档

## 模块职责

`AudioKit` 提供统一的音频播放入口，内部由 `AudioManager` 负责具体执行。

当前模块负责：

- 初始化音频系统
- 播放和切换 BGM
- 播放 2D / 3D 音效
- 音量与开关设置持久化
- 通过 `IAudioLoader` 对接资源加载后端

## 源码文件

- `Runtime/Kits/AudioKit/AudioKit.cs`
- `Runtime/Kits/AudioKit/Core/AudioManager.cs`
- `Runtime/Kits/AudioKit/Core/AudioDefines.cs`
- `Runtime/Kits/AudioKit/Core/AudioLoader/IAudioLoader.cs`
- `Runtime/Kits/AudioKit/Core/AudioLoader/ResourcesAudioLoader.cs`
- `Runtime/Kits/AudioKit/Adapters/ResKit/AudioKitResKitAdapter.cs`（可选）

## 总体结构

```text
AudioKit
└─ AudioManager.Instance
   ├─ BGM 双 AudioSource
   ├─ SFX 对象池
   ├─ IAudioLoader
   └─ PlayerPrefs 设置
```

## 调用链

### 初始化

1. 业务调用 `AudioKit.Init(...)`
2. 创建默认 `IAudioLoader` 或使用自定义 loader
3. 调用 `AudioManager.Instance.Init(mixer, loader)`
4. 初始化 BGM 轨道、SFX 池、音量设置

### 播放音效

1. 业务调用 `PlaySound` 或 `PlaySound3D`
2. `AudioManager.PlaySoundInternal(...)`
3. 校验初始化状态与音效开关
4. 必要时按优先级淘汰低优先级音效
5. 异步加载 `AudioClip`
6. 从对象池分配 `AudioSource`
7. 播放并登记到 `_activeSounds`

### 播放 BGM

1. 业务调用 `PlayMusic`
2. 取消旧切换任务
3. 异步加载新 `AudioClip`
4. 在双轨 BGM Source 间做淡入淡出切换

## 类型详解

## `IAudioLoader`

### 作用

定义音频资源加载策略接口。

### 方法

- `LoadAudioAsync(string path, CancellationToken cancellationToken)`
- `Release()`

## `ResourcesAudioLoader`

### 作用

`AudioKit.Core` 的默认加载器，通过 Unity `Resources.Load` 加载 `AudioClip`，不依赖 ResKit。

## `DefaultResKitAudioLoader<TLoader>`（可选 Adapter）

### 作用

基于 `ResKit` 的默认音频加载器实现。

### 字段

- `_resLoader : IResLoader`

### 方法

- 构造函数中分配 `ResKit` loader
- `LoadAudioAsync(...)` 异步加载 `AudioClip`
- `Release()` 回收 `ResKit` loader

## `SoundPriority`

### 枚举值

- `Low`
- `Normal`
- `High`
- `Critical`

用于音效池已满时的淘汰策略。

## `AudioManager`

### 作用

音频系统核心实现，负责播放、切换、池化、设置管理。

### 关键字段

- `_audioLoader`
- `_mixer`
- `_bgmGroup / _sfxGroup`
- `_bgmSourceA / _bgmSourceB`
- `_isUsingSourceA`
- `_currentBgmPath / _requestedBgmPath`
- `_sfxPool`
- `_activeSounds`
- `_managerCTS`
- `_bgmSwitchCTS`
- `_isInitialized`
- `_musicVolume / _soundVolume`
- `_isMusicOn / _isSoundOn`

### 内部类型

#### `ActiveSoundInfo`

字段：

- `Source`
- `FollowTarget`
- `Priority`

用于登记当前活跃音效。

### 关键方法

#### `Init(...)`

音频系统初始化入口。

职责：

- 校验 `AudioMixer` 和 loader
- 初始化混音器分组
- 初始化 BGM 双轨
- 初始化 SFX 对象池
- 读取本地设置

#### `PlaySoundInternal(...)`

统一音效入口。

职责：

- 拦截未初始化或已关闭音效
- 音效池满时尝试淘汰低优先级音效
- 异步加载音频并播放

#### `TryEvictLowPrioritySound(...)`

从活跃音效中找最低优先级对象，必要时淘汰。

#### `PlayMusic(...)`

统一 BGM 切换入口。

职责：

- 拦截未初始化和非法路径
- 避免对当前曲目重复切换
- 创建新的切换任务

#### `PlayMusicAsync(...)`

执行 BGM 资源加载与淡入淡出切换。

#### `StopMusic()`

停止双轨 BGM 并清理当前记录。

#### `SetMusicVolume / SetSoundVolume / SetMusicOn / SetSoundOn`

修改运行时设置并写入 `PlayerPrefs`。

#### `ApplyMixerVolume(...)`

把线性音量转换为分贝值写入 `AudioMixer`。

## `AudioKit`

### 作用

对外静态门面。

### 方法

- `Init(AudioMixer mixer)`（Resources 默认加载）
- `Init(AudioMixer mixer, IAudioLoader customLoader)`
- `PlayMusic(...)`
- `StopMusic()`
- `PlaySound(...)`
- `PlaySound3D(...)`

### 属性

- `MusicVolume`
- `SoundVolume`
- `MusicOn`
- `SoundOn`

安装 `AudioKit.ResKitAdapter` 后，可通过 `AudioKitResKit.Init<TLoader>(mixer)` 使用 ResKit。

## 设计约束

- 使用前必须先 `Init`
- 音效加载必须经 `IAudioLoader`
- 音效池容量有限，超过上限走优先级淘汰
- BGM 使用双轨淡入淡出

## 常见误用

- 未初始化直接播放
- 没有提供有效 `AudioMixer`
- 3D 音效跟随目标为空
- 切换场景时忘记考虑单例生命周期
