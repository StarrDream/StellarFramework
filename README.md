# StellarFramework

StellarFramework 是一个面向 Unity 项目的框架工程，包含：

- `MSV / Architecture` 架构层
- `ResKit` 统一资源入口
- `UIKit` UI 管理
- `HotUpdateKit + HybridCLR` 热更新链路
- `Tools Hub` 编辑器工具入口
- 可直接运行的样例和验证测试

它更适合做团队自维护的项目底座，而不是零配置的外部成品包。

## 环境

- Unity `2022.3 LTS`

## 目录

- `Assets/StellarFramework`
  框架主体，包含 Runtime、Editor、Samples、Tests、Generated、Resources
- `Assets/StellarFrameworkBootstrap`
  单包安装器和内嵌 payload
- `Assets/StellarFrameworkVerification`
  发布前验证区，仅供框架维护时使用

## 快速上手

1. 用 Unity 打开工程
2. 打开菜单 `StellarFramework -> Tools Hub`
3. 进入 `Start Here -> Quick Start`
4. 点击 `构建样例`
5. 先运行 `UIKit_Playable.unity`
6. 再运行 `ResKit_Playable.unity`
7. 需要热更新时，再进入 `资源管理 -> AA 配置与发布`

## 你会主要接触到的东西

- `Architecture<T>`
  用来组织 Model / Service / View
- `ResKit`
  统一资源加载入口，按需接 `Resources / AssetBundle / Addressables / Custom`
- `UIKit`
  UI 打开、关闭、栈管理和异步加载入口
- `HotUpdateKit`
  资源热更新和代码热更新的统一入口
- `Tools Hub`
  样例构建、资源构建、AA 发布、文档索引和诊断工具

## 文档入口

- [快速开始](Assets/StellarFramework/快速开始.md)
- [Samples 总览](Assets/StellarFramework/Samples/README.md)
- [ToolsHub 使用手册](Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md)
- [ResKit 说明文档](Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)
- [UIKit 说明文档](Assets/StellarFramework/Runtime/Kits/UIKit/UIKit-界面系统-说明文档-Guide.md)
- [HotUpdateKit 说明文档](Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-说明文档-Guide.md)
- [HybridCLR 热更新专题](Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HybridCLR-热更新-Guide.md)

## 如果你是框架维护者

- [ToolsHub 源码文档](Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-源码文档-Guide.md)
- [Architecture 源码文档](Assets/StellarFramework/Runtime/Core/Architecture/Architecture-MSV-架构源码文档-Guide.md)
- [ResKit 源码文档](Assets/StellarFramework/Runtime/Kits/Reskit/ResKit-统一资源-源码文档-Guide.md)
- [HotUpdateKit 源码文档](Assets/StellarFramework/Runtime/Kits/HotUpdateKit/HotUpdateKit-热更新-源码文档-Guide.md)

## 当前定位

这个仓库已经可以作为中大型项目的内部框架工程使用，但更适合由团队自己维护和演进。
