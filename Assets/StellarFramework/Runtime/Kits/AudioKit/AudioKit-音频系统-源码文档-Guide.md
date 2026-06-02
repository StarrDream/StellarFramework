# AudioKit / 音频系统源码文档

## 源码位置

- `Runtime/Kits/AudioKit/AudioKit.cs`：静态业务入口。
- `Runtime/Kits/AudioKit/Core/AudioManager.cs`：运行时音频管理器。
- `Runtime/Kits/AudioKit/Core/AudioDefines.cs`：Mixer 参数和常量。
- `Runtime/Kits/AudioKit/Core/AudioLoader/IAudioLoader.cs`：加载器接口。
- `Runtime/Kits/AudioKit/Core/AudioLoader/DefaultResKitAudioLoader.cs`：默认 ResKit 音频加载器。

## 核心类型

- `AudioKit`：外部唯一静态入口。
- `AudioManager`：继承 `MonoSingleton<AudioManager>`，持有 AudioSource、Mixer 和 loader。
- `SoundPriority`：音效播放优先级。
- `IAudioLoader`：音频资源加载和释放接口。
- `DefaultResKitAudioLoader<TLoader>`：把 ResKit loader 适配成 AudioKit loader。
- `AudioDefines`：音频分组、Mixer 参数、默认常量。

## 关键方法

- `AudioKit.Init<TLoader>(AudioMixer mixer)`：创建默认 ResKit loader 并初始化 AudioManager。
- `AudioKit.Init(AudioMixer mixer, IAudioLoader customLoader)`：注入第三方音频 loader。
- `AudioKit.PlayMusic`：走 AudioManager 的 BGM 通道，支持淡入淡出。
- `AudioKit.PlaySound` / `PlaySound3D`：分配音效通道并播放。
- `AudioKit.MusicVolume` / `SoundVolume`：同步到 Mixer 参数。
- `AudioManager` 内部播放方法：负责 AudioSource 创建、复用和停止。

## 数据流

业务调用 `AudioKit`，静态入口转发到 `AudioManager.Instance`。AudioManager 使用 `IAudioLoader` 加载 `AudioClip`，把 clip 填入 BGM 或 SFX AudioSource，播放完成后按策略释放或复用。

## 依赖关系

- 依赖 Unity AudioSource 和 AudioMixer。
- 默认 loader 依赖 ResKit。
- 继承链依赖 SingletonKit 的 `MonoSingleton<T>`。
- SettingsKit 通过 `AudioKitSettingsAdapter` 操作音量和开关。

## 扩展点

- 接入第三方资源系统：实现 `IAudioLoader` 后调用 `AudioKit.Init(mixer, loader)`。
- 增加音频分组：扩展 AudioManager 的通道和 Mixer 参数。
- 接入设置页：通过 SettingsKit adapter 读写 `MusicVolume`、`SoundVolume`、`MusicOn`、`SoundOn`。

## 测试入口

- `AudioKit 音频中心`：检查 Mixer 和参数。
- `SettingsKit_Playable.unity`：验证音量设置应用。
- 修改播放策略后，手动验证 BGM 切换、2D 音效、3D 音效和停止逻辑。
