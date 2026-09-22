# StellarFramework

## 中文

StellarFramework 是一个 Unity 基础开发框架，包含架构分层、UI、资源加载、配置、事件、设置、热更新和配套编辑器工具。

## 概览

本工程包含框架源码、Kit 导出器、Tools Hub、样例和验证内容。业务项目通过导出包按需接入框架能力。

- `Assets/StellarFramework/Tests` 是自动化 Behavior、Performance 和 Framework Policy 验证。
- `Assets/StellarFramework/Samples` 是面向使用者的 Kit 教学样例，不是完整业务 Demo。
- `Assets/StellarFrameworkVerification` 是维护者专用的 Integration、Player、Release 验证区，不会分发给使用者。
- `GameHotUpdate` 按 Runtime Delivery Example / Verification Fixture 处理，不作为普通 Sample。
- `StellarFramework Export` 用于导出 Recommended Profile、单 Kit、组合 Kit、样例包和独立 `Architecture.cs` / `Extensions.cs`。
- `StellarFramework.unitypackage` 用于完整框架的一键安装。

## 运行环境

- Unity `2022.3 LTS`
- Unity `6000.x`

## 使用方式

### 打开框架工程

使用 Unity `2022.3.62f3c1`（或兼容的 Unity 2022.3 LTS / Unity 6000.x）打开工程根目录。首次打开会解析 UniTask、Addressables、HybridCLR 等 UPM 依赖。

导出入口：`StellarFramework -> Export`。

### 按需导出

导出器会自动合并所选 Kit 的依赖，并在包旁生成依赖说明。

| 目标 | 导出内容 |
| --- | --- |
| 架构或静态扩展 | `Architecture.cs`、`Extensions.cs`，不引入 Kit |
| UI | `UIKit.Core`；默认使用 Resources，不依赖 ResKit |
| 本地化 | 只缺领域能力时导出 `LocalizationKit.Core`；`Localization Complete` 默认包含 UGUI + TMP 扫描/绑定、翻译 Workspace 与 JSON/CSV 外部翻译交换 |
| 资源加载 | `ResKit.Core`、`ResKit.AssetBundle`、`ResKit.Addressables` 或 `ResKit.YooAsset` |
| 代码热更 | `HybridCLRKit`；内容版本/下载由项目的 YooAsset 启动层负责 |
| 网格基础能力 | `GridKit`，无必需 Kit 或 UPM 依赖 |
| 世界组织基础 | `WorldKit.Core`，有限/无限 Chunk、强类型数据层、Dirty/Delta；无必需 Kit 或 UPM 依赖 |
| 世界数据生成 | `WorldGenKit.Core`，强类型 Channel/Storage、Stage DAG Compiler、确定 Seed、Rule 原语与 GenerationReport；无必需 Kit 或 UPM 依赖 |
| 世界生成 Builtins | `WorldGenKit.Builtins`，Height/Moisture/Water/Slope/Biome/Surface/Buildable；只依赖 WorldGenKit.Core |
| 世界导入与编辑 | `WorldGenKit.Authoring`，Typed Import、Sparse Override、Height/Biome/Surface 编辑与 Dirty Region 局部重算；只依赖 Core + Builtins |
| 世界资源生成 | `WorldGenKit.Resources`，Stable-ID Resource、Density/Coverage、Cluster/Richness、Budget/MinSpacing/Occupancy；**只依赖 WorldGenKit.Core** |
| 世界 Feature / POI | `WorldGenKit.Feature`，Landmark/Area/Compound、Quota、Reservation、Terrain Adaptation、Compound Layout；只依赖 WorldGenKit.Core |
| 通用放置验证 | `PlacementKit.Core`，Footprint、Slope/Water/Zone/Conflict/Connection 规则与显式失败原因；零依赖 |
| Feature 可选组合 | Resources / Placement / Authoring / WorldKit / SaveKit 五个独立 Adapter，按项目需要选装 |
| WorldGen Unity 表现 | DebugTexture / Mesh / Tilemap / UnityTerrain 四个独立 Adapter；同一 WorldData 可选 2D/3D 输出，Core 不引用 Unity 表现类型 |
| 无限世界流送 | `WorldKit.Streaming`：Generation Region、Demand Policy、Metadata/Data/Simulation/Presentation 分级；WorldGen / SaveKit / Unity Floating Origin 分别独立适配 |
| 连续二维空间索引 | `SpatialKit`，动态点索引、矩形/圆形查询和有限半径最近邻；无必需 Kit 或 UPM 依赖 |
| 批量模拟调度 | `SimulationKit`，索引最小堆、固定预算派发、分散首次派发与过期合并；无必需 Kit 或 UPM 依赖 |
| 通用路径搜索 | `PathKit`，Graph-first A* / Dijkstra、正 long 成本、边界预算与原子路径输出；V1 Core Semantics 已冻结，GridKit 通过可选适配器接入 |
| 声明式工作流 | `FlowKit.Core` 纯 C# 工作流运行时；`FlowKit.UnityIntegration` 提供可选 Unity Host/Binding，不依赖 UniTask、Addressables 或 HybridCLR |

完整组合位于 `StellarFramework -> Export -> 02 完整功能`，当前提供 `Localization Complete`、`ResKit Complete` 与 `UIKit Complete`；其中 UIKit Complete 默认组合 ResKit 与 UIKit.Adaptation。 `Hot Update Full` 位于 `03 扩展功能`。这些都只是经过验证的 Profile 组合，不会创建新的 Runtime 模块。

### 单包安装

- 导入 `StellarFramework.unitypackage`。
- Bootstrap 安装窗口会自动弹出。
- 点击 `一键安装 StellarFramework`。如果手动关闭，可从 `Window -> StellarFramework Bootstrap Installer` 重新打开。

详细说明：

- [StellarFrameworkBootstrap README](Assets/StellarFrameworkBootstrap/README.md)

## 快速开始

1. 打开 `StellarFramework -> Tools Hub`。
2. 进入 `Start Here -> Quick Start`。
3. 执行样例构建。
4. 运行 `UIKit_Playable.unity` 或 `ResKit_Playable.unity`。

详细说明：

- [快速开始](Assets/StellarFramework/FrameworkDoc/00-Overview/快速开始.md)
- [ToolsHub 说明文档](Assets/StellarFramework/FrameworkDoc/04-ToolsHub/StellarToolsHub-说明文档-Guide.md)

## 验证与发布

验证职责、目录边界、EditMode/PlayMode 选择、Samples 与 Verification 分工，以及 Local/Framework/Release Gate 见：

- [验证架构与发布验收规范](Assets/StellarFrameworkVerification/ValidationArchitecture.md)
- [维护者验证区](Assets/StellarFrameworkVerification/README.md)
- [导出验证矩阵（Evidence Ledger）](Assets/StellarFramework/FrameworkDoc/08-Validation/KitExportValidationMatrix.md)

## 架构

StellarFramework 以 `Architecture` 作为基础架构层，核心组织方式是：

- `Model` 负责状态与数据
- `Service` 负责业务逻辑与系统能力
- `View` 负责表现层交互

在这套基础分层之上，`UIKit`、`ResKit`、`SettingsKit` 等 Kit 可按需接入项目运行时；Addressables / YooAsset 是 ResKit 的可选资源后端，`HybridCLRKit` 是独立的代码热更新扩展。`Tools Hub` 和各类 Editor Modules 负责样例、资源构建、代码热更新产物导出、代码生成和调试辅助。

整体上可以理解为三层：

- `Architecture`：项目主架构
- `Runtime Kits`：功能模块
- `Editor Modules / Tools Hub`：编辑器工作流与辅助工具

Runtime Kit 按架构职责分为 Foundation、Extension 与 Adapter Profile；这只影响依赖约束和导出器展示，不代表默认安装。详细规则见 [Kit 架构分层与依赖规则](Assets/StellarFramework/FrameworkDoc/01-Architecture/KitArchitectureGuide.md)。

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
| `UIKit` | UI 面板管理与页面栈；可选 Adaptation 提供 Safe Area / Aspect Breakpoint / 多尺寸适配 |
| `ResKit` | `Resources / AssetBundle / Addressables / YooAsset / 自定义 Loader` 统一加载入口 |
| `HybridCLRKit` | 独立的启动期代码热更新；通过 ResKit 读取 Manifest、DLL 与 AOT metadata |
| `SettingsKit` | 设置项注册、扩展页、存储 |
| `LogKit` | 日志输出与诊断 |
| `PoolKit` | 对象池 |
| `SingletonKit` | 单例生命周期与注册 |
| `TimeKit` | 游戏世界时间、日历换算与高性能定时调度 |
| `SaveKit` | 可靠游戏存档、Section 分区、版本迁移、事务写入、备份恢复与可扩展 Serializer / Storage |
| `GridKit` | 负坐标网格、半开矩形、连续 DenseGrid、Footprint 变换与原子 Occupancy |
| `WorldKit` | 有限/无限平面世界组织、Chunk 生命周期、强类型 World/Region/Chunk 数据层、Dirty 与 Runtime Delta |
| `WorldGenKit` | 可扩展强类型世界数据生成 Pipeline、六类 Storage、确定性 Seed、Rule 原语与编译诊断；Core 不依赖 WorldKit |
| `WorldGenKit.Builtins` | Planar Height/Moisture/Water/Slope + Stable-ID Biome/Surface + Buildable 的可选基础生成闭环 |
| `WorldGenKit.Authoring` | Imported/Generated Base + Sparse Authoring Override、Height 编辑、Stable-ID Semantic Paint 与局部派生重算 |
| `WorldGenKit.Resources` | 独立 Core-only Resource Scatter：通用 Eligibility/Suitability → Candidate → Budget/Spacing/Occupancy → SpawnRecord |
| `WorldGenKit.Feature` | Landmark/Area/Compound Feature、确定性 Resolver、Reservation、Quota、Terrain Adaptation 与语义 Compound Layout |
| `WorldGenKit Presentation Adapters` | Dense Channel → Debug Texture / heightfield Mesh / Tilemap / Unity Terrain；四个可独立导出的 Unity Adapter |
| `PlacementKit` | 零依赖通用 Placement validation：Footprint、规则、Failure ID、Suitability 与自定义 Context |
| `SpatialKit` | 连续二维点、均匀空间哈希、矩形/圆形查询与最近邻 |
| `SimulationKit` | 纯 C# 批量模拟调度、固定预算派发、分散首次派发与过期合并 |
| `PathKit` | Graph-first 通用最短路径、A* / Dijkstra、确定性 tie-break、成本溢出保护；V1 Core Semantics 已冻结，GridKit 为可选适配器 |
| `FlowKit` | Graph JSON → Migration/Validation → immutable Plan → Scheduler/Runner；Signal、State、Blackboard、Timer、Operation、Parallel/Race/Join 与快照 |

### Editor Modules

| 模块 | 说明 |
| --- | --- |
| `Tools Hub` | 快速开始、资源构建、HybridCLR 产物导出、诊断工具 |
| `ActionKit` | ActionKit 编辑器支持 |
| `Addressables` | ResKit Addressables 后端的本地配置、Group 检查与 Player Content 构建 |
| `AudioKit` | AudioKit 工具入口 |
| `ConfigKit` | 配置工具入口 |
| `DevTools` | 调试与开发辅助工具 |
| `EventKit` | EventKit 工具入口 |
| `Packaging` | 打包与发布辅助 |
| `ResKit` | 资源构建与资源审计 |
| `SettingsKit` | 设置中心工具 |
| `UIKit` | UI 绑定生成与 UIKit 工具 |
| `UIKit UI适配` | Safe Area / Breakpoint Profile、常见屏幕 Preview 与 Anchor 风险检查 |
| `Localization 本地化` | UI 扫描绑定、稳定 BindingId、Translation Matrix、JSON/CSV 外部翻译交换与校验 |
| `Localization TMP` | 可选 TextMeshPro 本地化扫描与稳定 BindingId 绑定 |
| `FlowKit` | Graph Validator 与独立 FlowKit Graph 窗口（仅框架开发工程） |
| `World Framework` | Editor-only World/Profile/Pipeline/Biome/Resource/Feature/Placement Authoring、Heatmap、Validator 与 Runtime diagnostics |

### Samples

| 模块 | 说明 |
| --- | --- |
| `KitSamples` | 单模块最小可运行样例 |
| `ArchitectureDemo` | 完整架构示例 |

## 文档

- [快速开始](Assets/StellarFramework/FrameworkDoc/00-Overview/快速开始.md)
- [Samples 总览](Assets/StellarFramework/Samples/README.md)
- [ToolsHub 说明文档](Assets/StellarFramework/FrameworkDoc/04-ToolsHub/StellarToolsHub-说明文档-Guide.md)
- [ResKit 统一资源说明](Assets/StellarFramework/FrameworkDoc/02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)
- [UIKit 界面系统说明](Assets/StellarFramework/FrameworkDoc/02-Kits/UIKit/UIKit-界面系统-说明文档-Guide.md)
- [SettingsKit 设置系统说明](Assets/StellarFramework/FrameworkDoc/02-Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md)
- [HybridCLRKit 代码热更新说明](Assets/StellarFramework/FrameworkDoc/02-Kits/HybridCLRKit/HybridCLRKit-代码热更新-说明文档-Guide.md)
- [GridKit 网格系统说明](Assets/StellarFramework/FrameworkDoc/02-Kits/GridKit/GridKit-网格系统-说明文档-Guide.md)
- [GridKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/GridKit/GridKit-网格系统-源码文档-Guide.md)
- [WorldKit 世界组织说明](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldKit/WorldKit-世界组织系统-说明文档-Guide.md)
- [WorldKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldKit/WorldKit-世界组织系统-源码文档-Guide.md)
- [WorldGenKit 世界生成说明](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-世界生成系统-说明文档-Guide.md)
- [WorldGenKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-世界生成系统-源码文档-Guide.md)
- [WorldGenKit.Builtins 地形/Biome/Surface 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-Builtins-地形生物群系表面-Guide.md)
- [WorldGenKit.Authoring 导入与手工编辑指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-Authoring-导入与手工编辑-Guide.md)
- [WorldGenKit.Resources 资源生成与占用指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-Resources-资源生成与占用-Guide.md)
- [WorldGenKit.Feature 地标与 POI 生成指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-Feature-地标与POI生成-Guide.md)
- [WorldGenKit.DebugTextureAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-DebugTextureAdapter-Guide.md)
- [WorldGenKit.MeshAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-MeshAdapter-Guide.md)
- [WorldGenKit.TilemapAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-TilemapAdapter-Guide.md)
- [WorldGenKit.UnityTerrainAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-UnityTerrainAdapter-Guide.md)
- [WorldKit.Streaming 无限世界流送指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldKitStreaming/WorldKitStreaming-无限世界流送-Guide.md)
- [WorldGenKit.StreamingAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldGenKit/WorldGenKit-StreamingAdapter-Guide.md)
- [WorldKit.Streaming.SaveKitAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldKitStreaming/WorldKitStreaming-SaveKitAdapter-Guide.md)
- [WorldKit.Streaming.UnityAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/WorldKitStreaming/WorldKitStreaming-UnityAdapter-Guide.md)
- [WorldFramework.ToolsHub 生产 Authoring 与诊断指南](Assets/StellarFramework/FrameworkDoc/04-ToolsHub/WorldFramework-ToolsHub-生产Authoring-Guide.md)
- [World Framework 性能 / Release Matrix](Assets/StellarFramework/FrameworkDoc/06-WorldFramework/WorldFramework-Performance-Release-Matrix.md)
- [LocalizationKit 本地化系统指南](Assets/StellarFramework/FrameworkDoc/02-Kits/LocalizationKit/LocalizationKit-Guide.md)
- [GridKit.UnityProjectionAdapter 指南](Assets/StellarFramework/FrameworkDoc/02-Kits/GridKitUnityProjection/GridKit-UnityProjectionAdapter-Guide.md)
- [PlacementKit 通用放置规则指南](Assets/StellarFramework/FrameworkDoc/02-Kits/PlacementKit/PlacementKit-通用放置规则-Guide.md)
- [SpatialKit 空间索引说明](Assets/StellarFramework/FrameworkDoc/02-Kits/SpatialKit/SpatialKit-空间索引-说明文档-Guide.md)
- [SpatialKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/SpatialKit/SpatialKit-空间索引-源码文档-Guide.md)
- [SimulationKit 批量调度说明](Assets/StellarFramework/FrameworkDoc/02-Kits/SimulationKit/SimulationKit-批量模拟调度-说明文档-Guide.md)
- [SimulationKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/SimulationKit/SimulationKit-批量模拟调度-源码文档-Guide.md)
- [PathKit 路径搜索说明](Assets/StellarFramework/FrameworkDoc/02-Kits/PathKit/PathKit-路径搜索-说明文档-Guide.md)
- [PathKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/PathKit/PathKit-路径搜索-源码文档-Guide.md)
- [FlowKit 工作流系统说明](Assets/StellarFramework/FrameworkDoc/02-Kits/FlowKit/FlowKit-工作流系统-说明文档-Guide.md)
- [FlowKit 源码文档](Assets/StellarFramework/FrameworkDoc/02-Kits/FlowKit/FlowKit-工作流系统-源码文档-Guide.md)
- [FlowKit 业务编程规范](Assets/StellarFramework/FrameworkDoc/02-Kits/FlowKit/FlowKit-业务编程规范-Coding-Contract-Guide.md)
- [FlowKit + MSV Production Pattern](Assets/StellarFramework/Samples/Integration/FlowKitMsvIntegration/README.md)
- [PathKit.GridKit 适配器说明](Assets/StellarFramework/FrameworkDoc/02-Kits/PathKit/PathKit-GridKit适配器-Guide.md)
- [验证架构与发布验收规范](Assets/StellarFrameworkVerification/ValidationArchitecture.md)
- [维护者验证区 README](Assets/StellarFrameworkVerification/README.md)
- [Kit 分发矩阵与生产验收基线](Assets/StellarFramework/FrameworkDoc/08-Validation/KitExportValidationMatrix.md)
- [Kit 架构分层与依赖规则](Assets/StellarFramework/FrameworkDoc/01-Architecture/KitArchitectureGuide.md)

## English

StellarFramework is a modular Unity development framework covering architecture layering, UI, resources, configuration, events, settings, hot update, world systems, workflow, editor tooling, samples, and release validation.

### Environment

- Unity `2022.3 LTS`
- Unity `6000.x`

The primary development baseline is Unity `2022.3.62f3c1`. First import resolves optional UPM dependencies such as UniTask, Addressables, and HybridCLR.

### Distribution model

Use `StellarFramework -> Export` to export only the Kits a project needs. The exporter resolves dependency closure and generates dependency documentation beside the package.

Examples of independently selectable capabilities include `UIKit.Core`, ResKit backends, `GridKit`, `SpatialKit`, `SimulationKit`, `PathKit`, `FlowKit`, `LocalizationKit.Core`, `WorldKit.Core`, `WorldGenKit.Core`, Builtins/Authoring/Resources/Feature, `PlacementKit.Core`, Streaming, and the independent Unity presentation adapters.

The export window presents delivery-oriented sections: atomic Basic Features, production-ready Complete Features, and optional Extension Features. `Localization Complete` includes both UGUI and TMP scan/bind workflows plus external JSON/CSV translation exchange; `UIKit Complete` composes ResKit and UIKit.Adaptation. `Hot Update Full` remains a composed extension. These are composition presets over atomic profiles, not new runtime modules. If a project only needs localization domain logic, export `LocalizationKit.Core` directly; it has no framework or UPM dependency.

A complete `StellarFramework.unitypackage` remains available for one-package installation.

### Quick start

1. Open `StellarFramework -> Tools Hub`.
2. Go to `Start Here -> Quick Start`.
3. Build the samples.
4. Run a sample such as UIKit, ResKit, TimeKit, GridKit, or one of the productized examples.

### Architecture

The framework follows the MSV boundary: Model owns state, Service owns business rules and mutates Models, and View presents state / forwards intent.

Runtime profiles are classified as Foundation, Extension, or Adapter for dependency discipline and exporter presentation. This classification does not imply default installation. Foundation profiles must not depend on Extension profiles.

`WorldFramework.ToolsHub` is Editor-only and provides production authoring/diagnostics for World/Profile/Pipeline/Biome/Resource/Feature/Placement workflows. `GridKit.UnityProjectionAdapter` and the other Unity adapters keep engine-facing responsibilities outside their pure C# cores.

### Major Runtime Kits

The framework includes `ActionKit`, `AudioKit`, `BindableKit`, `ConfigKit`, `EventKit`, `FSMKit`, `HttpKit`, `UIKit`, `ResKit`, `HybridCLRKit`, `SettingsKit`, `LogKit`, `PoolKit`, `SingletonKit`, `TimeKit`, `SaveKit`, `GridKit`, `SpatialKit`, `SimulationKit`, `PathKit`, `FlowKit`, `LocalizationKit`, `WorldKit`, `WorldGenKit`, `PlacementKit`, and their explicit adapters.

World responsibilities stay separated: WorldKit organizes world/chunk/data ownership, WorldGenKit generates typed deterministic data, PlacementKit evaluates generic placement rules, and Unity adapters project data to textures, meshes, Tilemaps, Terrain, or floating-origin scene coordinates.

### Samples and validation

- `Assets/StellarFramework/Samples` — user-facing runnable examples and explicit integration samples.
- `Assets/StellarFramework/Tests` — automated Behavior, Performance, and Framework Policy tests.
- `Assets/StellarFrameworkVerification` — maintainer-only Integration, Player, and Release validation.

Sample rules require real 2D/3D evidence for non-UI Kits, bilingual zh-CN/en-US UI, fixed language selectors labeled `中文` and `English`, deterministic scene Builders, and explicit distribution closure.

### Documentation

Formal documentation lives under `Assets/StellarFramework/FrameworkDoc`. Start with `FrameworkDoc/README.md`, the LocalizationKit guide, KitArchitectureGuide, and KitExportValidationMatrix. Historical milestone plans and handoff records are archived under `FrameworkDoc/09-Development`.

Local Kit/Sample README files remain concise bilingual navigation and operation entry points while long-form formal guides migrate into FrameworkDoc.
