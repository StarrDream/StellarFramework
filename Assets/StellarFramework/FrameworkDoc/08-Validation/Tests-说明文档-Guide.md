# Tests / 说明文档

## Tests 是什么

Tests 是框架维护用的自动验证，不是用户 Sample，也不是业务项目。它保护 Kit 的公开 Contract、性能趋势、工程 Policy 和真实 Runtime 边界。

验证体系最高层规范见 [ValidationArchitecture.md](../../StellarFrameworkVerification/ValidationArchitecture.md)。

## 五层 Validation

| 层级 | 目标 | 典型位置 |
| --- | --- | --- |
| Kit Behavior | 单 Kit 功能、边界、失败原子性和回归 | EditMode/Kits，必要时 PlayMode |
| Performance | 规模、吞吐和结构性性能趋势 | EditMode/Performance |
| Framework Policy | Architecture、Catalog、Docs、Packaging、ToolsHub、Runtime Delivery | EditMode/Policies |
| Integration | 多 Kit 接口、生命周期和数据流 | StellarFrameworkVerification/Integration |
| Release | 空白工程、Package、Player、IL2CPP、AA、HybridCLR | StellarFrameworkVerification/Release、PlayerSmoke |

Samples 只负责教学；Verification 只负责维护者的组合与发布验收。

## EditMode / PlayMode 分工

EditMode 用于可以纯 C# 或静态结构检查完成的 Behavior、Performance、Policy。纯 Foundation 不应因为“更真实”被迫进入 PlayMode。

PlayMode 只用于真实 Unity Runtime 能力：MonoBehaviour 生命周期、Object.Destroy/OnDestroy、Coroutine、Scene、Resources、UIKit、异步资源和 Runtime 初始化。

## Behavior、Performance、Policy

- Behavior 关注正确性和确定性，不用性能阈值替代语义断言。
- Performance 记录规模、操作次数、Elapsed、环境、校验和及可靠的 GC 证据；GetTotalMemory 只能说明 coarse heap / GC trend。
- Policy 优先解析 asmdef/JSON、反射、文件存在和最小源码断言；不要锁死整篇文档文本。

当前样板：

- TimeKitTests、SaveKitCoreTests、GridKitTests、SpatialKitTests：Kit Behavior。
- GridKitBenchmarkTests、SaveKitBenchmarkTests、SpatialKitBenchmarkTests：Performance。
- *PolicyTests：Framework Policy。
- TimeKitPlayModeTests、SaveKitPlayModeTests、UIKitResKitPlayModeTests、EventKit/BindableKit PlayMode：真实 Runtime Behavior。

## Negative / Failure Tests

负向测试必须明确断言失败结果、状态不变和失败原子性。故意触发 Error/Warning 时使用 LogAssert.Expect 或项目等价机制，并在测试名或注释中说明。

某些负向测试会故意触发 Error / Warning。例如 TimeKit 非法调度参数测试。如果日志由 LogAssert.Expect 明确声明且测试通过，它属于预期验证输出，不代表 Runtime 故障。禁止通过全局关闭 Error 来掩盖未知问题。

## Samples 与 Verification

Samples 面向框架使用者，保持单 Kit、规模小、代码易读、可导出。它回答“怎么用”。

StellarFrameworkVerification 面向维护者，保留集中 Runner、Player/IL2CPP/HotUpdate Smoke 和发布前工具，不进入普通 Kit 或 Sample Profile。Integration 使用 FakeEntity、TestAgent、MovingPoint、TestPayload 等中性语义，不能扩展成 Crop/NPC/Building/Farm 等业务 Demo。

## 新 Kit Validation Contract

新 Kit 必须在设计文档中说明：

~~~text
Behavior Tests
Performance（required / not required 与 scale）
PlayMode（required / not required 与原因）
Sample（required / not required 与教学目标）
Policy（asmdef、依赖、Catalog、Sample closure）
Integration（extend / none，Fake-only）
Release（export、clean import、Player、IL2CPP、AA/HotUpdate）
~~~

不要求每个 Kit 都拥有 Benchmark、PlayMode、ToolsHub 或 Integration Scene。

## 常用运行方式

编辑器内使用 Window > General > Test Runner，分别选择 EditMode 或 PlayMode；先运行受影响的定向测试，再运行 Framework Gate。

命令行示例：

~~~text
Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile editmode.log
Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile playmode.log
~~~

Release Gate 的空白工程、Player、IL2CPP、Addressables 和 HybridCLR 步骤必须保留真实环境证据。无法运行时记录 BLOCKED、SKIPPED 或 NOT RUN。

## 相关入口

- [Tests 源码文档](Tests-源码文档-Guide.md)
- [PlayMode README](PlayMode/README.md)
- [验证架构与发布验收规范](../../StellarFrameworkVerification/ValidationArchitecture.md)
- [验证区 README](../../StellarFrameworkVerification/README.md)
- [导出验证矩阵](../KitCatalog/KitExportValidationMatrix.md)
