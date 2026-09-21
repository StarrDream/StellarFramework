# ArchitectureDemo / 唯一入门 Demo

## 中文

`ArchitectureDemo` 是 StellarFramework 唯一保留的用户入门 Demo。

它的目标不是展示所有 Kit，而是让第一次接触框架的人在一个小场景里理解“业务状态如何从 Model 经过 Service 到 View/UI”。

### 场景入口

`Scene/FrameworkArchitecture_Playable.unity`

### 当前展示能力

- `Architecture<T>` / MSV 分层
- `BindableKit`：Model 状态变更驱动 View
- `ActionKit`：动作/命令式调用
- `UIKit`：真实 Panel 打开与关闭
- `LocalizationKit`：`中文 / English` 运行时切换
- `LogKit`：基础日志入口

真实业务闭环是一个可以无限重复的小任务：

`第 N 轮 0/30 -> 挖矿 +10 -> 10/30 -> 20/30 -> 30/30 -> 完成本轮 -> 第 N+1 轮 0/30`

每次点击都走同一条架构链路：

`View -> CoinService.AdvanceCycle -> CoinModel -> BindableProperty -> View`

PlayMode 回归会实际跑完一整轮并进入第 2 轮，而不只验证一次数值增加。

UI 生命周期也形成闭环：

`打开 Panel -> 操作 -> Close -> 场景常驻“打开面板”按钮出现 -> 重新打开 Panel -> Model 状态继续保留`

关闭 View 不会销毁 Architecture / Model；这用于直观展示“业务状态生命周期”和“UI 生命周期”是两件不同的事。

### 推荐阅读顺序

1. 运行 `Scene/FrameworkArchitecture_Playable.unity`
2. 阅读 `Runtime/DemoEntry.cs`
3. 阅读 `Runtime/Architecture/`
4. 进入 `Assets/StellarFramework/FrameworkDoc`，按实际项目需要选择对应 Kit Guide

不要从这个 Demo 推断所有 Kit 的完整能力；它只负责建立整体框架认知。

## English

`ArchitectureDemo` is the single user-facing onboarding demo kept in StellarFramework.

Its purpose is not to cover every Kit. It gives new users one small repeatable gameplay loop for understanding how Model, Service, View, state binding, UI, localization, and logging work together.

### Entry scene

`Scene/FrameworkArchitecture_Playable.unity`

### Demonstrated capabilities

- `Architecture<T>` / MSV layering
- BindableKit state propagation
- ActionKit command/action flow
- UIKit panel lifecycle
- LocalizationKit runtime `中文 / English` switching
- LogKit basic logging

Functional loop:

`Round N: 0/30 -> Mine -> 10/30 -> 20/30 -> 30/30 -> Complete Round -> Round N+1: 0/30`

Every interaction follows `View -> CoinService.AdvanceCycle -> CoinModel -> BindableProperty -> View`.

The UI lifecycle is also repeatable:

`Open Panel -> interact -> Close -> scene-level Open Panel button appears -> reopen -> model state is preserved`.

For detailed Kit APIs and boundaries, continue with `Assets/StellarFramework/FrameworkDoc`.
