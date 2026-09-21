# Kit 导出验证矩阵

本文件记录框架原始工程的导出验证基线，是 Evidence Ledger（验证结果台账）。业务项目只需使用导出的 `.unitypackage` 与其同名依赖说明，无需导入本目录。验证职责和目录分类见 [ValidationArchitecture.md](../../StellarFrameworkVerification/ValidationArchitecture.md)。

状态只允许记录真实证据：

~~~text
PASS / FAIL / BLOCKED / SKIPPED / NOT RUN
~~~

NOT RUN 和 BLOCKED 不得写成 PASS；未运行的 Benchmark 不得填写推测数值；空白工程、Player、IL2CPP 或远端热更的环境阻塞要保留原因。

## 当前可选目标

当前 Catalog 包含 **67 个分发 Profile**：2 个 single-file、2 个 shared-runtime、5 个 tooling、1 个 generated-support、19 个 `kit`、38 个 `kit-with-dependencies`；架构 tier 为 **21 Foundation / 9 Extension / 27 Adapter / 10 non-tier**。Catalog 当前不再包含 sample Profile。

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
| LocalizationKit.Core | LocaleId/Key、不可变 Table/Catalog、显式 Fallback、Lookup/切换事件、命名参数格式化 | 零依赖、engine-free；不含 SettingsKit/UIKit/Unity/字体 |
| LocalizationKit.SettingsAdapter | LocalizationKit.Core + SettingsKit.Core，桥接 ILanguageSettingsAdapter | 不包含 UIKit；Core 不反向依赖 SettingsKit |
| LocalizationKit.UnityUGUIAdapter | ScriptableObject Table/Catalog、LocalizationContext、LocalizedText/Button | 仅 LocalizationKit.Core + com.unity.ugui；不包含 SettingsKit/UIKit |
| LocalizationKit.Editor | zh-CN/en-US coverage、duplicate/missing/empty/fallback validator | Editor-only；不进入 Player Runtime |
| TimeKit | LogKit、游戏世界 Tick 与定时调度 | ActionKit、UniTask、Addressables、HybridCLR |
| SaveKit.Core | LogKit、存档容器、Section、事务、Migration 与 FileSystem Storage | Newtonsoft、TimeKit、Addressables、HybridCLR |
| SaveKit.NewtonsoftJson | SaveKit.Core、Newtonsoft JSON Serializer | TimeKit、Addressables、HybridCLR |
| SaveKit.Tools | ToolsHub 存档中心、Verify、Raw/Hex、Migration Type Chain、Dry Run | 不增加运行时领域依赖 |
| GridKit | 负坐标几何、DenseGrid、Footprint、整数 Occupancy、Square/Hex Topology、Hex Edge/Vertex | Addressables、HybridCLR、所有其他 Kit 与 UPM |
| WorldKit.Core | 有限/无限 World、64-bit Chunk/Region、Chunk lifecycle、typed World/Region/Chunk data layer、Dirty/Delta | UnityEngine、GridKit、SpatialKit、PathKit、SaveKit、SimulationKit、WorldGenKit、PlacementKit、Addressables、HybridCLR |
| WorldGenKit.Core | 强类型 Channel/六类 Storage、Stage DAG Compiler、稳定 Seed、Rule 原语、GenerationReport | UnityEngine、WorldKit、GridKit、SpatialKit、PathKit、SaveKit、SimulationKit、PlacementKit、Addressables、HybridCLR |
| WorldGenKit.Builtins | Planar Sample、Fractal Height、可选 Moisture、WaterDepth、Slope、Stable-ID Biome/Surface、Buildable Mask | 仅依赖 WorldGenKit.Core；排除 UnityEngine、WorldKit、GridKit、PathKit、SaveKit、PlacementKit、Addressables、HybridCLR |
| WorldGenKit.Authoring | Typed buffer import、Stable-ID semantic import、Sparse AuthoringOverride、Height edit、Semantic Paint、Dirty Region、Regional Recompute | 仅依赖 WorldGenKit.Core + WorldGenKit.Builtins；排除 UnityEngine/UnityEditor、WorldKit、GridKit、PathKit、SaveKit、PlacementKit、Addressables、HybridCLR |
| WorldGenKit.Resources | Stable Resource/Category/Occupancy ID、Density/Coverage/Cluster/Richness、Budget、MinSpacing、Occupancy Resolver、Player Settings/Exposure | **仅依赖 WorldGenKit.Core**；排除 UnityEngine/UnityEditor、Builtins/Authoring、WorldKit、GridKit、SpatialKit、PathKit、SaveKit、PlacementKit、Addressables、HybridCLR |
| WorldGenKit.Feature | Landmark/Area/Compound、Quota、Reservation、Terrain Adaptation intent、Compound Layout | 只依赖 WorldGenKit.Core；Core 不依赖 Resources/Placement/WorldKit/SaveKit/Authoring/Unity |
| PlacementKit.Core | Rectangle/Circle Footprint、PlacementRequest、Slope/Water/Zone/Conflict/Connection rules、Failure IDs、自定义 Context | 零依赖、无 UnityEngine；不依赖 WorldKit/WorldGenKit/GridKit/SaveKit |
| Feature.ResourcesAdapter | Feature Reservation → Resource Occupancy | 只组合 Feature + Resources；错误生成顺序会原子失败 |
| Feature.PlacementAdapter | Feature Candidate/Footprint → PlacementRequest | 只组合 Feature + PlacementKit.Core |
| Feature.AuthoringAdapter | Flatten/Carve/Fill/Stamp → Authoring + Dirty Region | 只组合 Feature + Authoring/Builtins/Core |
| Feature.WorldKitAdapter / SaveKitAdapter | World/Region Feature usage tracking + Stable-ID SaveSection | WorldKit tracking 与 SaveKit persistence 均为可选边界；Feature Core 不反向依赖 |
| WorldGenKit.DebugTextureAdapter | Dense float/int Channel → Texture2D debug projection | 只依赖 Core + Builtins；调用方拥有 Texture/palette/pixel buffer |
| WorldGenKit.MeshAdapter | Dense Height → heightfield Mesh | 只依赖 Core + Builtins；调用方拥有 Mesh/scratch buffers；无 WorldKit/GridKit 依赖 |
| WorldGenKit.TilemapAdapter | Dense int semantic index → Tilemap block | 只依赖 Core + Builtins；显式 Tile palette/cell origin；非法 index 在 mutation 前失败 |
| WorldGenKit.UnityTerrainAdapter | Dense Height → TerrainData normalized heightmap | 只依赖 Core + Builtins；不隐式 resize TerrainData/改 world size；source range 显式 |
| WorldFramework.ToolsHub | World/Profile/Pipeline/Channel/Biome/Resource/Feature/Placement Authoring + Chunk/DataLayer/Delta Diagnostics + Heatmap/Memory/Validator | Editor-only tooling；依赖 ToolsHub.Core + WorldKit.Core + WorldGen Core/Builtins/Resources/Feature + PlacementKit.Core；Runtime 不反向依赖 |
| GridKit.UnityProjectionAdapter | TerrainData / MeshCollider / Physics → GridBakeCell + independent ManualOverride | 物理 source path 独立；只依赖 GridKit.Core + UnityEngine；GridKit Core export 不包含 Adapter；PathKit 组合留在项目层 |
| WorldKit.Streaming | Region Layout、Demand Policy、Metadata/Data/Simulation/Presentation Tier、Reconciler | 只依赖 WorldKit.Core；不修改冻结 WorldChunkState；无 Unity/WorldGen/SaveKit 依赖 |
| WorldGenKit.StreamingAdapter | Chunk/Region → absolute WorldGen RunKey、确定性按需生成 | WorldKit.Streaming + WorldGen Core/Builtins；无 Unity/SaveKit |
| WorldKit.Streaming.SaveKitAdapter | 显式 Delta Codec、Stable Snapshot、原子 Restore、SaveSection | WorldKit + Streaming + SaveKit.Core；不直接序列化 IWorldDelta 多态类型 |
| WorldKit.Streaming.UnityAdapter | double logical position ↔ small Unity Vector3、snapped floating origin | Unity-only 横向边界；Core/Streaming 不引用 UnityEngine |
| SpatialKit | 连续二维点、均匀空间哈希、Rect/Circle 查询、有限半径最近邻 | GridKit、ResKit、Addressables、HybridCLR、所有其他 Kit 与 UPM |
| SimulationKit | SimulationId、索引最小堆、固定预算派发、Staggered 首次延迟、过期合并 | UnityEngine、TimeKit、GridKit、SpatialKit、ResKit、Addressables、HybridCLR、所有其他 Kit 与 UPM |
| PathKit | Graph-first 通用 A* / Dijkstra、正 long 成本、确定性 tie-break、扩展预算、原子路径输出 | UnityEngine、GridKit、Addressables、HybridCLR、所有其他 Kit 与 UPM |
| PathKit.GridKitAdapter | PathKit + GridKit 的 GridPathGraph、Four/Eight、TraversalPolicy、转角策略与负坐标映射 | Addressables、HybridCLR、移动/世界服务与固定 Occupancy 语义 |
| FlowKit.Core | 纯 C# Graph/Compiler/Plan、Runner、Timer、Signal、State、Blackboard、Polling、Operation、Parallel/Race/Join 与 Snapshot | UnityEngine、UniTask、Addressables、HybridCLR、UI、资源和业务对象 |
| FlowKit.UnityIntegration | FlowHost、稳定 FlowBinding、JSON Graph 入口 | UniTask、Addressables、HybridCLR、ResKit、ToolsHub |
| FlowKit.ToolsHub | ToolsHub 内嵌 FlowKit 编辑器、Graph Validator、运行时诊断 | Editor-only；不进入玩家 Runtime；无独立 FlowKit 顶层菜单 |
| ResKit.Addressables | ResKit.Core + Addressables Load/Release Adapter | HybridCLR、YooAsset、catalog/download 热更新编排 |
| ResKit.YooAsset | ResKit.Core、UniTask、YooAsset 2.3.x Adapter | Addressables、HybridCLR、YooAsset 启动/版本/下载流程 |
| HybridCLRKit | ResKit.Core、HybridCLR 运行时与 DLL/AOT/Manifest 导出工具 | Addressables、YooAsset、HttpKit、内容版本/下载流程 |

完整 Profile、依赖闭包与 UPM 要求以 [KitDistributionCatalog.json](KitDistributionCatalog.json) 为准。

Demo / Verification 边界：

- Catalog 不再包含任何 `samples.*` Profile。
- `Assets/StellarFramework/Samples/ArchitectureDemo` 只作为仓库内唯一入门 Demo，不参与 Kit 导出和 Full/Base Package 分发。
- `StellarFrameworkVerification` 只用于维护者发布验证，不注册为 Kit、Demo 或 Adapter Profile。
- 自动回归由 `Assets/StellarFramework/Tests` 负责，不再通过逐 Kit Playable Sample 证明正确性。

## 已执行验证

- Unity 编译：无非预期 Console error。
- 分发边界测试：覆盖单文件导出、Adapter 排除、ToolsHub 程序集识别、依赖闭包与 Catalog 架构元数据。
- TimeKit：EditMode 与 PlayMode 测试通过；单 Kit 安装包已实际导出并检查外层 Bootstrap、内层 payload 与 LogKit 依赖闭包。
- 完整 EditMode（`StellarFramework.FrameworkValidation.Tests`）：270 项完成，270 通过、0 失败、0 跳过；这是本轮 `PathKit V1 Final Hardening` 之后重新运行的真实总数。HybridCLR AA 全链路仍需 Player/IL2CPP 环境。
- 完整 PlayMode：11 项完成，11 通过、0 失败、0 跳过；覆盖 EventKit、BindableKit、SaveKit、TimeKit、UIKit/ResKit 的真实 Runtime 行为。
- Package Publisher 路径边界：Base / Full payload 的框架根与 GameHotUpdate 根均使用目录边界判断；`StellarFrameworkVerification`、`StellarFrameworkBackup`、`StellarFramework2`、`GameHotUpdateBackup` 的 sibling-prefix 回归均被拒绝，实际 Full payload 导出不含 Verification 条目。
- 已实际导出并核对依赖说明：AudioKit.Core / ResKitAdapter、SettingsKit.Core / UnityAdapters / AudioKitAdapter、ConfigKit.Core / NewtonsoftJson。
- HybridCLRKit 的运行时与分发边界由独立策略测试覆盖；目标平台 IL2CPP 的真实内容更新、metadata 加载和入口执行仍作为发布 Gate。
- FlowKit 当前以 Core / Editor 行为测试、Graph Validator 与 ToolsHub 入口作为自动验证面；V1 Snapshot 仅覆盖 quiescent 终态与 Persistent Blackboard/State，不提供中途 continuation 恢复。空白工程导入、Player/IL2CPP 和真实外部 Operation 仍需按目标平台执行。
- SaveKit.Core：EditMode 覆盖 Slot/Section 安全、Container、Checksum、事务、Backup、Migration、Missing/Unknown、Restore DAG、跨 DTO 类型链和未来版本提前失败；Newtonsoft Adapter 已完成 Round Trip 验证。
- SaveKit：已完成 100000 CropSaveRecord End-to-End Save/Load 基准；ToolsHub 已验证 Raw/Hex 有界预览、Migration Type Chain 和只读 Dry Run 入口。
- SaveKit 示例：覆盖两个 Section 的 `RestoreAfter` 顺序、Save/Load/Delete、Revision/Diagnostics 和真实 V1→V2 DTO Migration；不解析私有磁盘格式。
- GridKit：既有 17 项 EditMode 行为测试保持通过；Topology 9 项测试通过，覆盖 Square4/8 兼容、Hex 邻居/距离/Ring/Range、Int32 溢出、caller buffer、canonical Hex Edge/Vertex；Core asmdef 继续无引用、无 UnityEngine。Standalone Source Export Policy 30/30 通过。
- WorldKit.Core：15 项 Behavior Tests 通过（7 项 identity/geometry/extent/lifecycle + 8 项 data-layer/chunk-registry/dirty/delta）；World Framework Foundation Boundary 5/5、Kit Architecture Metadata 5/5、Standalone Source Export Policy 30/30 通过。Core asmdef `references=[]`、`noEngineReferences=true`，源码不依赖 UnityEngine/GridKit/SpatialKit/PathKit/SaveKit/SimulationKit/WorldGenKit/PlacementKit。
- WorldGenKit.Core：行为验证 18/18 PASS（Channel/Seed 4、Pipeline/Plan 9、Storage/Rule 5），覆盖自定义 `game.magic_density`（无 Temperature）、稳定 PlanHash 与 GenerationReport；World Framework Boundary 6/6、Kit Architecture Metadata 5/5、Standalone Source Export Policy 30/30 PASS。Core asmdef `references=[]`、`noEngineReferences=true`，源码门禁禁止 Unity、WorldKit/既有 Kit、reflection scan、LINQ/yield hot path、`Dictionary<string, object>`、`string.GetHashCode()` 与 Unity Random。
- WorldGenKit.Builtins：Terrain 6/6 + Biome/Surface/Buildable 6/6 = 12/12 PASS；最终 World Framework Boundary 7/7、Kit Architecture Metadata 5/5、Standalone Source Export Policy 30/30 PASS。Builtins asmdef 只依赖 WorldGenKit.Core、`noEngineReferences=true`，并已验证 absolute-coordinate seam continuity、Imported Height 继续派生、optional Moisture、Stable-ID Biome/Surface、Buildable 与七阶段确定性全链路。
- WorldGenKit.Authoring：Import/Override 6/6 + Operations/Region/Stable-ID Paint 6/6 = **12/12 PASS**。Authoring asmdef 仅引用 Core + Builtins、`noEngineReferences=true`；最终 World Framework Boundary 8/8、Kit Architecture Metadata 5/5、Standalone Source Export Policy 30/30 PASS；相关 Core/Builtins/Authoring non-benchmark seal total 85/85 PASS。
- WorldGenKit.Resources：Occupancy 6/6 + Generation 8/8 + Budget/Spacing 6/6 + Exposure 5/5 = **25/25 PASS**。Resources asmdef 仅引用 WorldGenKit.Core、`noEngineReferences=true`；最终 World Framework Boundary 9/9、Kit Architecture Metadata 5/5、Standalone Source Export Policy 30/30 PASS；相关 WorldGen non-benchmark seal total **111/111 PASS**。Resources 当前契约已冻结。
- Feature / Placement 最终行为与策略证据：Feature Contract 8/8 + Resolver 7/7 + Feature.ResourcesAdapter 5/5 + PlacementKit.Core 8/8 + Feature.PlacementAdapter 5/5 + Compound 5/5 + Feature.AuthoringAdapter 5/5 + Feature WorldKit/SaveKit persistence 5/5 = **48/48 PASS**；Authoring + Resources targeted frozen-layer regression **37/37 PASS**；World Framework Boundary **16/16 PASS**；Kit Architecture Metadata **5/5 PASS**；Standalone Source Export **30/30 PASS**。最终 relevant non-benchmark total = **136/136 PASS, 0 failed, 0 skipped**；对应 Benchmark **1/1 PASS**。经用户明确授权后，仅清理 `ProjectSettings/EditorSettings.asset:39` 的既有尾随空格，repository-wide `git diff --check` 随后 PASS；最终 Unity diagnose healthy、0 console errors / 0 warnings。**Feature / Placement baseline = FROZEN / PASS。**
- Unity Presentation 最终证据：DebugTexture + Mesh + Tilemap + UnityTerrain Presentation Adapter tests **9/9 PASS**；Core/Builtins/Authoring frozen regression **42/42 PASS**；World Framework Boundary **20/20 PASS**；Kit Architecture Metadata **5/5 PASS**；Standalone Source Export **30/30 PASS**，合计 **106/106 relevant non-benchmark PASS, 0 failed, 0 skipped**；Presentation Benchmark **1/1 PASS**。四个 Adapter 只引用 WorldGenKit.Core + Builtins，Core/Builtins 继续 engine-free；Catalog 注册四个独立 adapter profile，总数 81，requiredProfileIds 0 缺失。**Unity Presentation baseline = FROZEN / PASS。**
- Streaming 最终证据：Streaming Core **11/11** + WorldGen Streaming Adapter **5/5** + SaveKit Delta Adapter **4/4** + Floating Origin **5/5** + E2E **2/2** + World Framework Boundary **24/24** + Metadata **5/5** + Standalone Source Export **30/30** = **86/86 key validation PASS**；另对既有 frozen runtime 进行小组式回归，WorldKit 15 + WorldGen Core 18 + Builtins 12 + Authoring 12 + Resources 25 + Feature/Placement 48 + Presentation 9 = **139/139 PASS**。Catalog 注册 `worldkit.streaming`、`worldgenkit.streaming`、`worldkit.streaming.savekit`、`worldkit.streaming.unity` 后共 **85 profiles**，requiredProfileIds 0 缺失。Streaming churn benchmark **1/1 PASS**：metadataRadius=24、2,401 target residents、200 movement steps、intentional seal min/median **41.033/41.088 ms**；final Unity diagnose healthy、0 error / 0 warning，repository-wide `git diff --check` PASS。**Streaming baseline = FROZEN / PASS。**
- Streaming 当前功能证据：WorldKit.Streaming Core **11/11 PASS**；WorldGen Streaming Adapter **5/5 PASS**；SaveKit Delta Adapter **4/4 PASS**；Floating Origin **5/5 PASS**；端到端 unload/rebuild + saved-delta restore **2/2 PASS**；World Framework Boundary **24/24 PASS**。Streaming Benchmark **1/1 PASS**：2,401 resident target、200 movement steps，streaming churn min/median **40.487/40.653 ms**，transitionChecksum=138,507,200，finalResident=2,401；预热后五次 `RunMovement` 热路径 `GC.GetAllocatedBytesForCurrentThread()` 合计 **0 bytes**，`GC.GetTotalMemory(false)` coarse heap delta=4,096 bytes。Catalog 当前 85 profiles，Metadata/Standalone 与冻结层最终 seal 完成后正式冻结。
- GridKit V1 RC ownership regression：write-side `allowedExistingOccupant` overload 已删除；`TryOccupy` 仅执行 Empty → Owner，`CanOccupy` Preview 永不修改，`TryRelease` 保持 Owner → Empty 原子语义。
- SpatialKit：Core asmdef 无引用且无 UnityEngine；Behavior 13 项与 Benchmark 2 项均通过，覆盖负坐标 floor、Rect/Circle 边界和截断、Nearest tie/exclude、失败原子性与极端查询范围保护。
- SpatialKit V1 Final Hardening：Same-Bucket/Cross-Bucket 数据集已显式构造并自证；Core semantic diff = NONE，Core Semantics Frozen = YES。
- SimulationKit V1 Final Hardening：Core semantic diff = NONE；正式明确 `destination.Length` 是单次 `CollectDue` 的 Count Budget，而非 Core 自动识别的 Frame Budget；实时主循环每帧/每个更新周期只 Collect 一次并把 `HasBacklog` 留给下一帧，连续同 tick Drain 保留为 Explicit Flush/Tool/Test/Benchmark 能力；`HasBacklog` 定义为当前 tick 仍有已到期但未派发的 Entry；`SimulationMutationResult` XML 已说明失败 Mutation 保持 Entry 调度状态不变但仍观察 nowTick。Core Semantics Frozen = YES。
- SimulationKit 行为验证：17 项 Core 行为测试与 2 项架构/导出策略测试通过；覆盖 ID 合法性、重复/缺失、时间回退、首延迟、实际派发时间重排、不追赶、预算/积压、稳定排序、注销、改周期、溢出原子性和 Clear。
- SimulationKit 性能验证（Unity 2022.3.62f3c1，Editor Test Runner）：100,000 条注册/查询/改周期/注销，Register=18.515 ms、Lookup=12.167 ms、SetInterval=43.349 ms、Unregister=37.395 ms，ManagedHeapDelta=0；100,000 条同刻到期的 **Explicit Backlog Drain Throughput**、Budget=512，196 次 Collect、100,000 次派发、Collect=125.541 ms，ManagedHeapDelta=0；100,000 条交错周期 backlog drain、101 个 tick、Budget=512，2,002 次 Collect、1,001,000 次派发、Dispatch=1525.800 ms，ManagedHeapDelta=4096；1,000,000 条存储压力，10,000 次无到期 Collect=0.328 ms、62,500 次查询=7.421 ms、31,250 次改周期=9.798 ms、Clear=1.748 ms，ManagedHeapDelta=0。Explicit backlog drain 是主动完整清空吞吐基准，不是默认 realtime per-frame 用法；ManagedHeapDelta 只作 coarse heap trend，不是严格零分配证明。
- SimulationKit 导出闭环以当前 Catalog 的 Runtime/Core Profile 为准；旧 Sample/With-Sample 包不再属于当前分发产品。
- 最新 Benchmark 证据（Unity 2022.3.62f3c1）：GridKit 既有 1,000,000 cells / 100,000 Occupancy 基准继续作为历史趋势；Topology 1,000,000 次 neighbor+distance 查询：Orthogonal4=61.336 ms、Orthogonal8=109.054 ms、Hex6=46.011 ms、checksum=21,000,000、`GC.GetTotalMemory(false)` allocationDelta=0。该 allocationDelta 仍只是 coarse heap trend，不是严格零分配证明。SaveKit 100,000 records，file 2,100,125 bytes，save 24.880 ms、load 22.780 ms。
- WorldKit Benchmark（Unity 2022.3.62f3c1，100,000 Chunk）：register=9.216 ms、两级 lifecycle transition=28.204 ms、typed Chunk Layer write=7.044 ms、read=7.956 ms、Dirty mark+write=8.833 ms、unload+remove=48.084 ms、checksum=4,999,950,000、dirtyWritten=100,000、`GC.GetTotalMemory(false)` allocationDelta=0。数值只作本机趋势，不构成跨平台性能承诺或严格零分配证明。
- WorldGenKit Core Benchmark（Unity 2022.3.62f3c1）：1,000,000 Dense writes=3.013 ms、250,000 sampled Dense reads=0.485 ms；100,000 Sparse writes=0.768 ms、reads=0.748 ms；100,000 Chunked writes=2.590 ms、reads=2.497 ms；1,000,000 typed handle storage resolves=17.523 ms；100,000 two-stage plan runs=58.870 ms；checksum=1,041,300,906,728；`GC.GetTotalMemory(false)` allocationDelta=0。数值只作本机趋势，不构成跨平台性能承诺或严格零分配证明。
- WorldGenKit.Builtins Benchmark（Unity 2022.3.62f3c1）：512×512=262,144 samples、7-stage Height/Moisture/Water/Slope/Biome/Surface/Buildable；compiled-noise-key 优化后 compile=1.185 ms、run=366.175 ms、buildable=203,295、water=58,849、checksum=11,548,786、`GC.GetTotalMemory(false)` allocationDelta=0。优化前同基准 run=931.154 ms；当前结果约降低 60.7%。数值只作本机单线程 Editor 趋势，不构成目标平台性能承诺。
- WorldGenKit.Authoring Benchmark（Unity 2022.3.62f3c1）：512×512 map；64×64=4,096 sample sparse Height Lower edit；dirty cascade=4,356 samples。两次真实 EditMode 观测：edit=0.209–1.123 ms、full Base+Override compose=0.247–0.543 ms、regional Water/Slope/Biome/Surface/Buildable recompute=0.161–0.396 ms；water=4,096、blocked=4,352、checksum=77,696，两次 `GC.GetTotalMemory(false)` allocationDelta 均为 0。数值只作本机单线程 Editor 趋势，不构成目标平台性能承诺。
- WorldGenKit.Resources Benchmark（Unity 2022.3.62f3c1）：512×512=262,144 samples，20,763 Density candidates，9,527 accepted，11,236 MinSpacing rejects。因单次 Editor timing 观测存在明显波动，最终 benchmark 改为 **1 次预热 + 5 次测量**：generate min/median=10.438/10.714 ms，Budget/MinSpacing/Occupancy resolve min/median=9.402/9.605 ms，checksum=36,212,402,757，`GC.GetTotalMemory(false)` coarse allocationDelta=4,096 bytes。数值只作本机单线程 Editor 趋势，不构成目标平台性能承诺或严格零分配证明。
- WorldGenKit.Feature + PlacementKit Benchmark（Unity 2022.3.62f3c1）：**1 次预热 + 5 次测量**；4,096 个互不重叠 Feature candidates 经 deterministic Resolver，min/median=**54.905/59.205 ms**；100,000 次 PlacementEvaluator、六条 built-in rules，min/median=**61.373/62.651 ms**；checksum=375,021,110，`GC.GetTotalMemory(false)` coarse allocationDelta=36,864 bytes。数值只作本机 Editor 趋势，不构成目标平台性能承诺或严格零分配证明。
- WorldGenKit Unity Presentation Benchmark（Unity 2022.3.62f3c1）：**1 次预热 + 5 次测量**；DebugTexture/Mesh/Tilemap 共用 128×128=16,384 sample logical dataset，min/median 分别为 **0.826/0.855 ms、0.849/0.858 ms、1.927/1.999 ms**；UnityTerrain 使用 129×129=16,641 samples，min/median **1.066/1.275 ms**；checksum=98,130，`GC.GetTotalMemory(false)` coarse allocationDelta=0。数值只作本机 Editor 趋势，不构成目标平台性能承诺或严格零分配证明。
- WorldKit.Streaming Benchmark（Unity 2022.3.62f3c1）：**1 次预热 + 5 次测量**；Metadata radius=24、目标 resident=2,401、连续移动 200 steps，Demand/Reconcile/Registry transition churn min/median **40.487/40.653 ms**；transitionChecksum=138,507,200，finalResident=2,401。预热且 scratch/registry 容量复用后，五次 `RunMovement` 热路径通过 `GC.GetAllocatedBytesForCurrentThread()` 实测合计 **0 bytes**；`GC.GetTotalMemory(false)` coarse heap delta=4,096 bytes。数值只作本机 Editor 趋势，不构成目标平台性能承诺；0-byte 结论仅针对该同步热路径与该测量环境。
- PathKit V1 Final Hardening：targeted 38 项通过；已修复 `PathSearchStatus.None = 0` 默认结果契约，并将 NoPath 基准改为真实 `BarrierRectGraph` Stress。Core semantic diff：新增 `None=0`、`Success` 移到非零值、default `PathSearchResult` 表示未执行/非成功；A* / Dijkstra / Graph / Grid Adapter 搜索语义未改变。`Core Semantics Frozen = YES`。
- PathKit Benchmark（Unity 2022.3.62f3c1，Editor Test Runner）：64/256/512 方形 Graph 的 A* 与 Dijkstra 均通过；512×512 A* 0.337 ms / 1,022 expanded、Dijkstra 80.656 ms / 262,143 expanded；1,000×1,000 逻辑节点 A* 3.903 ms / 1,998 expanded；重复 1,000 次 A* 32.805 ms、Dijkstra 759.114 ms。真实 NoPath BarrierRectGraph 为 256×256、BarrierColumn=128，A* 6.628 ms / 32,768 expanded、Dijkstra 6.910 ms / 32,768 expanded、Written=0/0；重复基准 ManagedHeapDelta=4,096。数值仅用于本机趋势，不构成跨平台性能承诺。
- PathKit Grid Adapter Benchmark（Unity 2022.3.62f3c1，Editor Test Runner）：256×256 FourWay A* 1.289 ms / 510 expanded；EightWay Dijkstra 241.918 ms / 65,535 expanded；路线 Cost=510、PathLength=511、HeapTrend=510/65,535。数值仅用于本机趋势，不构成跨平台性能承诺。
- PathKit 导出闭环当前只保留 Core 与 GridKit Adapter Profile；旧 Sample 包与 With-Sample 验收记录不再代表当前产品结构。
- 上述 ManagedHeapDelta 来自 GC.GetTotalMemory(false)，只作为 coarse heap / GC trend，不是严格零分配证明。

SpatialKit Benchmark 证据（Unity 2022.3.62f3c1，Editor Test Runner）：100,000 条动态操作，BucketSize=8、InitialCapacity=100000；Insert=11.588 ms、Lookup=3.032 ms、SameBucketUpdate=100000（7.601 ms）、CrossBucketUpdate=100000（14.593 ms）、RectQuery=10000（3.480 ms）、CircleQuery=10000（2.595 ms）、Nearest=10000（9.605 ms）、Remove=6.614 ms、Clear=0.241 ms；SameBucketChecksum=5000050000、CrossBucketChecksum=5000050000、Checksum=7960354605800、ManagedHeapDelta=0。1,000,000 条存储压力 Insert=93.353 ms、抽样查找=1.988 ms、部分移动=3.706 ms、Clear=1.327 ms、Checksum=2896778425750、ManagedHeapDelta=28672。ManagedHeapDelta 只作 coarse heap trend，不是严格零分配证明。

SpatialKit 当前只保留 Core 分发 Profile；旧 Sample / With-Sample 导出记录不再作为当前产品验收依据。

- 生产 Authoring 证据：WorldFramework ToolsHub **24/24 PASS**；World Framework Boundary **25/25 PASS**；Kit Architecture Metadata **6/6 PASS**；Standalone Source Export **30/30 PASS**；既有 frozen Runtime regression **166/166 PASS**。Relevant non-benchmark seal total **251/251 PASS, 0 failed, 0 skipped**。Catalog 增加 worldframework.tools 后共 **86 profiles**，requiredProfileIds 0 缺失；ToolsHub 仍是 Editor-only，Runtime 不反向依赖 UnityEditor/ToolsHub。最终 Unity diagnose healthy=true、Console 0 errors / 0 warnings，repository-wide git diff --check exit 0。Authoring 本身不要求单独 performance benchmark，性能与发布证据由统一 Release Matrix 管理。**Production Authoring baseline = FROZEN / PASS。**
- Integration 证据：六个 World Framework stress samples **24/24 PASS** + GridKit.UnityProjectionAdapter **6/6 PASS**；既有 frozen regression **190/190 PASS**；Boundary **26/26**、Metadata **7/7**、Standalone **30/30**。Relevant non-benchmark seal total **283/283 PASS, 0 failed, 0 skipped**。Catalog 共 **93 profiles = 20 Foundation / 9 Extension / 25 Adapter / 39 non-tier**，requiredProfileIds 0 缺失。TerrainGridNavigation 实际 PlayMode 为 768 baked cells / 48 blocked / 42 manual overrides / path 32 / cost override 5000，Console 0 errors。最终 Unity diagnose healthy=true、Console 0 errors / 0 warnings，repository-wide git diff --check exit 0。**Integration baseline = FROZEN / PASS。**
- Performance / Release 证据：frozen behavior **337/337 PASS**；Boundary **27/27**、Metadata **7/7**、Standalone **30/30**，因此 relevant non-benchmark release set **401/401 PASS**。Selected performance release set **13/13 PASS**，覆盖 Dense/Sparse、Chunk generation、Pipeline compile、Topology、Resource scatter、Feature resolve、Streaming churn、SaveDelta size、exact allocation 等十项要求。Chunk generation、Dense/Sparse+Resource+Feature reusable hot path、Streaming churn 的 GC.GetAllocatedBytesForCurrentThread() 断言均为 **0 bytes**。SaveDelta 1k/10k 大小为 **124,025 / 1,262,517 bytes**，10x 数量增长 **10.180x**。Streaming Editor release batch min/median **107.202/108.216 ms**，慢于历史约 41 ms，已作为趋势变化如实记录，不宣称无 timing regression。Catalog **93 profiles / 0 missing requiredProfileIds**；final compile/update idle、diagnose healthy、清理负向测试历史后 Console **0 errors / 0 warnings**、git diff --check exit 0。Combined selected seal evidence **414/414 PASS, 0 failed, 0 skipped**。**Performance / Release baseline = FROZEN / PASS。**
- Localization 当前证据：LocalizationKit.Core **15/15 PASS**，Settings/UnityUGUI/Editor Adapter tests **12/12 PASS**；World Framework Boundary 扩展为 **29/29 PASS**，Metadata **9/9 PASS**，Standalone **30/30 PASS**。Catalog 增加 `localizationkit.core/settings/ugui/editor` 后为 **97 profiles / 0 missing requiredProfileIds**。Core 零依赖 engine-free，SettingsAdapter 只桥接 SettingsKit，UGUI Adapter 只依赖 Core + com.unity.ugui，Editor Validator 为 Editor-only。`git diff --check` exit 0。

## 后续空白工程检查

每次发布前，在独立空白 Unity 工程中按以下顺序抽检：

1. 仅导入 `ToolsHub.Core`，确认 `Kit 安装状态` 可打开且没有 Kit 专属工具。
2. 导入某个 Core 包，确认编译通过且安装状态页只显示其依赖闭包。
3. 再导入其 Adapter，确认仅新增对应能力和工具入口。
4. 对 Addressables、HybridCLR 这类外部插件层，确认未安装插件时入口隐藏，安装后才显示。

> 本机曾尝试对 `ToolsHub.Core` 执行空白工程导入烟测，但 Unity LicensingClient 的 IPC 通道在启动阶段超时（返回码 199），因此该项未计为通过；需在许可服务可用的 Unity 环境重跑。

> 生产放行还必须在目标平台 IL2CPP Player 上执行 `HybridClrAaRunnerCanEnterHotUpdate` 等价的真实远端发布烟测：下载 catalog、bundle、Manifest、DLL 与 AOT metadata，完成 SHA256 校验并进入热更入口。该步骤不能由编辑器测试或离线构建替代。
