# Android Release Verification Harness Plan / Android Release 自动验证工具计划

> 状态：**PLANNED**。当前 Android Release Verification Pipeline 已完成真实端到端 PASS，本计划只负责把现有成果产品化、通用化并长期接入 StellarFramework；不重新搭建已经通过的基础环境。

## 1. 当前基线

- Unity 2022.3.62f3c1 Release Build 已可通过 UnitySkills 或 BatchMode 驱动。
- 独立 Android CLI 环境位于 `C:\Android\Sdk`，不修改 Unity Hub 自带 SDK/JDK。
- 专用 AVD：`StellarFramework_API35`，Android 15 / API 35 / Google APIs / x86_64，WHPX 可用。
- 当前链路已验证：`Build -> Emulator -> ADB install -> Clear Data -> Cold Start -> Runtime Smoke -> logcat -> Screenshot -> Force Stop -> Restart -> PASS/FAIL`。
- 当前正式证据：`Tools/AndroidVerification/Results/20260923-110122` = PASS；Release Build = 0 errors / 0 warnings。

## 2. 目标

把当前 StellarFramework 专用脚本升级为可持续使用的 **Android Verification Harness**：

1. StellarFramework 每次发布前都可以一键执行 Android Release 门禁；
2. 可把 Kit 的真实 Android Runtime 验证接进同一套结果体系，而不只判断“App 没崩”；
3. 能作为独立工具迁移到其他 Unity 工程，避免复制后手改大量脚本；
4. 支持本地开发、ToolsHub 和 CI 三种入口；
5. 测试完成后能够安全、可控地清理产物并关闭专用 Emulator / Android ADB 环境。

## 3. Phase A — 通用化现有 Harness

- 把工程名、Unity 路径、验证场景、APK 输出、AVD、API、ABI、运行时长、失败规则等从脚本硬编码抽为配置/Profile。
- `aapt` 继续自动读取 package name / launch Activity / native ABI，只有诊断场景才允许手动覆盖。
- 保留 AVD-name 精确定位，不允许用“第一个 ADB device”作为默认目标，避免误操作 PICO、手机或其他模拟器。
- 将 Unity Build Driver、Emulator Lifecycle、ADB Smoke、Evidence Collector、Result Aggregator 分层，脚本之间保持单一职责。
- 所有步骤统一返回结构化状态与错误原因，禁止吞异常或仅依靠控制台文本判断成功。

## 4. Phase B — Cleanup / Shutdown 生命周期

必须补齐正式的 **清理和关闭 Android ADB** 功能，并与“只停模拟器”区分开：

### B1. Stop Target Emulator

- 只根据配置的 AVD 名称停止本 Harness 启动的目标模拟器。
- 不关闭真实 Android/PICO 设备，不杀无关 Emulator。
- Pipeline 自己启动 Emulator 时，默认在 `finally` 中回收；用户明确 `KeepEmulator` 时才保留。

### B2. Stop ADB Server

- 提供显式 `Stop ADB Server` / `Shutdown Android Verification Environment` 操作，底层使用独立 SDK 的 `adb kill-server`。
- 因 ADB Server 是机器级共享进程，该操作必须与“Stop Target Emulator”分开，UI/CLI 明确提示它会断开当前机器所有 ADB 会话。
- `adb kill-server` 后的关闭确认不得再次调用 `adb devices`，避免为了验证反而把 ADB Server 重新拉起；使用进程级检查确认 `adb.exe` 是否退出。

### B3. Clean Generated Verification Data

- 只允许清理 Harness 自己生成的目录，不使用无边界 `git clean`、递归删除工程目录或模糊通配删除。
- 可清理范围：
  - `Library/StellarFramework/AndroidVerification/` 的握手/临时状态；
  - `Builds/AndroidVerification/` 的验证 APK 与中间输出；
  - `Tools/AndroidVerification/Results/` 中超过保留策略的历史结果。
- 默认保留最近一次 PASS/FAIL 证据；提供 `KeepLatestResults` / retention count 或 days 配置。
- 清理动作生成自己的摘要，明确列出删除了什么、保留了什么。

### B4. Full Shutdown & Clean

提供一个面向开发者的“一键收尾”入口：

`Stop target AVD -> wait process exit -> adb kill-server -> optional generated-data cleanup -> process-level verify -> summary`

支持至少以下开关：`KeepEmulator`、`KeepAdb`、`NoCleanup`、`KeepLatestResults`。默认行为必须安全，不能影响源码、Library 其他模块、其他构建产物或外接设备内容。

## 5. Phase C — Runtime Verification API

在 Unity Player 内增加轻量、可选、仅用于验证环境的 Runtime Reporter，使外层 ADB 不再只判断“进程存活”：

- 支持 `Begin / Pass / Fail / Complete` 或等价 Step API；
- 记录步骤名、耗时、异常、可选指标；
- 输出机器可读 JSON，并同时保留 Unity Log 证据；
- Runtime 层不直接依赖 PowerShell/ADB；Android Harness 通过 Adapter 采集结果；
- Release Verification Scene 可以主动报告 `Bootstrap`、`SaveKit`、`ResKit` 等步骤是否真正成功。

## 6. Phase D — StellarFramework Android Kit Gates

优先验证适合 Android Emulator 的能力，不为了覆盖率强行测试硬件相关 Kit：

- Bootstrap / Architecture：初始化、场景进入、核心 Service 注册。
- SaveKit：写入 -> 退出/重启 -> 读取 -> 数据一致性；后续增加迁移场景。
- ResKit：加载/释放/重复加载；Addressables/YooAsset Adapter 在其合法运行模式下独立验证。
- FlowKit：最小流程从 Start 到 Complete 闭环。
- Time / Pool / FSM / Simulation 等纯 Runtime 能力：运行步骤与异常门禁。
- Http/Network：只做可控测试端点/本地 mock，不让公网波动决定 Framework Release PASS/FAIL。
- UIKit：基础页面创建/切换/销毁与异常检查；视觉像素比对作为可选层，不作为首阶段硬门禁。

每个 Gate 都必须有明确的 **PASS 条件**，不能用“没有看到报错”替代功能断言。

## 7. Phase E — ToolsHub / Editor 入口

在 ToolsHub 或独立 Verification 面板提供：

- Check Android Environment
- Build Release Verification APK
- Run APK Smoke
- Run Full Verification
- Open Latest Result / Logs / Screenshot
- Stop Target Emulator
- Stop ADB Server
- Clean Verification Artifacts
- Shutdown & Clean Android Verification Environment

UI 只负责配置、触发和展示；核心执行逻辑继续放 Service/脚本层，避免 EditorWindow 成为业务实现主体。

## 8. Phase F — 跨工程复用

在 StellarFramework 内稳定后，抽出可复用发行形态，优先考虑 Editor-only UPM 包，例如：

`com.starrdream.unity-android-verification`

其他 Unity 工程安装后只需要提供 Profile/验证场景即可复用 Build、Emulator、ADB、Evidence、Cleanup、Result Aggregation。

要求：

- 不依赖 StellarFramework Runtime Core；
- 项目特定 Runtime Probe 通过接口/Adapter 注入；
- 不修改目标工程的 Unity External Tools SDK/JDK 配置；
- 默认不要求固定 package name；
- 能在 Windows + Unity LTS 下独立执行，并预留 macOS/Linux runner 扩展点而不提前过度实现。

## 9. Phase G — CI / Release Gate

最终形成推荐发布链：

`Compile -> EditMode -> PlayMode -> Framework Validation -> Android Release Build -> Android Emulator Runtime Verification -> Result Archive -> PASS/FAIL`

CI 输出至少包含：

- build result / warnings / errors；
- APK metadata；
- first-launch / restart result；
- runtime verification steps；
- full + app-scoped logcat；
- screenshot；
- machine-readable JSON summary；
- cleanup/shutdown summary。

## 10. 明确边界

Android Emulator Gate 不能替代：

- PICO/OpenXR/手势追踪/头显传感器；
- ARM64 真机 native plugin；
- 厂商 ROM 行为；
- 真机 GPU/Shader/热量/功耗/帧率；
- 真实局域网多人和物理设备交互。

这些继续属于 Device Verification。模拟器负责在更早阶段拦截 Build、安装、启动、生命周期、资源、存档、普通 UI/网络和通用 Runtime 回归。

## 11. 完成标准

- StellarFramework 可从 ToolsHub/CLI 一键得到 Android Release PASS/FAIL。
- 至少一组核心 Kit 不再只依赖“进程未崩”，而是通过 Runtime Reporter 给出功能级断言。
- 一键 Shutdown & Clean 能安全关闭目标 AVD、按需关闭 ADB Server、清理限定生成物并留下摘要。
- 工具可以在第二个普通 Unity 工程中仅靠配置复用，不修改其 Runtime 架构。
- 所有新增脚本/Editor/Runtime API 有完整注释和 FrameworkDoc 文档。
- 母工程原有 EditMode / PlayMode / Framework Validation / Standalone 等 Gate 不发生回归。
