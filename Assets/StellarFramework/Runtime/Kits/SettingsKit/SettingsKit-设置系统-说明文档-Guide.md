# SettingsKit / 设置系统说明文档

## 模块定位

`SettingsKit` 用于管理：

- 设置页
- 设置项
- 存储
- 应用策略
- 默认提供器

它适合所有需要“修改、应用、保存、回滚”的设置场景，例如：

- 音频
- 画质
- 语言
- 输入绑定
- 玩法开关

## 模块组成

- `SettingsKit`
  对外静态门面
- `SettingsManager`
  运行时核心管理器
- `SettingsRegistry`
  页面和设置项注册表
- `SettingDefinition`
  设置项定义体系
- `ISettingsStorage`
  存储接口
- `ISettingApplyStrategy`
  应用策略接口
- `ISettingsPageProvider`
  页面提供器接口

## 标准接入流程

```csharp
SettingsKit.ConfigureStorage(new PlayerPrefsSettingsStorage());
SettingsKit.InstallDefaultProviders();
SettingsKit.Init();

SettingsKit.TrySetValue(SettingsKeys.MusicVolume, 0.8f, out string error);
SettingsKit.ApplyPending(out error);
SettingsKit.Save(out error);
```

## 运行规则

`SettingsKit` 里的值通常分为两种状态：

- 当前值
  当前编辑或当前应用的值
- 已保存值
  上一次成功保存到存储中的值

典型流程：

1. `TrySetValue(...)`
2. `ApplyPending(...)`
3. `Save(...)`

如果想撤销没保存的修改，则调用：

```csharp
SettingsKit.RevertPending(out error);
```

## 页面和设置项

页面通过 `ISettingsPageProvider` 注入。

设置项通过各种 `SettingDefinition` 表达，例如：

- `BoolSettingDefinition`
- `FloatSettingDefinition`
- `IntSettingDefinition`
- `StringSettingDefinition`
- `ChoiceSettingDefinition`

## 默认能力

默认提供器通常会补齐：

- 音频设置页
- 画质设置页
- 语言设置页
- 输入绑定页
- 玩法设置页

## ToolsHub 关联

- `SettingsKit 设置中心`
  查看默认页面、设置项、存储和适配器状态
- `AudioKit 音频中心`
  可配合音量设置验证

## 使用约束

- 配置存储和 provider 最好在 `Init()` 前完成
- 只想读取页面时，可以通过 `GetPages()` 和 `GetEntriesForPage(...)`
- 自定义设置页必须显式注册 provider
- 是否立即生效由设置定义和 apply strategy 决定

## 常见问题

- 设置改了但没生效
  确认调用了 `ApplyPending(...)`。
- 设置生效但重启丢失
  确认调用了 `Save(...)`。
- 自定义设置页不出现
  确认实现 `ISettingsPageProvider` 并注册。
- 值写入失败
  检查 `SettingDefinition` 的 normalize / deserialize 规则是否能接受该输入。

## 相关文档

- [SettingsKit 源码文档](SettingsKit-设置系统-源码文档-Guide.md)
