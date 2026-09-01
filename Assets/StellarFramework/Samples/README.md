# Samples / 样例总览

`Assets/StellarFramework/Samples` 是面向框架使用者的教学入口，包含架构教学和各 Kit 的最小样例；不承担自动化回归、Integration 或 Release 验收。

## 目录

- `ArchitectureDemo/`
  架构教学案例，用来演示 `Architecture / Model / Service / View / UI` 的协作链路。
- `KitSamples/`
  单个模块的最小可运行案例，用来学习接线、资源和调用方式。
  其中 `KitSamples/Scenes/GridKit_Playable.unity`、`KitSamples/Scenes/TimeKit_Playable.unity`、`KitSamples/Scenes/SaveKit_Playable.unity`、`KitSamples/Scenes/SimulationKit_Playable.unity` 与 `KitSamples/Scenes/PathKit_Playable.unity` 是不带资源/热更前置的基础闭环，可单独导出；GridKit 适配器样例随适配器包导出。

## 建议顺序

1. `../快速开始.md`
2. `KitSamples/Scenes/UIKit_Playable.unity`
3. `KitSamples/Scenes/ResKit_Playable.unity`
4. `KitSamples/README.md`
5. `KitSamples/Scenes/README.md`
6. `ArchitectureDemo/README.md`
7. `ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`
8. 对应模块目录下的 `English-中文-Guide.md`
9. `KitSamples/Scenes/GridKit_Playable.unity`

## 说明

- `KitSamples` 适合按模块查 API、看资源组织方式和验证最小闭环。
- `ArchitectureDemo` 适合在基础样例跑通后，再理解 `Architecture / Model / Service / View / UI` 的协作链路。
- `KitSamples/Editor` 里的构建器会补齐样例场景、测试配置和依赖资源。
- `SettingsKit_Playable.unity` 已加入 `KitSamples`，可直接验证设置系统的默认页、扩展页、存储和即时应用。
- `TimeKit_Playable.unity` 与 `SaveKit_Playable.unity` 各自只依赖对应 Kit；SaveKit 样例另外演示 DTO、RestoreAfter 与 V1→V2 迁移。
- `GridKit_Playable.unity` 只依赖 GridKit.Core，演示负坐标、row-major DenseGrid、Footprint 变换与 Occupancy 原子冲突。
- `SimulationKit_Playable.unity` 只依赖 SimulationKit.Core，演示 Game Tick 与 Frame Step 分离、Burst/Staggered 首次派发、固定预算、过期合并和显式同 tick Drain。
- `PathKit_Playable.unity` 只依赖 PathKit.Core，演示 Graph-first A*/Dijkstra、加权边、NoPath 与确定性输出；`PathKit_GridKitAdapter_Playable.unity` 通过独立适配器接入 GridKit。
- 自动化测试位于 `Assets/StellarFramework/Tests`；框架开发者的组合、Player 和发布前冒烟位于 `Assets/StellarFrameworkVerification`，两者都不属于用户样例。

验证架构与发布 Gate 见 `Assets/StellarFrameworkVerification/ValidationArchitecture.md`。

