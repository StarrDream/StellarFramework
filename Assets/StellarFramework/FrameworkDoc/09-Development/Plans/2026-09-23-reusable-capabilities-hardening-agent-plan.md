# StellarFramework Reusable Capabilities Hardening Agent Plan

> 执行状态：P1–P7 **SEALED / PASS**；工作区：`C:\CodingToolsWorkerCenter\StellarFramework`；Unity：`2022.3.62f3c1`；UnitySkills：`http://localhost:8090/`
> 目标：把 `UIAdaptationKit`、`Hot Update Full` 进一步收口成可长期跨项目复用、可重复验证的能力；不重新设计已经成立的框架架构。

## 1. 当前已验证基线

执行本计划前必须保留以下事实边界，不得把历史 PASS 当成当前修改后的 PASS：

- Unity Console：当前 **0 Error**。
- FrameworkValidation EditMode：当前 **600/600 PASS**。
- StellarFramework 普通 PlayMode：当前 **15/15 PASS**。
- UIAdaptationKit focused：**18/18 PASS**。
- Localization focused：Core 15/15、UGUI/Settings 15/15、TMP 2/2、Translation Exchange 3/3、Scanner 7/7，全部 PASS。
- HotUpdate focused：Manifest 5/5、HybridCLR Exporter 5/5、YooAsset Update Policy 3/3、YooAsset ResKit Adapter 3/3、HotUpdate Source Policy 2/2，全部 PASS。
- Distribution gates：Standalone Source Export 32/32、Architecture Metadata 16/16、Package Publisher 25/25、Catalog Audit 7/7，全部 PASS。
- Windows64 Release IL2CPP 热更真实 E2E 已有历史 PASS：YooAsset RemoteCDN / HTTP Range resume -> ResKit -> Manifest/DLL SHA -> HybridCLR AOT metadata -> `Assembly.Load(HotUpdate.dll)` -> Entry 执行。
- Android Release Verification 基础流水线已有真实 PASS，但**尚未证明 Android IL2CPP 下的 HybridCLR 热更链**。

当前工作树本身已有其他未提交修改。Agent 必须：

- 不执行 `git reset`、`git clean`、强制 checkout 或任何会覆盖现有工作的命令；
- 不把 `Assets/Generated`、`Assets/TextMesh Pro` 等既有验证产物误当成本计划生成物清理；
- 不自动 commit / push，除非用户后续明确要求；
- 每一阶段修改完成后更新 `Assets/docs/chatgptwebmemory.md`。

## 2. 总体决策

本计划不引入新的“大一统 HotUpdateKit”。继续保持现有职责：

```text
ResKit.Core
   ↓
ResKit.YooAsset        -> 内容初始化 / 版本 / Manifest / 下载 / Cache
   ↓
HybridCLRKit           -> Manifest / DLL / AOT metadata / Entry
```

推荐业务启动顺序继续保持：

```text
YooAssetContentUpdater.UpdateHostPackageAsync(...)
                         ↓
HybridCLRKit.RunAsync(...)
```

UIAdaptationKit 只补行为闭环，不改变现有独立 UGUI Kit 定位。

## 3. P1 — UIAdaptationKit cutout-only 自动刷新闭环

### 3.1 问题

当前 `UIAdaptationController.LateUpdate()` 会比较：

- `Screen.width`
- `Screen.height`
- `Screen.safeArea`

只有变化时才调用 `ApplyCurrentScreen()`。因此存在理论缺口：**宽高和 safeArea 未变化，但 `Screen.cutouts` 本身变化时，不会自动刷新**。

### 3.2 实现要求

修改：

- `Assets/StellarFramework/Runtime/Kits/UIAdaptationKit/Runtime/UIAdaptationController.cs`
- `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Kits/UIKit/UIKitAdaptationTests.cs`
- UIAdaptationKit 正式 Guide。

要求：

1. 宽高 / safeArea 变化仍然保持即时检测；
2. cutout-only 变化增加低频自动探测；
3. **禁止每帧调用 `Screen.cutouts`**。Unity 的数组返回边界可能产生托管分配，不能为了修一个低频状态把 GC 带进每帧热路径；
4. 建议使用节流探测，例如默认 `0.5s` 左右一次；如果做成序列化配置，必须给安全默认值并写清 `<= 0` 的语义；
5. 单次 probe 只获取一次 `Screen.cutouts`，不要 probe 后又通过 `ApplyCurrentScreen()` 二次读取；直接把本次快照传入 `Apply(width, height, safeArea, cutouts)`；
6. 比较逻辑尽量在已有 `_lastCutouts` 上无额外中间集合完成；只有确认刷新时才进入正常 Normalize / Apply 路径；
7. `RefreshDisplayGeometry()` 手动刷新入口继续保留；
8. 不增加设备型号表，不做厂商特判。

### 3.3 测试

至少增加：

- cutout-only 快照变化会被 detector 判定为需要刷新；
- 相同 cutout 快照不会重复触发；
- 非法 / 越界 cutout 不造成无限重复刷新；
- 原有 width / height / safeArea / breakpoint / layout variant 测试全部保持 PASS。

如果为了可测试性抽出内部纯函数/helper，可以做，但不要引入复杂 Service 层或新 Kit。

### 3.4 完成标准

- UIAdaptation focused 全绿；
- FrameworkValidation 全绿；
- Console 0 Error；
- 文档从“cutout 会刷新”的描述变成与代码完全一致；
- Profiler/代码审查确认没有每帧 `Screen.cutouts` 轮询分配。

## 4. P2 — 固定 UniTask 版本，消除跨项目导入漂移

### 4.1 当前风险

当前母工程解析到：

```text
UniTask 2.5.11
commit e5acc106ee196bc5a32fb14cdf2987b0f96d11e0
```

但以下位置仍使用未 pin 的 Git URL：

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

这会导致未来新项目通过 Bootstrap 导入同一个 Kit 时可能解析到不同 UniTask 提交。

### 4.2 实现要求

修改至少包括：

- `Packages/manifest.json`
- `Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs`
- 相关 Package/Distribution Policy Tests。

推荐直接 pin 到当前已验证 commit：

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#e5acc106ee196bc5a32fb14cdf2987b0f96d11e0
```

如果 Agent 选择 `2.5.11` tag，必须先验证该 tag 与当前 commit 对应；否则使用 commit，不要猜。

同时增加分发规则：**所有由 Package Publisher 自动安装的第三方 Git UPM 依赖必须有 tag 或 commit pin**。不要把 Unity Registry 的 `package@version` 误判为 Git 漂移。

### 4.3 完成标准

- Unity 重新 resolve 后仍是 UniTask 2.5.11 / 预期 commit；
- 不升级其他第三方包；
- Package Publisher policy 能阻止未来再次加入 floating Git dependency；
- ResKit / SaveKit / ActionKit / HttpKit 等使用 UniTask 的相关回归保持全绿。

## 5. P3 — 修复 HotUpdate PlayMode Release Gate 的 discovery 闭环

### 5.1 当前问题

`Assets/StellarFramework/Tests/PlayMode/YooAssetHotUpdate/` 当前 asmdef 引用了：

- `StellarFramework.Verification.Editor`
- `YooAsset.Editor`

并限制 `includePlatforms=[Editor]`。

结果是当前 Unity Test Runner PlayMode discovery 中没有 `YooAssetHotUpdateEndToEndTests`。这不是热更运行时失败，而是 Gate 自己没有进入可发现的 PlayMode 测试集合。

### 5.2 目标结构

把“构建测试内容”和“运行时验证”拆开：

```text
Editor Prepare Gate
  -> 生成 HotUpdate DLL / Android 或 Standalone 对应 AOT metadata
  -> 构建 YooAsset verification package
  -> 写入/准备 verification config

PlayMode Runtime Gate
  -> 只依赖 Runtime Verification + Runtime Kits
  -> 启动本地 Range server / 使用准备好的 package
  -> YooAsset update
  -> ResKit load
  -> SHA
  -> HybridCLR entry
```

### 5.3 实现要求

1. PlayMode test assembly 不再引用 Editor-only assembly；
2. `YooAsset.Editor` 只留在 Editor Builder；
3. PackageName、PackageVersion、verification paths/config 等可共享常量移到 Runtime/Shared Verification 层，避免 PlayMode test 为了读常量反向依赖 Editor；
4. PlayMode test 必须出现在 `test_list(testMode=PlayMode)` discovery 中；
5. 普通 `StellarFramework.Tests.PlayMode` 15 项基础回归不要因为外部包未准备而变成常规红灯。建议把热更测试放到独立 namespace/category，例如 `StellarFramework.Tests.ReleaseGate`，由 Release Gate 显式运行；
6. 缺少 prepared package 时要明确报告“precondition missing”，不能表现成随机 NullReference/资源不存在；
7. 最终提供一个可自动执行的顺序：`Prepare -> Discover -> Run exact release gate -> Poll result`。可以由现有 ToolsHub HotUpdate Verification 或 `Tools/` 脚本驱动，但不要复制一套新的热更业务逻辑。

### 5.4 完成标准

- PlayMode discovery 能看到 HotUpdate E2E；
- prepared package 后 exact release gate PASS；
- 普通 PlayMode 仍独立 PASS；
- domain reload 后 UnitySkills job 能恢复则记录为工具链 PASS；若 UnitySkills 本身仍有恢复缺陷，必须保留 Unity Test Runner 的实际结果证据，不能把工具层失败冒充产品失败。

## 6. P4 — Android Release IL2CPP HotUpdate E2E

这是把 Hot Update Full 从“Windows 已证实”推进到用户主要目标平台 Android/PICO 前最关键的一步。

### 6.1 与现有 Android Harness 的关系

继续复用：

`Tools/AndroidVerification/`

不要另起第二套 ADB / Emulator / Result Aggregator。

新增一个明确的 HotUpdate Gate/Profile，使现有流程变成：

```text
Android target prepare
  -> HybridCLR Android Generate / Compile
  -> Android AOT metadata export
  -> HotUpdate.dll + Manifest SHA
  -> YooAsset Android verification package
  -> Start verification CDN
  -> Build Android Release IL2CPP APK
  -> Emulator / install
  -> adb reverse verification port
  -> launch HotUpdate verification mode
  -> YooAsset update
  -> ResKit
  -> real Android HybridCLR Runtime metadata load
  -> Assembly.Load(HotUpdate.dll)
  -> Entry execution
  -> structured PASS/FAIL
  -> restart/cache check
  -> evidence + cleanup
```

### 6.2 平台产物必须正确

禁止把 Windows64 的 AOT metadata 拿来充当 Android 证据。

Android Gate 必须在 Android BuildTarget 下重新：

- 运行 HybridCLR 目标平台生成步骤；
- 导出 Android 对应 AOT metadata；
- 生成当前 Android HotUpdate Manifest；
- 构建当前 Android YooAsset verification package。

任何目标平台切换、PlayerSettings、Scripting Backend、Architecture 修改都必须 `try/finally` 恢复。

### 6.3 Verification CDN

推荐在 `Tools/AndroidVerification` 增加一个独立、可复用的小型验证服务器，例如 Python 3.11 标准库实现：

- 只服务指定 verification package 根目录；
- 支持普通 GET；
- 支持 HTTP Range；
- 输出访问/Range 日志；
- 可选支持一次性 `interrupt-after-bytes` 故障注入；
- 只绑定 loopback；
- Pipeline finally 中可靠停止。

Android Emulator 使用固定或 Profile 配置端口，并通过：

```text
adb reverse tcp:<port> tcp:<port>
```

让 Release APK 访问 `127.0.0.1:<port>`。不要依赖“第一个 ADB device”。

如果 Android cleartext HTTP 被 Player 拒绝，只允许在 **verification build** 中临时设置对应 Unity Player setting，并在 finally 恢复；不得改变正式发布默认策略。

### 6.4 Runtime 配置注入

不要为了每次测试动态改正式 `HotUpdateSettings.asset`。

优先方式：Android Harness 启动 Activity 时通过 Intent Extras 传入：

- verification enabled；
- host URL / port；
- package name/version（如需要）。

Verification Runtime 在 `UNITY_ANDROID && !UNITY_EDITOR` 下读取当前 Activity Intent。该代码只属于 `StellarFrameworkVerification`，不进入业务 Kit 导出包。

如果最终选择其他配置注入方式，必须满足：

- 不留下临时 Assets diff；
- 不修改正式业务配置；
- Release 非 Development APK 可运行；
- Pipeline 可自动化。

### 6.5 Android PASS 条件

不能只判断 App 没崩。至少必须证明：

1. YooAsset 得到预期 package version；
2. Manifest 与 `HotUpdate.dll.bytes` 可通过 ResKit 读取；
3. SHA256 一致；
4. `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly` 在 Android Release IL2CPP 中实际成功；
5. `HotUpdate.dll` 实际加载；
6. `HotUpdate.HotUpdateMain.Main` 实际执行并留下稳定 marker；
7. Runtime Reporter / logcat 输出机器可读 PASS；
8. force-stop + restart 后仍可再次进入成功状态，并记录 cache / package 行为；
9. Console/build/logcat 不出现未白名单异常。

HTTP Range 故障注入如果同批实现成功，可作为额外 Android gate；若为了控制首批复杂度暂不在 Android 重复 fault injection，则现有 Windows/Editor Range resume Gate 必须继续保留，文档明确“协议恢复能力”和“Android IL2CPP 能力”是两份互补证据。

## 7. P5 — Clean Consumer Import Gate

要把“母工程能用”升级成“其他项目拿走能用”，至少为以下组合做一次真正的干净消费者验证：

- `UIAdaptationKit Complete`
- `Localization Complete`
- `Hot Update Full`

要求使用全新/隔离 Unity 2022.3.62f3c1 项目，不复用已经混入其他框架代码的测试项目制造假 PASS。

每个 Gate 至少验证：

1. `.unitypackage` Bootstrap 导入；
2. 自动 UPM dependency 安装；
3. 编译 0 Error；
4. 目标 asmdef/ToolsHub 入口存在；
5. 最小 Runtime Smoke 成功；
6. 不携带 Catalog 明确排除的 Kit/SDK；
7. Hot Update Full 再执行最小 content + HybridCLR startup gate。

这一步可以后续产品化为 Package Consumer Verification 工具，但首轮不要为了工具化而延迟真实验证。


### P5 实际执行记录 — SEALED / PASS

- UIAdaptationKit Complete Fix3：全新项目 `D:\SF-P5-UI-Fix3-Clean`，Bootstrap/UGUI import PASS，2 个必需 asmdef 存在、排除能力缺失、导入和 Smoke 编译错误均为 0；PlayMode smoke XML **1/1 PASS**。包 SHA256 `4ff67573edbba2bd87d5403367f2bcdec1da87fe4b808541634240ab59c159a8`；机器证据 `D:\SF-P5-UI-Fix3-Clean\P5ConsumerEvidence.json`。
- Localization Complete Fix3：全新项目 `D:\SF-P5-Loc-Fix3-Clean`，Bootstrap 自动安装 TMP + UGUI，4 个必需 asmdef 存在、排除能力缺失、导入和 Smoke 编译错误均为 0；PlayMode smoke XML **1/1 PASS**。包 SHA256 `166b569a6d53ddabcb42db28370a369c67645cc26d5a7d2ed6d76bb8dec95b73`；机器证据 `D:\SF-P5-Loc-Fix3-Clean\P5ConsumerEvidence.json`。
- Hot Update Full Fix3：全新项目 `D:\StellarFramework-ConsumerValidation-P5-Fix3-20260924-0015\HotUpdate`，Bootstrap import PASS，UPM 锁定 HybridCLR `4feac30cb2e105992986c737f7f54992b8300e1a`、UniTask `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`、YooAsset 2.3.19 `5df594f7dc2383735796d8816d636a9b82976ea2`；无旧 HotUpdateKit/UIKit/Addressables。Windows64 Manifest、DLL SHA 与四份 AOT metadata 核验通过；PlayMode Release Gate exact test **1/1 PASS**，覆盖 Range resume、`ResKit:YooAsset` Manifest/DLL、SHA、HybridCLR AOT metadata、Assembly.Load 及 Main entry。包 SHA256 `1025703c01acc15a7eabfbe82e2888ea27aaf932db07b2f2d29131ac48e687d2`；机器证据 `D:\StellarFramework-ConsumerValidation-P5-Fix3-20260924-0015\HotUpdate\P5ConsumerHotUpdate-Evidence.json`。
- Unity Test Runner 使用同一次 batchmode Editor 启动执行 Hot Update Prepare + Gate，避免 Unity 重启时清理 Temp config；超长外部项目路径仅在 consumer harness 通过临时 `P:` drive alias 缩短，不更改框架代码。失败的早期 setup 日志保留供审计，最终 XML PASS；无 `TOOLING EVIDENCE GAP`。
## 8. P6 — Maturity 晋级规则

禁止因为“代码看起来不错”直接改成熟度。

### UIAdaptationKit

当前已是 stable。完成 P1 后继续保持 stable，不需要重新命名或拆包。

### ResKit.YooAsset

开始时为 `rc`。P4/P5 的验证证据满足计划门槛，P6 将其升为 `stable`：

- YooAsset focused tests 全绿；
- Range/cache E2E 仍 PASS；
- Android HotUpdate E2E 中内容更新真实 PASS；
- Clean Consumer Import PASS；
- 无未解决 P0/P1。

### HybridCLRKit

开始时为 `experimental`。P6 按已取得证据升为 `rc`，并保留 Stable 的第二道门槛：

1. Android Release IL2CPP E2E + PlayMode Gate 修复后，评估升 `rc`；该 Gate 与 clean consumer 已 PASS；
2. P7 重新复现 Windows64 Release IL2CPP Gate，并确认 Android Gate、Clean Consumer Import、自动导出/依赖闭包、失败语义仍稳定后，再评估升 `stable`。P5 的 Windows64 consumer PlayMode Gate 不等同于 Windows64 Release Player Gate。

`Hot Update Full` Recommended Profile 的成熟度继续由依赖闭包最低成熟度派生，不手工伪造 Stable。

### P6 实际决策与验证（SEALED / PASS）

- `ResKit.YooAsset`: `rc` → `stable`。证据覆盖 YooAsset focused policy/adapter tests、Range resume/cache PlayMode E2E、P4 真 Android Release IL2CPP 内容更新、P5 Hot Update Full clean consumer import/runtime；无未解决 P0/P1。
- `HybridCLRKit` / `HybridCLRKit.Tools`: `experimental` → `rc`。Android Release IL2CPP、HotUpdate PlayMode Release Gate、P5 clean consumer 均通过；P7 Windows64 Release IL2CPP Gate 才是进一步升 Stable 的必要证据。
- `Hot Update Full`: 自动由闭包最低 maturity 得出 `rc`，不在 Recommended Profile 上手工设置等级。
- Catalog policy、profile maturity expectation、Publisher closure maturity expectation 与当前状态文档已同步修改。
- Unity Test Runner focused policy：`RuntimeKitProfilesUseSchemaV4ArchitectureAndMaturityMetadata` **1/1 PASS** (`1f36734a`)，`RecommendedProfileMaturityUsesWorstDependencyInClosure` **1/1 PASS** (`4427a6e5`)。
- FrameworkValidation 回归：主 `StellarFramework.Tests.FrameworkValidation` filter **614/614 PASS** (`b0df19cd`)，独立 Addressables validation assembly 的 `AddressablesBuildToolTests` **3/3 PASS** (`ee4abe7d`)；合计 **617/617 PASS**，0 failed / 0 skipped。
- 最终 UnitySkills Diagnose：compile/update idle，Console **0 errors / 0 warnings**。P6 **SEALED / PASS**；P7 继续执行 Windows64 Release IL2CPP Gate 和最终全量回归。
- 调试记录：第一次测试请求命中了旧断言程序集，AssetDatabase refresh 后确认 import 完成；第二次检查发现首个宽泛 Catalog patch 改到了 HttpKit 而非 ResKit.YooAsset。已按 profile id 修正并恢复 HttpKit RC，之后最终 focused tests 与全部回归通过。前述两次失败不计入最终验证结果，也未放宽断言或跳过 Gate。

## 9. P7 — 最终回归与交付证据

本计划完成时至少输出：

- Unity compile 0 Error；
- Console 0 Error；
- UIAdaptation focused PASS；
- HotUpdate focused PASS；
- FrameworkValidation 全量 PASS；
- 普通 PlayMode 全量 PASS；
- HotUpdate Release PlayMode Gate PASS；
- Package Publisher / Catalog / Standalone export policies PASS；
- Windows64 IL2CPP HotUpdate Gate 仍可复现；
- Android Release IL2CPP HotUpdate Gate PASS；
- Clean Consumer Import Gate 结果；
- 对应结果文件、日志和必要截图路径；
- `chatgptwebmemory.md` 最新状态。

如果任何 Gate 因 UnitySkills / Test Runner 工具问题无法给出产品断言，必须明确标记为 **TOOLING EVIDENCE GAP**，不能记成 PASS，也不能记成产品 FAIL。

### P7 实际执行记录 — SEALED / PASS

- UIAdaptationKit / HotUpdate focused suites **44/44 PASS**；Catalog / architecture / Publisher / Standalone export maturity policies **89/89 PASS**；Unity Test Runner fresh EditMode discovery **1,568/1,568**。
- FrameworkValidation **614/614 PASS**，独立 `AddressablesBuildToolTests` **3/3 PASS**；`StellarFramework.Tests.PlayMode` 产品测试 **15/15 PASS**。PlayMode fresh discovery 总数为 **17**，另外两项分别是 HotUpdate ReleaseGate 与 UnitySkills PlayModeRecovery。
- 官方 `Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1`：fresh PlayMode discovery **17/17**，exact `YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate` Runnable 且 **1/1 PASS**；machine evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\P7-HotUpdate-PlayMode-ReleaseGate.json`。
- Windows64 Release IL2CPP Player Gate **PASS**：Unity `StandaloneWindows64` 下由 HybridCLR 官方 `GenerateAll()` 重新生成四份 AOT metadata，重建 6-bundle YooAsset verification package，构建 Release IL2CPP full Player（Development=false / scriptsOnly=false / createSolution=false / 0 errors）。Player 真正完成 Range 中断与 offset 262,144 bytes 恢复、经 `ResKit:YooAsset` 读取 Manifest / DLL、载入 `HotUpdate` Assembly 并执行 `HotUpdate.HotUpdateMain.Main`。严格聚合证据=`D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-05\P7-Windows64-Release-IL2CPP-GateEvidence.json`。Build 的 1 条 HybridCLR `CheckSettings` warning（隔离 clean consumer 未配置 source hot-update modules，运行使用 P5 已核验 DLL）已保留并注明，0 build errors。
- P4 Android Release IL2CPP Gate 与 P5 clean consumer Gate 仍使用此前封存的真实 PASS 证据：`Tools/AndroidVerification/Results/20260923-204313/pipeline-result.json` / `result.json`；P5 各 clean consumer evidence 路径及锁定 SHA 见本计划 §7 和 memory。
- P7 maturity 决策：`HybridCLRKit`、`HybridCLRKit.Tools` 从 RC 升为 Stable；`Hot Update Full` 由依赖闭包自动派生为 Stable，未手工设置 Profile maturity。最终分布为 Stable **82** / RC **2** / Experimental **0**（仅 HttpKit、ResKit.Addressables 保持 RC）。
- 精确 PlayMode Release Gate 执行时 Console 记录了预期故障注入的两条错误日志和两条 YooAsset 中止清理 warning；原始日志已归档至 `D:\SF-P7-Win64-20260924\P7Artifacts\P7-Expected-Range-Interruption-Console.log`。归档后清空 Console，最终 UnitySkills Diagnose 为 healthy、compile/update idle、0 errors / 0 warnings。
- P7 机器测试结果保存在 `D:\SF-P7-Win64-20260924\P7Artifacts\`（focused、policy、FrameworkValidation、Addressables、PlayMode 和 Release Gate JSON）。Catalog / maturity tests / memory 的 scoped `git diff --check` **PASS**，新增的验证文档无尾随空格。全仓 `git diff --check` 只报告无关 dirty file `Assets/AddressableAssetsData/AddressableAssetSettings.asset` 第 61、63 行的两个尾随空格；本轮未改动该文件。无 `TOOLING EVIDENCE GAP`，未自动 commit/push。

## 10. Agent 执行顺序

严格按以下顺序推进，不要同时大面积修改：

1. 记录当前 git/compile/test 基线；
2. P1 UIAdaptation cutout fix -> focused tests -> full regression；
3. P2 UniTask pin -> package resolve -> distribution tests；
4. P3 HotUpdate PlayMode discovery -> exact E2E；
5. P4 Android HotUpdate E2E -> 真实 Emulator Release Gate；
6. P5 clean consumer import；
7. 根据真实证据执行 P6 maturity 调整；
8. P7 全量回归、文档、memory；
9. 最后检查 `git diff --check` 和 `git status`，只报告本任务改动与既有脏项边界。

每个 Phase 必须先闭环验证再进入下一个。遇到单项失败时优先定位根因，不通过删测试、改断言、关闭验证、吞异常来制造绿色结果。

## 11. 明确禁止范围

- 不恢复旧 `HotUpdateKit`；
- 不把 Addressables 重新定义成 StellarFramework 正式热更编排；
- 不让 HybridCLRKit 直接依赖 YooAsset；
- 不把 YooAsset 类型引入 ResKit.Core；
- 不为 cutout 建设备型号数据库；
- 不增加每帧 `Screen.cutouts` GC；
- 不把 Verification Runtime/Editor 工具带入业务 Kit 导出包；
- 不为了升成熟度删除失败 Gate；
- 不顺手重构无关 Kit；
- 不覆盖用户当前工作树已有修改。

## 12. 最终目标状态

完成后应达到：

```text
UIAdaptationKit
  = 独立 Stable UGUI 适配包
  = width / height / safeArea / cutout 变化均有闭环
  = 无每帧 cutout GC

LocalizationKit
  = 保持当前独立 Stable 状态，不在本计划扩大范围

Hot Update Full
  = 可重复安装的固定依赖
  = 可发现、可自动运行的 Release Gate
  = Windows64 Release IL2CPP 有证据
  = Android Release IL2CPP 有证据
  = 干净消费者项目可导入运行
  = maturity 由证据而不是主观判断晋级
```
