# AudioKit / 音频系统说明文档

## 模块定位

`AudioKit` 是框架的统一音频入口。

它负责：

- BGM 播放与切换
- 2D / 3D 音效播放
- 音量和开关设置
- 音频资源加载后端接入

业务层应只依赖 `AudioKit`，不直接在各个脚本里散落 `AudioSource` 管理逻辑。

## 模块组成

- `AudioKit`
  静态门面
- `AudioManager`
  运行时核心管理器
- `IAudioLoader`
  音频资源加载接口
- `DefaultResKitAudioLoader<TLoader>`
  基于 `ResKit` 的默认音频加载器

## 初始化方式

### 使用 ResKit Loader

```csharp
AudioKit.Init<ResourceLoader>(mixer);
```

### 使用自定义 Loader

```csharp
AudioKit.Init(mixer, customLoader);
```

初始化需要满足：

- 有可用的 `AudioMixer`
- Mixer 中已配置 BGM / SFX 分组
- 有可用的音频资源加载器

## 常用调用

```csharp
AudioKit.PlayMusic("Audio/BGM/Main", 0.5f);
AudioKit.PlaySound("Audio/UI/Click");
AudioKit.PlaySound3D("Audio/SFX/Explosion", transform.position);
AudioKit.MusicVolume = 0.8f;
AudioKit.SoundOn = true;
```

## 运行规则

- BGM 使用双轨淡入淡出切换
- 音效通过对象池复用 `AudioSource`
- 3D 音效支持位置播放和跟随目标
- 音量和静音状态写入 `PlayerPrefs`

## 适合场景

- 游戏主界面 BGM
- 战斗音效
- UI 点击音效
- 动态切换的音量设置

## ToolsHub 关联

- `AudioKit 音频中心`
  检查 Mixer、分组和样例音频资源
- `SettingsKit 设置中心`
  可通过 adapter 把音量设置写入 `AudioKit`

## 使用约束

- 使用前必须初始化
- 大量并发音效会受到优先级与通道数量影响
- 资源路径要和所选 loader 的加载规则一致

## 常见问题

- 没声音
  检查是否初始化、Mixer 是否配置正确、资源路径是否可加载。
- 音效被抢占
  检查 `SoundPriority` 和活跃音效数量。
- 设置改了没生效
  检查是否正确通过 `AudioKitSettingsAdapter` 或相关逻辑接入。

## 相关文档

- [AudioKit 源码文档](AudioKit-音频系统-源码文档-Guide.md)
