# Samples / Demo 说明文档

## 模块职责

Samples 只承担一个职责：给第一次接触 StellarFramework 的使用者提供一个足够小、可运行、可阅读的整体入门 Demo。

当前唯一入口是 `ArchitectureDemo`。

## 使用方式

1. 打开 `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`。
2. 运行场景并点击 Mine。
3. 观察 Model、Service、BindableProperty 与 View/UI 的数据流。
4. 切换 `中文 / English`，确认 LocalizationKit 与真实 Panel 共用同一语言上下文。
5. 再进入对应 Kit Guide 学习完整 API。

## 边界

Samples 不承担：

- 每个 Kit 的完整 API 展示
- 自动化回归
- 性能证明
- Addressables / HybridCLR 发布验证
- Player / IL2CPP 验收
- 大型跨 Kit 业务演示

这些职责分别由 FrameworkDoc、Tests 和 StellarFrameworkVerification 承担。

## 相关文档

- [Demo 索引](Samples_Index.md)
- [Samples 源码文档](Samples-源码文档-Guide.md)
- [快速开始](../00-Overview/快速开始.md)
