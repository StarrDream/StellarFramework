# Samples / 源码文档

## 模块职责

Samples 只承担用户教学：展示每个 Kit 的最小接线、调用顺序、资源组织和可见结果。它们不承担自动化回归、性能证明或完整业务 Demo。

## 源码与资源

- Samples/README.md、KitSamples/README.md、Samples_Index.md：入口和前置条件。
- KitSamples/Editor/ExamplePlayableSceneBuilder.cs：生成最小样例场景和支持资源。
- KitSamples/Example_*：各 Kit 的示例脚本和独立 asmdef。
- ArchitectureDemo：Architecture 教学场景和配套 Model/Service/View 代码。

## 当前结构

~~~text
Samples
├─ KitSamples
│  ├─ Scenes
│  ├─ Example_*
│  ├─ Common
│  ├─ Generated
│  └─ Editor
├─ ArchitectureDemo
└─ README / Guide / Index
~~~

每个 Sample Profile 只闭包自身 Kit、Common 场景说明、场景和必要支持资源。Core Kit Profile 不包含 Samples；StellarFrameworkVerification 永远不作为 Sample Profile。

## 运行链路

1. Quick Start 或样例入口触发 ExamplePlayableSceneBuilder。
2. Builder 生成/修复最小场景、Prefab、配置和材质。
3. 打开对应 Playable 场景。
4. 通过 Game 视图、Debug 文本、日志或最小交互理解 API 用法。

## 设计约束

- 样例代码优先体现最小闭环和公开 API。
- 不增加 Crop、NPC、Building、Farm、Logistics、Economy 等业务语义。
- 不把样例当成测试套件；回归测试放 Tests，组合与发布验证放 StellarFrameworkVerification。
- 修改路径、场景或资源后同步 QuickStart、README、Catalog 和对应 Policy。
- 不为了样例制作正式 UI Skin、美术、动画或音频。

## 相关验证

样例场景可作为人工教学检查；自动化路径由 QuickStartCatalogPolicyTests、OnboardingSurfacePolicyTests、SampleGenerationPolicyTests 和必要 PlayMode 测试保护。它们证明入口和最小闭环，不把 Samples 变成 Verification。
