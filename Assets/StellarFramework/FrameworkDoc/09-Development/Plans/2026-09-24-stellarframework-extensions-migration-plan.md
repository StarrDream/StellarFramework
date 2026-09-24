# StellarFramework 三仓分层与双发布迁移计划

> 制定日期：2026-09-24  
> 修订日期：2026-09-24  
> 状态：Planning / 尚未执行仓库切换  
> 本文取代最初“把扩展 Kit 从开发工程物理搬到 Extensions”的方案。  
> 核心修订：**开发工程保持完整，Kit 只在发布阶段按 General / Extensions 分流。**

## 1. 最终仓库定位

本次调整后的目标不是把研发工程拆成多个互相依赖的 Unity 工程，而是建立一个完整研发母仓和两个面向使用者的稳定发布仓。

| 仓库 | 角色 | 主要受众 | 是否 Source of Truth |
| --- | --- | --- | --- |
| StarrDream/StellarFramework.Dev | 完整框架研发母仓 | 框架开发者、维护者、Agent、贡献者 | **是** |
| StarrDream/StellarFramework | 通用稳定完整版框架 | 普通 Unity 使用者、新手、业务项目 | 否 |
| StarrDream/StellarFramework.Extensions | 高级/专项扩展发布仓 | 需要 World、Flow、算法、热更新等能力的项目 | 否 |

三个仓库均可公开，但职责不同。

最终数据流：

~~~text
                         StellarFramework.Dev
                    完整框架研发母仓 / 唯一研发源
                              │
               ┌──────────────┴──────────────┐
               │                             │
       General Release                Extensions Release
               │                             │
               ▼                             ▼
      StellarFramework          StellarFramework.Extensions
       面向普通使用者                 面向高级使用者
~~~

核心原则：

> **研发时不拆 Kit，发布时才拆产品边界。**

---

## 2. 为什么改为三仓模型

当前工程已经同时承担 Runtime / Editor Framework 开发、Samples、EditMode / PlayMode / Policy / Performance 测试、Framework Verification、Android / IL2CPP / HotUpdate Release Verification、Release Pipeline、开发计划、Review、Handoff、迁移记录以及 ToolsHub / Export / Publisher 的研发与验证。

这些内容对框架维护非常重要，但会显著增加普通使用者理解仓库的成本。

因此不再通过“把开发工程物理拆散”来解决新手体验问题，而采用：

~~~text
一个完整 Dev 工程
      ↓
经过验证的 Release Profile
      ↓
两个干净的用户仓库
~~~

这样既保留最佳研发体验，也保证面向使用者的仓库足够清晰。

---

## 3. StellarFramework.Dev 的职责

StellarFramework.Dev 是以后唯一允许进行框架功能研发的母工程。

它保留全部：

- 通用 Kit。
- 高级 / 扩展 Kit。
- Runtime / Editor。
- ToolsHub。
- Export / Publisher。
- Samples。
- Tests。
- FrameworkVerification。
- Android / Standalone / HotUpdate Release Harness。
- Clean Consumer 工具。
- Development Plans / Reviews / Handoffs。
- 内部诊断、Policy、Audit。
- 研发阶段资源和验证 Fixture。

### 3.1 Dev 中不按发布仓物理拆 Kit

例如以下 Kit 在 Dev 中仍保持正常并列开发：

~~~text
Assets/StellarFramework/
├─ Architecture
├─ EventKit
├─ PoolKit
├─ UIKit
├─ ResKit
├─ GridKit
├─ SpatialKit
├─ PathKit
├─ SimulationKit
├─ WorldKit
├─ WorldGenKit
├─ PlacementKit
├─ FlowKit
├─ HybridCLRKit
└─ ...
~~~

禁止为了发布仓边界而在 Dev 中制造：

- Git submodule。
- 相互嵌套的本地 Package。
- 两份重复源码。
- 跨仓相对路径依赖。
- 为 .World / .Flow 等发布分类强行修改 Runtime namespace。

开发者应该始终能够在一个 Unity 工程中调试和验证完整框架。

---

## 4. 两个用户仓库的职责

### 4.1 StellarFramework

定位：

> **普通 Unity 项目可以直接使用的完整通用框架。**

它不是“精简版 Demo”，而是正式、稳定、完整、可用于业务项目的用户版框架。

主要包含：

- 通用 Runtime Kits。
- 对应 Editor Modules。
- ToolsHub 用户能力。
- Export 用户能力。
- 正式文档。
- 精简但有效的 Samples。
- Bootstrap / 安装入口。
- 必要的公开 Contract Tests；是否保留由发布 Profile 决定。

不应包含大量纯研发内容：

- Development Plans。
- Reviews / Handoffs。
- 内部 Migration Fixture。
- 大规模 Release Harness。
- Android Emulator Harness。
- 内部 Publisher 测试资产。
- 临时 Generated / BuildArtifacts。
- 仅用于框架维护的验证场景和调试资源。

### 4.2 StellarFramework.Extensions

定位：

> **仍然具有复用价值，但明显属于高级场景、特定技术路线或特定项目类型的扩展集合。**

它是用户发布仓，不是另一个研发母工程。

第一阶段发布域：

~~~text
StellarFramework.Extensions
├─ Algorithms
├─ World
├─ Flow
└─ HotUpdate
~~~

它可以提供：

- 单 Kit 导出。
- 扩展域组合包。
- 对应用户文档。
- 对应 Samples。
- 必要的公开 Contract Tests。

开发仍回到 StellarFramework.Dev 完成。

---

## 5. Kit 发布归属

Kit 的“归属”现在指 **发布目标**，不代表它在 Dev 中必须搬目录。

### 5.1 General — 发布到 StellarFramework

第一阶段建议：

| 能力 | 发布目标 |
| --- | --- |
| Architecture | StellarFramework |
| EventKit | StellarFramework |
| BindableKit | StellarFramework |
| ConfigKit | StellarFramework |
| SettingsKit | StellarFramework |
| LogKit | StellarFramework |
| SingletonKit | StellarFramework |
| PoolKit | StellarFramework |
| TimeKit | StellarFramework |
| FSMKit | StellarFramework |
| ActionKit | StellarFramework |
| AudioKit | StellarFramework |
| SaveKit | StellarFramework |
| HttpKit | StellarFramework |
| UIKit | StellarFramework |
| UIAdaptation / UIKit.Adaptation | StellarFramework |
| LocalizationKit | StellarFramework |
| ResKit | StellarFramework |
| Resources / AssetBundle / Addressables / YooAsset Adapter | StellarFramework |

判断标准仍然是：

> 普通 Unity 项目、大众项目是否有较高概率直接使用。

### 5.2 Extensions — 发布到 StellarFramework.Extensions

第一阶段候选：

#### Algorithms

- GridKit
- SpatialKit
- PathKit
- SimulationKit

#### World

- WorldKit.Core
- WorldKit.Streaming
- WorldGenKit.Core
- WorldGenKit.Builtins
- WorldGenKit.Authoring
- WorldGenKit.Resources
- WorldGenKit.Feature
- PlacementKit
- World / WorldGen / Save / Streaming / Unity Presentation Adapters

#### Flow

- FlowKit.Core
- FlowKit.UnityIntegration
- Flow Graph Editor / Validator
- Flow 相关 Adapter

#### HotUpdate

- HybridCLRKit
- HybridCLR 专项 Editor / Publisher / Verification

### 5.3 边界不是永久冻结

如果真实使用反馈证明某个 Kit 已经成为大众项目的高频基础能力，可以调整发布归属。

调整只修改：

- Release Catalog。
- Release Profile。
- 用户文档。
- 发布验证矩阵。

**不要求在 Dev 中移动源码目录。**

---

## 6. Release Catalog 设计

Dev 中需要让发布归属成为机器可读信息，而不是靠人工记忆。

建议为 Kit Catalog 增加类似概念：

~~~text
DistributionTarget:
    General
    Extensions

ExtensionDomain:
    None
    Algorithms
    World
    Flow
    HotUpdate
~~~

实际字段名以现有 Catalog 架构为准，不为了本计划强行照抄命名。

一个 Kit 至少应能描述：

- Stable ID。
- Display Name。
- Runtime / Editor 内容。
- 依赖。
- 可选 Adapter。
- Samples。
- Docs。
- Distribution Target。
- Extension Domain，仅 Extensions 使用。
- Export Profile。
- Verification Profile。

Release Publisher 根据 Catalog 自动决定某个文件进入哪个用户仓。

禁止长期维护“手写复制文件清单”作为唯一发布机制。

---

## 7. Source of Truth 规则

必须写死：

~~~text
StellarFramework.Dev
        ↓
        ↓ 唯一允许产生正式源码变更的地方
        ↓
StellarFramework / StellarFramework.Extensions
~~~

用户仓发现 Bug 时：

~~~text
用户仓发现问题
    ↓
在 StellarFramework.Dev 修复
    ↓
Tests / Verification
    ↓
重新发布
~~~

禁止直接修改 StellarFramework 或 StellarFramework.Extensions 后，再人工猜测如何同步回 Dev。

这条规则用于防止三个仓库长期漂移。

---

## 8. Git 仓库迁移策略

### 8.1 当前仓库先复制为 Dev

当前 StarrDream/StellarFramework 已经是完整研发工程。

因此迁移顺序不是“先删主仓，再重建开发工程”，而是：

~~~text
当前 StellarFramework
      ↓ 完整历史 / 完整研发能力
StellarFramework.Dev
      ↓ 验证 Dev 可独立继续研发
再整理现有 StellarFramework
~~~

### 8.2 Git 历史

优先保留当前研发历史到 StellarFramework.Dev。

目标：

- Dev 能追溯当前框架研发历史。
- 现有 StellarFramework 不做危险的历史重写。
- 用户仓以后只接收稳定发布 commit。

### 8.3 当前 dirty worktree

创建 Dev 基线前必须：

1. 记录所有 tracked modified / untracked。
2. 区分已完成工作、进行中工作、生成产物、临时内容。
3. 不允许使用粗暴 git clean / git reset --hard。
4. 必要工作先形成明确 checkpoint。
5. Dev 基线必须知道自己对应哪个源 commit 和哪些尚未提交内容。

---

## 9. 发布仓不是手工镜像

StellarFramework 和 StellarFramework.Extensions 都不应该通过人工拖文件长期维护。

推荐流程：

~~~text
StellarFramework.Dev
      ↓
Release Catalog
      ↓
Dependency Closure
      ↓
Release Staging
      ↓
Validation
      ↓
Repository Publisher
      ↓
User Repository
~~~

Publisher 至少需要负责：

- 选择正确 Kit / Adapter。
- 依赖闭包。
- 排除研发专用内容。
- 同步 .meta。
- 同步正式 Docs / Samples。
- 生成 Release Manifest。
- 检测目标仓是否存在非预期差异。
- 在真正写入前支持 Dry Run。

---

## 10. Release Manifest

每次正式发布建议生成机器可读 Manifest，例如：

~~~json
{
  "product": "StellarFramework",
  "version": "x.y.z",
  "sourceRepository": "StarrDream/StellarFramework.Dev",
  "sourceCommit": "<sha>",
  "profile": "General",
  "validation": "PASS"
}
~~~

Extensions 对应记录 product、profile、sourceCommit 和兼容的主框架版本。

Manifest 用于：

- 追踪用户仓源码来自哪个 Dev commit。
- 防止“这个文件到底在哪边改过”的问题。
- Release 回滚。
- 自动化版本比较。
- 后续 CI / Publisher Gate。

---

## 11. 依赖方向

逻辑依赖必须满足：

~~~text
General Runtime
不能依赖 Extension Runtime

Extension Runtime
可以依赖 General Public Contract
~~~

例如：

~~~text
HybridCLRKit -> ResKit             允许
WorldKit.SaveAdapter -> SaveKit    允许
General SaveKit -> WorldKit        禁止
~~~

### 11.1 Extension 域之间

Extensions 域之间默认仍保持低耦合。

如果组合存在真实价值，使用显式 Adapter，例如：

~~~text
World.FlowAdapter
World.SaveAdapter
Path.GridAdapter
~~~

Core 不知道 Adapter，Adapter 可以知道两侧。

### 11.2 Dev 在一个工程不等于允许循环依赖

即使所有 Kit 都在 StellarFramework.Dev 一个 Unity 工程中，也必须继续用 Architecture Policy 检查模块边界。

“开发不拆仓”不能成为建立跨域硬耦合的理由。

---

## 12. 命名与兼容策略

发布分类不强制改变现有公开：

- namespace。
- asmdef 名称。
- Stable ID。
- serialized type。
- Unity GUID。

第一阶段优先目标：

> 改变仓库职责和发布方式，而不是制造一次大规模 Breaking Change。

例如 WorldKit 被发布到 StellarFramework.Extensions，并不意味着 Runtime namespace 必须立刻改成 StellarFramework.Extensions.World。

如果未来要统一 namespace，应单独走 Major Version / Migration Guide。

---

## 13. 三类内容的发布规则

### 13.1 Runtime / Editor Source

按 Release Catalog 分流到用户仓。

### 13.2 Tests / Verification

不采用“全部发布”或“全部删除”的极端方案。

用户仓可保留：

- 公开 Contract Tests。
- 安装/编译 Smoke Tests。
- 对使用者有学习价值的简单测试。

Dev 独占：

- Framework Policy 全套。
- 内部 Architecture Audit。
- Release Pipeline Tests。
- Android / Emulator Harness。
- Migration Tests。
- 大型 Fixture。
- Publisher 内部测试。
- 研发诊断工具。

### 13.3 Docs / Plans

用户仓保留：

- Quick Start。
- Installation。
- Kit Guide。
- API / Architecture 使用说明。
- Compatibility。
- Migration Guide。
- CHANGELOG。

Dev 保留：

- Plans。
- Reviews。
- Handoffs。
- 内部审计报告。
- 开发过程记录。
- Release Pipeline 设计文档。

---

## 14. README / GitHub 首页策略

### 14.1 StellarFramework

首页面向第一次访问项目的使用者。

建议结构：

~~~text
Hero
↓
Why StellarFramework
↓
ToolsHub / Export
↓
Core Capabilities
↓
30-Second Quick Start
↓
Architecture
↓
Kit Catalog
↓
Quality & Verification
↓
Extensions
↓
Documentation
~~~

单独提供 README_EN.md。

主 README 不再平铺大量研发目录和几十个内部验证入口。

### 14.2 StellarFramework.Extensions

首页重点：

- 它是可选扩展，不是主框架必装项。
- Algorithms / World / Flow / HotUpdate。
- 每个扩展域的适用场景。
- 所需 StellarFramework 版本。
- 安装/导出方式。
- Compatibility。

### 14.3 StellarFramework.Dev

首页重点：

- 框架开发环境搭建。
- Architecture Rules。
- Test / Verification。
- Release Pipeline。
- Contribution。
- 两个用户仓如何由 Dev 发布。

---

## 15. 分阶段迁移计划

### Phase 0 — Baseline Freeze

目标：冻结当前完整研发工程状态。

任务：

- 记录当前 branch / HEAD / remote。
- 记录 dirty / untracked。
- 运行当前编译验证。
- 运行现有 FrameworkValidation / PlayMode / RuntimeTools / Architecture / Catalog / Publisher / Standalone 等 Gate。
- 保存当日真实验证证据。
- 记录现有 Export Catalog。
- 检查 StellarFramework.Extensions 当前状态。
- 确认 StellarFramework.Dev 仓库是否已创建。

Exit Gate：

> 能准确回答“迁移开始前完整开发工程是什么状态”。

### Phase 1 — Dev Repository Bootstrap

目标：让 StellarFramework.Dev 成为完整研发母仓。

任务：

- 从当前 StellarFramework 建立 Dev。
- 尽量保留完整 Git 历史。
- 保留全部通用和扩展 Kit。
- 保留完整 Tests / Verification / Plans / Release Pipeline。
- 调整仓库 README 为开发者定位。
- 更新 remote / branch 策略。
- 验证 Unity 能直接打开并编译。
- 验证当前完整 Framework Gate。

Exit Gate：

> 即使不再把当前 StellarFramework 当开发仓，也可以在 Dev 中完整继续所有研发工作。

### Phase 2 — Distribution Audit

目标：把原来的“物理迁移审计”改成“发布归属审计”。

产出每个 Kit 的分类：

- General。
- Extensions.Algorithms。
- Extensions.World。
- Extensions.Flow。
- Extensions.HotUpdate。
- Shared Tooling。
- Dev Only。
- Blocked / Needs Adapter。

同时扫描：

- asmdef references。
- Editor Module。
- Export Profile。
- Tests / Samples。
- Docs。
- Resources / GUID。
- Generated code。
- Third-party dependency。

Exit Gate：

> 每个要发布的文件都有明确来源、目标和依赖，不靠猜测复制。

### Phase 3 — Release Catalog / Publisher

目标：让双发布成为工具能力。

任务：

- 给 Catalog 增加 Distribution Target。
- 增加 Extension Domain。
- 建 General Release Profile。
- 建 Extensions Release Profile。
- 建 Algorithms / World / Flow / HotUpdate 可选 Profile。
- 支持 Dry Run。
- 输出变更清单。
- 输出 Dependency Closure。
- 输出 Release Manifest。
- 默认拒绝把 Dev-only 内容发布到用户仓。

Exit Gate：

> 可以从 Dev 可重复地产出两个用户仓所需内容。

### Phase 4 — StellarFramework User Repository Cutover

目标：把现有 StarrDream/StellarFramework 从“研发现场”收敛为用户仓。

任务：

- 使用 General Release Profile 生成正式内容。
- 保留完整通用框架源码。
- 保留用户 ToolsHub / Export。
- 保留必要 Samples / Docs。
- 清理 Dev-only 内容。
- 重构 README / README_EN。
- 修复所有失效链接和菜单。
- 建立来自 Dev 的 Release Manifest。

注意：

> 不是手工删到“看起来干净”为止，而是以 Release Profile 的输出为准。

### Phase 5 — StellarFramework.Extensions User Repository Cutover

目标：让现有 Extensions 成为正式高级扩展发布仓。

任务：

- 从 Dev 发布 Algorithms / World / Flow / HotUpdate。
- 保留各 Kit 独立 asmdef 和导出能力。
- 不合并成巨型 Extensions Runtime。
- 建立 Extensions Catalog。
- 添加用户 Docs / Samples。
- 建立兼容版本说明。
- 建立 Release Manifest。

Exit Gate：

> Extensions 仓可以被普通用户理解和使用，但开发仍回到 Dev。

### Phase 6 — Clean Consumer Validation

至少建立：

#### Consumer A — General Only

- 只使用 StellarFramework。
- Compile PASS。
- Sample PASS。
- 不需要 Extensions。
- ToolsHub / Export 不出现缺失引用。

#### Consumer B — General + Single Extension

- General + Algorithms。
- General + World。
- General + Flow。
- General + HotUpdate。

分别验证依赖闭包。

#### Consumer C — Typical Composition

按实际高价值组合验证，例如：

- General + World + Algorithms。
- General + HotUpdate。

不为组合测试人为制造隐藏硬依赖。

### Phase 7 — Release Gate

对两个用户仓分别执行：

~~~text
Compile
↓
Targeted EditMode
↓
PlayMode
↓
Architecture / Dependency Audit
↓
Export Validation
↓
Clean Consumer
↓
Standalone / Android / HotUpdate（按 Profile）
↓
Release Manifest Check
~~~

自动 Test Runner 继续优先通过现有 Safe Test Gate，避免 EditMode Compile / Refresh 循环等待问题。

### Phase 8 — GitHub Home / Documentation Finalization

在发布边界和 Publisher 稳定以后再做最终主页。

完成：

- StellarFramework README。
- StellarFramework README_EN。
- Extensions README。
- Dev README。
- 三仓互链。
- Quick Start。
- Compatibility Matrix。
- Contribution 入口指向 Dev。
- Issue / Bug Report 流程说明。

### Phase 9 — Final Audit / Stable Release

检查：

- Dev 是唯一研发源。
- 用户仓没有独立漂移。
- Release Manifest 能追到 Dev SHA。
- General 不依赖 Extensions。
- Extensions 显式依赖 General Contract。
- Catalog 与实际发布内容一致。
- 两个用户仓均通过各自 Gate。

完成后再建立最终迁移 Tag / Release。

---

## 16. Git 提交与发布规则

迁移后长期遵守：

1. 功能开发 commit 进入 Dev。
2. Dev 验证通过后产生 Release commit。
3. 用户仓 commit 应尽量由 Publisher 生成。
4. 用户仓出现紧急问题也先回 Dev 修复。
5. Release commit message 记录版本和 Source SHA。
6. 不在两个用户仓进行平行功能开发。
7. 不允许 Publisher 覆盖用户仓中未识别的人工修改而不报警。

推荐发布提交信息：

~~~text
release: StellarFramework 1.x.y

Source: StellarFramework.Dev@<sha>
Profile: General
Validation: PASS
~~~

Extensions 同理。

---

## 17. 版本策略

三个仓不需要使用完全相同版本号，但必须有兼容关系。

建议：

~~~text
StellarFramework.Dev
研发版本，不作为用户安装版本

StellarFramework
1.x.y

StellarFramework.Extensions
1.x.y
requires StellarFramework >= A.B.C < D.0.0
~~~

Extensions Release Manifest 必须明确兼容的 General 版本或 Source Contract。

---

## 18. 回滚策略

迁移不做一次性“大爆炸”。

每个 Phase 建立独立 checkpoint。

出现以下情况停止：

- Dev 无法完整替代当前开发工程。
- Publisher 需要手工复制内部实现才能工作。
- General 发布结果仍引用 Extension-only assembly。
- Extensions 发布缺少必要 General dependency。
- Unity GUID / serialized reference 异常。
- Clean Consumer 失败。
- 用户仓出现无法解释的额外差异。
- 为仓库分层被迫大规模改 Runtime API。

回滚优先恢复到上一个已验证的 Dev / Release checkpoint。

---

## 19. Definition of Done

三仓迁移完成必须同时满足：

- StellarFramework.Dev 已成为完整、可持续开发的唯一研发母仓。
- 所有 General 和 Extension Kit 在 Dev 中仍可一起开发和验证。
- StellarFramework 已成为清晰、稳定、完整的通用用户框架。
- StellarFramework.Extensions 已成为清晰的高级扩展发布仓。
- 扩展 Kit 没有为了仓库分类在 Dev 中被强行拆散。
- 双发布由 Catalog / Profile / Publisher 驱动，而不是长期手工复制。
- General Runtime 不依赖 Extensions Runtime。
- Extensions 只通过公开 Contract 依赖 General。
- 用户仓内容均能追溯到 Dev Source SHA。
- General Clean Consumer PASS。
- Extension Clean Consumer PASS。
- 必要的 PlayMode / Standalone / Android / HotUpdate Gate PASS。
- 三个 README 定位一致且互相链接。
- 用户文档与开发文档完成分层。
- 现有功能没有因为仓库分层产生静默回归。
- Assets/docs/chatgptwebmemory.md 在实际执行后记录真实最终状态。

---

## 20. 明确不做的事情

本计划不做：

- 不把 Dev 拆成 General Dev + Extensions Dev。
- 不因为发布到 Extensions 就强制移动 Dev 中 Kit 的源码目录。
- 不创建一套重复的 Runtime 源码。
- 不让用户仓反向成为 Source of Truth。
- 不把 Extensions 合并成一个巨型程序集。
- 不在本次迁移同时大规模改 namespace / asmdef。
- 不在仓库切换时顺手重写稳定 Runtime。
- 不为了“仓库看起来干净”删除有真实用户价值的 Docs / Samples / Tests。
- 不使用粗暴 reset / clean 清理当前用户工作。

---

## 21. 下一步

本计划批准后，执行顺序调整为：

~~~text
P0 当前研发基线冻结
    ↓
P1 创建并验证 StellarFramework.Dev
    ↓
P2 发布归属 / 依赖审计
    ↓
P3 Release Catalog + 双 Publisher
    ↓
P4 StellarFramework 用户仓收敛
    ↓
P5 StellarFramework.Extensions 用户仓发布
    ↓
P6 Clean Consumer
    ↓
P7 Release Gate
    ↓
P8 三仓 README / Docs
    ↓
P9 Final Audit / Stable Release
~~~

第一项实际工作应是：

> **冻结当前 StellarFramework 的真实开发基线，并创建/接管 StarrDream/StellarFramework.Dev，确认它能够完整承载当前开发工程。**

在 Dev 验证通过之前，不对现有 StellarFramework 做大规模删除或发布化清理。

