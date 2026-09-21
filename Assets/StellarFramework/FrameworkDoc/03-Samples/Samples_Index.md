# Demo 索引

StellarFramework 当前只保留一个用户可运行 Demo。

## 唯一 Demo

| 入口 | 用途 |
| :--- | :--- |
| `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity` | 建立 Architecture / MSV、BindableKit、ActionKit、UIKit、LocalizationKit、LogKit 的整体协作认知 |

## 为什么不再“一 Kit 一个 Sample”

过去每个 Kit 都维护独立 Playable 场景，带来了大量场景 Builder、公共资源、分发 Profile、Smoke Test 和文档同步成本。很多 Sample 最终变成“为了验证 Sample 而维护 Sample”，而不是帮助真实项目开发。

当前规则：

- 一个小 Demo 负责“第一次理解框架”。
- 每个 Kit 的完整用法由对应 `FrameworkDoc/02-Kits` Guide 负责。
- 核心行为由 EditMode / PlayMode 自动测试负责。
- 性能由 Benchmark / Performance Gate 负责。
- 发布与热更新集成由 `StellarFrameworkVerification` 负责。
- 不因为缺一个可视化场景就默认新增 Sample。

## 推荐路径

1. Tools Hub -> Start Here -> Quick Start。
2. 打开并运行 `ArchitectureDemo`。
3. 根据项目需求选择对应 Kit Guide。
4. 需要理解内部实现时再阅读对应源码 Guide。
5. 需要确认框架质量时查看 Tests 与 Verification，而不是依赖 Demo 人工点一遍。
