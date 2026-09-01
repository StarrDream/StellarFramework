# Kit 导出验证矩阵

本文件记录框架原始工程的导出验证基线，是 Evidence Ledger（验证结果台账）。业务项目只需使用导出的 `.unitypackage` 与其同名依赖说明，无需导入本目录。验证职责和目录分类见 [ValidationArchitecture.md](../../StellarFrameworkVerification/ValidationArchitecture.md)。

状态只允许记录真实证据：

~~~text
PASS / FAIL / BLOCKED / SKIPPED / NOT RUN
~~~

NOT RUN 和 BLOCKED 不得写成 PASS；未运行的 Benchmark 不得填写推测数值；空白工程、Player、IL2CPP 或远端热更的环境阻塞要保留原因。

## 当前可选目标

当前目录包含 60 个分发 Profile：2 个单文件目标、2 个 Runtime 支持包、2 个 ToolsHub 包、1 个生成支持包、17 个 Foundation Kit、3 个 Extension Kit、11 个 Adapter Profile，以及 22 个可选样例包。

Catalog schema v2 以 `tier` / `category` 描述架构职责；它不改变 `kind` 的分发语义，也不会让同层 Kit 自动安装。完整规则见 [KitArchitectureGuide.md](KitArchitectureGuide.md)。

关键组合如下：

| 目标 | 导入后包含 | 明确不包含 |
| --- | --- | --- |
| AudioKit.Core | PoolKit、SingletonKit、ToolsHub.Core | ResKit、Addressables、HybridCLR |
| AudioKit.ResKitAdapter | AudioKit.Core、ResKit.Core | Addressables、HybridCLR |
| ConfigKit.Core | 文本来源、路径和覆盖规则 | Newtonsoft Json、Addressables、HybridCLR |
| ConfigKit.NewtonsoftJson | ConfigKit.Core、ToolsHub.Core、JSON 配置工具 | Addressables、HybridCLR |
| SettingsKit.Core | SingletonKit、设置定义与存储 | AudioKit、LogKit、Addressables、HybridCLR |
| SettingsKit.UnityAdapters | SettingsKit.Core、Unity 图形/语言/输入适配器 | AudioKit、Addressables、HybridCLR |
| SettingsKit.AudioKitAdapter | SettingsKit.Core、AudioKit.Core | ResKit、Addressables、HybridCLR |
| TimeKit | LogKit、游戏世界 Tick 与定时调度 | ActionKit、UniTask、Addressables、HybridCLR |
| SaveKit.Core | LogKit、存档容器、Section、事务、Migration 与 FileSystem Storage | Newtonsoft、TimeKit、Addressables、HybridCLR |
| SaveKit.NewtonsoftJson | SaveKit.Core、Newtonsoft JSON Serializer | TimeKit、Addressables、HybridCLR |
| SaveKit.Tools | ToolsHub 存档中心、Verify、Raw/Hex、Migration Type Chain、Dry Run | 不增加运行时领域依赖 |
| GridKit | 负坐标几何、DenseGrid、Footprint、整数 Occupancy | Addressables、HybridCLR、所有其他 Kit 与 UPM |
| SpatialKit | 连续二维点、均匀空间哈希、Rect/Circle 查询、有限半径最近邻 | GridKit、ResKit、Addressables、HybridCLR、所有其他 Kit 与 UPM |
| SimulationKit | SimulationId、索引最小堆、固定预算派发、Staggered 首次延迟、过期合并 | UnityEngine、TimeKit、GridKit、SpatialKit、ResKit、Addressables、HybridCLR、所有其他 Kit 与 UPM |
| PathKit | Graph-first 通用 A* / Dijkstra、正 long 成本、确定性 tie-break、扩展预算、原子路径输出 | UnityEngine、GridKit、Addressables、HybridCLR、所有其他 Kit 与 UPM |
| PathKit.GridKitAdapter | PathKit + GridKit 的 GridPathGraph、Four/Eight、TraversalPolicy、转角策略与负坐标映射 | Addressables、HybridCLR、移动/世界服务与固定 Occupancy 语义 |
| HotUpdate.Core | ResKit.Core、HttpKit、热更策略抽象 | Addressables、HybridCLR、代码热更实现 |
| HotUpdate.Addressables | HotUpdate.Core、ResKit.Addressables | HybridCLR |
| HotUpdate.HybridCLR | HotUpdate.Addressables、HybridCLR 运行时与导出工具 | 无 |

样例 Profile 也遵守同一依赖边界：

| 目标 | 导出内容 | 依赖与排除 |
| --- | --- | --- |
| Sample.TimeKit | TimeKit Sample 脚本、Common 场景说明、`TimeKit_Playable.unity` | 依赖 `TimeKit`；排除 SaveKit、ResKit、Addressables、HybridCLR |
| Sample.SaveKit | SaveKit Sample DTO/Section、Common 场景说明、`SaveKit_Playable.unity` | 依赖 `SaveKit.Core` + UniTask；排除 TimeKit、ResKit、Addressables、HybridCLR、Newtonsoft |
| Sample.GridKit | GridKit 示例脚本、Common 场景说明、`GridKit_Playable.unity` | 依赖 `GridKit`；无 UPM；排除 Addressables、HybridCLR、其他 Kit |
| Sample.SpatialKit | SpatialKit 示例脚本、Common 场景说明、`SpatialKit_Playable.unity` | 依赖 `SpatialKit`；无 UPM；排除 GridKit、ResKit、Addressables、HybridCLR |
| Sample.SimulationKit | SimulationKit 示例脚本、Common 场景说明、`SimulationKit_Playable.unity` | 依赖 `SimulationKit`；无 UPM；手动 tick；排除 TimeKit、GridKit、ResKit、Addressables、HybridCLR |
| Sample.PathKit | 独立 Graph、A* / Dijkstra、加权边与结果面板、`PathKit_Playable.unity` | 依赖 `PathKit`；无 UPM；排除 GridKit、Addressables、HybridCLR |
| Sample.PathKit.GridKitAdapter | 负坐标网格、TraversalPolicy、阻挡/加权/转角策略、`PathKit_GridKitAdapter_Playable.unity` | 依赖 `PathKit.GridKitAdapter`（含 PathKit + GridKit）；无 UPM；排除 Addressables、HybridCLR |

完整 Profile、依赖闭包与 UPM 要求以 [KitDistributionCatalog.json](KitDistributionCatalog.json) 为准。

Verification 边界：StellarFrameworkVerification 不注册为普通 Kit、Sample 或 Adapter Profile；Full Package 和 Kit/Sample Exporter 均通过排除路径保持维护者验证资产不进入用户分发。

样例验证规则：核心 Kit Profile 不包含 `Assets/StellarFramework/Samples`；样例则按 Kit 拆成独立 Profile 和独立 asmdef，导出时才随对应 Kit 闭包一起生成。

## 已执行验证

- Unity 编译：无非预期 Console error。
- 分发边界测试：覆盖单文件导出、Adapter 排除、ToolsHub 程序集识别、依赖闭包与 Catalog 架构元数据。
- TimeKit：EditMode 与 PlayMode 测试通过；单 Kit 安装包已实际导出并检查外层 Bootstrap、内层 payload 与 LogKit 依赖闭包。
- 完整 EditMode：279 项完成，278 通过、0 失败、1 项明确标记为 Player/IL2CPP 环境专用而跳过；HybridCLR AA 全链路用例仍需 Player/IL2CPP 环境。
- 完整 PlayMode：11 项完成，11 通过、0 失败、0 跳过；覆盖 EventKit、BindableKit、SaveKit、TimeKit、UIKit/ResKit 的真实 Runtime 行为。
- Package Publisher 路径边界：Base / Full payload 的框架根与 GameHotUpdate 根均使用目录边界判断；`StellarFrameworkVerification`、`StellarFrameworkBackup`、`StellarFramework2`、`GameHotUpdateBackup` 的 sibling-prefix 回归均被拒绝，实际 Full payload 导出不含 Verification 条目。
- 已实际导出并核对依赖说明：AudioKit.Core / ResKitAdapter、SettingsKit.Core / UnityAdapters / AudioKitAdapter、ConfigKit.Core / NewtonsoftJson。
- HotUpdate.HybridCLR 的完整启动路径已单独验证通过。
- SaveKit.Core：EditMode 覆盖 Slot/Section 安全、Container、Checksum、事务、Backup、Migration、Missing/Unknown、Restore DAG、跨 DTO 类型链和未来版本提前失败；Newtonsoft Adapter 已完成 Round Trip 验证。
- SaveKit：已完成 100000 CropSaveRecord End-to-End Save/Load 基准；ToolsHub 已验证 Raw/Hex 有界预览、Migration Type Chain 和只读 Dry Run 入口。
- SaveKit 示例：独立 asmdef 仅依赖 SaveKit.Core 与 UniTask，不包含框架业务样例或热更插件。
- TimeKit 示例：模板和开发场景均由 `ExamplePlayableSceneBuilder.BuildAllSamples()` 生成，场景只包含相机、方向光、样例脚本和统一说明面板。
- SaveKit 示例：覆盖两个 Section 的 `RestoreAfter` 顺序、Save/Load/Delete、Revision/Diagnostics 和真实 V1→V2 DTO Migration；不解析私有磁盘格式。
- GridKit：17 项 EditMode 行为测试与 1 项 1M/100k 基准通过；覆盖 same-owner 重复失败、cross-owner takeover 防护、Preview self-overlap/只读/他人冲突；Core asmdef 无引用、无 UnityEngine，`GridKit_Playable` 场景验证通过且无 missing script。
- GridKit V1 RC ownership regression：write-side `allowedExistingOccupant` overload 已删除；`TryOccupy` 仅执行 Empty → Owner，`CanOccupy` Preview 永不修改，`TryRelease` 保持 Owner → Empty 原子语义。
- SpatialKit：Core asmdef 无引用且无 UnityEngine；Behavior 13 项与 Benchmark 2 项均通过，覆盖负坐标 floor、Rect/Circle 边界和截断、Nearest tie/exclude、失败原子性与极端查询范围保护；`SpatialKit_Playable` 只挂 SpatialKit 样例脚本和公共说明面板，场景校验无 missing script。
- SpatialKit V1 Final Hardening：Same-Bucket/Cross-Bucket 数据集已显式构造并自证；Sample Circle 改为运行时圆线，QueryMatched/Nearest 使用 SpatialKit 实际返回 ID 高亮，mutation 清理旧查询状态；Core semantic diff = NONE，Core Semantics Frozen = YES。
- SimulationKit V1 Final Hardening：Core semantic diff = NONE；正式明确 `destination.Length` 是单次 `CollectDue` 的 Count Budget，而非 Core 自动识别的 Frame Budget；实时主循环每帧/每个更新周期只 Collect 一次并把 `HasBacklog` 留给下一帧，连续同 tick Drain 保留为 Explicit Flush/Tool/Test/Benchmark 能力；`HasBacklog` 定义为当前 tick 仍有已到期但未派发的 Entry；`SimulationMutationResult` XML 已说明失败 Mutation 保持 Entry 调度状态不变但仍观察 nowTick。Core Semantics Frozen = YES。
- SimulationKit 行为验证：17 项 Core 行为测试与 2 项架构/导出策略测试通过；覆盖 ID 合法性、重复/缺失、时间回退、首延迟、实际派发时间重排、不追赶、预算/积压、稳定排序、注销、改周期、溢出原子性和 Clear。
- SimulationKit Sample Final Hardening：增加 `Frame Step (Collect once)`，明确 Game Tick 与 Frame Step 分离；Burst 的 20 个同刻到期 ID 在 Budget=4 时跨 5 个 Frame Step 消化，Frame Step 不推进 Game Tick；保留 `Manual Drain (same tick)` 作为显式 Flush/Debug 单批操作，Staggered、Register、Unregister、SetInterval 和滚动区域继续可用。
- SimulationKit 性能验证（Unity 2022.3.62f3c1，Editor Test Runner）：100,000 条注册/查询/改周期/注销，Register=18.515 ms、Lookup=12.167 ms、SetInterval=43.349 ms、Unregister=37.395 ms，ManagedHeapDelta=0；100,000 条同刻到期的 **Explicit Backlog Drain Throughput**、Budget=512，196 次 Collect、100,000 次派发、Collect=125.541 ms，ManagedHeapDelta=0；100,000 条交错周期 backlog drain、101 个 tick、Budget=512，2,002 次 Collect、1,001,000 次派发、Dispatch=1525.800 ms，ManagedHeapDelta=4096；1,000,000 条存储压力，10,000 次无到期 Collect=0.328 ms、62,500 次查询=7.421 ms、31,250 次改周期=9.798 ms、Clear=1.748 ms，ManagedHeapDelta=0。Explicit backlog drain 是主动完整清空吞吐基准，不是默认 realtime per-frame 用法；ManagedHeapDelta 只作 coarse heap trend，不是严格零分配证明。
- SimulationKit 导出闭环：已实际生成 `StellarFramework-SimulationKit.unitypackage`、`StellarFramework-Sample-SimulationKit.unitypackage` 以及合并包 `StellarFramework-SimulationKit-With-Sample.unitypackage`。Core payload 仅包含 SimulationKit Runtime 五个 `.cs`、Core asmdef 与两份源码/说明文档；Sample payload 额外包含 Common、Example_SimulationKit 和 `SimulationKit_Playable.unity`，三份 Bootstrap manifest 均无 UPM/Kit 依赖，组合包只声明同一 payload 内的 Core 闭包。
- SimulationKit 空白工程烟测：Core 单 Kit 包在 `C:\GitProjects\SimulationKitCleanImport-20260901-214600` 完成安装、编译与 Bootstrap 清理；最新 Sample 单 Kit 包在 `C:\GitProjects\SimulationKitFinalSampleCleanImport-20260901-225001` 完成安装、编译与 Bootstrap 清理，未发现 CS 编译错误、异常或 Missing 脚本；合并包 payload 另已完成外层 manifest、内层路径和依赖闭包核对。
- SimulationKit Sample 手动验收（Unity 2022.3.62f3c1）：Burst Reset → Advance +5 → Budget 4 → Frame Step 1 返回 `1,2,3,4` 且 `HasBacklog=true`；连续到 Frame Step 5 返回 `17,18,19,20` 且 `HasBacklog=false`，Game Tick 全程保持 5；Staggered 在 tick=9 以 Frame Step 与 Manual Drain 各取一批，Game Tick 未被 Frame Step 改写；Register、Unregister、SetInterval、Manual Drain 与滚动 UI 均可用，控制台无 error/warning。
- 最新 Benchmark 证据（Unity 2022.3.62f3c1）：GridKit 1,000,000 cells，100,000 次 CanOccupy 与 Occupy/Release，fill 0.440 ms、linear read 1.996 ms、coord↔index 21.630 ms、CanOccupy 7.830 ms、Occupy/Release 24.878 ms；SaveKit 100,000 records，file 2,100,125 bytes，save 24.880 ms、load 22.780 ms。
- PathKit V1 Release Candidate：Core 与 GridKit Adapter 的 31 项 EditMode 行为/性能/架构测试通过；覆盖 Graph-first A* / Dijkstra、确定性 tie-break、inconsistent heuristic reopen、CostOverflow、扩展上限、输出原子性、负坐标网格、四/八方向、转角策略与动态 traversal policy。Core Semantics Frozen = NO，仍需独立 review 后再冻结。
- PathKit Benchmark（Unity 2022.3.62f3c1，Editor Test Runner）：64/256/512 方形 Graph 的 A* 与 Dijkstra 均通过，512×512 A* 0.916 ms / 1,022 expanded、Dijkstra 80.074 ms / 262,143 expanded；1,000×1,000 逻辑节点 A* 3.896 ms / 1,998 expanded；重复 1,000 次 ManagedHeapDelta=0。数值仅用于本机趋势，不构成跨平台性能承诺。
- PathKit 导出闭环：已实际生成 `StellarFramework-PathKit.unitypackage`（16,508 bytes）、`StellarFramework-PathKit-GridKitAdapter.unitypackage`（41,737 bytes）、`StellarFramework-Sample-PathKit.unitypackage`（25,436 bytes）和 `StellarFramework-Sample-PathKit-GridKitAdapter.unitypackage`（49,491 bytes）。Core payload 仅含 PathKit Core；Adapter payload 含 PathKit + GridKit + Adapter 闭包；Sample payload 额外含 Common、对应示例和场景，均排除 Tests、Verification、Addressables、HybridCLR。
- PathKit 空白工程烟测：Core 在 `C:\GitProjects\PathKitCleanImport-20260902-0115` 完成 Bootstrap 安装与脚本编译；Adapter 在 `C:\GitProjects\PathKitAdapterCleanImport-20260902-0115` 完成依赖闭包安装与编译；Sample Adapter 在 `C:\GitProjects\PathKitSampleCleanImport-20260902-0115` 完成安装、编译和场景导入。批处理模式仅有许可证 IPC / SkyManager 环境警告，无 CS 编译错误、异常或 Missing 脚本。
- PathKit Sample 手动验收（Unity 2022.3.62f3c1）：Core 与 GridKit Adapter 场景均由 `ExamplePlayableSceneBuilder.BuildAllSamples()` 生成；层级均含 Main Camera、Directional Light、Sample Guide 和示例脚本；PlayMode 截图确认搜索控制、结果面板及网格可视化，停止后 Console error/warning=0。
- 上述 ManagedHeapDelta 来自 GC.GetTotalMemory(false)，只作为 coarse heap / GC trend，不是严格零分配证明。

SpatialKit Benchmark 证据（Unity 2022.3.62f3c1，Editor Test Runner）：100,000 条动态操作，BucketSize=8、InitialCapacity=100000；Insert=11.588 ms、Lookup=3.032 ms、SameBucketUpdate=100000（7.601 ms）、CrossBucketUpdate=100000（14.593 ms）、RectQuery=10000（3.480 ms）、CircleQuery=10000（2.595 ms）、Nearest=10000（9.605 ms）、Remove=6.614 ms、Clear=0.241 ms；SameBucketChecksum=5000050000、CrossBucketChecksum=5000050000、Checksum=7960354605800、ManagedHeapDelta=0。1,000,000 条存储压力 Insert=93.353 ms、抽样查找=1.988 ms、部分移动=3.706 ms、Clear=1.327 ms、Checksum=2896778425750、ManagedHeapDelta=28672。ManagedHeapDelta 只作 coarse heap trend，不是严格零分配证明。

SpatialKit 导出闭环：已实际生成 `StellarFramework-SpatialKit.unitypackage`、`StellarFramework-Sample-SpatialKit.unitypackage` 以及合并包 `StellarFramework-SpatialKit-With-Sample.unitypackage`。Core payload 仅包含 SpatialKit Runtime；Sample payload 额外包含 Common、Example_SpatialKit 和 `SpatialKit_Playable.unity`，Bootstrap manifest 无 UPM 依赖。合并包在新建 Unity 2022.3 空白工程 `C:\GitProjects\SpatialKitCleanImport-20260901-174925` 中完成 payload 导入、脚本编译和安装器清理，未发现编译错误。

## 后续空白工程检查

每次发布前，在独立空白 Unity 工程中按以下顺序抽检：

1. 仅导入 `ToolsHub.Core`，确认 `Kit 安装状态` 可打开且没有 Kit 专属工具。
2. 导入某个 Core 包，确认编译通过且安装状态页只显示其依赖闭包。
3. 再导入其 Adapter，确认仅新增对应能力和工具入口。
4. 对 Addressables、HybridCLR 这类外部插件层，确认未安装插件时入口隐藏，安装后才显示。

> 本机曾尝试对 `ToolsHub.Core` 执行空白工程导入烟测，但 Unity LicensingClient 的 IPC 通道在启动阶段超时（返回码 199），因此该项未计为通过；需在许可服务可用的 Unity 环境重跑。

> 生产放行还必须在目标平台 IL2CPP Player 上执行 `HybridClrAaRunnerCanEnterHotUpdate` 等价的真实远端发布烟测：下载 catalog、bundle、Manifest、DLL 与 AOT metadata，完成 SHA256 校验并进入热更入口。该步骤不能由编辑器测试或离线构建替代。
