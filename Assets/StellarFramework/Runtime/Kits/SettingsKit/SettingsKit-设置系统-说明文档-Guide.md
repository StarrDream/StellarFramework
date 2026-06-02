# SettingsKit / 设置系统说明文档

SettingsKit 管理游戏设置页、设置项、存储、应用策略和默认提供器。它适合音频、画质、语言、输入绑定、玩法开关等需要“修改、应用、保存、回滚”的设置。

## 入口 API

- `SettingsKit.ConfigureStorage(storage)`：配置存储。
- `SettingsKit.RegisterProvider(provider)`：注册设置页提供器。
- `SettingsKit.InstallDefaultProviders(options)`：安装默认音频、画质、语言、输入、玩法页。
- `SettingsKit.Init()`：初始化注册表和存储。
- `SettingsKit.GetPages()`：获取设置页。
- `SettingsKit.GetEntriesForPage(pageId)`：获取页面设置项。
- `SettingsKit.TrySetValue(key, rawValue, out error)`：写入待应用值。
- `SettingsKit.ApplyPending(out error)`：应用待处理设置。
- `SettingsKit.Save(out error)`：保存。
- `SettingsKit.RevertPending(out error)`：回滚未保存修改。

## 使用模板

```csharp
SettingsKit.ConfigureStorage(new PlayerPrefsSettingsStorage());
SettingsKit.InstallDefaultProviders();
SettingsKit.Init();

SettingsKit.TrySetValue(SettingsKeys.MusicVolume, 0.8f, out string error);
SettingsKit.ApplyPending(out error);
SettingsKit.Save(out error);
```

## ToolsHub 关联

- `SettingsKit 设置中心` 用于查看默认页、设置项、存储和适配器状态。
- `AudioKit 音频中心` 可配合音量设置验证。

## 常见问题

- 设置改了但没生效：确认调用 `ApplyPending`。
- 设置生效但重启丢失：确认调用 `Save`。
- 自定义设置页不出现：确认实现 `ISettingsPageProvider` 并注册。

## 源码阅读

见 [SettingsKit 源码文档](SettingsKit-设置系统-源码文档-Guide.md)。
