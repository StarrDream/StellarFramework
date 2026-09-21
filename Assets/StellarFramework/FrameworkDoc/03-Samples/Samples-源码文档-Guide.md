# Samples / Demo 源码文档

## 模块职责

Samples 只保留一个用户入门 Demo，不承担逐 Kit 教学场景、自动回归、性能验证或发布验收。

## 源码

当前结构：

~~~text
Samples
├─ ArchitectureDemo
│  ├─ Art
│  ├─ Fonts
│  ├─ Scene
│  ├─ Runtime
│  ├─ Resources
│  └─ Localization
└─ README.md
~~~

`ArchitectureDemo/Runtime/DemoEntry.cs` 是入口。

`ArchitectureDemo/Runtime/Architecture/` 展示 Model / Service / View 的组合方式。

`ArchitectureDemo` 是自包含目录；字体、材质、语言切换脚本和 Panel 资源都归属该 Demo，不再保留跨 Sample 的 Common 支撑层。

## 依赖边界

ArchitectureDemo 可以依赖它要展示的基础 Kit，但：

- 不进入 KitDistributionCatalog。
- 不提供 `samples.*` Profile。
- 不提供 Sample unitypackage 导出入口。
- 正式框架 payload 通过 PackagePublisher 排除 `Assets/StellarFramework/Samples`。
- Kit Core / Adapter 不能反向依赖 Demo。

## 验证

Demo 只保留必要的 PlayMode 行为回归，验证真实 Mine 点击、Model 状态和 UI 更新。

各 Kit 的正确性继续由 `Assets/StellarFramework/Tests` 下的行为测试、Policy 与 Performance Gate 负责。
