# SettingsKit / 设置系统

`SettingsKit` 用来管理一条完整的设置链路：

- 设置定义
- 页面注册
- 值归一化
- 应用策略
- 本地存储
- 保存与回滚
- 自定义扩展页

它不是一个只能“弹出菜单”的小工具，而是一套可复用的设置域模型。默认附带的 `SettingsMenuOverlay` 只是参考 UI，实现重点仍然在核心层和策略层。

## 1. 适用场景

- 游戏设置菜单
- 声音设置
- 画面设置
- 键位设置
- 语言设置
- 某个业务模块自己的扩展设置页

如果项目后期要从 `OnGUI` 切到 `UIKit`、`UIToolkit` 或自研界面，核心逻辑不需要重写。

## 2. 程序集划分

- `StellarFramework.SettingsKit`
  包含设置定义、注册表、管理器、存储实现和默认 `OnGUI` Overlay。
- `StellarFramework.SettingsKit.Adapters`
  包含可选适配层，目前提供 `AudioKit`、Unity 画面参数、简易语言设置和简易键位设置适配器。

如果你只需要“设置定义 + 保存/读取 + 自己的 UI”，只引用核心程序集即可。

## 3. 核心概念

### 3.1 `SettingsPageDefinition`

用于定义一个设置页。

```csharp
registry.RegisterPage(new SettingsPageDefinition(
    "audio",
    "Audio / 声音设置",
    "Unified entry for music, SFX, and mute behavior.",
    10));
```

### 3.2 `SettingDefinition`

框架内置以下类型：

- `BoolSettingDefinition`
- `FloatSettingDefinition`
- `IntSettingDefinition`
- `StringSettingDefinition`
- `ChoiceSettingDefinition`

每个设置项都包含：

- `Key`
- `PageId`
- `DisplayName`
- `Description`
- `DefaultValue`
- `ApplyImmediately`
- `RequiresRestart`
- `ApplyStrategy`

### 3.3 `ISettingApplyStrategy`

这个接口负责回答一个问题：

“设置值变化以后，真正的业务侧应该怎么生效？”

例如音量设置可以这样接入：

```csharp
new DelegateSettingApplyStrategy("Audio.MusicVolume", (_, value) =>
{
    audioAdapter.MusicVolume = (float)value;
    return null;
});
```

返回 `null` 表示应用成功，返回错误字符串表示应用失败。

### 3.4 `ISettingsPageProvider`

这是横向扩展入口。任何模块都可以注册自己的页面和设置项，而不需要改动主菜单代码。

```csharp
public sealed class MyFeatureSettingsProvider : ISettingsPageProvider
{
    public string ProviderName => "MyFeature";

    public void Register(SettingsRegistry registry)
    {
        registry.RegisterPage(new SettingsPageDefinition(
            "feature",
            "Feature / 功能设置",
            "业务模块自己的扩展页。"));

        registry.RegisterSetting(new BoolSettingDefinition(
            "feature.auto_lock",
            "feature",
            "Auto Lock / 自动锁定",
            "示例开关。",
            true));
    }
}
```

## 4. 默认安装方式

`SettingsKit` 提供 `InstallDefaultProviders`，可以一次接入常见页面：

- Gameplay / 游戏设置
- Audio / 声音设置
- Graphics / 画面设置
- Input / 键位设置
- Language / 语言设置

```csharp
SettingsKit.ConfigureStorage(new PlayerPrefsSettingsStorage());

SettingsKit.InstallDefaultProviders(new DefaultSettingsInstallOptions
{
    AudioAdapter = new AudioKitSettingsAdapter(),
    GraphicsAdapter = new UnityGraphicsSettingsAdapter(),
    LanguageAdapter = myLanguageAdapter,
    InputAdapter = myInputAdapter,
    AdditionalProviders = new ISettingsPageProvider[]
    {
        new MyFeatureSettingsProvider()
    }
});

SettingsKit.Init();
```

## 5. 默认 UI

核心程序集自带 `SettingsMenuOverlay`。

它是一个运行时 `OnGUI` 菜单，适合下面几类场景：

- 快速验证设置系统是否接通
- 工具型或开发期菜单
- Example / Sample 场景
- 项目早期原型

使用方式：

```csharp
var overlay = gameObject.AddComponent<SettingsMenuOverlay>();
overlay.title = "Game Settings";
overlay.visibleOnStart = true;
overlay.toggleKey = KeyCode.F10;
```

如果项目最终使用 `UIKit` 或别的正式 UI，只需要复用 `SettingsKit` 的数据层和策略层即可。

## 6. 存储策略

默认实现是 `PlayerPrefsSettingsStorage`。

如果你希望改成下面这些方案，直接实现 `ISettingsStorage` 即可：

- `ConfigKit`
- JSON 文件
- 云存档
- 平台账号存储

## 7. Example 与 ToolHub 入口

- 运行时代码：
  [Example_SettingsKit.cs](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Example_SettingsKit/Example_SettingsKit.cs>)
- 扩展示例：
  [ExampleSettingsExtensionsProvider.cs](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Example_SettingsKit/ExampleSettingsExtensionsProvider.cs>)
- 可运行场景：
  [SettingsKit_Playable.unity](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Scenes/SettingsKit_Playable.unity>)
- 编辑器入口：
  `StellarFramework -> Tools Hub -> 框架核心 -> SettingsKit 设置中心`

## 8. 当前边界

`SettingsKit` 已覆盖设置系统的核心职责，但以下内容仍然保持可替换：

- 最终菜单外观
- 本地化插件
- 输入系统实现
- 更复杂的“确认后回滚画面设置”交互
- 云端同步策略

这部分刻意没有写死，目的是让框架保持横向扩展能力。
