# StellarFramework

面向 Unity 项目的模块化框架。这个仓库是可直接打开的完整 Unity 工程，包含通用 Kit、Tools Hub 和入门样例。要把选定能力带入游戏项目，请在本工程中导出 Kit。

发布版本：1.0.0

源码提交：[03c9b93eb569b439a24206513784b29496dd62c2](https://github.com/StarrDream/StellarFramework.Dev/commit/03c9b93eb569b439a24206513784b29496dd62c2)

## 环境要求

- Unity Editor **2022.3.62f3c1**
- 本仓 <code>Packages/manifest.json</code> 中声明的 UPM 依赖
- 首次打开时需要 Unity Package Manager 完成依赖解析

## 运行入门样例

1. 克隆本仓，或从 GitHub 下载 ZIP 并解压。
2. 在 Unity Hub 中添加并打开仓库目录。
3. 等待资源导入和 Package Manager 完成。
4. 从菜单 **StellarFramework → Tools Hub** 打开工具中心，在 Start Here 查看入门说明。
5. 打开 <code>Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity</code>，等待编译完成后点击 Play。

Tank Arena 是框架的可玩整体示例，覆盖坦克战斗、战局结算和本地存档，并展示 Architecture、BindableKit、FSMKit、ActionKit、EventKit、PoolKit、ConfigKit、SaveKit、SettingsKit、LocalizationKit 与 UIAdaptationKit 的实际协作。热更新发布需单独运行 Android / Windows Release Gate；Editor 预览不会下载远端包。逐 Kit 的 API 与完整接入步骤见 <code>Assets/StellarFramework/FrameworkDoc/02-Kits</code>。

## 导出 Kit 到游戏项目

1. 从菜单 **StellarFramework → Export** 打开 Kit 导出窗口。
2. 选择一个 Kit Profile，或选择一个 Recommended Profile。
3. 检查导出摘要中的框架依赖和 UPM 包。
4. 导出 <code>unitypackage</code>，在目标 Unity 项目中通过 **Assets → Import Package → Custom Package…** 导入。
5. 保留包内 <code>.meta</code> 文件，并按导出摘要配置目标项目的 UPM 依赖。

Profile 描述可单独选择的 Kit、Adapter、Editor 工具或支持文件。导出器会补齐 Catalog 中声明的框架依赖；目标项目仍需自行解析所列 UPM 包。完整依赖和配置说明见 <code>Assets/StellarFramework/FrameworkDoc/02-Kits</code>。

## 按用途查找

| 需求 | 起点 |
| --- | --- |
| 资源加载 | ResKit.Core；需要时再加 AssetBundle、Addressables 或 YooAsset Adapter |
| 本地化 | LocalizationKit.Core；按 UI 系统选择 UGUI 或 TMP Adapter |
| 屏幕与安全区适配 | UIAdaptationKit.Core；UIKit 为可选组合 |
| UI 界面 | UIKit |
| 配置、存档、事件、对象池等基础能力 | 对应 Kit 使用文档 |

Algorithms、World、Flow 和 HybridCLR 热更新属于扩展仓能力，见 [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions)。Extensions 需要与本仓匹配的 General 版本。

## 文档与发布信息

- Kit 用法：<code>Assets/StellarFramework/FrameworkDoc/02-Kits</code>
- Tools Hub：<code>Assets/StellarFramework/FrameworkDoc/04-ToolsHub</code>
- 样例目录：<code>Assets/StellarFramework/Samples</code>
- 精确发布范围和外部依赖：[RELEASE-MANIFEST.json](RELEASE-MANIFEST.json)

本仓由 [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev) 生成。功能问题和源码改动请提交到 Dev 工程。
