# StellarFramework

StellarFramework 是面向 Unity 项目的模块化 C# 框架。此仓库包含可运行的 Tank Arena 案例和 Kit 导出工程；可以先运行案例，再从 Tools Hub 选择需要的能力导出为 unitypackage。

发布版本：**1.0.3**

Dev 源码提交：[d4c9ad1292cfec8c19a057ccaefb72a555fb8fdd](https://github.com/StarrDream/StellarFramework.Dev/commit/d4c9ad1292cfec8c19a057ccaefb72a555fb8fdd)

## 框架主体介绍

在 Unity Hub 中添加并打开本仓库，等待 Unity 完成导入。可先运行 Tank Arena，再从 **StellarFramework → Export** 选择单个 Kit、组合 Profile 或资源后端，导出到自己的 Unity 工程。导入后按包内依赖清单安装所需 UPM 包。

案例场景：`Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`。脚本、预制体和美术资源均随 Sample 提供；玩法与 Kit 协作见 [Tank Arena 案例说明](Assets/StellarFramework/Samples/TankArena/CaseStudy.md)。

## 环境要求

- Unity Editor 2022.3.62f3c1
- 首次打开需访问 Unity Package Manager，以解析 `Packages/manifest.json`
- 运行案例或导出 Kit 需要 Unity Editor；阅读文档不需要安装 Unity

## 框架概念

| 概念 | 说明 |
| --- | --- |
| Runtime | 框架运行时基础；项目按需加入 Kit |
| Kit | 按职责拆分的能力，例如 ResKit、UIKit、LocalizationKit |
| Adapter / Provider | 为 Kit 对接具体后端；资源加载、内容更新和代码更新可以分别选择 |
| Profile | 一组可导出的 Kit 与 Adapter 配置，可单独选择或组合 |
| Tools Hub | Unity Editor 内的配置、构建、诊断和导出工具，不进入游戏 Player |

ResKit 提供统一资源入口。Resources、AssetBundle、Addressables (AA) 和 YooAsset 是可选加载后端；YooAsset 内容更新 Provider 与 HybridCLR 代码更新 Provider 分别管理资源内容和代码程序集。

## 架构介绍

`StellarFramework.cs` 定义 MSV 基础架构。`Architecture<T>` 注册 Model 和 Service，管理初始化、查询与销毁；View 通过只读架构接口读取 Model，并把交互交给 Service。Service 承担应用操作并更新 Model。架构只定义模块职责和访问边界，不规定状态通知实现。

~~~mermaid
flowchart LR
    Startup["游戏启动"] -->|"Init / 生命周期"| Architecture["Architecture<T><br/>注册、查询、生命周期"]
    Architecture -->|"注册 / 初始化"| Model["Model<br/>应用状态与数据"]
    Architecture -->|"注册 / 初始化"| Service["Service<br/>应用操作与业务流程"]
    View["View<br/>StellarView / Unity UI"] -->|"交互：调用"| Service
    Service -->|"读取 / 更新"| Model
    View -->|"只读查询"| Model
~~~

源码位于 `Assets/StellarFramework/Runtime/Core/Architecture/StellarFramework.cs`。阅读 [MSV 架构说明](https://github.com/StarrDream/StellarFramework.Dev/blob/d4c9ad1292cfec8c19a057ccaefb72a555fb8fdd/Assets/StellarFramework/FrameworkDoc/01-Architecture/Architecture/Architecture-MSV-架构说明文档-Guide.md) 或 [架构源码文档](https://github.com/StarrDream/StellarFramework.Dev/blob/d4c9ad1292cfec8c19a057ccaefb72a555fb8fdd/Assets/StellarFramework/FrameworkDoc/01-Architecture/Architecture/Architecture-MSV-架构源码文档-Guide.md)。

## Kit 介绍

按项目场景选择 Kit。Tools Hub 支持单 Kit、Profile 或多个后端组合导出。

| Kit | 简介 | 文档 |
| --- | --- | --- |
| LogKit | 分类日志与运行时诊断 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/LogKit/LogKit-PerformanceKit-说明文档-Guide.md) |
| EventKit | 类型化事件发布与订阅 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/EventKit/EventKit-事件系统-说明文档-Guide.md) |
| BindableKit | 可观察数据和变化通知 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/BindableKit/BindableKit-数据绑定-说明文档-Guide.md) |
| ActionKit | 组合与执行可复用动作 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/ActionKit/ActionKit-动作系统-说明文档-Guide.md) |
| FSMKit | 状态转换与状态机生命周期 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/FSMKit/FSMKit-状态机-说明文档-Guide.md) |
| TimeKit | 统一时间和计时控制 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/TimeKit/TimeKit-时间系统-说明文档-Guide.md) |
| PoolKit | 对象复用与分配管理 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/PoolKit/PoolKit-对象池-说明文档-Guide.md) |
| SingletonKit | 注册单例对象及生命周期管理 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/SingletonKit/SingletonKit-单例系统-说明文档-Guide.md) |
| ConfigKit | 项目配置加载和访问 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/ConfigKit/ConfigKit-配置系统-说明文档-Guide.md) |
| SaveKit | 存档读写和序列化适配 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/SaveKit/SaveKit-存档系统-说明文档-Guide.md) |
| SettingsKit | 玩家设置及存储适配 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md) |
| ResKit | 统一资源加载与释放 API | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md) |
| Resources / AssetBundle / Addressables (AA) | 可选加载后端，可单独导出或组合 | [后端说明](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md) |
| YooAsset | ResKit 资源后端；内容更新 Provider 管理版本、下载和缓存 | [资源说明](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md) |
| HybridCLR | ResKit 可选代码更新 Provider | [使用说明](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/CodeUpdate/HybridCLR/ResKit-CodeUpdate-HybridCLR-说明文档-Guide.md) |
| UIKit | UI 面板生命周期与打开、关闭 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/UIKit/UIKit-界面系统-说明文档-Guide.md) |
| UIAdaptationKit | 安全区和屏幕布局适配 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/UIAdaptationKit/UIAdaptationKit-说明文档-Guide.md) |
| LocalizationKit | 语言切换与 UGUI/TMP 本地化绑定 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/LocalizationKit/LocalizationKit-Guide.md) |
| AudioKit | 音效、音乐播放和资源接入 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/AudioKit/AudioKit-音频系统-说明文档-Guide.md) |
| HttpKit | 异步 HTTP 请求与响应处理 | [文档](Assets/StellarFramework/FrameworkDoc/02-Kits/HttpKit/HttpKit-网络请求-说明文档-Guide.md) |

## 发布链接

- [StellarFramework.Extensions 拓展仓](https://github.com/StarrDream/StellarFramework.Extensions)：Algorithms、World、Flow 扩展 Kit。
- [StellarFramework.Dev 开发仓](https://github.com/StarrDream/StellarFramework.Dev)：框架源码、工具和发布工程。

## 友情链接

- [QFramework 开源框架](https://github.com/liangxiegame/QFramework)
