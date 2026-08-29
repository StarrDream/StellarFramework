# StellarFramework

StellarFramework 是一个 Unity 基础开发框架，提供架构分层、UI、资源加载、热更新、设置系统，以及配套的编辑器工具、样例和文档。

## 运行环境

- Unity `2022.3 LTS`
- Unity `6000.x`

## 安装

### 直接打开工程

使用 Unity 打开当前工程即可。

### 接入已有项目

在原框架工程中打开 `StellarFramework -> Framework Source -> Kit Package Exporter`，按需选择 Kit、样例或独立源码文件。导出器会自动合并所选 Kit 的依赖，并在包旁生成依赖说明。

- 只需要架构或静态扩展：导出 `Architecture.cs`、`Extensions.cs`，不引入任何 Kit。
- 只需要 UI：导出 `UIKit.Core`；它默认使用 Resources，不依赖 ResKit。
- 需要资源加载：按需选 `ResKit.Core`、`ResKit.AssetBundle` 或 `ResKit.Addressables`。
- 需要代码热更：再显式选择 `HotUpdate.AddressablesAdapter` 与 `HotUpdate.HybridCLR`；不会被基础 Kit 隐式带入。

### 单包安装

- 可以使用仓库 `Release` 中已导出的 `unitypackage`
- 也可以使用当前工程自行导出 `StellarFramework.unitypackage`
- 导入后打开 `StellarFramework -> 安装 -> 单包安装器`

详细说明：

- [StellarFrameworkBootstrap README](Assets/StellarFrameworkBootstrap/README.md)

## 快速开始

1. 打开 `StellarFramework -> Tools Hub`
2. 进入 `Start Here -> Quick Start`
3. 执行样例构建
4. 运行 `UIKit_Playable.unity`
5. 运行 `ResKit_Playable.unity`

详细说明：

- [快速开始](Assets/StellarFramework/快速开始.md)
- [ToolsHub 说明文档](Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-说明文档-Guide.md)

## 架构

StellarFramework 以 `Architecture` 作为基础架构层，核心组织方式是：

- `Model` 负责状态与数据
- `Service` 负责业务逻辑与系统能力
- `View` 负责表现层交互

在这套基础分层之上，`UIKit`、`ResKit`、`SettingsKit` 等 Kit 可按需接入项目运行时；`HotUpdateKit`、Addressables 与 HybridCLR 是明确选择的可选层。`Tools Hub` 和各类 Editor Modules 负责样例构建、资源工作流、热更新配置、代码生成和调试辅助。

整体上可以理解为三层：

- `Architecture`：项目主架构
- `Runtime Kits`：功能模块
- `Editor Modules / Tools Hub`：编辑器工作流与辅助工具

## 目录结构

```text
Assets
├─ StellarFramework/                 核心运行时、编辑器模块、样例与文档
├─ StellarFrameworkBootstrap/        单包安装与引导内容
├─ StellarFrameworkVerification/     框架验证区
├─ GameHotUpdate/                    热更新示例资源
├─ AddressableAssetsData/            Addressables 配置
├─ StreamingAssets/                  示例运行资源
└─ Scenes/                           示例场景
```

## 模块

### Runtime Kits

| 模块 | 说明 |
| --- | --- |
| `Architecture` | `Model / Service / View` 基础架构分层 |
| `ActionKit` | 行为与时序动作能力 |
| `AudioKit` | 音频播放与管理 |
| `BindableKit` | 数据绑定 |
| `ConfigKit` | 配置读取与访问 |
| `EventKit` | 事件注册与派发 |
| `FSMKit` | 状态机 |
| `HttpKit` | HTTP 请求封装 |
| `UIKit` | UI 面板管理与页面栈 |
| `ResKit` | `Resources / AssetBundle / Addressables / 自定义 Loader` 统一加载入口 |
| `HotUpdateKit` | Addressables 资源更新与 HybridCLR 启动热更新 |
| `SettingsKit` | 设置项注册、扩展页、存储 |
| `LogKit` | 日志输出与诊断 |
| `PoolKit` | 对象池 |
| `SingletonKit` | 单例生命周期与注册 |

### Editor Modules

| 模块 | 说明 |
| --- | --- |
| `Tools Hub` | 快速开始、样例构建、资源构建、热更新配置与发布、诊断工具 |
| `ActionKit` | ActionKit 编辑器支持 |
| `Addressables` | AA 配置、构建、发布与热更新工作流 |
| `AudioKit` | AudioKit 工具入口 |
| `ConfigKit` | 配置工具入口 |
| `DevTools` | 调试与开发辅助工具 |
| `EventKit` | EventKit 工具入口 |
| `Packaging` | 打包与发布辅助 |
| `ResKit` | 资源构建与资源审计 |
| `SettingsKit` | 设置中心工具 |
| `UIKit` | UI 绑定生成与 UIKit 工具 |

### Samples

| 模块 | 说明 |
| --- | --- |
| `KitSamples` | 单模块最小可运行样例 |
| `ArchitectureDemo` | 完整架构示例 |

## 文档

- [快速开始](Assets/StellarFramework/快速开始.md)
- [Samples 总览](Assets/StellarFramework/Samples/README.md)
- [ToolsHub 说明文档](Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-说明文档-Guide.md)
- [ResKit 统一资源说明](Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)
- [UIKit 界面系统说明](Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-说明文档-Guide.md)
- [SettingsKit 设置系统说明](Assets/StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md)
- [HotUpdateKit 热更新说明](Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-说明文档-Guide.md)
- [Kit 分发矩阵与生产验收基线](Assets/StellarFramework/KitCatalog/KitExportValidationMatrix.md)
