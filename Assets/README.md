# StellarFramework

StellarFramework 是一个 Unity 基础开发框架，提供架构分层、UI、资源加载、热更新、设置系统、日志系统以及配套的编辑器工具、样例和文档。

项目以 `Assets/StellarFramework` 为核心目录，当前包含运行时模块、`Tools Hub` 编辑器工具、`KitSamples` 样例、`ArchitectureDemo` 示例和各模块 Guide 文档。

## 简介

StellarFramework 主要整理 Unity 项目里常见的基础能力，减少重复接线工作，统一项目内的模块组织方式和工具入口。

当前仓库主要包含以下内容：

- 基础架构分层：`Architecture / Model / Service / View`
- UI 管理：`UIKit`
- 统一资源加载：`ResKit`
- Addressables 资源工作流与 HybridCLR 启动热更新：`HotUpdateKit`
- 设置、日志、对象池、事件、配置等通用模块
- 统一编辑器入口：`Tools Hub`
- 最小样例与完整示例
- 模块说明文档、源码导读文档、快速开始文档

## 特性

- 模块化组织，运行时与编辑器能力分开维护
- 提供基础架构层，便于拆分业务状态、业务逻辑和表现层
- 统一 `Resources / AssetBundle / Addressables / 自定义 Loader` 的资源加载入口
- 提供 Addressables 本地内置与远端热更工作流
- 提供 HybridCLR 启动期代码热更新链路
- 提供 `Tools Hub` 作为统一工具入口
- 提供 `KitSamples` 和 `ArchitectureDemo` 用于验证和参考
- 每个核心模块都配有对应 Guide 文档

## 运行环境

- Unity `2022.3 LTS`
- Unity `6000.x`

样例与工具链当前围绕以上版本维护，具体兼容情况建议结合项目所用渲染管线、Addressables 和 HybridCLR 版本自行验证。

## 安装方式

当前仓库内包含两种常见使用方式：

### 1. 直接作为项目目录使用

保留当前 `Assets` 目录结构，直接用 Unity 打开工程，然后从 `StellarFramework -> Tools Hub` 开始接入和验证。

### 2. 作为已有项目的框架目录接入

将 `Assets/StellarFramework` 及相关依赖目录接入到现有项目中，再通过 `Tools Hub`、样例和 Guide 文档逐步完成初始化。

如果使用单包安装方式，可以参考：

- [StellarFrameworkBootstrap README](StellarFrameworkBootstrap/README.md)

## 快速开始

1. 打开 Unity 菜单 `StellarFramework -> Tools Hub`
2. 进入 `Start Here -> Quick Start`
3. 点击样例构建，生成运行样例需要的场景、资源和默认配置
4. 运行 `UIKit_Playable.unity`
5. 运行 `ResKit_Playable.unity`
6. 如果需要资源热更新，再进入 `热更新 -> AA 配置与发布`

详细说明请阅读：

- [快速开始](StellarFramework/快速开始.md)
- [Tools Hub 使用手册](StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md)

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

## 模块说明

### Runtime

| 模块 | 说明 |
| --- | --- |
| `Architecture` | 基础架构分层，负责 `Model / Service / View` 的协作方式 |
| `UIKit` | UI 面板管理、页面栈和基础 UI 工作流 |
| `ResKit` | 统一资源加载入口，支持 `Resources / AssetBundle / Addressables / 自定义 Loader` |
| `HotUpdateKit` | 串联 Addressables 资源更新和 HybridCLR 代码热更新启动流程 |
| `SettingsKit` | 设置项注册、默认页、扩展页和存储能力 |
| `LogKit` | 日志输出和基础运行时诊断能力 |
| `PoolKit` | 常用对象池能力 |

### Editor

| 模块 | 说明 |
| --- | --- |
| `Tools Hub` | 统一编辑器工具入口，包含快速开始、样例构建、资源构建、热更新配置与发布、UIKit 工具和部分诊断能力 |

### Samples

| 模块 | 说明 |
| --- | --- |
| `KitSamples` | 单模块最小可运行样例，用于验证模块接线和使用方式 |
| `ArchitectureDemo` | 完整架构示例，用于演示 `Architecture / Model / Service / View / UI` 的协作链路 |

## 示例代码

```csharp
using StellarFramework;
using StellarFramework.Bindable;
using UnityEngine;

public sealed class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}

public sealed class PlayerModel : AbstractModel
{
    public readonly BindableProperty<int> Hp = new BindableProperty<int>(100);
}

public sealed class PlayerService : AbstractService
{
    public void TakeDamage(int damage)
    {
        PlayerModel model = GetModel<PlayerModel>();
        model.Hp.Value = Mathf.Max(0, model.Hp.Value - damage);
    }
}

public sealed class GameEntry : MonoBehaviour
{
    private void Awake()
    {
        GameApp.Interface.Init();
    }
}
```

## 文档

### 入门

- [快速开始](StellarFramework/快速开始.md)
- [Samples 总览](StellarFramework/Samples/README.md)
- [Tools Hub 使用手册](StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md)

### 核心模块

- [ResKit 统一资源说明](StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)
- [UIKit 界面系统说明](StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-说明文档-Guide.md)
- [HotUpdateKit 热更新说明](StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-说明文档-Guide.md)
- [SettingsKit 设置系统说明](StellarFramework/Runtime/Kits/SettingsKit/SettingsKit-设置系统-Guide.md)
- [SingletonKit 单例系统说明](StellarFramework/Runtime/Kits/SingletonKit/SingletonKit-单例系统-Guide.md)

### 进阶

- [ResKit Addressables Guide](StellarFramework/Runtime/Kits/Reskit/Loaders/AddressableLoader/ResKit-Addressables-可寻址资源-Guide.md)
- [ResKit AssetBundle Guide](StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/ResKit-AssetBundle-资源包-Guide.md)
- [HybridCLR 热更新 Guide](StellarFramework/Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md)
- [测试与验证说明](StellarFramework/Tests/Tests-源码文档-Guide.md)

## 样例

- [Samples 总览](StellarFramework/Samples/README.md)
- `KitSamples`：最小模块样例
- `ArchitectureDemo`：完整架构示例

## 说明

- `StellarFrameworkVerification` 主要用于框架验证和发布前检查
- 如果只需要单个模块，可以从对应 Kit 的 Guide 文档单独接入
- 当前推荐从 `Tools Hub` 和 `快速开始` 进入
