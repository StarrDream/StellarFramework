# StellarFramework 框架介绍视频说明稿（5 分钟版）

> 定位：这是介绍视频口播稿，不是教程。重点讲框架特色、框架内容、ToolsHub，以及架构 Kit 的使用思路。

## 1. 开场

StellarFramework 是一套面向 Unity 项目的工程化开发框架。

它主要解决的不是某一个玩法问题，而是项目底层的基础设施问题：异步流程、资源加载、UI 管理、热更新、配置、设置、事件、音频、对象池、状态机，以及编辑器工具入口。

简单说，它是用来帮 Unity 项目把底座搭稳的。项目开始阶段就把这些基础能力统一起来，后面业务越做越多时，代码不会到处散，资源、UI、配置和热更新也不会各写各的。

## 2. 框架核心特色

StellarFramework 有几个比较明显的特色。

第一个特色，是全框架主要异步链路都以 UniTask 为基础。

资源加载、UI 打开、配置读取、网络请求、动作流程、热更新启动，整体都是 async/await 的写法。这样比传统 Coroutine 更容易组织流程，也更适合处理取消、对象销毁和异步加载这类 Unity 项目常见问题。

第二个特色，是框架内置热更新思路。

资源热更走 Addressables，负责 catalog、hash、bundle 的更新和缓存；代码热更走 HybridCLR，通过热更 DLL、AOT metadata 和 Manifest 进入热更逻辑。框架里的 HotUpdateKit 负责把这些步骤收口，包括 Manifest 读取、SHA256 校验、metadata 加载和热更入口调用。

第三个特色，是资源和 UI 都做了统一入口。

资源侧通过 ResKit 管理。业务层不需要直接关心资源来自 Resources、AssetBundle、Addressables，还是项目自定义 Loader。UI 侧通过 UIKit 管理，面板打开、关闭、缓存、页面栈和 UIRoot 初始化都由框架统一处理。

第四个特色，是它不只是 Runtime 代码，还带了一套编辑器工作台，也就是 ToolsHub。

## 3. ToolsHub 是什么

ToolsHub 是 StellarFramework 在 Unity 编辑器里的统一入口。

菜单路径是：

```text
StellarFramework -> Tools Hub
```

在这里可以完成很多框架相关操作，比如：

- Quick Start 上手入口
- 样例构建
- 文档中心
- ResKit 资源审计
- UIKit 工具
- ConfigKit 配置中心
- SettingsKit 设置中心
- AssetBundle 打包
- Addressables 配置与发布
- HybridCLR DLL 导出
- 单例注册表生成

它的价值是把框架能力集中起来。团队成员不用在项目目录和 Unity 菜单里到处找工具，新人也可以先从 Quick Start 开始跑通样例，再逐步了解各个 Kit。

ToolsHub 本身也是模块化设计，后续新增工具可以作为模块挂进去，不需要把所有编辑器工具都写死在一个窗口里。

## 4. 框架内容和 Kit

StellarFramework 的运行时能力是按 Kit 拆分的。

比较核心的几个 Kit 是：

- Architecture：提供基础架构层，按 Model、Service、View 拆分业务。
- ResKit：统一资源加载、缓存和释放。
- UIKit：统一 UI 面板生命周期和页面栈。
- HotUpdateKit：收口 Addressables 资源热更和 HybridCLR 代码热更。
- ConfigKit：管理配置读取和缓存。
- SettingsKit：管理设置项、设置页、保存、应用和回滚。
- EventKit：提供全局事件广播。
- BindableKit：提供可订阅数据，适合放在 Model 中驱动 UI。
- AudioKit：统一 BGM、音效和音量控制。
- ActionKit：处理短流程动作和简单动画链。
- FSMKit：轻量状态机。
- PoolKit：纯 C# 对象池。
- HttpKit：网络请求封装。
- LogKit：统一日志入口。
- SingletonKit：单例生命周期和注册管理。

这些 Kit 不要求一次性全部使用。项目可以先从 Architecture、ResKit、UIKit 这几条主链路接入，等需要热更新、设置、配置、音频或事件系统时，再逐步使用对应 Kit。

## 5. 架构 Kit 的使用思路

Architecture 是框架的业务组织基础。

它不是一个很重的业务框架，核心思路很简单：把状态、逻辑和表现拆开。

- Model 负责数据和状态。
- Service 负责业务逻辑和系统能力。
- View 负责界面表现和交互。

View 不应该到处直接改数据，而是读取 Model 的状态，再通过 Service 去驱动行为。这样可以减少 Unity 项目里常见的脚本互相引用、UI 直接改业务数据、逻辑散落在场景对象里的问题。

在实际项目里，可以把全局模块放在一个 GameApp 里，比如账号、设置、资源、主流程；如果有战斗、关卡、玩法模块，也可以按场景或玩法再拆出自己的架构入口。

这套架构的重点不是限制写法，而是给项目一个清晰的默认边界。

## 6. 总结

StellarFramework 的定位可以总结成一句话：

它是一套以 UniTask 为异步基础，内置资源管理、UI 管理、热更新、配置设置、事件音频等常用能力，并通过 ToolsHub 提供统一编辑器入口的 Unity 工程框架。

它的重点不是替你写具体玩法，而是把项目底层的通用能力整理好。这样团队在写业务时，可以少处理重复的基础设施问题，把更多精力放在真正的游戏逻辑和内容开发上。

如果第一次接触这个框架，建议先看三个地方：

1. ToolsHub，看整体工具入口。
2. Runtime/Kits，看框架能力模块。
3. Architecture、ResKit、UIKit，看项目最核心的业务、资源和 UI 三条主链路。

这三个地方看明白，基本就能理解 StellarFramework 的整体设计方向。

## 7. 建议配图

介绍视频里不用截太多图，建议控制在这些画面：

1. `Assets/StellarFramework` 目录总览  
   展示 Runtime、Editor、Samples、Tests、Generated。

2. `Runtime/Kits` 目录  
   展示框架包含哪些 Kit。

3. `StellarFramework -> Tools Hub`  
   展示 Quick Start 和工具分组。

4. `Architecture` 相关脚本  
   展示 Model / Service / View 的基础结构。

5. `ResKit` 和 `UIKit` 目录或关键脚本  
   展示资源和 UI 是框架主链路。

6. `HotUpdateManifest.json` 和 `HybridCLRHook.cs`  
   展示框架有热更新链路。

