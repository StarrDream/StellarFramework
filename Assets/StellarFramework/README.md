# StellarFramework

`Assets/StellarFramework` 是框架主目录，包含 Runtime Kit、MSV 架构、ToolsHub 编辑器工具、Samples、Tests、Generated 和框架默认资源。

这份 README 只做总入口。第一次接触框架先读快速开始；要接功能读说明文档；要维护源码读源码文档。

## 文档入口

- [快速开始](快速开始.md)
- [ToolsHub 使用手册](Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md)
- [ToolsHub 扩展开发手册](Editor/StellarToolsHub/StellarToolsHub-扩展开发-Guide.md)
- [ToolsHub 源码文档](Editor/StellarToolsHub/StellarToolsHub-源码文档-Guide.md)
- [架构说明文档](Runtime/Core/Architecture/Architecture-MSV-架构说明文档-Guide.md)
- [架构源码文档](Runtime/Core/Architecture/Architecture-MSV-架构源码文档-Guide.md)
- [Runtime 扩展源码文档](Runtime/Extensions/RuntimeExtensions-源码文档-Guide.md)
- [Samples 源码文档](Samples/Samples-源码文档-Guide.md)
- [Tests 源码文档](Tests/Tests-源码文档-Guide.md)
- [Generated 源码文档](Generated/Generated-源码文档-Guide.md)
- [Resources 说明与源码文档](Resources/Resources-说明与源码文档-Guide.md)

## Kit 说明文档

说明文档讲“怎么用”：定位、入口 API、最小模板、ToolsHub 关联、样例和常见问题。

- [ActionKit 说明文档](Runtime/Kits/ActionKit/ActionKit-动作系统-说明文档-Guide.md)
- [AudioKit 说明文档](Runtime/Kits/AudioKit/AudioKit-音频系统-说明文档-Guide.md)
- [BindableKit 说明文档](Runtime/Kits/BindableKit/BindableKit-数据绑定-说明文档-Guide.md)
- [ConfigKit 说明文档](Runtime/Kits/ConfigKit/ConfigKit-配置系统-说明文档-Guide.md)
- [EventKit 说明文档](Runtime/Kits/EventKit/EventKit-事件系统-说明文档-Guide.md)
- [FSMKit 说明文档](Runtime/Kits/FSMKit/FSMKit-状态机-说明文档-Guide.md)
- [HotUpdateKit 说明文档](Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-说明文档-Guide.md)
- [HttpKit 说明文档](Runtime/Kits/HttpKit/HttpKit-网络请求-说明文档-Guide.md)
- [LogKit / PerformanceKit 说明文档](Runtime/Kits/LogKit/LogKit-PerformanceKit-说明文档-Guide.md)
- [PoolKit 说明文档](Runtime/Kits/PoolKit/PoolKit-对象池-说明文档-Guide.md)
- [ResKit 说明文档](Runtime/Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)
- [SettingsKit 说明文档](Runtime/Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md)
- [SingletonKit 说明文档](Runtime/Kits/SingletonKit/SingletonKit-单例系统-说明文档-Guide.md)
- [UIKit 说明文档](Runtime/Kits/UIKit/UIKit-界面系统-说明文档-Guide.md)

## Kit 源码文档

源码文档讲“怎么实现”：源码位置、核心类型、关键方法、数据流、依赖关系、扩展点和测试入口。

- [ActionKit 源码文档](Runtime/Kits/ActionKit/ActionKit-动作系统-源码文档-Guide.md)
- [AudioKit 源码文档](Runtime/Kits/AudioKit/AudioKit-音频系统-源码文档-Guide.md)
- [BindableKit 源码文档](Runtime/Kits/BindableKit/BindableKit-数据绑定-源码文档-Guide.md)
- [ConfigKit 源码文档](Runtime/Kits/ConfigKit/ConfigKit-配置系统-源码文档-Guide.md)
- [EventKit 源码文档](Runtime/Kits/EventKit/EventKit-事件系统-源码文档-Guide.md)
- [FSMKit 源码文档](Runtime/Kits/FSMKit/FSMKit-状态机-源码文档-Guide.md)
- [HotUpdateKit 源码文档](Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-源码文档-Guide.md)
- [HttpKit 源码文档](Runtime/Kits/HttpKit/HttpKit-网络请求-源码文档-Guide.md)
- [LogKit / PerformanceKit 源码文档](Runtime/Kits/LogKit/LogKit-PerformanceKit-源码文档-Guide.md)
- [PoolKit 源码文档](Runtime/Kits/PoolKit/PoolKit-对象池-源码文档-Guide.md)
- [ResKit 源码文档](Runtime/Kits/Reskit/ResKit-统一资源-源码文档-Guide.md)
- [SettingsKit 源码文档](Runtime/Kits/SettingsKit/SettingsKit-设置系统-源码文档-Guide.md)
- [SingletonKit 源码文档](Runtime/Kits/SingletonKit/SingletonKit-单例系统-源码文档-Guide.md)
- [UIKit 源码文档](Runtime/Kits/UIKit/UIKit-界面系统-源码文档-Guide.md)

## 专题文档

- [ResKit Resources 后端](Runtime/Kits/Reskit/Loaders/ResourceLoader/ResKit-Resources-内置资源-Guide.md)
- [ResKit Addressables 后端](Runtime/Kits/Reskit/Loaders/AddressableLoader/ResKit-Addressables-可寻址资源-Guide.md)
- [ResKit AssetBundle 后端](Runtime/Kits/Reskit/Loaders/AssetBundleLoader/ResKit-AssetBundle-资源包-Guide.md)
- [AA 本地内置](Runtime/Kits/HotUpdateKit/AA-LocalBuiltIn-Guide.md)
- [AA 远端热更](Runtime/Kits/HotUpdateKit/AA-RemoteHotUpdate-Guide.md)
- [HotUpdateManifest](Runtime/Kits/HotUpdateKit/HotUpdateManifest-Guide.md)
- [HybridCLR 热更新](Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md)
- [UniTask 异步任务规范](Runtime/Kits/StellarFramework-UniTask-异步任务-Guide.md)

## 推荐阅读顺序

1. 新人跑通项目：读 [快速开始](快速开始.md)，打开 ToolsHub 的 `Quick Start`，构建并运行样例。
2. 接业务功能：按 Kit 说明文档复制最小模板。
3. 接资源和热更：读 ResKit、HotUpdateKit、AA 本地内置和 AA 远端热更。
4. 读源码和改框架：按对应模块源码文档定位关键类型、数据流和测试入口。
5. 扩展编辑器工具：读 ToolsHub 使用手册，再读 ToolsHub 扩展开发手册和源码文档。

## 目录职责

- `Runtime/Core`：MSV 架构、CoroutineRunner、Runtime 扩展方法。
- `Runtime/Kits`：框架功能 Kit，每个 Kit 保持说明文档和源码文档双轨。
- `Editor/StellarToolsHub`：统一编辑器工具入口、工具模块和文档中心。
- `Samples`：可运行样例、样例构建器生成的场景与资源。
- `Tests`：EditMode 策略测试、文档测试、运行链路测试。
- `Generated`：工具生成的代码，例如 AssetMap 和 SingletonRegister。
- `Resources`：框架默认设置、UIRoot、示例配置和 Resources 后端示例资源。
