# StellarFramework

StellarFramework 是面向 Unity 项目的模块化 C# 框架。本仓库提供可在 Unity Hub 中打开的完整工程：可以运行 Tank Arena 案例，也可以从导出器中选择需要的 Kit、适配器和工具，再将生成的 `.unitypackage` 导入自己的项目。

发布版本：**1.0.3**

源码提交：[fba5bb700554e5aafe6472e421bcb8523a84cb87](https://github.com/StarrDream/StellarFramework.Dev/commit/fba5bb700554e5aafe6472e421bcb8523a84cb87)

## 环境要求

- Unity Editor **2022.3.62f3c1**
- 首次打开时可访问 Unity Package Manager，以解析 `Packages/manifest.json` 中的依赖

## 框架概览

框架按“基础运行时 → 功能 Kit → Adapter / Provider → Unity 或第三方实现”分层。游戏代码调用 Kit 提供的接口；适配器负责连接具体后端。Tools Hub 属于 Editor 工具，用于安装、配置、诊断和导出，不是 Player 运行时依赖。

资源能力由 ResKit 统一管理。Resources、AssetBundle、Addressables（AA）和 YooAsset 提供可选的资源加载后端；YooAsset 内容更新和 HybridCLR 代码更新作为独立 Provider 接入 ResKit，可分别选择和组合。

| 能力范围 | 主要 Kit |
| --- | --- |
| 基础服务与流程 | LogKit、EventKit、TimeKit、PoolKit、SingletonKit、BindableKit、FSMKit、ActionKit |
| 数据与配置 | ConfigKit、SaveKit、SettingsKit |
| 资源与更新 | ResKit、Resources / AssetBundle / Addressables / YooAsset Adapter、YooAsset 内容更新、HybridCLR 代码更新 |
| UI 与表现 | UIKit、UIAdaptationKit、LocalizationKit、AudioKit |
| 网络 | HttpKit |

Kit 的可选组件、依赖和独立导出 Profile 以随仓 Kit Catalog 为准。

本仓库同时包含完整 Unity 示例工程，适合先运行案例了解框架，再通过导出器获取项目需要的部分。

## 快速开始

1. 克隆本仓库，或下载并解压 GitHub 提供的 ZIP。
2. 在 Unity Hub 中添加仓库目录并打开工程，等待资源导入和依赖解析完成。
3. 打开 `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`，等待脚本编译后点击 Play。
4. 手机上用左侧摇杆移动坦克、右侧摇杆控制炮塔；桌面可用 WASD、鼠标和空格操作。

Tank Arena 是带完整回合流程的框架案例，包含敌人波次、维修、暂停、结算、本地化、本地存档和屏幕适配。暂停面板的 SYSTEMS 页面会显示案例当前调用的 Kit。案例玩法、资源位置和 Kit 协作说明见 `Assets/StellarFramework/Samples/TankArena/CaseStudy.md`。要在自己的项目中使用单个 Kit，继续查看下方“将 Kit 导入自己的项目”。

编辑器预览使用本地编译的案例程序集，不会下载远端热更内容，也不代表已执行热更发布流程。

## 将 Kit 导入自己的项目

1. 在 Unity 中打开本仓库，从菜单 **StellarFramework → Export** 进入导出器。
2. 选择单个 Profile、常用组合，或在适配器列表中多选要一起使用的能力。
3. 检查导出摘要中的框架依赖和 UPM 包，然后导出 `.unitypackage`。
4. 在自己的 Unity 工程中选择 **Assets → Import Package → Custom Package…**，导入导出包并等待安装器完成依赖配置。

导出器会自动补齐框架内的硬依赖、合并重复依赖，并在包内附上依赖清单。Resources、AssetBundle 和 Addressables（AA）后端可分别选择；Resources + AssetBundle 等组合也可以合并导出。第三方 UPM 依赖按包内清单安装。每个 Kit 的使用条件与初始化示例见 `Assets/StellarFramework/FrameworkDoc/02-Kits`。

资源加载、资源内容更新和 C# 代码更新是 ResKit 中可分别选择的扩展：YooAsset 提供资源加载与内容更新能力，HybridCLR 提供代码加载能力。Addressables 适配器负责 ResKit 加载与释放，不负责 StellarFramework 的内容更新编排。设计与接入说明见 `Assets/StellarFramework/FrameworkDoc/01-Architecture/ResourceAndCodeUpdatePlugins.md`。

## 按任务查找

| 需要 | 文档入口 |
| --- | --- |
| 资源加载、AssetBundle、Addressables 或 YooAsset | `FrameworkDoc/02-Kits/Reskit` |
| 本地化与 TMP/UGUI 适配 | `FrameworkDoc/02-Kits/LocalizationKit` |
| 安全区与屏幕布局 | `FrameworkDoc/02-Kits/UIAdaptationKit` |
| UI 面板与资源加载策略 | `FrameworkDoc/02-Kits/UIKit` |
| 代码热更新与发布 | `FrameworkDoc/02-Kits/Reskit/CodeUpdate/HybridCLR` |
| Tools Hub 操作 | `FrameworkDoc/04-ToolsHub` |

Algorithms、World 和 Flow 扩展见 [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions)。Extensions 需要与本仓相同发布版本的 General。

## 发布内容

`RELEASE-MANIFEST.json` 记录本次发布对应的 Dev 提交、包含的 Profile、外部 UPM 依赖和文件数量。框架源码及发布模板由 [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev) 维护；使用者仓中的框架问题和源码改动请回到 Dev 处理。
