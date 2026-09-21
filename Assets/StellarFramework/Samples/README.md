# Samples / Demo 总览

## 中文

`Assets/StellarFramework/Samples` 只保留一个面向使用者的入门 Demo：`ArchitectureDemo`。

StellarFramework 不再为每个 Kit 维护独立 Sample 场景。这样可以避免大量重复场景、Builder、测试资源与分发 Profile 带来的维护成本，也避免 Sample 与正式文档长期漂移。

### 唯一入口

打开：

`Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`

它用于建立框架整体认知，展示：

- Architecture / Model / Service / View 分层
- BindableKit 状态绑定
- ActionKit 命令调用
- UIKit 面板入口
- LocalizationKit 中英文切换
- LogKit 基础日志

这个 Demo 不追求覆盖全部 Kit，也不承担发布验收。

### 其他 Kit 怎么学

所有 Kit 的正式使用说明、依赖边界、最小代码片段、源码说明和排错统一维护在：

`Assets/StellarFramework/FrameworkDoc`

不要通过新增 Sample 场景补文档。只有当一个跨 Kit 行为无法用文档、自动测试或维护者验证表达时，才重新评估是否需要新的 Demo。

### 验证边界

- 自动化测试：`Assets/StellarFramework/Tests`
- 维护者发布验证：`Assets/StellarFrameworkVerification`
- 用户入门 Demo：`Assets/StellarFramework/Samples/ArchitectureDemo`

三者职责分离。

## English

`Assets/StellarFramework/Samples` now contains exactly one user-facing onboarding demo: `ArchitectureDemo`.

StellarFramework no longer maintains one runnable Sample scene per Kit. This avoids duplicate scenes, builders, fixture assets, distribution profiles, and documentation drift.

### Single entry point

Open:

`Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`

It demonstrates the collaboration between Architecture/MSV, BindableKit, ActionKit, UIKit, LocalizationKit, and LogKit. It is intentionally small and does not attempt to cover every Kit.

### Learning other Kits

The authoritative usage guides, dependency boundaries, minimal code snippets, source guides, and troubleshooting notes live under:

`Assets/StellarFramework/FrameworkDoc`

### Validation boundary

- Automated regression tests: `Assets/StellarFramework/Tests`
- Maintainer release verification: `Assets/StellarFrameworkVerification`
- User onboarding demo: `Assets/StellarFramework/Samples/ArchitectureDemo`
