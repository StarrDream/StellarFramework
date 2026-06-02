# SettingsKit / 设置系统源码文档

## 源码位置

- `Runtime/Kits/SettingsKit/Core/SettingsKit.cs`：静态门面。
- `Runtime/Kits/SettingsKit/Core/SettingsManager.cs`：设置运行时管理器。
- `Runtime/Kits/SettingsKit/Core/SettingsRegistry.cs`：设置页和设置项注册表。
- `Runtime/Kits/SettingsKit/Core/SettingDefinitions.cs`：Bool、Float、Int、String、Choice 定义。
- `Runtime/Kits/SettingsKit/Core/SettingsContracts.cs`：接口和数据契约。
- `Runtime/Kits/SettingsKit/Core/SettingsEntry.cs`：运行时设置项。
- `Runtime/Kits/SettingsKit/Core/PlayerPrefsSettingsStorage.cs`：默认存储。
- `Runtime/Kits/SettingsKit/Providers/BuiltinSettingsProviders.cs`：默认设置页。
- `Runtime/Kits/SettingsKit/Adapters/SettingsAdapters.cs`：AudioKit、Graphics、Language、Input 适配器。
- `Runtime/Kits/SettingsKit/Core/SettingsMenuOverlay.cs`：运行时设置菜单示例。

## 核心类型

- `SettingsKit`：业务静态入口。
- `SettingsManager`：继承 `Singleton<SettingsManager>`，负责初始化、读写、应用、保存和回滚。
- `SettingsRegistry`：注册 `SettingsPageDefinition` 和 `SettingDefinition`。
- `SettingsPageDefinition`：设置页元数据。
- `SettingDefinition`：设置项抽象定义。
- `BoolSettingDefinition`、`FloatSettingDefinition`、`IntSettingDefinition`、`StringSettingDefinition`、`ChoiceSettingDefinition`：内置类型定义。
- `SettingEntry`：设置项运行时值，包含当前值、待应用值、默认值和 dirty 状态。
- `ISettingsStorage`：存储接口。
- `ISettingsPageProvider`：设置页提供器接口。
- `ISettingApplyStrategy`：设置应用策略接口。
- `AudioKitSettingsAdapter`、`UnityGraphicsSettingsAdapter`、`SimpleLanguageSettingsAdapter`、`SimpleInputBindingAdapter`：默认适配器。

## 关键方法

- `SettingsKit.ConfigureStorage`：设置存储后端。
- `SettingsKit.RegisterProvider`：注册设置页提供器。
- `SettingsKit.InstallDefaultProviders`：安装内置提供器。
- `SettingsKit.Init`：调用 `SettingsManager.Init`，装载注册表和存储值。
- `SettingsKit.TrySetValue`：校验并写入 pending 值。
- `SettingsKit.ApplyPending`：调用设置项的 apply strategy。
- `SettingsKit.Save`：把已应用值写入 storage。
- `SettingsKit.RevertPending`：丢弃未应用改动。
- `SettingsKit.ResetPage` / `ResetAll`：恢复默认值。
- `SettingsManager.SettingChanged`：设置变化事件。

## 数据流

设置提供器创建页面和设置项定义，注册到 `SettingsRegistry`。初始化时，`SettingsManager` 从 `ISettingsStorage` 读取存档值，生成 `SettingEntry`。UI 调用 `TrySetValue` 后只改变 pending 值。`ApplyPending` 执行 apply strategy，比如写入 AudioKit 或 Unity Graphics。`Save` 再把值写入存储。`RevertPending` 放弃还没应用的修改。

## 依赖关系

- 依赖 SingletonKit 的 `Singleton<SettingsManager>`。
- 默认音频适配器依赖 AudioKit。
- 画质适配器依赖 Unity QualitySettings、Screen、Application 等 Unity API。
- 默认存储依赖 PlayerPrefs。
- ToolsHub 的 SettingsKit 设置中心读取页面、entry 和状态。

## 扩展点

- 新增设置页：实现 `ISettingsPageProvider`。
- 新增设置类型：继承 `SettingDefinition`，实现 normalize、serialize、deserialize。
- 新增应用策略：实现 `ISettingApplyStrategy`。
- 新增存储：实现 `ISettingsStorage`，可接 JSON、云端或本地加密存储。
- 新增 UI：读取 `SettingsKit.GetPages()` 和 `GetEntriesForPage(pageId)` 动态生成控件。

## 测试入口

- `SettingsKit_Playable.unity`：设置页运行时样例。
- ToolsHub `SettingsKit 设置中心`：编辑器查看入口。
- 修改定义或存储时测试：初始化、TrySetValue、ApplyPending、Save、RevertPending、ResetAll。
