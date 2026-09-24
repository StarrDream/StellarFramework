# StellarFramework 当前验证状态

> 这是面向维护者的“当前状态摘要”。历史阶段证据、Benchmark 明细与旧 Profile 数量保留在 `KitExportValidationMatrix.md`，不要把历史数字继续堆到本页。

## 2026-09-22 验证基线（历史）

### 编译与运行

- Unity Editor compile：**PASS**，0 errors / 0 warnings。
- Console Error：**0**。
- `StellarFramework.Tests.FrameworkValidation` EditMode：**600 / 600 PASS**，0 failed，0 skipped。
- `StellarFramework` PlayMode：**15 / 15 PASS**，0 failed，0 skipped。
- `FrameworkArchitecture_Playable.unity`：可进入 PlayMode，Console **0 Error**。
- ArchitectureDemo scene validation：Missing Reference **0**；仅存在 UIKit Layer 占位/重复名称类 Info，不构成 release blocker。

### Catalog

- 原子 Distribution Profile：**84**。
- Recommended Profile：**5**。
- 原子 kind：
  - `kit-with-dependencies`：38
  - `tooling`：20
  - `kit`：22
  - `single-file`：2
  - `shared-runtime`：1
  - `generated-support`：1
- Runtime tier：
  - Foundation：21
  - Extension：12
  - Adapter：27
  - non-tier：24

- Maturity：
  - Stable：79
  - RC：3
  - Experimental：2

当前非 Stable 原子 Profile：

```text
RC:           HttpKit
RC:           ResKit.Addressables
RC:           ResKit.YooAsset
Experimental: HybridCLRKit
Experimental: HybridCLRKit.Tools
```

因此当前 Recommended Profile 的闭包成熟度为：Localization / ResKit / UIAdaptationKit / UIKit Complete = Stable；Hot Update Full = Experimental。

以上 maturity 数字是 2026-09-22 的历史快照。P6 当前 maturity 状态如下：

## 2026-09-24 P6 成熟度状态

```text
Stable       80
RC            4
Experimental  0
```

当前非 Stable 原子 Profile：

```text
RC:           HttpKit
RC:           ResKit.Addressables
RC:           HybridCLRKit
RC:           HybridCLRKit.Tools
```

`ResKit.YooAsset` 已基于 focused tests、Range/cache Gate、Android Release IL2CPP 内容更新 E2E 和 Hot Update Full clean consumer PASS 升为 Stable。`HybridCLRKit` 与 `HybridCLRKit.Tools` 已基于 Android Release IL2CPP Gate、PlayMode Gate 和 clean consumer PASS 升为 RC；Stable 评估保留到 P7 Windows64 Release IL2CPP Gate 重跑后。`Hot Update Full` 由完整依赖闭包自动派生为 RC。

P4 Android 机器证据：`Tools/AndroidVerification/Results/20260923-204313/pipeline-result.json` 与同目录 `result.json`。P5 clean consumer 机器证据路径及依赖锁见本轮 Agent Plan 与 `Assets/docs/chatgptwebmemory.md`。

P6 focused maturity policy 为 **2/2 PASS**。FrameworkValidation 主 filter **614/614 PASS**，独立 Addressables validation class **3/3 PASS**，合计 **617/617 PASS**，0 failed / 0 skipped；UnitySkills Diagnose 为 compile/update idle、Console 0 errors / 0 warnings。

## 2026-09-24 P7 最终成熟度与回归状态

```text
Stable       82
RC            2
Experimental  0
```

当前非 Stable 原子 Profile：

```text
RC: HttpKit
RC: ResKit.Addressables
```

P7 基于 P4 Android Release IL2CPP HotUpdate E2E、P5 Hot Update Full clean consumer、P7 Windows64 Release IL2CPP Player Gate 和完整回归，将 `HybridCLRKit` 与 `HybridCLRKit.Tools` 从 RC 升为 Stable。`Hot Update Full` maturity 继续由完整依赖闭包自动派生为 Stable，没有手工设置 Recommended Profile 等级。Windows Player 聚合证据：`D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-05\P7-Windows64-Release-IL2CPP-GateEvidence.json`；P4 Android 证据：`Tools/AndroidVerification/Results/20260923-204313/pipeline-result.json` 与同目录 `result.json`；P5 clean consumer 证据路径与依赖锁见 Agent Plan 和 `Assets/docs/chatgptwebmemory.md`。

P7 回归证据：UIAdaptationKit / HotUpdate focused suites **44/44 PASS**；maturity、Catalog、Publisher、Standalone Export policies **89/89 PASS**；FrameworkValidation **614/614 PASS**，独立 Addressables policy **3/3 PASS**。`StellarFramework.Tests.PlayMode` 产品测试为 **15/15 PASS**；Unity Test Runner 的 PlayMode fresh discovery 总数为 **17**，其中另外 1 项是 HotUpdate ReleaseGate、1 项是 UnitySkills 自身的 PlayModeRecovery 测试。HotUpdate Release PlayMode Gate fresh discovery 总数 **17**，exact gate **1/1 PASS**。P7 结果 JSON 保存在 `D:\SF-P7-Win64-20260924\P7Artifacts\`。Windows64 Release IL2CPP build 有 0 errors 和 1 条 HybridCLR `CheckSettings` warning：隔离 clean consumer 未配置 source hot-update modules，Gate 使用 P5 已核验 Windows DLL；Runtime 聚合 Gate 的各项断言均 PASS。

Release Gate 故意中断 HTTP Range 下载，以验证恢复流程。Unity Console 中对应的两条错误日志和两条 YooAsset abort 清理警告已归档至 `D:\SF-P7-Win64-20260924\P7Artifacts\P7-Expected-Range-Interruption-Console.log`。Gate 运行后已清空 Console；UnitySkills Diagnose 显示健康、compile/update idle、Console **0 errors / 0 warnings**。未发现 `TOOLING EVIDENCE GAP`。

Recommended Profile：

```text
Localization Complete
ResKit Complete
UIAdaptationKit Complete
UIKit Complete
Hot Update Full
```

Catalog 仍不包含 `samples.*` Profile；用户 Sample 与发布验证职责继续分离。

## 当前发布含义

本页的 PASS 表示当前母工程统一回归基线正常，不等价于“所有 Profile 都已经完成所有目标平台实机发布”。

以下验证仍按能力类型单独判断：

- Android / iOS / HarmonyOS 等目标平台 Player；
- IL2CPP；
- HybridCLR 真机代码热更；
- YooAsset / Addressables 真实远端内容发布；
- 平台 SDK 或第三方插件专项集成。

这些能力的未执行项必须继续保留为 RC / Experimental 或明确 Gate，不能因为母工程统一回归 PASS 就自动升级为 Stable。

## 更新规则

1. 当前结构与统一测试结果只更新本文件。
2. 阶段性 Benchmark、冻结记录、专项验证追加到 `KitExportValidationMatrix.md`。
3. Catalog 增删 Profile 时同步更新本页的 Catalog 摘要。
4. 不回写篡改历史阶段数字；历史证据只追加澄清。
5. Release 前必须重新运行 compile、FrameworkValidation EditMode、PlayMode 与对应 clean-project / Player Gate。
