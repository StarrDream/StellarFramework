# StellarFramework 验证架构与发布验收规范

## 1. 目的与边界

这份文档定义框架仓库的验证职责、目录边界和发布证据规则。它不新增 Runtime Kit，不替换 Unity Test Framework，也不把验证体系做成新的运行时架构。

长期目标是保持：

~~~text
小而稳定的 Runtime
+ 清晰的自动化 Tests
+ 最小教学 Samples
+ 有限的维护者 Verification
+ 真实的 Release Evidence
~~~

本仓库只存放框架开发工程。业务项目应按需导出 Kit、样例或单文件内容，不应把业务玩法反向写入本仓库。

## 2. 五层 Validation Contract

| 层级 | 回答的问题 | 默认位置 | 是否导出 |
| --- | --- | --- | ---: |
| Kit Behavior | 单个 Kit 是否符合公开 Contract？ | Tests/EditMode/Kits，必要时 PlayMode | 否 |
| Performance | 目标规模是否出现结构性退化？ | Tests/EditMode/Performance | 否 |
| Framework Policy | 架构、Catalog、文档、打包边界是否遵守？ | Tests/EditMode/Policies | 否 |
| Integration | 多个 Kit 的接口、生命周期和数据流能否组合？ | StellarFrameworkVerification/Integration | 否 |
| Release | 空白工程、Player、IL2CPP、AA、热更和真实包体是否可用？ | StellarFrameworkVerification/Release、PlayerSmoke | 否 |

Samples 不属于这五层 Verification 本体。Samples 只负责回答“开发者应该如何使用这个 Kit”。

### 2.1 Kit Behavior

验证功能正确性、确定性、边界输入、失败原子性和回归。纯 C# 优先使用 EditMode；只有真实 Unity 生命周期、Coroutine、Object.Destroy、Scene、Resources、UIKit 等不可替代时才使用 PlayMode。

当前样板：

- TimeKitTests、SaveKitCoreTests、GridKitTests：Kit Behavior。
- TimeKitPlayModeTests：需要真实 Unity 时间流逝的 Runtime Behavior。
- SaveKitPlayModeTests：需要真实 FileSystem、异步 Coroutine 的 Runtime Behavior。

### 2.2 Performance

验证规模、吞吐、结构性趋势和可重复的环境证据，不代替行为测试。当前样板：

- GridKitBenchmarkTests：1M DenseGrid、坐标索引和 100k Occupancy。
- SaveKitBenchmarkTests：100000 条存档记录的 End-to-End Save/Load。

Benchmark 必须记录规模、操作次数、耗时、环境、校验和以及可可靠取得的 GC 证据。GC.GetTotalMemory(false) 只能描述为 coarse heap / GC trend，不能宣称严格的零分配证明；不设置依赖机器速度的固定毫秒门槛。

### 2.3 Framework Policy

验证框架自身的工程规则，包括：

- Architecture、asmdef、依赖闭包和禁止循环依赖。
- Catalog schema、tier/category、Sample 边界和 Verification 不分发。
- README、QuickStart、Onboarding、双 Guide 和文档入口。
- Exporter、Bootstrap installer、UPM dependency、Addressables/HybridCLR opt-in。
- ToolsHub 可用程序集过滤和入口合并。

优先使用结构化检查：asmdef/JSON 解析、文件存在、反射和有限源码断言。文档测试只锁入口和链接，不锁整篇文章逐字相等。

### 2.4 Integration Verification

Integration 只证明 Kit 协作，不是 Demo。未来的 Foundation Integration 只允许小型逻辑网格、FakeEntity/TestAgent/MovingPoint/TestPayload、空间更新/查询、预算调度、简单寻路、时间推进和最小存取。

禁止在框架仓库中为了 Integration 增加 Crop、NPC、Building、Farm、Logistics、Economy、正式 UI、美术、动画或音频。出现正式资产、大量 Prefab、超过 3 个 Integration Scene、真实业务语义或为了 Demo 反向扭曲 API 时，必须停止扩展并把内容移到真实游戏项目或独立 Playground 仓库。

本轮不创建 FoundationIntegration Scene；等 SpatialKit、SimulationKit、PathKit 稳定后再评估。

### 2.5 Release Verification

Release 验证真实分发链路，不能由 EditMode 推断替代：

- 空白 Unity 工程导入 ToolsHub、Core Kit、Adapter 和样例包。
- UPM dependency 自动安装与重复导入恢复。
- Windows/目标平台 Player Build、IL2CPP、Addressables、HybridCLR 和远端热更 Smoke。
- 真实 unitypackage、Bootstrap payload、Manifest、Bundle、DLL、AOT metadata 和 SHA256。

环境阻塞必须记录为 BLOCKED、SKIPPED 或 NOT RUN，不能写成 PASS。

## 3. 当前目录与维护边界

下图标注当前真实目录；Future 只表示规划，不代表已经存在：

~~~text
StellarFramework
├─ Runtime
├─ Tests
│  ├─ EditMode
│  │  ├─ FrameworkValidation
│  │  │  ├─ Kits
│  │  │  │  ├─ TimeKit
│  │  │  │  ├─ SaveKit
│  │  │  │  └─ GridKit
│  │  │  ├─ Performance
│  │  │  │  ├─ SaveKit
│  │  │  │  └─ GridKit
│  │  │  ├─ Policies
│  │  │  │  ├─ Architecture
│  │  │  │  ├─ Documentation
│  │  │  │  ├─ Packaging
│  │  │  │  ├─ Samples
│  │  │  │  ├─ ToolsHub
│  │  │  │  ├─ RuntimeDelivery
│  │  │  │  └─ Verification
│  │  │  ├─ Addressables              (独立 Addressables asmdef)
│  │  │  └─ FrameworkValidation 根目录 (Legacy / Cross-cutting)
│  │  └─ UIKit
│  └─ PlayMode
├─ Samples
│  └─ KitSamples、ArchitectureDemo
└─ KitCatalog
   └─ KitDistributionCatalog.json、KitExportValidationMatrix.md

StellarFrameworkVerification       (Current，Maintainer-only)
├─ Editor
├─ Example_FrameworkValidation
├─ Scenes
├─ README.md
└─ ValidationArchitecture.md

StellarFrameworkVerification       (Future，按需创建)
├─ Integration
├─ PlayerSmoke
├─ Release
└─ Tools
~~~

Tests/EditMode/FrameworkValidation 保留现有程序集 StellarFramework.FrameworkValidation.Tests，不为了目录美观上移 asmdef。目录迁移不改变旧 namespace，也不锁死测试的物理路径。

根目录下尚未归类的 DeveloperQuickToolsLogicTests、FrameworkValidationReportTests、HotUpdateManifestTests、HybridCLRHotUpdateAssetExporterTests、ResKitAssetBundleManagerTests 属于 Legacy / Cross-cutting，职责清晰后再迁移。

StellarFrameworkVerification 是维护者专用区，当前包含集中 Runner、发布前工具和场景；不进入普通 Kit、Sample 或 Full Package 分发。Future Integration、PlayerSmoke、Release、Tools 只有在有真实内容时创建，不创建空目录。

## 4. Samples 与 Verification 的区别

| 类型 | 面向谁 | 目标 | 是否导出 | 多 Kit | 业务语义 |
| --- | --- | --- | ---: | ---: | ---: |
| Sample | 框架使用者 | 教学 | 是，可选 | 尽量少 | 极少 |
| Kit Behavior | 框架维护者 | 单 Kit 正确性 | 否 | 否 | 否 |
| Performance | 框架维护者 | 性能趋势 | 否 | 可按需 | 否 |
| Policy | 框架维护者 | 工程规则 | 否 | 是 | 否 |
| Integration | 框架维护者 | 多 Kit 协作 | 否 | 是 | Fake-only |
| Release | 框架维护者 | 真实分发链路 | 否 | 是 | 最小 |

ArchitectureDemo 继续作为架构教学样例；KitSamples 继续作为各 Kit 的最小教学面。它们不是 Bug 回归套件，也不是完整游戏示范。GameHotUpdate 按 Runtime Delivery Example / Verification Fixture 处理，不粗暴归入普通 Sample。

Fixture 为测试服务，Sample 为学习服务。当前没有新增 Fixture 架构的必要。

## 5. 决策树

~~~text
我要验证什么？
├─ 单 Kit 纯逻辑？                  → EditMode / Kit Behavior
├─ 目标规模性能？                   → Performance
├─ 工程约束、Catalog、文档、打包？   → Policy
├─ 需要 Unity 生命周期或真实资源？   → PlayMode
├─ 多个 Kit 的组合数据流？           → Verification / Integration
├─ 真实 Player、Package 或平台链路？ → Release Verification
└─ 只是教开发者如何调用？            → Sample
~~~

## 6. 新 Kit Validation Contract 模板

每个新 Kit（从 SpatialKit 开始）在设计和验收文档中必须填写：

~~~text
Validation Contract

Behavior Tests:
- 公开 API、边界输入、失败原子性、Regression

Performance:
- required / not required
- scale target、操作次数、证据类型

PlayMode:
- required / not required
- 只有真实 Runtime/Lifecycle/Resource 需要时填写原因

Sample:
- required / not required
- teaching goal、最小场景与可导出边界

Policy:
- asmdef、依赖、Catalog、Sample closure、禁止依赖

Integration:
- extend existing verification / none
- 只使用 Fake-only 语义

Release:
- export、clean import、Player、IL2CPP、Addressables、HotUpdate
~~~

不要求每个 Kit 都拥有 1M Benchmark、PlayMode、ToolsHub、Integration Scene；由真实能力决定。

## 7. Source of Truth 与 Evidence Ledger

| 来源 | 唯一职责 |
| --- | --- |
| KitDistributionCatalog.json | 分发 Profile、依赖闭包和 UPM 事实 |
| KitArchitectureGuide.md | Kit 分层、依赖和最低交付规则 |
| ValidationArchitecture.md | 验证架构、目录、职责和 Gate |
| KitExportValidationMatrix.md | 已执行验证证据台账 |
| Tests | 自动验证实现 |
| Samples | 用户教学 |
| StellarFrameworkVerification | 维护者 Integration / Release |

KitExportValidationMatrix 是 Evidence Ledger，不是验证架构规范，也不是自动测试源码。只有真实执行过的测试、真实导出、真实空白工程/Player 结果或明确环境阻塞才能写入。统一状态概念：

~~~text
PASS / FAIL / BLOCKED / SKIPPED / NOT RUN
~~~

NOT RUN 不等于 PASS；BLOCKED 不等于 PASS。Benchmark 未运行不得写数值，代码看起来正确不得写成实测通过。

建议 Gate：

- Local Fast Gate：编译和定向 EditMode。
- Framework Gate：全量 EditMode、必要 PlayMode、Policy 和性能回归。
- Release Gate：导出、干净工程导入、目标 Player、平台特定 Smoke。

## 8. 负向日志、Regression 与迁移规则

故意触发 Error/Warning 的负向测试必须使用 LogAssert.Expect 或项目等价机制，并在测试名或注释中说明原因。TimeKit 非法调度参数测试产生的预期日志不代表 Runtime 故障；禁止全局关闭 Error 来掩盖未知问题。

重大 Bug 修复必须补 Regression Test，测试名或注释应表达曾经错误的行为和现在锁死的 Contract。GridKit 的 cross-owner takeover impossible 是样板。

所有文件迁移使用 git mv 或等价方式并保留对应 .meta GUID；不批量修改 namespace，不为目录美观拆分现有 asmdef，不删除集中 FrameworkValidation Runner。

## 9. SpatialKit 前 Exit Criteria

开始 SpatialKit 前，至少满足：

- 本文档、Tests 双 Guide、PlayMode README、Verification README 均可导航。
- Root README、KitArchitectureGuide、Validation Matrix 已链接本 Contract。
- P0 Behavior/Performance 和明显 Policy 已按安全方案分类。
- asmdef、namespace、.meta/GUID、Catalog 和导出边界无破坏。
- Full EditMode、必要 PlayMode、Catalog/Docs/Verification Policy 已有真实证据。
- 未新增大型 Demo、ValidationKit、TestingKit、BenchmarkKit 或自定义 Test Engine。

## 10. 常见边界

- 真实 Crop、NPC、Building、Weather、Economy、World Gameplay 属于真实游戏项目，不属于框架 Sample 或 Integration。
- Addressables、HybridCLR、UniTask 等是按 Kit/Adapter 选择的外部能力；未安装时入口应隐藏，不能被 Core 隐式拉入。
- 不能运行的 IL2CPP、Unity Licensing 或远端热更步骤必须保留阻塞说明，并在环境可用时重跑。

详细运行命令和程序集约束见 [Tests 使用指南](../StellarFramework/Tests/Tests-说明文档-Guide.md)、[Tests 源码指南](../StellarFramework/Tests/Tests-源码文档-Guide.md)、[Verification README](README.md)、[Kit 架构分层与依赖规则](../StellarFramework/KitCatalog/KitArchitectureGuide.md) 与 [导出验证矩阵](../StellarFramework/KitCatalog/KitExportValidationMatrix.md)。
