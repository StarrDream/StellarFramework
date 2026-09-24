# Kit Catalog 全量审计

## 审计范围

本审计针对当前 Catalog schema v4 的全部 **84 个原子 Profile + 5 个 Recommended Profile**，目标不是重新评价每个 Kit 的业务设计，而是确认“可选择、可导出、可安装、可理解、成熟度不误导”。

审计维度：

```text
Catalog identity / output
Dependency closure
Source paths
Documentation paths
UPM install sources
Runtime / ToolsHub boundary
Maturity propagation
Legacy aliases
BuildArtifacts hygiene
External release gates
```

## 本轮发现并已修复

### P0 — ToolsHub.Core 失效 sourcePath

`toolshub.core` 仍引用已经不存在的 `WorkflowHubModules.cs`。母工程不会因此编译失败，但独立导出契约存在失真。

处理：删除该失效 Catalog 路径，并新增全 Catalog source/documentation path existence Policy。

### P0 — YooAsset Bootstrap 无安装源

`reskit.yooasset` 声明 `com.tuyoogame.yooasset`，但 Publisher 的 `UpmPackageSources` 没有对应映射。母工程 manifest 已安装 YooAsset，因此此前会掩盖 clean-project 导入问题。

处理：Publisher 使用与母工程一致的 YooAsset `2.3.19` Git UPM 源；新增“Catalog 所有 requiredUpm 必须能被 Publisher 解析”的 Policy。

### P1 — 独立 Runtime 包未统一携带正式 Guide

旧 Profile 大多只有 Runtime 源码进入 payload，正式文档虽然存在于 `FrameworkDoc`，但不会随包导入。

处理：schema v4 增加显式 `documentationPaths`；当前 **60 个 Runtime Profile 全部声明并验证文档目录存在**。Publisher 将 source + documentation 一起放入 payload。

### P1 — Legacy Alias 暴露给新用户

`uikit.adaptation` / `uikit.adaptation.tools` 必须保留兼容解析，但不应继续作为正常新选项出现在导出列表。

处理：Publisher 的正常 Profile picker 隐藏 `LegacyDistributionAlias`；旧 ID 仍保留在 Catalog，可继续解析历史工作流。

### P1 — 已退场 Sample 分发入口仍残留在 Export / Guide

Catalog 已经没有 `sample` Profile，但 Export 窗口仍保留空的“样例”页，旧分发文档也仍指导开发者从 Export 导出样例包。

处理：删除空 Sample 导出页与 `GetSourceProjectSampleProfiles()`，分发文档统一为“一个 ArchitectureDemo + 随包 Guide + 自动测试 / Verification”的现行策略。

### P1 — Tooling Profile 的 PlayerRuntime 元数据不一致

19 个 Tooling Profile 的物理源码都位于 Editor 边界，但 `toolshub.core`、`flowkit.tools`、`savekit.tools` 没有显式声明 `PlayerRuntime` exclusion。

处理：补齐三个 Profile，并新增 Policy：所有 `kind=tooling` Profile 必须只包含 Editor 路径且显式排除 PlayerRuntime。

### P1 — Catalog 依赖只校验 ID，未校验真实 asmdef 引用

仅检查 `requiredProfileIds` 存在仍可能漏掉一种错误：源码 asmdef 已新增内部 `StellarFramework.*` 引用，但 Catalog 没同步依赖。

处理：新增全 Profile asmdef closure Policy。每个 Profile 自己打包的 asmdef 对其他内部 asmdef 的硬引用，都必须能从该 Profile 的 Catalog 闭包中解析到。

### P2 — 人类依赖说明可能与机器闭包漂移

`requiredProfileIds` 负责真实闭包，`requiredKits` 用于依赖说明。如果两者不同步，包可能正确但文档误导。

处理：新增全量一致性 Policy，`requiredKits` 必须与直接 `requiredProfileIds` 的 displayName 集合完全一致。

### P2 — `runtime.tools` 从 Legacy Candidate 升级为正式 RuntimeTools.Core

此前 `runtime.tools` 只有 `CoroutineRunner`，没有实际消费者，因此被标记为 Legacy Candidate。后续工具层审查确认：应该保留“轻量独立工具层”这一职责，但不能继续作为杂物箱，也不能复制 TimeKit / PoolKit / UIKit 等正式 Kit 的能力。

处理：保留历史 `runtime.tools` ID 和原有输出文件名，避免旧自动化失效；Profile 正式升级为 `RuntimeTools.Core`（Extension / Infrastructure），新增高价值 Core/Transform/Physics 工具，并新增独立 `RuntimeTools.Tools` ToolsHub Profile。Runtime Core 仍保持零 StellarFramework Kit 依赖。

### P2 — `ToolsHub.Core` 单独导出缺少正式 Guide

其他 Tooling Profile 会通过 Runtime 依赖闭包获得对应 Kit Guide，但 `toolshub.core` 自身没有 Runtime 依赖。

处理：给 `toolshub.core` 增加 `FrameworkDoc/04-ToolsHub` documentation path；以后单独导 ToolsHub.Core 也会携带正式说明。

## 当前硬门禁结果

修复后自动审计应满足：

```text
Missing source path          = 0
Missing documentation path   = 0
Runtime profile without docs = 0
Duplicate profile id/output  = 0
Missing requiredProfileId    = 0
Runtime -> ToolsHub source    = 0
Unknown UPM install source    = 0
Maturity inversion           = 0
Internal asmdef closure gap  = 0
requiredKits drift           = 0
Tooling -> PlayerRuntime gap = 0
```

这些规则已固化到 `KitCatalogAuditPolicyTests`，以后新增/移动 Kit 时会直接作为 EditMode 回归失败，而不是等人工发现。

## 成熟度复核

当前 Catalog maturity：

```text
Stable       82
RC            2
Experimental  0
```

非 Stable：

- RC：HttpKit
- RC：ResKit.Addressables

P6 历史决策：基于 YooAsset focused tests、Range/cache Release PlayMode Gate、Android Release IL2CPP 内容更新 E2E，以及 Hot Update Full clean consumer 导入与运行证据，将 `ResKit.YooAsset` 从 RC 升为 Stable；`HybridCLRKit` 与其工具从 Experimental 升为 RC，等待 Windows64 Release IL2CPP Player Gate。

P7 决策：结合 P4 Android Release IL2CPP E2E、P5 clean consumer、P7 Windows64 Release IL2CPP Player Gate 和当前 focused/policy/regression tests，将 `HybridCLRKit`、`HybridCLRKit.Tools` 从 RC 升为 Stable。`Hot Update Full` 仍由完整依赖闭包派生为 Stable，没有手工改 Recommended Profile maturity。P7 Windows Player 聚合机器证据为 `D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-05\P7-Windows64-Release-IL2CPP-GateEvidence.json`；Android 证据位于 `Tools/AndroidVerification/Results/20260923-204313/pipeline-result.json` 和同目录 `result.json`；P5 clean consumer 证据及包 SHA 记录于 Agent Plan 和 `Assets/docs/chatgptwebmemory.md`。

P7 当前回归：UI/HotUpdate focused suites **44/44 PASS**；Catalog / architecture / publisher / standalone export policies **89/89 PASS**；FrameworkValidation **614/614 PASS**，Addressables policy **3/3 PASS**；`StellarFramework.Tests.PlayMode` 产品测试 **15/15 PASS**。PlayMode fresh discovery 总数为 **17**，其中另外包含 HotUpdate ReleaseGate 1 项和 UnitySkills PlayModeRecovery 1 项；HotUpdate PlayMode Release Gate exact gate **1/1 PASS**。对应结果位于 `D:\SF-P7-Win64-20260924\P7Artifacts\`。Windows build 有一条 HybridCLR `CheckSettings` warning（隔离 clean consumer 未配置 source hot-update modules，验证运行使用 P5 已核验 DLL），无 build errors；该 warning 保留在聚合证据中。

## BuildArtifacts 审计

`BuildArtifacts/StellarFramework/Kits` 仍保留较多历史 `Sample-* / With-Sample / Validation-* / 旧 HotUpdate-*` 文件。它们不会进入 Catalog，也不影响源码编译，但人工浏览目录时可能拿错包。

本轮没有物理删除这些历史证据，避免破坏既有验证记录和本地引用。正式规则已写入 `07-Distribution/BuildArtifactsGuide.md`：

- 当前正式产物以 Catalog `output` 为准；
- Validation / Legacy 文件不得作为用户入口；
- 本地是否“已经存在所有 84 个包”不是 Catalog 正确性的判定依据；
- Release 批次应重新导出目标 Profile，并使用同批生成的 Dependencies.md。

## 仍需目标环境验证的 Gate

以下项目不能由本轮母工程审计替代：

- HttpKit 真实网络失败/超时/证书环境；
- Addressables 真正远端 Catalog/Bundle 发布；
- YooAsset Offline mode 和未覆盖配置组合；
- HybridCLR Windows64 Release IL2CPP 的最终重跑；
- iOS / Android / HarmonyOS 等平台专项 Player 构建与真机能力。

这些 Gate 继续限制尚未覆盖的组合；P6 maturity 只针对已满足计划门槛的能力调整。

## 结论

Catalog 当前没有发现新的 P0 结构缺口。2026-09-22 RuntimeTools 第二批产品化基线为 Unity compile **0 errors / 0 warnings**、`FrameworkValidation` EditMode **600/600 PASS**、`StellarFramework` PlayMode **15/15 PASS**。2026-09-24 P6 maturity policy focused tests **2/2 PASS**；FrameworkValidation 主 filter **614/614 PASS**，独立 Addressables validation **3/3 PASS**，合计 **617/617 PASS**，0 failed / 0 skipped；UnitySkills Diagnose compile/update idle，Console **0 errors / 0 warnings**。

后续新增 Profile 必须同时通过 schema/maturity、路径、UPM、文档、机器/人类依赖一致性、asmdef 闭包、Tooling 边界与成熟度 Policy；不再接受“母工程能编译，所以独立包应该也能用”的假设。
