# StellarFramework

面向 Unity 项目的通用、模块化、可按需导出的生产框架。

> 本仓库是 **General 用户发布仓**。正式研发只在 `StarrDream/StellarFramework.Dev` 进行；高级算法、World、Flow 与 HybridCLR 热更新能力发布在 `StarrDream/StellarFramework.Extensions`。

当前发布版本：`1.0.0`
来源：`StellarFramework.Dev@48491db8044172dce4eb8dd41d8b6fa2b09cf648`

## 适合直接使用的能力

- Architecture / MSV、EventKit、PoolKit、SingletonKit、TimeKit、FSMKit、ActionKit。
- ConfigKit、SettingsKit、SaveKit、LogKit、HttpKit。
- UIKit、UIAdaptationKit、LocalizationKit、AudioKit。
- ResKit，以及 Resources / AssetBundle / Addressables / YooAsset 后端。
- Tools Hub、Kit Export、单文件 Architecture / Extensions 导出。

## 快速开始

1. 使用 Unity `2022.3 LTS`（当前研发基线 `2022.3.62f3c1`）打开仓库。
2. 等待 Package Manager 完成依赖解析。
3. 打开 `StellarFramework -> Tools Hub`。
4. 从 Quick Start 或 `StellarFramework -> Export` 选择需要的 Kit。

框架仍坚持按需使用：不要为了“统一”把项目不需要的 Kit 全部装进去。

## Extensions

以下能力从 General 中独立发布，但仍与本仓使用同一套公开 Contract：

- **Algorithms**：GridKit、SpatialKit、PathKit、SimulationKit。
- **World**：WorldKit、WorldGenKit、PlacementKit 与相关 Adapter。
- **Flow**：FlowKit Core / Unity Integration / Editor Tooling。
- **HotUpdate**：HybridCLRKit 与 HotUpdate Publisher。

安装高级能力请使用 `StarrDream/StellarFramework.Extensions`。General Runtime 不依赖 Extensions Runtime。

## 架构原则

- Model 保存状态；Service 承担业务规则与状态变化；View 只负责表现与输入转换。
- Unity、第三方 SDK、资源后端、平台差异通过 Adapter 隔离。
- Core Kit 不反向依赖 Adapter。
- 依赖必须显式；真实失败不得通过 catch-all 或静默 fallback 伪装为成功。
- 热路径避免不必要分配、LINQ / 反射扫描和隐式生命周期。

## 文档

各 Kit 的使用文档随其所属发布仓提供。General 中可直接从 `Assets/StellarFramework/FrameworkDoc/02-Kits` 查看通用 Kit 文档。

## 仓库关系

```text
StellarFramework.Dev              唯一研发源
        |\
        | \----> StellarFramework.Extensions   高级扩展发布
        |
        +------> StellarFramework              通用用户发布
```

Bug 与功能修改应先进入 Dev，通过验证后再重新发布到用户仓。
