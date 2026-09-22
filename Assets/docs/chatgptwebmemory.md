### 2026-09-18 — P12 Performance / Release Seal pre-freeze

- P11 is frozen; P12 started by auditing existing benchmarks rather than creating duplicates.
- Added WorldFrameworkReleaseBenchmarkTests to cover the actual P12 gaps: multi-Chunk generation, typed WorldDelta serialized-size scaling and exact reusable-hot-path allocation.
- Selected P12 performance release set is **13/13 PASS**: 10 existing selected benchmark tests plus 3 new gap tests.
- Exact allocation assertions pass at 0 bytes for chunk generation after warmup/reused scratch, Dense/Sparse/Resource/Feature reusable hot paths, and the existing Streaming churn hot path.
- SaveDelta size: 1k entries 124,025 bytes; 10k entries 1,262,517 bytes; 126.25 bytes/delta at 10k; 10x count growth 10.180x.
- P12 Editor timing is intentionally treated as trend-only. Streaming final selected release batch is 107.202/108.216 ms min/median for 2,401 residents and 200 steps with checksum 138,507,200 and exact allocation 0, slower than the historical P9 ~41 ms. Do not claim no timing regression; no P11 change touched Streaming semantics.
- Added P12 umbrella Runtime policy banning System.Reflection/assembly discovery and Dictionary<string, object/dynamic> per-cell bags across WorldKit/Streaming/WorldGen/Placement/GridKitUnityProjection. Fresh Boundary is **27/27 PASS**.
- Fresh frozen behavior regression: P0/P1 **117/117**, P2-P6 **82/82**, P7-P9 **84/84**, P10+P11 **54/54** = **337/337 PASS**.
- Current policies: Metadata 7/7, Standalone 30/30, Boundary 27/27. Catalog 93 profiles with 0 missing requiredProfileIds. Compile idle, diagnose healthy, git diff --check exit 0.
- Three Console errors after the regression are expected TimeKit negative-input logs; preserve visible failure behavior. Final P12 seal must clear completed-test Console history and then prove 0 errors / 0 warnings without altering TimeKit.
- Added Assets/docs/WorldFramework-P12-Performance-Release-Matrix.md and linked it from README / implementation plan.
- P12 is still ACTIVE until the post-document lightweight seal passes. No commit/push.

### 2026-09-18 — P12 frozen / P13 opened

- Post-document P12 lightweight seal rerun passed: Boundary **27/27**, Metadata **7/7**, Standalone **30/30**.
- Catalog remains **93 profiles / 0 missing requiredProfileIds**.
- Unity project identity remains StellarFramework_DEEE9F8A; compile/update idle; unity_diagnose healthy=true.
- Console was explicitly cleared only after all negative-input tests completed. Final seal Console is **0 errors / 0 warnings**. The three prior TimeKit LogError entries were expected negative-test behavior and TimeKit was not modified to hide them.
- repository-wide git diff --check = **exit 0**.
- Frozen P0-P11 behavior = **337/337 PASS**. Policy release set = Boundary 27 + Metadata 7 + Standalone 30 = 64/64. Relevant non-benchmark release set = **401/401 PASS**.
- Selected P12 performance release set = **13/13 PASS**. Combined selected seal evidence = **414/414 PASS, 0 failed, 0 skipped**.
- **P12 Performance / Release Seal = FROZEN / PASS.**
- Active milestone is now **P13 — Example Productization / Localization / Final Clean Seal**: LocalizationKit first, then Example visual/UI standard, generated Example assets, directory migration, 22 Kit examples, integration/showcase unification, cleanup and final re-seal.
- Preserve existing dirty baseline; no reset/clean/commit/push was performed.

### 2026-09-18 — P13 Localization / Example foundation + F1 start

- LocalizationKit family 已落地：Core 15/15 PASS；Settings/UnityUGUI/Editor 12/12 PASS；Sample 5/5 PASS。Core 保持零依赖 engine-free；SettingsAdapter 单向桥接 ILanguageSettingsAdapter；UnityUGUIAdapter 提供 SO authoring/context/localized text/button；Editor Validator 检查 zh-CN/en-US coverage/duplicate/missing/empty/fallback。
- LocalizationKit PlayMode 已真实验证 zh-CN -> en-US -> zh-CN 文本切换，Console 0 error / 0 warning。
- Adobe Source Han Sans CN Regular 官方字体/License 通过非阻塞 UnityWebRequest installer + SHA256 后落在 Samples/Common/Fonts；font SHA256 E2BC8A2E7F37474B774FFF8DB758681ECE40BB6947A90D571BCE9DD60671A8E4。
- 新 ExampleVisualContract 与 ExampleAssetFactory 已建立；Factory 4/4 PASS，21 项 Common Generated assets 可幂等重建且 GUID 稳定。
- FlowKitMsvIntegration Playable 已补齐；Scene tests 4/4 PASS；真实 PlayMode 3D indicators 从 Prepared-only 进入 Ready+Completed，Console 0/0。
- SampleManifest 目前 31 个 active entrypoints，policy 4/4 PASS。只有 localizationkit / flowkit.msv 为 ready，其它仍 pending。
- F1 六个旧 Kit Sample API 已完成第一步改造并 fresh compile 0 errors：Action/Bindable/Event/Singleton/Config/Log。Config 已取消 OnGUI 主验证；Log 不再 Start 时自动产生 Warning。
- 接下来继续 F1 六个真实 3D evidence + LocalizationKit bilingual UGUI + PlayMode smoke；完成后更新 Manifest ready 状态。
- No commit/push/reset/clean.

### 2026-09-18 — LocalizationKit formal audit + FrameworkDoc migration pilot

- 用户要求建立 `Assets/StellarFramework/FrameworkDoc/` 统一文档中心；决定采用“中央正式文档 + Kit/Sample 原目录短 README 入口”的方式，不暴力一次性移动所有文档。
- FrameworkDoc 已新增：`README.md`、`00-Overview/P13-Completion-Plan.md`、`09-Development/Documentation-Migration-Map.md`、`02-Kits/LocalizationKit/LocalizationKit-Guide.md`。
- LocalizationKit 结构审计结果：符合 StellarFramework Kit 规范。Core zero-dependency/engine-free；SettingsAdapter 与 UnityUGUIAdapter 单向；Editor-only Validator 独立；Sample 独立 profile；无 runtime reflection/assembly scan。
- LocalizationKit 增加跨语言 placeholder contract：Core parser 统一提取 argument names；Editor Validator 报 `InvalidTemplate` / `PlaceholderMismatch`。Fresh adapter tests **15/15 PASS**。
- 当前 fresh regression：Core 15/15、Adapter 15/15、Localization Sample 5/5、F1 Scene 9/9、Manifest 4/4、Boundary 29/29、Metadata 10/10、Standalone 30/30。
- 固定语言 selector 规范：按钮标签永远 `中文` / `English`，自身不进入 Localization Table；用户点击后其余可本地化 UI 切换 zh-CN / en-US。
- LocalizationKit 正式 Guide 已从 Runtime Kit 路径迁到 FrameworkDoc；Runtime 只保留双语 README 导航，Catalog sourcePath 与 metadata test 已同步。
- Catalog 当前 98 profiles / 0 missing dependency。`git diff --check` 当前 0；新增 `.gitattributes` 仅豁免 Unity `.unity` YAML 的 blank-at-EOL，避免合法 `key: ` 空值被误报。
- 下一步继续：F1 README 双语 Gate / F1 分发 closure -> F2 -> F3 -> F4 -> 6 World Integrations -> ArchitectureDemo -> FrameworkDoc 全量迁移 -> 旧模板/Generated 清理 -> P13 final seal。

### 2026-09-18 — P13 plan frozen by user request

- User added a new final milestone after P12: P13 Example Productization / Localization / Final Clean Seal.
- P13 must organize and clean all Example cases and playable scenes, standardize directories/builders/assets/docs, and remove obsolete duplicates only after replacements pass.
- GUI/UGUI is only UI. For non-UI Kits, actual verification must be shown through real 2D/3D objects, movement, routes, spawning/recycling, occupancy/grid, Terrain, audio emitters or other visible world evidence. Logs/IMGUI text cannot be the primary validation.
- Missing Example art should be created by deterministic Editor builders / ExampleAssetFactory rather than pulling arbitrary external assets.
- All sample UI must support zh-CN and en-US through a new independent LocalizationKit. SettingsKit already exposes ILanguageSettingsAdapter; P13 will connect it through an optional LocalizationKit.SettingsAdapter rather than merging localization into SettingsKit.
- Current P13 planning inventory: 22 Kit Example directories, 22 Kit Playable scenes, 5 already-generated World Framework Integration scenes, 1 ArchitectureDemo scene = 28 current scenes. P11 TerrainGridNavigation will make World Framework Integration six; LocalizationKit and FlowKitMsvIntegration may add additional playable scenes.
- Repository currently has no reusable Runtime LocalizationKit and no ttf/otf/ttc font assets. LocalizationKit Core will be font-agnostic; Example font resolution must be explicit and diagnostic, and no OS font files will be copied/distributed.
- User explicitly authorized selecting an open-source Chinese/English font. P13 default font decision: use the official Adobe Source Han Sans SC (思源黑体) release under SIL OFL 1.1, keep the license/copyright notice beside the font, and import only the minimal required weights (Regular first). Source Han Sans SC covers both Simplified Chinese and English, so it is the default single-family Example font. Noto Sans CJK SC is only the documented fallback if platform compatibility requires it.
- P13 execution order: LocalizationKit Core -> Unity/Settings adapters + validator -> Example visual/UI standard + generated asset factory -> directory/builder migration -> 22 Kit examples -> Integration/Showcase -> cleanup -> full frozen regression + final release re-seal.
- Detailed plan written to Assets/docs/WorldFramework-P13-Example-Localization-Cleanup-Plan.md and summarized in WorldFramework-Implementation-Plan.md.
### 2026-09-18 — P11 Batch 5 StellarGridMap Migration

- Migration sample follows the preserved StellarGridMap reference: Generated Base + Runtime Modification + SavePatch, but decomposes responsibilities into WorldGenKit, WorldKit Delta, GridKit, PathKit, PlacementKit, SpatialKit and SaveKitAdapter instead of copying the old monolith.
- 48×48 base + 48-cell runtime road + one guaranteed non-idempotent walkability override = **49 typed Deltas**. A* route crosses the full map with 48 path cells; PlacementKit accepts a real generated site; SpatialKit indexes two entities.
- First test run was 3/4 because the test incorrectly required the 48 road writes to change bytes even when that generated row was already walkable. Added one guaranteed inverse-of-base runtime edit so layered-state mutation is explicitly proven; corrected tests **4/4 PASS**.
- PlayMode live: PathLength 48, RuntimeDeltaCount 49, RestoredDeltaCount 49, SpatialCount 2, PlacementAllowed true, Console errors 0.
- Catalog **91 profiles**, missing 0; Metadata 6/6, Standalone 30/30, diagnose healthy, diff-check 0.
- **StellarGridMap Migration = COMPLETE / PASS; P11 progress 5/6.** Next/final P11 sample: TerrainGridNavigation. No commit/push.

### 2026-09-18 — P11 Batch 6 TerrainGridNavigation + final freeze

- Implemented the P0-frozen Terrain/Mesh -> Grid Bake -> Manual Override boundary as a real StellarFramework.GridKit.UnityProjectionAdapter, rather than keeping bake logic private to the TerrainGridNavigation sample.
- The adapter includes explicit TerrainData and Physics/MeshCollider projection sources, caller-owned bake scratch, atomic destination replacement, baked height/slope/walkability/cost data, independent walkability/cost overrides and explicit hard-safety composition.
- Initial source placement under Runtime/Kits/GridKit/Adapters/UnityProjection failed Standalone **29/30** because the GridKit Foundation source-tree policy correctly forbids UnityEngine anywhere beneath the Core tree. The adapter was physically relocated to Runtime/Kits/GridKitUnityProjection; policy was not weakened. Standalone returned to **30/30 PASS**.
- Adapter tests final **6/6 PASS**. One intermediate 5/6 was test isolation only: a loaded TerrainCollider shared the default layer with the test cube. The test now uses a dedicated layer and production code was unchanged.
- TerrainGridNavigation integration final **4/4 PASS**. Real PlayMode values: 768 baked cells, 48 AutoBake blocked cells, 42 manual overrides, path length 32, path success true, forced-block preserved, manual cost 5000, Console 0 errors.
- Added distribution profiles gridkit.unityprojection and samples.worldframework.terraingridnavigation; narrowed the gridkit profile sourcePaths so Core export excludes the Unity adapter.
- Added explicit Metadata contract for the adapter. The Metadata test DTO initially lacked sourcePaths, causing 3 test-assembly compile errors; fixed test DTO only. Fresh discovery found 7 Metadata tests and **7/7 PASS**.
- P11 self = six integration samples 24/24 + adapter 6/6 = **30/30 PASS**.
- Frozen P2-P9 runtime rerun **166/166**, P10 ToolsHub **24/24**, so P2-P10 frozen regression **190/190 PASS**.
- Policies: Boundary **26/26**, Metadata **7/7**, Standalone **30/30**.
- P11 relevant seal total **283/283 PASS, 0 failed, 0 skipped**.
- Catalog final for P11: **93 profiles = 20 Foundation / 9 Extension / 25 Adapter / 39 non-tier**, missing requiredProfileIds 0.
- Final health: StellarFramework_DEEE9F8A, compile/update idle, unity_diagnose healthy=true, Console 0 errors / 0 warnings, repository-wide git diff --check exit 0.
- **P11 Integration Samples = FROZEN / PASS, 6/6 complete. P12 Performance / Release Seal is now ACTIVE.** No commit/push.

### 2026-09-18 — P11 Batch 4 InfiniteFactory

- Added independent InfiniteFactory sample using WorldKit.Streaming, WorldGenKit.StreamingAdapter, Resources, SaveKitAdapter and SaveKit.Core. Focus jumps to large signed coordinates and converges to the expected 49-tier ring.
- Player resource settings are real WorldResourceGenerationSettings with NewChunksOnly semantics; unmodified Chunk generation is rebuilt from seed/settings rather than saved.
- Added explicit ResourceDepletion Delta codec and real SaveKit roundtrip. After clearing runtime state, regenerating the base Chunk and loading Delta restores target richness to 0.
- Compile caught missing direct UniTask asmdef reference due SaveKit async return types; fixed sample asmdef only.
- InfiniteFactory tests **4/4 PASS**; PlayMode live state = focus(-900000,700000), residents 49, resources 559, deltas 1, restored richness 0, Console errors 0.
- Catalog **90 profiles**, missing 0; Metadata 6/6, Standalone 30/30, diagnose healthy, diff-check 0.
- **InfiniteFactory = COMPLETE / PASS; P11 progress 4/6.** Next: StellarGridMap Migration. No commit/push.

### 2026-09-18 — P11 Batch 3 Survival3D

- Added independent Survival3D sample: 129×129 / 16,641 samples, real Height/Moisture/Water/Slope/Biome/Surface pipeline, Resource generation/resolution, Village Feature -> Placement adapter -> PlacementKit rules -> Feature resolver, then UnityTerrainAdapter projection.
- The first fresh test run failed 0/4 on an explicit Sample defect: WorldResourcePlanarDomain constructor order is width,height,sampleStep,originX,originY; positional arguments accidentally passed OriginX=-64 as sampleStep. Fixed by named arguments only; no Runtime change.
- Corrected sample passed Survival3DIntegrationSampleTests **4/4**. Actual PlayMode scene reported Resolution=129, ResourceCount=338, VillageCount=5, PlacementRejectedCount=209; live Terrain existed and Console errors were 0.
- Catalog audit corrected the Terrain adapter profile dependency to the real id worldgenkit.unityterrain. Catalog = **89 profiles**, missing dependencies 0; Metadata **6/6**, Standalone **30/30**, diagnose healthy=true, 0 errors / 0 warnings, diff-check exit 0.
- **Survival3D = COMPLETE / PASS; P11 progress 3/6.** Next: InfiniteFactory. No commit/push.

### 2026-09-18 — P11 Batch 2 HexStrategy

- Added independent HexStrategy sample with only GridKit.Core + WorldGenKit.Resources + WorldGenKit.Feature dependencies; no WorldKit/Builtins/Authoring/Tilemap/SaveKit/PathKit dependency.
- Radius 18 axial domain contains **1,027 cells** and **2,970 canonical internal edges**. Resources are generic candidates produced from Hex cells, then resolved by the real occupancy resolver. City/Wonder Features are resolved by the real quota/reservation resolver.
- Fresh HexStrategy tests **4/4 PASS**. Actual PlayMode scene smoke reported CellCount=1027, InternalEdgeCount=2970, AcceptedResourceCount=223, AcceptedFeatureCount=13 and Console error count 0.
- Added samples.worldframework.hexstrategy. Catalog = **88 profiles**, missing dependencies 0; Metadata **6/6**, Standalone **30/30**, diagnose healthy=true, 0 errors / 0 warnings.
- **HexStrategy = COMPLETE / PASS; P11 progress 2/6.** Next is Survival3D. No commit/push.

# StellarFramework — ChatGPT Web Development Memory

> Purpose: persistent handoff/context memory for ChatGPT Web + Coding Tools work on this repository.
> Last verified: 2026-09-17
> Rule: every task that changes this project must update this file in the same work session.

## 1. Mandatory working protocol

1. Before editing, read this file and inspect the current Git status/diff.
2. Treat pre-existing user changes as protected baseline. Do not revert, overwrite, clean, reformat, or “fix” unrelated dirty files.
3. Change only the files required by the current task. New work must not regress already-working features.
4. After editing, update this document with:
   - what changed and why;
   - important files/contracts affected;
   - validation actually executed and its real result;
   - known risks, blockers, or follow-up items.
5. Do not record a test/benchmark/release step as PASS unless it was actually executed. Use PASS / FAIL / BLOCKED / SKIPPED / NOT RUN accurately.
6. Prefer clear production-grade C#, low GC pressure, predictable performance, explicit errors, SOLID/component boundaries, and adapters for Unity/SDK/third-party dependencies.
7. Do not add broad fallback code that hides defects. Invalid state should be observable and diagnosable.
8. Runtime business code should avoid reflection/scanning when an explicit registry, generated mapping, adapter, or configuration boundary can be used.
9. Keep Core/Domain independent from Unity, third-party SDKs, networking/storage implementations whenever the Kit contract permits it.

## 2. Repository / tooling snapshot

- Repository: https://github.com/StarrDream/StellarFramework
- Coding Tools workspace root: `C:\CodingToolsWorkerCenter`
- Project path inside workspace: `StellarFramework`
- User-provided MCP endpoint: `http://127.0.0.1:5890/mcp`
- Coding Tools MCP observed version: `0.3.0`, trusted permission mode.
- Branch at this snapshot: `main`
- HEAD at this snapshot: `ddf708c feat: refine FlowKit node authoring`
- `main` and `origin/main` were aligned when this memory file was created.
- Unity project baseline: Unity `2022.3.62f3c1`; framework also targets compatible Unity 2022.3 LTS / Unity 6000.x.

### Pre-existing dirty working-tree baseline on 2026-09-15

These changes existed before this memory file was created and must not be silently reverted:

- Modified: `Assets/AddressableAssetsData/Windows/addressables_content_state.bin`
- Deleted: `Assets/AddressableAssetsData/link.xml`
- Deleted: `Assets/AddressableAssetsData/link.xml.meta`
- Modified: `Assets/StellarFrameworkBootstrap/Payloads/StellarFramework-FullHotUpdate-Payload.unitypackage.bytes`
- Modified: `Packages/packages-lock.json`
- Modified: `ProjectSettings/EditorSettings.asset`
- Untracked: `Assets/Screenshots.meta`
- Untracked: `Assets/Screenshots/`

Always re-check Git status before each new task because this list can change.

## 3. Source-of-truth hierarchy

Do not infer architecture from folder names alone. Prefer these sources in this order:

1. Runtime code + asmdef + tests for actual behavior.
2. `Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json` for distribution profiles, dependency closure, tier/category and UPM facts.
3. `Assets/StellarFramework/KitCatalog/KitArchitectureGuide.md` for Kit layering and dependency rules.
4. `Assets/StellarFrameworkVerification/ValidationArchitecture.md` for validation responsibilities and release gates.
5. `Assets/StellarFramework/KitCatalog/KitExportValidationMatrix.md` for evidence already executed.
6. Root `README.md` and per-Kit usage/source guides.

`CODELY.md` is useful background but is currently stale in places: its overview still says “14 Kits”, while the current Runtime contains additional V1 Kits such as TimeKit, SaveKit, GridKit, SpatialKit, SimulationKit, PathKit and FlowKit. Do not treat that count as authoritative.

## 4. Framework architecture

StellarFramework is a reusable modular Unity framework, not a game project. Business gameplay should not be pushed back into the framework unless a stable reusable abstraction has been proven.

### MSV / Architecture core

- `Architecture<T>` is the project container.
- Model owns mutable state/data.
- Service owns business/system logic and may mutate Models.
- View is presentation/interaction and should consume Models through read-only contracts.
- `IReadOnlyArchitecture` exposes `GetReadOnlyModel<T>()` and Services to Views.
- Legacy View `GetModel<T>()` exists but is obsolete.
- Architecture lifecycle is explicit: Uninitialized → Initializing → Initialized → Disposing → Disposed.
- Model/Service registration is not allowed as arbitrary runtime mutation after initialization.
- Dispose deinitializes Services then Models and clears the container/static instance.
- Domain-reload/Enter-Play-Mode static reset is handled by `ArchitectureRuntimeReset`.

Core architecture file currently lives at:
`Assets/StellarFramework/Runtime/Core/Architecture/StellarFramework.cs`

### Runtime layering

```text
Runtime/Core
  └─ Architecture

Foundation Kit
  ↓
Extension Kit

Adapter Profile connects optional Kits / Unity / third-party technology.
```

Rules:

- Foundation cannot depend on Extension.
- Extension may depend on stable Foundation contracts.
- Adapter is the preferred place for optional integrations.
- Foundation does not mean “always installed”; all Kits remain opt-in via dependency closure.
- Do not move business concepts such as Crop/NPC/Quest/Farm into Foundation just for convenience.

Current Foundation examples include LogKit, EventKit, PoolKit, SingletonKit, FSMKit, ActionKit, BindableKit, ConfigKit.Core, HttpKit, ResKit.Core, SettingsKit.Core, TimeKit, SaveKit.Core, GridKit, SpatialKit, SimulationKit, PathKit and FlowKit.Core.

Current Extension examples: AudioKit.Core, UIKit.Core, HotUpdate.Core.

Current Adapter examples include ConfigKit.NewtonsoftJson, SettingsKit adapters, AudioKit.ResKitAdapter, ResKit AssetBundle/Addressables, UIKit.ResKitAdapter, HotUpdate Addressables/HybridCLR, SaveKit.NewtonsoftJson, PathKit.GridKitAdapter and FlowKit.UnityIntegration.

## 5. Distribution / editor / samples

- Main framework source: `Assets/StellarFramework/`
- Framework bootstrap installer: `Assets/StellarFrameworkBootstrap/`
- Maintainer-only verification: `Assets/StellarFrameworkVerification/`
- Runtime delivery fixture/example: `Assets/GameHotUpdate/`
- User teaching samples: `Assets/StellarFramework/Samples/`
- Automated tests: `Assets/StellarFramework/Tests/`
- Tools Hub: `StellarFramework -> Tools Hub`
- Kit export entry: `StellarFramework -> Framework Source -> Kit Package Exporter`

The exporter must compute actual dependency closure from the Catalog. Core packages must not drag optional Addressables/HybridCLR/other adapters into projects implicitly.

Samples teach usage; they are not regression suites. Verification is maintainer-only and must not be distributed as ordinary Kit content.

## 6. Validation contract

Five validation responsibilities:

1. Kit Behavior — public API, boundaries, deterministic behavior, atomic failure, regressions.
2. Performance — scale/throughput trends with reproducible evidence; avoid machine-specific fixed millisecond gates.
3. Framework Policy — asmdef/dependency/Catalog/docs/packaging/sample/ToolsHub rules.
4. Integration — small fake-only multi-Kit cooperation checks.
5. Release — real package export/import, Player, IL2CPP, Addressables/HybridCLR/hot-update smoke as applicable.

Use EditMode for pure C# logic. Use PlayMode only when Unity lifecycle/resources/runtime behavior genuinely require it.

For benchmarks, `GC.GetTotalMemory(false)` is only a coarse heap/GC trend and must never be described as a strict zero-allocation proof.

Major bug fixes should add a regression test. Expected Error/Warning paths in negative tests must be explicitly asserted rather than globally silenced.

## 7. V1 foundation contracts already established

### TimeKit

- `foundation / simulation`.
- World time truth is `long Tick`; 1 Tick = 1 game millisecond.
- Calendar is a view over Tick.
- Runtime driver advances using `Time.unscaledDeltaTime`; Unity `Time.timeScale = 0` does not pause world time.
- Use `ActionKit.Delay` for short flow/UI waits; use TimeKit scheduling for world-time events.
- Supports `ScheduleAfter`, `ScheduleAt`, `ScheduleEvery`, catch-up policies and callback budgets.
- High-scale usage should prefer stable `ITimeEventReceiver` over captured lambdas.
- Saves should persist business target ticks/state, not delegates, receiver objects, scheduler heap or `TimerHandle`.

### SaveKit

- `foundation / data`.
- Saves are split into stable business-domain Sections, not one Section per GameObject.
- Core owns container format, versions, checksum/integrity, transaction, backup, migration and restore ordering.
- Business owns Capture / Validate / Restore and DTOs.
- Prepare/Apply restore boundary is important: all known Sections prepare successfully before any Restore begins.
- Restore dependencies use a deterministic DAG.
- Unknown sections can be preserved; Missing Section policy is explicit.
- Typed migrations must deserialize using the stored/old DTO type first, then migrate stepwise to the current type.
- FileSystem storage uses current/backup/temp transaction flow.
- V1 does not claim stream-first / zero-copy for very large saves; large domains should use compact/custom binary strategies.
- SaveKit should persist stable IDs/timestamps, not GameObject/Transform/Component/TimerHandle/delegate/runtime handles.

### GridKit

- `foundation / world`, pure C#, independently exportable.
- Integer 2D grid, negative coordinates, half-open `GridRect [Min, MaxExclusive)`, row-major `DenseGrid<T>`.
- Footprints are immutable/canonical; Occupancy uses positive integer owner IDs.
- Occupancy mutation is atomic: failed occupy/release must leave zero partial changes.
- `TryOccupy` is Empty → Owner only; it is not Move/Replace/Transfer/idempotent reapply.
- Placement/pathfinding/chunk/tilemap/save are upper-layer responsibilities.

### SpatialKit

- `foundation / world`, pure C# continuous 2D point index using dynamic uniform spatial hashing.
- Stores external `SpatialId` + point data, not Unity objects.
- Supports Insert/Remove/Update, half-open rect query, closed-circle query and finite-radius nearest.
- Query order is not a public contract; caller sorts by stable key if needed.
- No Transform tracking, 3D, KNN, persistence, Jobs/Burst or Unity lifecycle in V1 Core.

### SimulationKit

- `foundation / simulation`, pure C# scheduler.
- Scheduler stores business `SimulationId`, interval and next-due tick; it returns due IDs to caller-owned buffers.
- It never calls gameplay callbacks and does not own Unity Update/PlayerLoop.
- `destination.Length` is one `CollectDue` call's count budget; real frame spreading is a caller policy.
- For 100k same-tick due items with a 500 buffer, the intended real-time pattern is one call per frame, not while-draining backlog in the same frame.
- `HasBacklog` means due entries remain at the current tick.
- No catch-up replay storm: after dispatch, next due is based on the current dispatch tick + interval.
- Save business IDs/state/last simulation tick and rebuild scheduler on load.

### PathKit

- `foundation / world`, pure C# Graph-first synchronous shortest-path core.
- A* and Dijkstra; positive `long` costs with overflow checks.
- A* requires admissible heuristic and supports Closed reopen for admissible-but-inconsistent heuristics.
- Dijkstra never invokes heuristic.
- Deterministic tie-breaking is part of the V1 contract.
- Output is atomic: `OutputBufferTooSmall` writes no partial path.
- `PathSearchStatus.None` is only the default/not-run state; an executed search returns an explicit result.
- Grid support lives in the separate `PathKit.GridKitAdapter`; Core must not learn GridCoord/NPC/Transform/movement.
- V1 Core semantics are frozen; future work should not casually alter them.

### FlowKit

- `FlowKit.Core` is `foundation / flow`; `FlowKit.UnityIntegration` is an adapter.
- Graph JSON → explicit migration/validation → immutable `FlowPlan` → budgeted Runner/Schedulers.
- Core is pure C# and does not depend on Unity, UniTask, Addressables, HybridCLR, UI or game-domain objects.
- Unity integration provides `FlowHost`, stable `FlowBinding`, JSON entry and explicit host configuration.
- Visual editor/project validation/runtime diagnostics live in the ToolsHub FlowKit module, not in player Runtime.
- Stable IDs and explicit registries are required; runtime assembly scanning/Find-style binding is intentionally avoided.
- External side effects are expressed through `IFlowOperationAdapter`, capabilities, operation results, Signal/State/Blackboard boundaries.
- Immediate-only cycles are rejected; loops need a completion boundary such as Delay/Signal/State/Operation.
- Snapshot V1 is not arbitrary mid-execution persistence: it only captures safe quiescent state and must validate FlowId/PlanHash/PlanVersion.

## 8. Current development state / recent history

Recent mainline commits at the time of this snapshot:

- `ddf708c` refine FlowKit node authoring
- `57c4382` integrate FlowKit editor into ToolsHub
- `d614483` finalize FlowKit v1 workflow editor and samples
- `e1a7d2c` add FlowKit v1 workflow runtime
- `74f2462` optimize ToolsHub combined mesh collider
- `9b83ac5` freeze PathKit V1 core semantics
- `4fef3d8` freeze SimulationKit v1 usage contract
- `69fb788` freeze SpatialKit v1 semantics

According to the current Kit Architecture Guide, TimeKit, SaveKit, GridKit, SpatialKit, SimulationKit, PathKit and FlowKit V1 Core Semantics are frozen.

The next stated framework phase is **Tiny Foundation Integration**: a small maintainer-only integration verification using fake/minimal semantics. It must not grow into a game demo. WorldKit, PlacementKit, InventoryKit and WorldGenKit are later Extension candidates; ProductionKit/LogisticsKit should first prove their domain abstractions in real projects.

### Confirmed continuation roadmap

The previously planned Kit line is now explicitly recorded as:

1. Tiny Foundation Integration — not a Kit; maintainer-only integration verification for the frozen Foundation set.
2. WorldKit — world/region/chunk/coordinate/streaming-state/data-organization layer.
3. WorldGenKit — procedural world generation extension; generation algorithms must stay out of WorldKit Core.
4. PlacementKit — placement/snap/footprint/terrain/occupancy/rule composition extension; must consume GridKit rather than modify its Core contract.
5. InventoryKit — reusable inventory/container/item-stack extension with domain-neutral boundaries.

Current completed/frozen Foundation line:

- TimeKit
- SaveKit
- GridKit
- SpatialKit
- SimulationKit
- PathKit
- FlowKit

ProductionKit and LogisticsKit remain domain candidates, not immediate framework commitments. They should only be promoted after their abstractions are proven in real projects.

Recommended continuation order is:

`Tiny Foundation Integration -> WorldKit -> WorldGenKit -> PlacementKit -> InventoryKit`

WorldKit must not become a “god world manager”. Responsibilities remain separated:

- Time / world clock -> TimeKit
- Save / persistence -> SaveKit
- integer grid / occupancy facts -> GridKit
- continuous 2D spatial candidates -> SpatialKit
- large-scale low-frequency scheduling -> SimulationKit
- path search -> PathKit
- procedural generation -> WorldGenKit
- placement rules -> PlacementKit
- inventory/domain containers -> InventoryKit

### FlowKit project-level coding contract / integration status

FlowKit V1 Core remains frozen. The project-level workflow business-code convention has now been established around StellarFramework MSV and implemented in Authoring/Editor/Sample layers without changing FlowRunner semantics.

Mandatory production integration direction:

`Flow Graph -> Operation Adapter -> Domain Service -> Model -> View / Flow Facts Bridge -> Signal/State -> FlowKit`

Rules now established:

- Model remains the business-state source of truth.
- Service owns business rules and Model mutation.
- View only presents state / forwards intent; FlowKit APIs must not spread through Views.
- Operation is an External Capability Call. `FlowExternalCallKind` classifies Command / Query / AsyncRequest / Presentation / Resource / Network / Other while runtime still uses one `IFlowOperationAdapter` abstraction.
- Operation Adapter is a translation/lifecycle boundary, not a business Service. Production code must avoid giant operation-ID switch routers.
- Flow Facts Bridge projects domain facts into Signal/State and must not become a second Model.
- State is a workflow projection of current truth; Signal is a transient occurrence; OperationResult describes one external call; Blackboard is Flow-local context only; Binding is stable object location.
- Blackboard must not hold Models, UnityEngine.Object, SDK handles, delegates or large business collections.
- Binding uses stable IDs; `FlowBinding.Target` may expose an explicit Unity object/component while null Target preserves the old self-binding behavior.
- Stable IDs are centralized. Default production convention is lower_snake_case dot segments: Operation/Signal/State/Blackboard at least 3 segments, Binding at least 2.
- Authoring Catalog is build/editor metadata only; FlowRunner does not depend on it.
- Authoring Catalogs may be partial by default: known contracts are type-checked but unknown IDs remain allowed so modules can own separate catalogs.
- `StrictUnknownReferences` plus optional exact `StrictFlowIds` turns unknown Operation/Signal/State/Blackboard/Binding references into errors for release-ready flows. Empty strict FlowId list means all flows; invalid-only strict lists must never silently become global.
- Failure/cancel/timeout routes are explicit; no fake success fallback.
- Future SubFlow / reusable composition remains a separate later design phase because it changes structural semantics. Do not add it casually to frozen Core.

Implemented enforcement / UX:

- `FlowKit-业务编程规范-Coding-Contract-Guide.md` is the first-class coding contract.
- `FlowAuthoringCatalog` carries typed operation arguments/results/capabilities, Signal/State/Blackboard value kinds, Binding expected types, and strict validation scope.
- `FlowKitContractValidator` performs project-level contract/reference/type checks.
- `FlowAuthoringContractEntryDrawer` provides category-focused Inspector authoring.
- ToolsHub FlowKit editor has a `业务骨架` action using `FlowKitProjectScaffolder`.
- Scaffold creates Contracts / Bootstrap / Operations / Facts / Bindings / Graphs / Tests and a compiling failure-by-default Operation adapter example; it never overwrites an existing module.
- FireDrill sample no longer teaches one universal operation adapter + giant operationId switch; operations are registered explicitly through focused immediate handlers.
- SchoolTraining sample now routes every Operation failed/cancelled port to an explicit `flow.fail` path.

Current remaining FlowKit follow-up after this phase: consider a dedicated larger Production Pattern sample / additional static policy checks if useful, then design P5 SubFlow/Composition separately. Runtime V1 remains frozen.

### WorldKit / WorldGenKit design reference — StellarGridMap

Before implementing WorldKit, use the user's repository
`https://github.com/StarrDream/StellarGridMap` (default branch `master`) as a first-class design reference.

Important existing ideas worth preserving conceptually:

- `WorldGenerationContext` as an explicit generation data context rather than hidden global state.
- `WorldProfileSO` / `PipelineProfile` as data-driven world-generation profiles and stage toggles.
- deterministic per-stage seed separation as a concept; however do **not** reuse the current fallback based on `string.GetHashCode()`, because framework generation requires a stable cross-run/platform hash/ID contract.
- `ChunkModel` / Chunk coordinates / Chunk streaming intent.
- layered world-state concept:
  `BaseGeneratedLayer + RuntimeModificationLayer + SavePatchLayer`.
- explicit generation Services instead of one giant terrain generator.
- validation/report/debug-overlay stages as part of generation quality, not afterthoughts.
- static code generation / no runtime reflection philosophy.
- the larger 16-stage survival/building-management generation pipeline is useful input for WorldGenKit scope and future integration examples.

Do **not** copy StellarGridMap wholesale into WorldKit. Its current module intentionally bundles terrain generation,
building, economy, citizens, survival, time, camera, selection and UI; StellarFramework must separate those responsibilities.

Planned responsibility split informed by StellarGridMap:

- **WorldKit**: world identity/bounds, Region/Chunk coordinates and ownership, chunk lifecycle/streaming state,
  world data organization/access, world-runtime delta abstraction, stable world/profile identity.
- **WorldGenKit**: height/moisture/water/biome/semantic masks/zones/resource candidate maps,
  deterministic generation stages, stage seeds, terrain flattening/generation validation/debug outputs.
- **GridKit**: discrete cell/grid/occupancy primitives; WorldKit must not reinvent generic grid math.
- **SpatialKit**: continuous spatial indexing/querying; Chunk ownership does not replace SpatialKit search.
- **PathKit**: path search; road/path-cost generation may produce PathKit-ready data but not duplicate pathfinding.
- **PlacementKit**: building footprints, placement rules, suitability/placement validation.
- **SaveKit Adapter**: serialize/restore typed world deltas/patches; WorldKit must not own JSON/file IO.
- **TimeKit**: day/night/world time; do not bring StellarGridMap's day/night system into WorldKit.
- Economy/Citizens/Survival remain game/domain systems, not WorldKit Foundation responsibilities.

Important implementation lessons from current StellarGridMap code:

- avoid whole-world dense duplicate storage such as global height/biome/mask arrays plus copied per-Chunk arrays;
  for large worlds, prefer chunk-first/lazy/pageable authoritative storage or views over duplicated data.
- an 8192x8192 float field alone is ~256 MiB before object/array overhead; multiple height/moisture/suitability/risk maps
  can reach multi-GB memory, so WorldKit/WorldGenKit must design memory budgets explicitly.
- Core Foundation should remain pure C# where practical: do not base WorldKit Core on `Mathf`, `Vector2Int`,
  MonoBehaviour, ScriptableObject, Mesh or URP. Unity-facing types belong in adapters/integration.
- avoid string coordinate persistence such as `"x,y"` and parallel lists in SavePatch data;
  use typed/versioned delta records with SaveKit migration support.
- do not copy silent null-return behavior from older runtime facade methods; missing required dependencies/configuration
  should remain diagnosable according to current StellarFramework coding standards.

When WorldKit starts, first produce a boundary/design document comparing StellarGridMap concepts against existing
GridKit / SpatialKit / SimulationKit / PathKit / SaveKit before writing runtime code.

### WorldGenKit extensibility follow-up — custom channels, layered resources, player-adjustable abundance

WorldGenKit must remain open to project-defined map attributes and resource types.

#### Custom world-data channels

- Do not hardcode world cells as fixed fields such as Temperature/Moisture/Fertility/Magic.
- Use registered typed channels/layers such as `terrain.height`, `terrain.temperature`, `game.magic_density`.
- Projects may omit unused channels entirely and register custom channels without modifying WorldGenKit Core.
- Authoring may use stable string IDs, but compiled runtime pipelines should resolve them to typed/indexed handles
  rather than doing per-cell string/dictionary/object lookups.
- Support storage strategies appropriate to the data: Dense, Sparse, Chunked, Constant, Computed/Derived, External.
- Generation stages declare required/optional inputs and produced channels so ToolsHub validation can catch
  missing producers, duplicate producers, circular dependencies and unused data.

#### Layered resource / occupancy model

- A cell/area must support multiple simultaneous semantic/content layers. Never model a cell as one `Resource` slot.
- Tree + iron ore + flower may coexist if their occupancy policies allow it.
- Suggested occupancy categories include Ground, SurfaceSolid, Vegetation, Mineral, Underground, Decoration,
  Building, Road, Water and project-defined custom slots.
- Resource scattering should create `SpawnCandidate` records, then resolve density/spacing/conflicts/priority/
  max-per-cell/occupancy into final `SpawnRecord` data.
- 3D resources may use free local/world positions owned by a Chunk rather than being locked to cell centers.

#### Player-adjustable resource distribution

Developers must be able to expose selected resource-generation knobs to players without changing Core code.
Examples: iron x2, copper x0.5, coal x1.5, forest/tree coverage 50%, etc.

Do not represent every adjustment as one generic multiplier. Distinguish at least:

- **Occurrence/Density multiplier**: number/frequency of deposits or spawn candidates.
- **Coverage target**: desired area/cell coverage, useful for vegetation/forest.
- **Cluster/Vein size multiplier**: deposit footprint/cluster size.
- **Richness/Amount multiplier**: amount contained in each accepted resource node.
- **Regeneration multiplier** (optional gameplay integration): runtime respawn/recovery rate; not the same as generation.

Each ResourceDefinition/Profile should declare which knobs are user-exposable, default value, min/max/step,
and whether the value is world-generation-only or runtime-adjustable.

Use layered modifiers instead of mutating definitions:

`EffectiveResourceRule = DefinitionBase * ProjectProfile * WorldPreset * PlayerGenerationSettings * Difficulty/ScenarioModifier`

The original ResourceDefinition remains immutable source data.

Important behavior for already-generated worlds:

- Default: player generation changes affect **new/un-generated chunks only**.
- Optional explicit policies may support regenerate-unvisited chunks or full regeneration, but must never silently
  delete/move existing resources, buildings or player modifications.
- Mid-game rebalancing of existing resources is a separate runtime/domain operation and must be opt-in, explicit,
  conflict-aware and save-safe; do not conflate it with procedural generation.
- Persist the resolved world-generation settings/profile/version/seed through SaveKit so a loaded world continues
  using the same generation rules for future chunks.

Resource increases must still pass occupancy/spacing/budget constraints. If iron x2 and trees 150% cause spatial
competition, the resolver must produce deterministic conflict results instead of blindly instantiating everything.
Support per-resource, per-category and global generation budgets/caps where appropriate.

ToolsHub / optional game-settings UI should expose player-tunable parameters generated from the definitions/catalog,
while developers retain control over which values players are allowed to change.

### WorldGenKit extensibility follow-up — World Features / POI / landmarks

Special generated structures such as towers, villages, rice paddies, shipwrecks, ruins, dungeons, camps, shrines,
bridges or secret areas must **not** be modeled as ordinary ResourceScatter entries.

Introduce a separate semantic generation concept, tentatively **WorldFeature / POI**:

- `WorldFeatureDefinition`: stable ID, category/tags, footprint/bounds, uniqueness/count policy, spacing,
  generation constraints, terrain adaptation requirements, content/template reference and player exposure policy.
- `WorldFeatureRule`: scores candidate locations from arbitrary WorldData channels (height, slope, biome,
  water depth/distance, coast distance, road/settlement distance, custom project channels, etc.).
- `WorldFeatureCandidate`: deterministic candidate with score, seed, footprint/reservation area and required edits.
- `WorldFeatureResolver`: resolves uniqueness, min-distance, footprint overlap, reserved zones, biome/region quotas,
  feature-to-feature conflicts and generation budgets.
- `WorldFeatureInstanceData`: pure semantic result (FeatureId, world/chunk position, orientation, seed,
  footprint/reserved cells, parameters) without directly instantiating Unity prefabs.
- Unity 2D/3D presentation adapters instantiate Tilemap stamps, prefabs, terrain stamps, meshes, scenes, etc.

Support at least three feature classes conceptually:

1. **Landmark / Single Site**: tower, shrine, shipwreck, giant tree.
2. **Area Feature**: rice paddy, swamp patch, crater, ruins field; may paint/override terrain/surface/biome channels.
3. **Compound Feature / Settlement**: village, camp, fortress, dungeon entrance; generated from a reusable
   FeatureTemplate/SubGenerator with internal layout, roads, buildings and props.

Generation order must support **reservation before ordinary scatter**:

`Terrain/Base Channels -> Feature Candidate/Reservation -> terrain/authoring adaptations -> resource scatter -> final validation`

This prevents trees/ore/resources from occupying a future village, tower footprint, rice paddy or shipwreck zone.
Features may expose explicit overlap policy, e.g. underground ore may coexist with a village while surface trees may not.

Feature definitions should support:

- fixed count / density / probability / per-region quota / unique-per-world;
- required or forbidden biome/tags;
- min/max height, slope, water depth, coast/water/road/settlement distance;
- min-distance between same/different feature categories;
- required adjacency/connectivity;
- footprint and influence/reservation radius;
- terrain flatten/carve/fill/stamp requests;
- optional authored template plus procedural internal generator;
- deterministic per-feature seed;
- versioned stable IDs so existing worlds survive later feature additions.

Existing generated worlds:

- adding a new FeatureDefinition must not silently rewrite generated chunks;
- by default it affects newly generated chunks only;
- explicit policies may target unvisited/unmodified regions or authoring-selected regions;
- unique world features should be tracked by WorldKit/SaveKit so future chunks know whether one has already been placed.

ToolsHub should provide a Feature/POI authoring page with rule editing, footprint/reservation preview,
candidate heatmap, conflict diagnostics, seed preview, and accepted/rejected candidate statistics.

### World Framework 0→1 implementation plan frozen

The formal implementation plan is now recorded in:
`Assets/docs/WorldFramework-Implementation-Plan.md`.

The plan treats the work as a **World Framework family**, not one oversized WorldKit:
`WorldKit.Core + WorldGenKit.Core + WorldGen resource/feature modules + PlacementKit.Core + optional Adapters/Integrations`.
Existing GridKit/SpatialKit/PathKit/SaveKit/SimulationKit/TimeKit retain their own responsibilities.

Implementation phases are P0-P12:

- P0 Architecture Freeze + Tiny Foundation Integration
- P1 GridKit topology foundation (Square/Hex/Cell-Edge-Vertex)
- P2 WorldKit Core
- P3 WorldGenKit Core
- P4 Terrain/Biome/Surface MVP
- P5 Import + Manual Authoring
- P6 Resource + Occupancy
- P7 Feature/POI + PlacementKit
- P8 Unity presentation adapters
- P9 Infinite World + Streaming
- P10 ToolsHub production authoring
- P11 integration samples
- P12 performance/release seal

V1 target: broad **planar finite + infinite** world support with square/hex, typed channels, generation pipeline,
manual/import/procedural sources, resource/feature/placement systems, 2D + 3D presentation adapters, ToolsHub and SaveKit delta integration.
Planet/spherical and full 3D voxel worlds are explicit future Extensions; Core must remain compatible but V1 will not implement them fully.

Key release rule: do not add game-domain concepts to WorldKit/WorldGenKit Core when the requirement can be solved via
Channel/Rule/Stage/Layer/Occupancy/Feature/Adapter contracts.

### World Framework persistent development tracking

A dedicated persistent status ledger now exists:

`Assets/docs/WorldFramework-Development-Status.md`

Use it as the source of truth for **current development progress**, while
`WorldFramework-Implementation-Plan.md` remains the source of truth for the roadmap/design plan.

Every World Framework implementation task must update the status ledger with:

- active P-phase;
- finished / in-progress / blocked work;
- material API decisions;
- changed modules/files;
- exact tests actually executed;
- PASS / FAIL / NOT RUN / BLOCKED validation state;
- next concrete task.

Never infer PASS from code inspection.

Independent Kit usage is now a hard requirement:

- PathKit.Core must remain independently usable for custom graph pathfinding.
- GridKit.Core must remain independently usable.
- GridKit + PathKit.GridKitAdapter must support grid navigation without WorldKit/WorldGenKit.
- An existing Unity Terrain/Mesh/scene must be able to use a Unity Grid Projection/Bake adapter to produce logical grid
  height/slope/walkability/cost data, followed by a persistent Manual Override layer and optional PathKit navigation.
- WorldKit must remain usable without WorldGenKit.
- WorldGenKit must remain usable without WorldKit.

If a simple project must import the whole World Framework stack to use PathKit/GridKit or Terrain-to-grid navigation,
the architecture is considered incorrect.

P0 implementation work started on 2026-09-16.

Current P0 audit findings:

- `GridKit.Core`, `PathKit.Core`, `SpatialKit.Core`, and `SimulationKit.Core` currently have no assembly references and
  `noEngineReferences=true`.
- `PathKit.Core` already consumes generic `IPathGraph`; preserve this standalone design.
- `PathKit.GridKitAdapter` currently references only PathKit.Core + GridKit.Core and delegates walkability/traversal/cost
  through `IGridPathTraversalPolicy`; preserve this boundary.
- GridKit currently has square 4/8-neighbor primitives but does not yet have the planned generic Topology, Hex,
  Cell/Edge/Vertex model. This belongs to P1 GridKit work, not WorldKit.
- SaveKit owns storage/serialization; future WorldKit.Core must not depend on SaveKit.Core. Use a WorldKit.SaveKitAdapter.

P0 architecture source:

`Assets/docs/WorldFramework-P0-Architecture-Freeze.md`

First P0 Tiny Foundation Integration baseline was actually executed through UnitySkills on 2026-09-16:

- GridKitTests 17/17
- PathKitCoreTests 15/15
- PathKitGridKitAdapterTests 11/11
- SpatialKitTests 13/13
- SimulationKitTests 17/17
- SaveKitCoreTests 29/29
- TimeKitTests 6/6

Total: **108/108 PASS, 0 failed, 0 skipped**.

This validates the existing foundation baseline only. It does NOT mean new WorldKit/WorldGenKit/PlacementKit code or
their future distribution profiles have passed; those do not exist yet.

P0 also added executable architecture policy coverage:
`WorldFrameworkFoundationBoundaryTests`.
It locks the current zero-dependency/no-engine boundaries for GridKit/SpatialKit/PathKit/SimulationKit and prevents the
PathKit.GridKitAdapter or existing Foundation source from silently acquiring future WorldKit/WorldGenKit/PlacementKit dependencies.

The new policy test itself was actually compiled and run. Its first draft had 10 C# syntax errors caused by JSON-string
quote escaping; those were fixed, Unity then compiled with 0 errors / 0 warnings, and the policy suite passed 3/3.
Current verified P0 session total is therefore **111/111 PASS** (108 foundation baseline + 3 architecture policy tests).

P0 concrete API contract draft now exists:
`Assets/docs/WorldFramework-P0-Core-API-Contracts.md`.

Important API decisions in that draft:

- new large Kits use dedicated namespaces: `StellarFramework.WorldKit`, `StellarFramework.WorldGenKit`,
  `StellarFramework.PlacementKit`;
- planar V1 chunk/region coordinates use long integer coordinates and continuous logical planar coordinates use double;
- authored/persisted IDs are validated stable ordinal strings, while hot runtime paths use typed numeric handles;
- WorldKit data layers and WorldGen channels must not perform per-cell string/object dictionary lookup;
- Stage registration is explicit; pipeline compile validates dependencies before execution; no reflection discovery;
- deterministic generation uses stable framework hashing, never `string.GetHashCode()` or shared Unity Random state;
- Placement P0 freezes rule/result semantics but intentionally does not force one universal footprint geometry;
- Dense/Sparse/Chunked hot-path accessor hierarchy is intentionally deferred to P3 benchmark work instead of being
  prematurely frozen.

P0 pressure review found and corrected two architecture issues before Runtime coding:

1. `ChannelHandle<T>` and `WorldDataLayerHandle<T>` must include a registry generation/owner token (or equivalent),
   not only an integer index, so cross-plan/schema handle misuse cannot silently address the wrong slot.
2. A universal occupancy type owned by WorldGenKit would break standalone PlacementKit/GridKit. V1 instead keeps
   occupancy/reservation semantics owned by each independent Core/module (GridKit occupancy, WorldGen generation
   reservations, PlacementKit placement claims) and bridges equivalent stable semantics through adapters.

Pressure review document:
`Assets/docs/WorldFramework-P0-Design-Review.md`.

GridKit independent-use / projection contract is now formalized in:
`Assets/docs/WorldFramework-P0-GridKit-Projection-Topology-Contract.md`.

Important decisions:

- preserve current square GridKit V1 APIs;
- P1 adds topology capability instead of reinterpreting `GridCoord` as universal;
- Hex uses a dedicated `HexCoord` (axial public form) and Hex topology;
- Cell/Edge/Vertex are topology capabilities owned by GridKit, with topology-specific efficient identity types where needed;
- isometric is normally a coordinate/presentation mapping over square topology, not a separate logical topology;
- `GridKit.UnityProjectionAdapter` may depend on UnityEngine + GridKit.Core but not WorldKit/WorldGenKit/PlacementKit/PathKit/SaveKit;
- Terrain/Mesh bake data is adapter-owned, not hardcoded into GridKit.Core;
- manual walkability/cost overrides are stored separately from auto-bake data and survive rebake;
- PathKit integration continues through `IGridPathTraversalPolicy` / PathKit.GridKitAdapter without changing PathKit.Core.

### World Framework P0 frozen / P1 opened

P0 Architecture Freeze is complete and frozen as of 2026-09-16.

Final P0 unique validation coverage in the session:

- foundation behavior baseline + TimeKit: 108 tests
- WorldFrameworkFoundationBoundaryTests: 3 tests
- PathKitPolicyTests: 3 tests
- SimulationKitPolicyTests: 2 tests
- KitArchitectureMetadataPolicyTests: 5 tests

Total: **121/121 PASS, 0 failed, 0 skipped**.

UnitySkills final diagnose reported healthy Editor state with 0 console errors / 0 warnings and no active compilation.
`git diff --check` also passed.

P0 planned distribution IDs are frozen in `WorldFramework-P0-Architecture-Freeze.md`, but no future World profile is
advertised as available before actual source/asmdefs/export validation exist.

Current active milestone is **P1 — GridKit Topology Foundation**.

P1 implementation started with a non-breaking additive topology layer under GridKit:

- `IGridTopology<TCoord>` uses caller-owned `Span<TCoord>` neighbor buffers;
- `Orthogonal4Topology` and `Orthogonal8Topology` wrap existing square semantics without modifying old APIs;
- dedicated axial `HexCoord` and `HexDirection`;
- `HexTopology` supports stable six-neighbor order, distance, ring and range;
- coordinate overflow is explicit;
- new `GridTopologyTests` cover square compatibility, Hex negatives, buffer validation, overflow, ring/range count and uniqueness.

These P1 changes still require Unity compile/test validation before they may be marked PASS.

P1 second batch adds optional GridKit topology capabilities for data on Cell edges/vertices:

- `IGridEdgeTopology<TCell,TEdge>`
- `IGridVertexTopology<TCell,TVertex>`
- canonical `HexEdge` represented by its sorted two adjacent cells;
- canonical `HexVertex` represented by its sorted three sharing cells;
- `HexTopology` implements edge/vertex enumeration and adjacency.

This is intended to support Civ-like rivers/walls/borders on Hex edges without putting those semantics in WorldKit.
The second batch is NOT PASS until Unity compile/tests are rerun.

P1 also adds:

- a Benchmark-category 1M neighbor+distance query trend benchmark for Orthogonal4/Orthogonal8/Hex;
- an architecture policy that rejects UnityEngine, LINQ, IEnumerable/yield, WorldKit, WorldGenKit and PlacementKit
  references inside the GridKit Topology hot-path source folder.

Packaging decision: do not make the standalone `Sample.FlowKit` depend on the whole `StellarFramework.Runtime` merely to demonstrate MSV. The standalone sample must remain exportable with FlowKit dependency closure. A real `Architecture<T> / Model / Service / View + FlowKit` production demo belongs in a Framework Integration/Tiny Foundation Integration layer or in a real project. The ToolsHub scaffold intentionally generates only FlowKit boundary code and connects to project-owned Services/Models rather than generating a second business architecture.

## 9. Anti-regression / design reminders

- No “helpful” cross-layer dependency that makes a standalone Foundation Kit non-standalone.
- No business-domain object references inside generic Core Kits.
- No hidden catch-all fallback that turns real errors into silent success.
- No partial mutation on failed occupancy/save/path outputs where the public contract promises atomic failure.
- No large Demo or gameplay semantics inside Integration Verification.
- No claim that sample behavior equals release validation.
- No preservation of runtime handles across save/load when the Kit contract says to rebuild from stable business data.
- No direct modification of generated/package/Addressables/HybridCLR artifacts unless the task explicitly requires it.
- Preserve Unity `.meta` GUIDs when moving tracked assets; use proper move semantics rather than recreate/delete.

## 10. Change log

### 2026-09-15 — Initial project familiarization

- Inspected Coding Tools workspace and scoped all work to `C:\CodingToolsWorkerCenter\StellarFramework`.
- Confirmed no existing project `chatgptwebmemory.md` existed.
- Read root README/CODELY, Unity/package baseline, Kit architecture/distribution/validation docs, Architecture core, current Runtime Kit/asmdef layout, and the usage contracts for TimeKit, SaveKit, GridKit, SpatialKit, SimulationKit, PathKit and FlowKit.
- Reviewed current Git branch/status and recent FlowKit/PathKit/SimulationKit/SpatialKit history.
- Created this file as the persistent development memory.
- No Runtime/Editor/framework behavior was changed by this familiarization task.
- Validation for this task: documentation/source inspection only; no Unity tests or package builds were required or run.

### 2026-09-15 — Roadmap and FlowKit next-stage direction recorded

- Recorded the remaining continuation order: Tiny Foundation Integration -> WorldKit -> WorldGenKit -> PlacementKit -> InventoryKit.
- Explicitly kept ProductionKit / LogisticsKit as domain candidates rather than immediate framework Kits.
- Recorded the responsibility boundary that WorldKit must not absorb Time/Save/Grid/Spatial/Simulation/Path/Generation/Placement/Inventory concerns.
- Recorded the next FlowKit gap: project-level workflow business-code conventions, adapter patterns, failure/cancellation/timeout rules, naming/folder structure, tests, diagnostics and enforceable policy.
- No Runtime/Editor behavior changed in this memory-only update.
- Validation: memory file patch/read only; no Unity tests required.

### 2026-09-15 — FlowKit MSV Coding Contract and typed project integration implemented

- Kept FlowKit Core / FlowRunner V1 execution semantics frozen; changes were concentrated in UnityIntegration, ToolsHub Editor, tests, samples and docs.
- Added the MSV business integration contract: Flow Graph -> Operation Adapter -> Service -> Model -> View/Facts Bridge -> Signal/State -> FlowKit.
- Extended `FlowAuthoringCatalog` with `FlowExternalCallKind`, typed arguments, result kind, required capability, additional-argument policy, Signal/State/Blackboard value kinds and Binding expected type.
- Added modular validation semantics: Catalogs are partial/non-strict by default; known contracts are type-checked, while `StrictUnknownReferences` + optional exact `StrictFlowIds` enables closed-world validation for selected release-ready flows.
- Added stable-ID and argument-key validation. Strict scopes with only invalid/blank FlowIds explicitly do not become accidental global scopes.
- Added `FlowKitContractValidator` and integrated it into project build validation.
- Added category-specific `FlowAuthoringContractEntryDrawer` Inspector UX.
- Added FlowKit ToolsHub `业务骨架` generation with no-overwrite behavior and standard Contracts/Bootstrap/Operations/Facts/Bindings/Graphs/Tests layout.
- Added a compiling failure-by-default Operation Adapter example to generated scaffolds; generated adapters never fake Success before implementation.
- Added optional `FlowBinding.Target`; null preserves the previous FlowBinding-self binding contract.
- Refactored FireDrill teaching integration away from a universal operationId switch router.
- Closed all 14 previously-unrouted failed/cancelled Operation outputs in `SchoolTrainingWorkflow.flow.json` via an explicit business-failure endpoint.
- Expanded FlowKit Editor regression coverage to stable IDs, partial/strict catalog semantics, required arguments, value type mismatches, Binding strictness, strict-scope edge cases, scaffold no-overwrite and Binding Target compatibility.
- Validation actually executed:
  - `git diff --check` on FlowKit/docs/tests/sample changes: PASS (only Git line-ending notices).
  - Unity actual script compilation rebuilt `StellarFramework.FlowKit.Unity.dll`, `StellarFramework.Samples.FlowKit.dll`, `StellarFramework.ToolsHub.FlowKit.Editor.dll` and `StellarFramework.ToolsHub.FlowKit.Editor.Tests.dll`: PASS.
  - Direct Roslyn subset checks for new runtime/sample/editor code: PASS.
  - Pure Core `FlowCompiler` verification over all current `.flow.json`: 2/2 PASS, 0 compile warnings after fixing SchoolTraining failure/cancel routes.
  - Unity MCP Test Runner, final EditMode run of `StellarFramework.ToolsHub.FlowKit.Editor.Tests`: **24 passed / 0 failed / 0 skipped**, result state Passed.
  - Unity MCP Test Runner, FlowKit Core EditMode run of `StellarFramework.FlowKit.Tests`: **34 passed / 0 failed / 0 skipped**, result state Passed.
  - Unity Console query for Error/Warning entries containing `FlowKit` after final tests: 0 entries.
- Validation history note: the first real EditMode run correctly exposed a strict-scope bug where an empty contract dictionary bypassed unknown Binding validation. The validator activation rule was fixed (`HasValidationRules` includes strict scopes), then the full suite was rerun successfully. A later strict-scope invalid-ID edge case was also added and passed.
- Environment note: ordinary `dotnet build` / NuGet restore remains unsuitable for this Unity-generated solution on the current machine because .NET 10 restore reports `Value cannot be null (path1)` before C# compilation. This was not counted as a FlowKit failure; Unity compilation, direct Roslyn checks and Unity Test Runner were used instead.
- Pre-existing non-FlowKit dirty files recorded earlier remain untouched.

### 2026-09-15 — FlowKit P4 MSV Production Pattern closed out

- Added a dedicated Framework Integration Sample at
  `Assets/StellarFramework/Samples/Integration/FlowKitMsvIntegration/`.
- Kept the standalone `Sample.FlowKit` dependency boundary intact. The new integration sample explicitly depends on
  `StellarFramework.Runtime`, `StellarFramework.FlowKit.Core` and `StellarFramework.FlowKit.Unity`.
- The sample demonstrates the full production path:
  `Flow Graph -> Operation Adapter -> Service -> Model -> View / Facts Projector -> State/Signal -> FlowKit`.
- Added:
  - `FlowMsvSampleApp`
  - `FlowMsvSampleModel` + read-only Model contract
  - `FlowMsvSampleService`
  - focused Prepare / Complete Operation Adapters
  - `FlowMsvSampleFactsProjector` pure C# projection logic
  - `FlowMsvSampleFactsBridge` MonoBehaviour lifecycle wrapper
  - `FlowMsvSampleView`
  - `FlowMsvSampleConfigurator` / `FlowMsvSampleComposition`
  - `FlowKitMsvProductionPattern.flow.json`
  - dedicated EditMode integration tests
  - strict `FlowMsvProductionPatternCatalog.asset`
- The graph uses State for the readiness gate and a separate Signal for the transient ready-changed occurrence, demonstrating the intended Signal-vs-State contract.
- Added distribution profile `samples.flowkit-msv-integration` to `KitDistributionCatalog.json`.
- Added the production-pattern link to the FlowKit coding guide / Samples docs / root README.
- While running the real integration tests, they exposed an existing MSV Core defect: `Architecture<T>.Init()` did not call the abstract `InitModules()` method even though all architecture docs and DemoApp rely on that lifecycle.
- Fixed `Architecture<T>.Init()` to call `InitModules()` before module initialization. If registration throws, uninitialized registrations are cleared, Architecture references are detached, state returns to Uninitialized, and the original exception is rethrown (no swallowed exceptions).
- Added `ArchitectureLifecycleTests.InitInvokesInitModulesBeforeInitializingRegisteredModules` as a dedicated regression test and added `StellarFramework.Runtime` to the FrameworkValidation test assembly references.
- Corrected the new sample Graph to use the real FlowKit ports:
  - `flow.entry.next`
  - `flow.wait.state.changed`
- UnitySkills transport note:
  - the earlier `http://127.0.0.1:5890/mcp` gateway temporarily stopped listening during this work;
  - continued successfully against the StellarFramework Unity instance through `http://localhost:8090/` (UnitySkills 2.8.3);
  - project identity confirmed as `StellarFramework`, instance `StellarFramework_DEEE9F8A`;
  - Bypass mode was explicitly enabled by the user before executing Unity Test Runner actions through 8090.
- Final validation actually executed after the fixes:
  - Unity compile through 8090: **0 errors / 0 warnings**.
  - FlowKit + MSV Integration EditMode tests: **3 passed / 0 failed / 0 skipped**.
  - FlowKit Editor EditMode tests: **24 passed / 0 failed / 0 skipped**.
  - FlowKit Core EditMode tests: **34 passed / 0 failed / 0 skipped**.
  - Architecture lifecycle regression: **1 passed / 0 failed / 0 skipped**.
  - Standalone source / package policy tests: **30 passed / 0 failed / 0 skipped**.
  - Kit architecture metadata tests: **5 passed / 0 failed / 0 skipped**.
  - Strict typed Production Pattern Catalog is covered by the FlowKit project build-validator test; FlowKit Editor suite remained 24/24 after the catalog was created.
- Final combined seal rerun through UnitySkills 8090 Bypass mode: **97 / 97 relevant EditMode tests passed, 0 failed, 0 skipped** across Integration, FlowKit Editor, FlowKit Core, Architecture lifecycle, standalone packaging policy and Kit metadata policy suites.
- P4 status: production MSV integration pattern is considered complete. P5 SubFlow / reusable Flow composition remains intentionally unimplemented and must be designed separately before touching frozen FlowKit Core semantics.

### 2026-09-16 — World Framework P2 WorldKit Core started

- P0 Architecture Freeze is complete; P1 GridKit Topology Foundation is frozen as PASS.
- P2 active milestone: `WorldKit Core`.
- First WorldKit runtime batch added zero-dependency/no-engine `StellarFramework.WorldKit.Core`, stable `WorldId`, signed 64-bit Chunk/Region coordinates, double logical planar point, finite half-open chunk bounds, explicit Finite/Infinite extent, and explicit adjacent-only Chunk lifecycle transitions.
- Unity compile after the first batch: 0 errors / 0 warnings.
- `WorldKitCoreTests`: 7/7 PASS.
- First `WorldFrameworkFoundationBoundaryTests` rerun: 2/4 PASS because the newly edited policy incorrectly applied the old "must not contain WorldKit" guard to WorldKit itself. This was a test-design bug, not a WorldKit runtime defect.
- The policy was corrected without weakening runtime boundaries: pre-existing Foundation roots retain the future-World-stack prohibition; WorldKit gets a dedicated no-Unity/no-GridKit/no-SpatialKit/no-PathKit/no-SaveKit/no-SimulationKit/no-WorldGenKit/no-PlacementKit source dependency check while its asmdef remains zero-reference and engine-free.
- P2 second Runtime batch now includes typed World data-layer registry/handles with registry-generation protection, on-demand finite/infinite Chunk registry, deterministic dirty-Chunk tracking, and an ordered semantic WorldDelta contract/set that remains independent of SaveKit/serialization. New behavior tests were added; this batch is NOT PASS until Unity compile/Test Runner validation completes.
- `WorldDataLayerStore<T>` is also part of P2: one typed store per registered layer, with World/Region/Chunk scope-specific access. A Chunk payload can be a DenseGrid, graph page, sparse page, DTO or project-owned type; WorldKit does not prescribe the payload representation and does not use `Dictionary<string, object>` for hot data access.
- P2 delivery metadata/docs are being closed: `worldkit.core` is now registered as a zero-dependency `foundation / world` profile; WorldKit usage/source docs and root README entries were added. The validation matrix's previously stale profile count was corrected to the actual post-WorldKit total: 66 profiles, including 19 Foundation and 24 sample profiles. This Catalog/profile change still requires metadata + standalone source export policy validation before P2 is sealed.
- P2 distribution validation has now passed: WorldKit behavior 15/15, WorldFramework Foundation Boundary 5/5, Kit Architecture Metadata 5/5, Standalone Source Export Policy 30/30. WorldKit 100k benchmark also passed with register=9.216 ms, transition2x=28.204 ms, layer write=7.044 ms, layer read=7.956 ms, dirty mark/write=8.833 ms, unload/remove=48.084 ms and coarse `GC.GetTotalMemory(false)` allocationDelta=0. A final source-policy extension now also rejects reflection scanning, LINQ/yield hot paths and `Dictionary<string, object>` inside WorldKit Core; that extended policy still needs its final rerun before P2 is sealed.
- P2 final seal completed: extended Foundation Boundary reran 5/5 PASS, Unity compile remained 0 errors / 0 warnings, UnitySkills diagnose was healthy with 0 console errors / warnings, and `git diff --check` passed (line-ending notices only). Relevant P2 Behavior/Policy/Standalone validation is 55/55 PASS plus WorldKit benchmark 1/1 PASS. P2 WorldKit Core is now frozen.

Current active milestone is **P3 — WorldGenKit Core**.

P3 Batch 1 has started with a zero-dependency/no-engine `StellarFramework.WorldGenKit.Core`, stable Channel/Stage/Rule IDs, storage/scope/source descriptors, typed generation-protected Channel handles/registry, and framework-owned stable 64-bit seed derivation. WorldGenKit.Core does not depend on WorldKit.Core; integration remains adapter-owned.
- P3 determinism is locked by a fixed regression vector: seed=123456789, x=-42, y=77, stage=`stage.height`, localKey=999 must derive `0x56D9FA3612E0585D`. WorldGenKit source policy also rejects Unity/existing Kit dependencies, reflection scanning, LINQ/yield hot paths, `Dictionary<string, object>`, `string.GetHashCode`, and Unity Random usage.
- P3 Batch 1 first behavior run was 3/4 and correctly exposed a default-struct bug: `default(WorldChannelStorageDescriptor)` looked valid because Dense/World are enum zero values. The descriptor now carries an explicit-construction marker, making default invalid without changing the public enum ordinals. This fix requires compile/test rerun.
- P3 Batch 1 reran 4/4 PASS with Unity compile 0 errors / 0 warnings. Stage/Pipeline work is now in progress. A pre-test review corrected optional-channel binding semantics: pure Optional inputs may be unbound; Required/Produced/Mutated channels are preflight-required.
- P3 Pipeline compiler/plan now has behavior tests covering DAG ordering/execution, missing/duplicate writers, cycles, illegal ProvidedInput production, cross-registry handles, descriptor errors, optional inputs, execution preflight guards and fail-fast stage results. These tests are present but are NOT PASS until Unity Test Runner executes them.
- P3 Pipeline tests are now 7/7 PASS with compile 0/0. Rule/storage/report work has been added: typed rule interfaces plus allocation-conscious scalar primitives (Range/Threshold/Curve/Noise/Distance/Inverse/composition/tag), all six storage kinds with typed binding, and optional immutable `WorldGenerationReport`. New storage/rule tests are NOT PASS until run.
- P3 storage/rule tests are now 5/5 PASS with compile 0/0. Added explicit acceptance coverage for a custom `game.magic_density` channel driving a threshold rule without any Temperature channel, stable PlanHash across equivalent dependency graphs, and immutable GenerationReport capture. These newest acceptance tests are not yet run.
- Added P3 performance benchmark for 1M Dense operations, 100k Sparse, 100k Chunked pages, 1M typed handle resolutions and 100k two-stage pipeline runs. It is NOT RUN until Unity Test Runner executes it.
- Custom Magic/PlanHash/Report acceptance is now included in `WorldGenKitPipelineTests` 9/9 PASS. WorldGenKit benchmark also passed 1/1 on Unity 2022.3.62f3c1: Dense write/read 3.013/0.485 ms, Sparse write/read 0.768/0.748 ms, Chunked write/read 2.590/2.497 ms, 1M handle resolves 17.523 ms, 100k two-stage plan runs 58.870 ms, coarse heap delta 0.
- P3 delivery docs/metadata added: WorldGenKit usage/source guides, root README entries, `worldgenkit.core` Catalog profile classified as zero-dependency `extension / world`, architecture guide section and validation-matrix evidence. Catalog total is now 67 profiles: 19 Foundation, 4 Extension, 12 Adapter and 24 Sample profiles. Final Metadata/Standalone/Boundary gate reruns are still required before P3 is frozen.
- P3 final seal completed: WorldGen behavior 18/18 PASS, World Framework Boundary 6/6, Kit Metadata 5/5, Standalone Source Export 30/30; relevant non-benchmark total **59/59 PASS** plus WorldGen benchmark 1/1. Unity compile remained 0 errors / 0 warnings, UnitySkills diagnose healthy with 0 console errors/warnings, and `git diff --check` passed with line-ending notices only. P3 WorldGenKit Core is frozen.

Current active milestone is **P4 — Terrain / Biome / Surface MVP**. P4 must live above Core in a separate Builtins/Extension assembly so Height/Moisture/Water/Slope/Biome/Surface/Buildable do not become hardcoded Core fields.
- Before creating `WorldGenKit/Builtins`, the frozen `worldgenkit.core` Catalog profile was narrowed from the entire WorldGenKit root to explicit Core subdirectories/files, and the Core policy test was narrowed to the same Core directories. This prevents standalone Core export/policy from accidentally treating future Builtins as Core.
- P4 Batch 1 code now exists: `StellarFramework.WorldGenKit.Builtins` is engine-free and references only WorldGenKit.Core. Added `WorldPlanarSampleLayout`, integer-period deterministic fractal value noise, `WorldHeightStage`, optional `WorldMoistureStage`, `WorldWaterDepthStage` and `WorldSlopeStage`. Builtin noise stages use `SeedScope.World` plus absolute logical sample coordinates from RunKey origin so adjacent tiles share one deterministic field. This batch is NOT PASS until Unity validation.
- P4 Batch 1 first compile failed with exactly 2 CS8156 errors from passing `context.RunKey` property expressions directly as `in` parameters. The utility now copies RunKey to a local before passing by readonly reference; recompile is required and the failure is retained in the ledger.
- P4 Batch 1 recompiled successfully with 0 errors / 0 warnings. Added terrain Builtins tests covering negative absolute coordinates, deterministic noise, exact monolithic-vs-two-adjacent-tile Height matching, imported Height driving Water/Slope without Height generator, optional Moisture, and explicit storage-length failure. Tests are NOT RUN yet.
- P4 Batch 1 terrain tests are now 6/6 PASS. P4 Batch 2 code adds stable `WorldBiomeId`/`WorldSurfaceId`, immutable catalogs, optional criteria over Height/Moisture/WaterDepth/Slope, deterministic Biome selection, Surface mapping, and byte Buildable mask generation with slope/water/blocked-biome policy. Batch 2 validation is NOT RUN yet.
- Added P4 Batch 2 tests for unique/fallback catalogs, deterministic priority/stable-ID tie selection, optional Moisture absence/presence, precompiled Surface mapping + invalid index failure, Buildable constraints, and a seven-stage end-to-end Height/Moisture/Water/Slope/Biome/Surface/Buildable pipeline with deterministic repeat-output checks. Tests are NOT RUN yet.
- P4 Batch 2 compiled cleanly and its Biome/Surface/Buildable/end-to-end suite passed 6/6. Added a Builtins source/asmdef boundary policy (engine-free, only WorldGenKit.Core dependency) and a 512x512 full seven-stage Builtins benchmark; those newest gates are not yet run.
- Builtins boundary needed an explicit Unity asset refresh before the newly added test was discovered; after refresh it ran 7/7 PASS. The first 512x512 seven-stage benchmark passed with compile=10.711 ms, run=931.154 ms, coarse heap delta=0, but exposed repeated Stable Rule ID work inside fractal-noise hot loops. Added an additive `WorldNoiseKey` compiled-noise sampling path in WorldGenKit.Core; the legacy WorldRuleId overload and existing stable seed regression semantics remain unchanged. Builtins fractal noise now uses the compiled key and must be revalidated/rebenchmarked.
- Compiled-noise optimization is validated: P3 seed regression 4/4, Storage/Rule 5/5, P4 Terrain 6/6 and semantic/full-pipeline 6/6 all PASS. 512x512 seven-stage Builtins benchmark improved from 931.154 ms to 366.175 ms (~60.7% lower), compile=1.185 ms and coarse heap delta=0. Added formal `worldgenkit.builtins` Catalog profile (only dependency: `worldgenkit.core`), Builtins guide, README/architecture/validation matrix entries; Catalog now has 68 profiles, including 5 Extensions. Final P4 metadata/standalone/all-gate reruns remain.
- P4 final seal completed: P3 Core regression 18/18, P4 Builtins behavior 12/12, World Framework Boundary 7/7, Kit Metadata 5/5, Standalone Source Export 30/30; relevant non-benchmark total **72/72 PASS** plus optimized Builtins benchmark 1/1. Unity compile is 0 errors/0 warnings, UnitySkills diagnose healthy with 0 console errors/warnings, and `git diff --check` passed. P4 Terrain/Biome/Surface MVP is frozen.

Current active milestone is **P5 — Import & Manual Authoring**. P5 must prove WorldGenKit is not procedural-only: neutral typed imports, sparse AuthoringOverride data, terrain edit operations, semantic paint operations and dirty-region recomputation remain engine-free; Unity Texture/Terrain/Tilemap import belongs in adapters.
- P5 Batch 1 code now exists in separate engine-free `StellarFramework.WorldGenKit.Authoring` depending only on WorldGenKit.Core + Builtins. Added local `WorldSampleRect`, exact caller-buffer Dense import, failure-atomic Stable-ID Biome/Surface import, and generic sparse `WorldDenseOverrideLayer<T>` that composes over but never mutates base data and tracks union dirty bounds. Validation is NOT RUN yet.
- P5 Batch 1 compiled cleanly. Six tests were added to lock rect/dirty geometry, typed Height/Mask import, failure-atomic Biome/Surface Stable-ID imports, sparse override composition without base mutation, remove/clear reversion to base, and dirty-bound consumption. Tests are NOT RUN yet.
- P5 Batch 1 tests are now 6/6 PASS. Batch 2 adds region-only execution to Builtins WaterDepth/Slope/Biome/Surface/Buildable, Height Raise/Lower/SetHeight/Flatten/Smooth, generic paint, and dirty propagation. Height edits keep Water pointwise dirty but expand Slope by one sample ring; Biome/Surface/Buildable inherit that expanded region so derived semantic data can be recomputed locally. Batch 2 validation is NOT RUN yet.
- Dirty propagation was refined before validation: downstream regions are nullable/optional. Moisture edits now invalidate only Biome -> Surface -> Buildable, not WaterDepth/Slope; Biome paint invalidates Surface/Buildable only; Surface paint has no current Builtins downstream dependency.
- P5 Batch 2 first compile failed with exactly 2 CS0246 errors in `WorldHeightAuthoringOperations` / `WorldAuthoringPaint`: both used `WorldPlanarSampleLayout` without the Builtins namespace import. The missing using directives were added; recompile is required.
- P5 Batch 2 then recompiled cleanly. Regional Builtins methods now take public `WorldGenerationDataSet` rather than requiring callers to construct the Core-internal `WorldGenerationContext`; normal pipeline `Execute` still delegates to whole-region execution. Five tests were added for edit/base semantics, Smooth snapshot behavior, generic paint, minimal dependency propagation and end-to-end local derived recomputation with outside-region immutability. Tests are NOT RUN yet.
- P5 Batch 2 test compilation exposed 5 CS8156 errors from passing nullable dirty-region `.Value` property expressions by `in`; the test now copies each dirty rect to a local before conversion. Runtime Authoring/Builtins code was not the failing source; recompile is required.
- P5 Batch 2 recompiled cleanly and `WorldGenKitAuthoringOperationsRegionTests` passed **5/5**. Stable-ID semantic paint helpers were then added for Biome/Surface; they resolve catalog IDs before any sparse override/dirty mutation, so unknown IDs fail atomically. The expanded Batch 2 suite now has 6 tests and needs rerun.
- Added P5 Authoring boundary policy (engine-free, only Core + Builtins dependencies) and a 512x512 benchmark measuring a preallocated 64x64 sparse Height edit, Base+Override composition and region-only Water/Slope/Biome/Surface/Buildable recomputation. Validation pending.
- Expanded P5 Batch 2 suite passed **6/6** including Stable-ID Biome/Surface paint failure atomicity. Authoring Boundary passed **8/8**. 512x512 benchmark passed 1/1: 4,096-sample sparse Lower edit=1.123 ms, full 262,144-sample Base+Override compose=0.543 ms, 4,356-sample derived regional recompute=0.161 ms, coarse heap delta=0. Added formal `worldgenkit.authoring` profile, Authoring guide, README/architecture/validation matrix entries; Catalog is now 69 profiles with 6 Extensions. Final P5 Metadata/Standalone/full regression gates remain.
- P5 final seal completed: P3 Core 18/18, P4 Builtins 12/12, P5 Authoring 12/12, Boundary 8/8, Metadata 5/5, Standalone Source Export 30/30 => **85/85 relevant non-benchmark PASS**. Builtins benchmark rerun passed with compile=1.152 ms/run=408.326 ms/checksum unchanged. Authoring benchmark rerun passed with edit=0.209 ms, compose=0.247 ms, regional recompute=0.396 ms, heap delta=0; together with the first run this gives observed ranges of 0.209–1.123 / 0.247–0.543 / 0.161–0.396 ms. UnitySkills diagnose healthy with 0 errors/warnings and `git diff --check` PASS. P5 is frozen.

Current active milestone is **P6 — Resource Scatter & Occupancy**. P6 must keep resources as semantic spawn/candidate/occupancy data rather than `WorldCell.Resource`; support stable IDs, deterministic candidate generation/resolution, coexistence/conflicts, density/spacing/priority/budgets, player generation modifiers and explicit existing-world regeneration policy without silently repopulating old modified areas.
- P6 Batch 1 code now exists in separate engine-free `StellarFramework.WorldGenKit.Resources` depending only on WorldGenKit.Core + Builtins. Stable Resource/Category/Occupancy IDs compile occupancy types into a registry-local 64-bit mask for hot paths; ResourceDefinition/Catalog stay Stable-ID driven while SpawnCandidate/Resolver use resource indices. Occupancy state tracks both Occupied and Excluded masks so conflicts work in both directions. Resolver prevalidates all candidates, uses caller-owned heap scratch, and ranks independent of input order by Priority -> Score -> DeterministicKey -> stable Resource ID rank -> coordinates. Validation is NOT RUN yet.
- P6 Batch 1 compiled cleanly. Six tests were added for registry/masks, atomic failed occupancy, Tree + underground Ore coexistence, Building reservation excluding Tree but not allowed Ore, candidate input-order independence and resolver prevalidation before mutation. Tests are NOT RUN yet.
- P6 Batch 1 tests are now 6/6 PASS. Batch 2 code adds explicit existing-world application policy, global/category/resource generation multipliers (Occurrence/Cluster/Richness), resolved settings, deterministic Density seed+cluster generation and exact Coverage target selection. Candidate generation consumes optional caller-provided eligibility byte mask and suitability float scores rather than hardcoding Biome/Temperature/etc.; coverage uses caller-owned ranking scratch and cluster-coherent deterministic tie ranking. Validation is NOT RUN yet.
- Resource generation settings now retain immutable category/resource Stable-ID entry arrays in addition to lookup dictionaries so a SaveKit adapter can persist/reconstruct the exact generation settings. Seven Batch 2 tests were added for resolved multipliers, iron×2/copper×0.5 monotonicity, repeatability, exact forest 50% eligible coverage, suitability priority, settings reconstruction/future tile determinism and non-finite suitability rejection. Tests NOT RUN yet.
- P6 Batch 2 first test compile failed with 23 fixture-only errors: Core namespace missing for RunKey/Seed, an internal resolved-settings constructor was used directly, and the occurrence monotonic test mixed in cluster scaling with undersized buffers. Tests were corrected to use the public settings resolver, adequate density buffers and occurrence-only modifiers for the iron×2/copper×0.5 comparison. Runtime Resources code was not the compile failure source; recompile pending.
- P6 Batch 2 corrected tests compiled and passed **7/7**. Batch 3 adds compiled Global/Category/Resource budgets and a full deterministic resolver overload with caller-owned accepted-count and spacing scratch. MinSpacing uses per-sample grid buckets and only compares the same resource; budget/spacing checks happen before occupancy mutation. The old lightweight resolver now throws if a definition has `MinSpacing > 0`, preventing silent rule loss. Six Batch 3 tests were added and are NOT RUN yet.
- P6 Batch 3 Budget/Spacing tests are now **6/6 PASS**. Added an eighth generation regression checking two adjacent absolute-coordinate tiles generated in A->B versus B->A order produce identical per-tile candidates, locking generation-order independence before P6 seal.
- P6 generation suite rerun is **8/8 PASS**. Added a Resources boundary policy (engine-free, only Core+Builtins, no Authoring/other Kit/reflection/LINQ/runtime string-object patterns) and a 512x512 benchmark covering Density candidate generation plus Budget+MinSpacing+Occupancy resolution. Validation pending.
- Resources Boundary passed **9/9**. 512x512 Resources benchmark passed 1/1: 20,763 candidates, 9,527 accepted, generate=12.890 ms, resolve=11.670 ms, spacing rejects=11,236, occupancy/budget rejects=0, coarse heap delta=0. Added developer-controlled player generation exposure metadata at Global/Category/Resource scopes; Occurrence/Cluster/Richness each have exposed/min/max/default controls, and settings validation rejects unexposed or out-of-range overrides before generation. Five tests added; NOT RUN yet.
- P6 Exposure test compilation first failed with 9 CS1061 errors because the test file missed `using System;`, hiding array `AsSpan()` extensions. The runtime Exposure implementation was not the failing source. Added the using; recompile pending.
- P6 Exposure tests then compiled cleanly and passed **5/5**.
- Before sealing P6, a modularity review found that Resources should not depend on Builtins just to reuse `WorldPlanarSampleLayout`. Added standalone `WorldResourcePlanarDomain` and migrated CandidateGenerator, full Resolver, P6 tests and benchmark. `StellarFramework.WorldGenKit.Resources` asmdef now references **only `StellarFramework.WorldGenKit.Core`**; Builtins/Authoring/WorldKit/GridKit/SpatialKit/PathKit/SaveKit/PlacementKit remain excluded.
- Core-only Resources refactor was actually validated: Unity compile **0 errors / 0 warnings**; P6 Occupancy 6/6, Generation 8/8, Budget/Spacing 6/6, Exposure 5/5 => **25/25 PASS**; World Framework Boundary **9/9 PASS**; Resources benchmark **1/1 PASS**. Latest benchmark: 512x512, 20,763 candidates, 9,527 accepted, 11,236 spacing rejects, generate=10.131 ms, resolve=12.248 ms, checksum=36,212,402,757, coarse heap delta=0.
- Added formal `worldgenkit.resources` Catalog profile requiring only `worldgenkit.core`, plus Resources guide, README, architecture guide and validation-matrix entries. Catalog target is now 70 profiles: 19 Foundation / 7 Extension / 12 Adapter / 24 Sample. **Do not mark P6 frozen yet**: Metadata, Standalone Source Export, full P3/P4/P5/P6 regression, final diagnose and diff gates still need to run after this profile/docs registration.
- P6 final seal actually executed after the Resources profile/docs registration: Metadata **5/5**, Standalone Source Export **30/30**, P3 Core **18/18**, P4 Builtins **12/12**, P5 Authoring **12/12**, P6 Resources **25/25**, World Framework Boundary **9/9** => **111/111 relevant non-benchmark PASS**, 0 failed, 0 skipped. Resources benchmark also passed **1/1**.
- The original Resources benchmark used a single timing sample and showed noisy Editor observations from roughly 10 ms to 32 ms, so the benchmark itself was hardened (test-only change, no Runtime semantic change) to perform one warmup plus five measured iterations and report min/median. Final measured result on Unity 2022.3.62f3c1: generate min/median **10.438 / 10.714 ms**, resolve min/median **9.402 / 9.605 ms**, 20,763 candidates, 9,527 accepted, 11,236 spacing rejects, checksum=36,212,402,757, coarse heap delta=4,096 bytes. This is Editor trend evidence, not a device guarantee or strict allocation proof.
- Final P6 health gate: Unity compile **0 errors / 0 warnings**; Unity Console was explicitly cleared and `unity_diagnose` then returned healthy with **0 console errors / 0 console warnings**; final `git diff --check` passed with only line-ending notices. **P6 Resource Scatter & Occupancy is frozen.**

Current active milestone is **P7 — Feature / POI + PlacementKit**. P7 follows the frozen plan: Stable-ID FeatureDefinition, Landmark/Area/Compound feature classes, deterministic candidate/reservation, terrain-adaptation request data, unique-per-world tracking boundary, independent PlacementKit.Core, and acceptance scenarios for tower/rice-paddy/village/shipwreck plus Feature Reservation -> Resource Scatter cooperation.
- P7 Batch 1 code has started in engine-free `StellarFramework.WorldGenKit.Feature`, referencing only WorldGenKit.Core. Added Stable Feature/Category/TerrainStamp IDs, Landmark/Area/Compound kinds, Rectangle/Circle footprint geometry, rotated AABB reservation bounds, per-world/per-region quotas, FeatureDefinition/Catalog, deterministic candidate data, reservation/instance data and generic Flatten/Carve/Fill/Stamp terrain-adaptation requests. Feature Core currently has no Resources/WorldKit/PlacementKit/Unity dependency. Validation is NOT RUN yet.
- P7 Batch 1 first Unity compile passed with 0 errors / 0 warnings. Added eight Feature contract tests for Stable-ID/catalog duplication, Feature kinds, default-struct footprint/bounds guards, rotated reservation AABBs, half-open overlap semantics, quotas, candidate reservation and Stamp terrain-adaptation validation. Tests are NOT RUN yet.
- P7 Batch 1 Feature contract tests passed **8/8**. Batch 2 now adds a deterministic Feature Resolver: it prevalidates all candidates/existing counts/reservations before output mutation, uses caller-owned heap/count/output scratch, ranks by Priority -> Score -> DeterministicKey -> Stable Feature ID -> coordinates, enforces per-world/per-region quotas, and rejects overlap with both existing and newly accepted reservations. External persistent usage state is read-only input; accepted instances/reservations are explicit outputs. Validation is NOT RUN yet.
- Added seven Batch 2 resolver tests for Priority/input-order independence, Score/DeterministicKey tie order, existing reservation blocking, unique-per-world usage, same-call per-region quota, non-overlap acceptance and invalid candidate prevalidation before accepted-count/reservation mutation. Tests are NOT RUN yet.
- P7 Batch 2 first resolver run was **4/7 PASS, 3 FAIL**. Root cause was another default-struct trap: `new WorldFeatureQuota()` produced zero/zero limits, making ordinary Features quota-ineligible. Resolver order was not the defect. `WorldFeatureQuota` now has explicit initialization state; default is invalid, `WorldFeatureDefinition` rejects invalid quota, and callers use `Unlimited()`, `UniquePerWorld()` or explicit limits. Rerun pending.
- After quota hardening, Feature Contract reran **8/8 PASS** and Feature Resolver **7/7 PASS**, compile 0/0.
- P7 Batch 3 now exists as optional engine-free `StellarFramework.WorldGenKit.Feature.ResourcesAdapter` (Feature + Resources dependencies only). It compiles Stable-ID feature reservation bindings to feature indices and rasterizes continuous Feature reservation bounds onto `WorldResourcePlanarDomain` occupancy cells. Apply is two-pass: all target cells are checked first; if a conflicting resource already occupies a sample, the adapter throws before mutating any feature reservation, enforcing the intended Feature Reservation -> Resource Scatter pipeline order. Validation is NOT RUN yet.
- Added five Batch 3 integration tests for Stable-ID binding compile, exact bounds rasterization, tower reservation -> tree scatter rejection, atomic failure on wrong pipeline order with an already occupied tree cell, and unbound feature no-op. Tests are NOT RUN yet.
- P7 Batch 3 Feature.ResourcesAdapter tests passed **5/5**, compile 0/0.
- P7 Batch 4 now starts independent `StellarFramework.PlacementKit.Core` with **zero assembly references** and no engine references. It owns PlacementType/Rule/Failure stable IDs, Rectangle/Circle footprint + rotated bounds, PlacementRequest, generic `IPlacementRule<TContext>` evaluation with caller-owned failure output, common `PlacementSiteFacts`, and built-in slope/water-depth/zone/conflict/connection/base-suitability rules. Terrain/Grid/World sampling remains adapter-owned. Validation is NOT RUN yet.
- P7 Batch 4 first compile passed 0 errors / 0 warnings. Added eight PlacementKit Core behavior tests for default-footprint rejection, rotated geometry, built-in pass/failure semantics, collect-all vs first-failure mode, zone/connection Any/All behavior, custom rule extension without Core changes, and rule-list prevalidation before evaluation. Tests are NOT RUN yet.
- PlacementKit.Core first compile is **0 errors / 0 warnings**. Added eight Core behavior tests covering Stable-ID/default-footprint guards, rotated footprint bounds, built-in slope/water/zone/conflict/connection failures, successful score accumulation, first-failure mode, custom rule extension without Core changes, failure-buffer preflight and touching-vs-overlap bounds semantics. Tests are NOT RUN yet.
- P7 Batch 4 first Unity compile passed 0 errors / 0 warnings. Added eight PlacementKit.Core tests for Stable IDs, default footprint guards, rotated bounds, request validation, built-in slope/water/zone/conflict/connection behavior, collect-all vs first-failure behavior, failure-buffer preflight and a custom magic-density placement rule proving OCP extensibility. Tests are NOT RUN yet.
- P7 Batch 4 PlacementKit.Core tests passed **8/8** using the currently live UnitySkills instance at http://127.0.0.1:8092/. project_get_info confirmed product StellarFramework, Unity 2022.3.62f3c1 and project path C:/CodingToolsWorkerCenter/StellarFramework/Assets; port 8090 was refusing connections at that moment, so future tooling should discover/use the active instance rather than assume 8090.
- P7 Batch 5 starts optional engine-free StellarFramework.WorldGenKit.Feature.PlacementAdapter depending only on Feature + PlacementKit.Core. It compiles Stable-ID Feature→PlacementType bindings and converts Feature candidate pose + Rectangle/Circle footprint into PlacementRequest, keeping both Core assemblies independent. Validation is NOT RUN yet.
- Added five Batch 5 Feature.PlacementAdapter tests for binding compile, rectangle/circle geometry conversion, unbound no-op, tower slope rejection and shipwreck water-depth acceptance. Tests are NOT RUN yet.
- P7 Batch 5 Feature.PlacementAdapter tests passed **5/5** through the active UnitySkills 8092 instance; compile remained 0 errors / 0 warnings.
- P7 Batch 6 starts Compound Feature support in Feature Core without adding Placement/Unity/game-domain dependencies: Stable CompoundTemplate/Slot/ElementType IDs, immutable semantic templates, compiled Feature→Template binding restricted to `WorldFeatureKind.Compound`, and deterministic parent-pose transformation into semantic member instances. This is the village internal-layout contract; visual prefabs/building gameplay remain adapter/domain-owned. Validation is NOT RUN yet.
- Added five Batch 6 Compound tests for duplicate slots, invalid binding to non-Compound definitions, deterministic village layout transforms, destination preflight atomicity and unbound Compound no-op. Tests are NOT RUN yet.
- P7 Batch 6 Compound tests passed **5/5** through UnitySkills 8092; compile 0/0.
- P7 Batch 7 starts optional engine-free Feature.AuthoringAdapter. It converts absolute Feature bounds into P5 WorldSampleRect using WorldGenerationRunKey origin + WorldPlanarSampleLayout.SampleStep, delegates Flatten/Carve/Fill to existing P5 Height Authoring operations, requires an explicit Stamp applicator rather than faking Stamp behavior, and returns WorldAuthoringDirtyPropagation.FromHeightEdit results for local Water/Slope/Biome/Surface/Buildable recompute. Validation is NOT RUN yet.
- Added five Batch 7 Authoring-adapter tests for absolute-bounds rice-paddy Flatten + dirty propagation, Carve/Fill composition, out-of-tile no-op, non-positive Carve preflight and explicit Stamp applicator delegation. Tests are NOT RUN yet.
- P7 Batch 7 first compile failed with exactly **1 CS8156**: `request.Bounds` was a property expression passed directly via `in`. The adapter now copies it to a local `WorldFeatureBounds` before the readonly-ref call. Recompile pending.
- P7 Batch 7 second compile then failed only in the new tests with **10 CS0246** errors because `WorldGenerationRunKey`'s Core namespace import was missing. Added `using StellarFramework.WorldGenKit;`; Runtime Feature.AuthoringAdapter was not the failing assembly. Recompile pending.
- P7 Batch 7 then compiled cleanly and Feature.AuthoringAdapter tests passed **5/5** through UnitySkills 8092.
- Added five architecture policy gates for P7 boundaries: Feature Core only depends on WorldGen Core; PlacementKit.Core has zero references; Feature.ResourcesAdapter is only Feature+Resources; Feature.PlacementAdapter is only Feature+PlacementCore; Feature.AuthoringAdapter is limited to Feature+Authoring+Builtins+WorldGen Core. All adapters/Core remain no-engine. Validation is NOT RUN yet.
- PlacementKit.Core first Unity compile passed 0/0. Added eight tests covering IDs/default structs, geometry, site-fact validation, built-in rule pass/fail behavior, collect-all vs first-failure semantics, failure-buffer preflight before rule execution, and a custom `magic_density` placement context/rule that works without modifying PlacementKit Core. Tests are NOT RUN yet.
- P7 boundary policy rerun through active UnitySkills 8092 passed **14/14**. New gates lock Feature Core to WorldGen Core only, PlacementKit.Core to zero dependencies, and each Feature adapter to its declared sides while keeping all P7 assemblies no-engine.
- P7 current behavior was rerun as one checkpoint instead of relying on isolated earlier runs: Feature Contract 8/8, Resolver 7/7, Feature.ResourcesAdapter 5/5, PlacementKit.Core 8/8, Feature.PlacementAdapter 5/5, Compound Feature 5/5 and Feature.AuthoringAdapter 5/5 => **43/43 PASS, 0 failed, 0 skipped**.
- Frozen P5/P6 targeted regressions after the P7 adapter work also passed: Authoring 12/12 + Resources 25/25 = **37/37 PASS**. Current P7 checkpoint targeted non-benchmark evidence is **94/94 PASS** (43 P7 + 14 boundary + 37 frozen-layer regression).
- P7 is **not frozen yet**. Remaining seal work includes explicit unique-per-world persistence/tracking integration boundary on the WorldKit/SaveKit side, formal distribution profiles/guides, performance/benchmark evidence, Metadata + Standalone Source Export reruns, final diagnose and diff checks.
- P7 Batch 8 now adds the explicit unique/per-region tracking persistence boundary without changing Feature Core: engine-free Feature.WorldKitAdapter stores catalog-indexed world/region usage in a typed WorldKit World-scope layer and captures Stable-ID snapshots; commits use caller scratch and preflight overflow/index validity before mutation. Engine-free Feature.SaveKitAdapter registers a worldgen.feature.usage SaveSection over that snapshot with UseDefault, validation and restore. Validation is NOT RUN yet.
- Batch 8 runtime adapters compiled cleanly 0/0. Added five tests for typed WorldKit layer registration, Stable-ID snapshot remapping when catalog order changes, invalid snapshot failure atomicity, empty-region count access, and a real InMemory SaveKit Save→Clear→Load round trip where restored world usage makes a second unique tower candidate fail quota. Tests are NOT RUN yet.
- Batch 8 persistence tests passed **5/5**, including actual InMemory SaveKit Save→Clear→Load and post-load unique tower quota rejection. Added explicit architecture boundary tests for Feature.WorldKitAdapter (Feature + WorldKit only) and Feature.SaveKitAdapter (WorldKitAdapter + SaveKit only); boundary rerun pending.
- P7 boundary rerun after the persistence adapters passed **16/16**.
- Added seven formal P7 Catalog profiles: placementkit.core, worldgenkit.feature, and five Feature adapters for Resources/Placement/Authoring/WorldKit/SaveKit. Catalog parses at **77 total profiles** with 20 Foundation / 8 Extension / 17 Adapter and no missing requiredProfileIds. Added Feature and Placement guides and updated README, architecture guide and validation matrix. Metadata/Standalone validation is pending.
- First P7 distribution policy run: Standalone Source Export **30/30 PASS**, Boundary **16/16 PASS**, Metadata **4/5 FAIL**. The only failure was the ArchitectureGuide documentation policy expecting the historical `Tiny Foundation Integration` phrase; roadmap cleanup had removed that phrase. Restored it as the completed P0 baseline audit term; no runtime/Catalog dependency semantics changed. Metadata rerun pending.
- Metadata rerun passed **5/5**. P7 now has a benchmark pending execution: 4,096 non-overlapping Feature candidates through the current deterministic Resolver plus 100,000 PlacementKit evaluations using six built-in rules, with one warmup + five measured iterations and min/median timing.

### 2026-09-16 — Conversation handoff snapshot before opening a new chat

- The current conversation is ending because of context length. **Use this section as the immediate continuation point in the next chat; do not restart P7 from scratch.**
- Current milestone remains **P7 — Feature / POI + PlacementKit**, and P7 is **NOT FROZEN yet**.
- P6 is already **FROZEN / PASS**. Its final seal evidence remains: P3 Core 18/18 + P4 Builtins 12/12 + P5 Authoring 12/12 + P6 Resources 25/25 + Boundary 9/9 + Metadata 5/5 + Standalone 30/30 = **111/111 relevant non-benchmark PASS**; final Resources benchmark uses one warmup + five measured iterations with generate min/median 10.438/10.714 ms and resolve min/median 9.402/9.605 ms; final compile 0/0, diagnose healthy 0/0 and `git diff --check` PASS.
- P7 implementation already present and validated up through persistence/distribution work:
  - `WorldGenKit.Feature` Core: Stable-ID FeatureDefinition, Landmark/Area/Compound, footprint/reservation, quota, deterministic resolver, terrain adaptation request.
  - `WorldGenKit.Feature.ResourcesAdapter`: Feature reservation -> Resource occupancy integration with atomic preflight; 5/5 tests.
  - `PlacementKit.Core`: zero-dependency/no-engine placement footprint/request/rule/evaluation contracts plus slope/water/zone/conflict/connection/base-suitability rules; 8/8 tests.
  - `WorldGenKit.Feature.PlacementAdapter`: Feature candidate -> PlacementRequest bridge; 5/5 tests.
  - Compound Feature semantic template/layout support for village-style internal composition; 5/5 tests.
  - `WorldGenKit.Feature.AuthoringAdapter`: Flatten/Carve/Fill/Stamp request bridge into P5 Authoring with dirty propagation; 5/5 tests.
  - `WorldGenKit.Feature.WorldKitAdapter` + `WorldGenKit.Feature.SaveKitAdapter`: typed unique/per-region usage tracking and SaveKit round-trip persistence; 5/5 tests including restored unique-tower quota rejection.
- Current P7 checkpoint evidence already executed:
  - P7 behavior suites: **43/43 PASS** before persistence batch;
  - frozen P5/P6 targeted regression: **37/37 PASS**;
  - P7 boundary after persistence adapters: **16/16 PASS**;
  - Standalone Source Export: **30/30 PASS**;
  - Metadata first run 4/5 due only to removed historical documentation phrase; restored `Tiny Foundation Integration`; Metadata rerun **5/5 PASS**;
  - seven formal P7 distribution profiles are registered; Catalog currently parses as **77 total profiles** = 20 Foundation / 8 Extension / 17 Adapter, with no missing requiredProfileIds.
- **Immediate next task in the new conversation:** execute the already-added P7 benchmark: 4,096 non-overlapping Feature candidates through deterministic Feature Resolver + 100,000 PlacementKit evaluations with six built-in rules, one warmup + five measured iterations, report min/median. Do not mark PASS until Unity Test Runner actually runs it.
- After the benchmark, perform the P7 final seal rerun: current P7 behavior (including Batch 8 persistence), frozen P5/P6 regressions as appropriate, Boundary 16/16, Metadata 5/5, Standalone 30/30, final Unity compile, console clear + `unity_diagnose`, and `git diff --check`. Only then decide whether P7 can be marked **FROZEN / PASS**.
- Tooling note: during the latest P7 work, the live UnitySkills server moved from the old 8090 endpoint to **`http://127.0.0.1:8092/`**. `project_get_info` confirmed project `StellarFramework`, Unity `2022.3.62f3c1`, path `C:/CodingToolsWorkerCenter/StellarFramework/Assets`. In a new chat, discover/check the active UnitySkills instance instead of blindly assuming 8090 or 8092.
- Do not commit/push unless explicitly requested. Preserve unrelated dirty files. Keep updating this memory file and `Assets/StellarFramework/FrameworkDoc/06-WorldFramework/WorldFramework-Development-Status.md` whenever project state changes.

### 2026-09-16 — P7 seal continuation / latest handoff snapshot

- Continued directly from the prior P7 handoff; implementation was not restarted.
- The already-added P7 Feature/Placement benchmark was actually run through Unity Test Runner and passed **1/1** on Unity 2022.3.62f3c1. Methodology remains one warmup + five measured iterations. Results: 4,096 non-overlapping Feature candidates -> Resolver min/median **54.905 / 59.205 ms**; 100,000 PlacementEvaluator calls with six built-in rules -> min/median **61.373 / 62.651 ms**; checksum=375,021,110; coarse `GC.GetTotalMemory(false)` heap delta=36,864 bytes. Treat this as Editor trend evidence only, not device performance or strict zero-allocation proof.
- UnitySkills 2.8.3 nuance discovered during the seal: passing a fully-qualified **class** name to `test_run_by_name` expanded one attempted run to 1,397 EditMode tests. That accidental broad run was not used as seal evidence. Exact simple class names are reliable for class suites; the fully-qualified method name worked correctly for the single benchmark method. A Domain Reload during recovery moved the StellarFramework UnitySkills endpoint from 8092 back to **8090** through the existing `Assets/__StellarTempRecovery/Editor/UnitySkills8090Recovery.cs` recovery hook. Always identify the project at the endpoint before calling it because 8091 is PICOHands.
- Correct final seal rerun evidence actually executed after the benchmark: current P7 behavior including Batch 8 = **48/48 PASS**; frozen P5/P6 targeted regression = **37/37 PASS**; World Framework Boundary = **16/16 PASS**; Kit Architecture Metadata = **5/5 PASS**; Standalone Source Export = **30/30 PASS**. Final relevant non-benchmark total = **136/136 PASS, 0 failed, 0 skipped**.
- Final Unity health: `debug_check_compilation` reported not compiling/updating; Console was explicitly cleared; `unity_diagnose` returned **healthy=true**, **0 console errors**, **0 console warnings**, server healthy on StellarFramework.
- Final repository-wide `git diff --check` is **BLOCKED / FAIL for one unrelated protected baseline line**, not P7 code: `ProjectSettings/EditorSettings.asset:39` contains trailing whitespace on `m_CacheServerEndpoint:`. `ProjectSettings/EditorSettings.asset` was already dirty before this continuation and the working protocol forbids silently fixing unrelated user baseline changes, so it was intentionally left untouched. P7 tracked Catalog/README diff check passed with only line-ending notices.
- **P7 remains NOT FROZEN solely because the required repository-wide diff gate is not legitimately green.** Do not redo the benchmark or 136-test seal from scratch in the next chat. The immediate task is to resolve/accept the pre-existing `EditorSettings.asset` whitespace blocker without violating baseline protection, then rerun the final lightweight health/diff gate and mark P7 FROZEN / PASS if green.
- No commit/push was performed.

### 2026-09-17 — P7 frozen / P8 opened

- User explicitly authorized continuing the P7 seal. Only the trailing whitespace on `ProjectSettings/EditorSettings.asset:39` (`m_CacheServerEndpoint:`) was removed; no other existing EditorSettings values or unrelated dirty files were changed.
- Repository-wide `git diff --check` then passed with line-ending notices only. StellarFramework UnitySkills identity was rechecked at port 8090; `debug_check_compilation` reported not compiling/updating, Console was cleared, and `unity_diagnose` returned **healthy=true, 0 console errors, 0 console warnings**.
- **P7 — Feature / POI + PlacementKit is FROZEN / PASS.** Final evidence: P7 behavior 48/48 + P5/P6 targeted regression 37/37 + Boundary 16/16 + Metadata 5/5 + Standalone 30/30 = **136/136 relevant non-benchmark PASS**, P7 benchmark **1/1 PASS**, final repository diff/Unity health gates PASS.
- Current active milestone is now **P8 — Unity Presentation Adapters**. Frozen plan requires at least: 2D Tilemap Adapter, Debug Texture Adapter, and a first 3D adapter (Mesh or Unity Terrain); the second 3D adapter follows after the first is stable. Acceptance requires the same logical WorldData to output both 2D and 3D while Core remains free of Tilemap/Terrain/Mesh references.
- Immediate P8 task: audit existing Unity-facing Grid/WorldGen adapters and current channel/layout contracts, then define the smallest shared presentation boundary before implementation. Preserve all frozen P0-P7 Core semantics.
- No commit/push was performed.

### 2026-09-17 — P8 Unity Presentation Adapters frozen / P9 opened

- P8 audit confirmed there was no existing WorldGen DebugTexture/Mesh/Tilemap/Terrain presentation adapter and no implemented GridKit UnityProjection adapter to reuse. Chosen boundary: no new Presentation Core; every Unity adapter directly consumes `WorldGenerationDataSet + WorldPlanarSampleLayout + typed ChannelHandle`, so WorldGen Core/Builtins stay engine-free.
- Added four independent Unity adapters under `Runtime/Kits/WorldGenKit/Adapters`: DebugTexture, Mesh, Tilemap and UnityTerrain. Each asmdef references only WorldGenKit.Core + Builtins; none pulls WorldKit/GridKit/SpatialKit/PathKit/SaveKit/PlacementKit/Resources/Feature/Authoring. DebugTexture/Tilemap/Mesh/Terrain targets remain caller-owned; mutable output is preflighted before writes where invalid input can be detected.
- P8 Presentation tests reached **9/9 PASS**. Important regression details: same Dense Height Dataset drives both 2D DebugTexture and 3D Mesh; invalid palette/height/buffer paths leave prior output intact. Terrain's first suite was 8/9 only because the test compared Unity's quantized stored height to literal 0.25; the test was corrected to compare against the TerrainData's actual stored baseline, then passed 9/9. Runtime adapter behavior was not changed for that failure.
- Boundary expanded from P7's 16 to **20/20 PASS**, one gate per P8 adapter. A transient Unity Test Runner `starting` timeout with 0 tests executed occurred once; it was treated as a tooling failure and a clean rerun was used as evidence.
- P8 benchmark **1/1 PASS**, Unity 2022.3.62f3c1, one warmup + five measurements: 16,384-sample DebugTexture **0.826/0.855 ms min/median**, Mesh **0.849/0.858 ms**, Tilemap **1.927/1.999 ms**; 16,641-sample UnityTerrain **1.066/1.275 ms**; checksum=98,130; coarse heap delta=0. Editor trend only.
- Added four Catalog profiles (`worldgenkit.debugtexture`, `worldgenkit.mesh`, `worldgenkit.tilemap`, `worldgenkit.unityterrain`) and four per-adapter guides. Catalog now parses as **81 total profiles**, 20 Foundation / 8 Extension / 21 Adapter / 32 non-tier, with **0 missing requiredProfileIds**. Root README, architecture guide and validation matrix were updated.
- Final P8 seal evidence before the final lightweight health recheck: Presentation 9/9 + P3/P4/P5 frozen regression 42/42 + Boundary 20/20 + Metadata 5/5 + Standalone 30/30 = **106/106 relevant non-benchmark PASS**, benchmark 1/1. **P8 is FROZEN / PASS.**
- Post-document final gate also passed: Metadata **5/5**, Standalone **30/30**, Unity compile idle, Console clear + `unity_diagnose` **healthy=true / 0 errors / 0 warnings**, and repository-wide `git diff --check` PASS (line-ending notices only). P8 is fully sealed; do not reopen it for P9 implementation.
- Current active milestone is **P9 — Infinite World / Streaming**. Frozen P9 scope: generation/macro regions, demand generation, streaming policy, separate metadata/data/simulation/presentation states, logical position + Unity floating-origin adapter, and SaveKit delta integration. Acceptance includes positive/negative exploration, generation-order independence, rebuild of unmodified chunks, delta restore of modified chunks and stable long-distance Unity presentation.
- Do not restart P8 in the next chat. Immediate P9 task is to audit frozen WorldKit chunk/data-layer APIs plus WorldGen run-key/chunked storage before designing the smallest streaming contracts. Preserve P0-P8 frozen semantics.
- No commit/push was performed.

### 2026-09-17 — P9 Infinite World / Streaming active implementation snapshot

- P9 did **not** modify frozen P2 `WorldChunkState`. New engine-free `StellarFramework.WorldKit.Streaming` owns `None / Metadata / Data / Simulation / Presentation` residency and deterministic demand/reconciliation policy. `WorldRegionLayout` uses floor division for signed Chunk -> Region mapping; demand is row-major and explicit about overflow/caller buffer capacity.
- Current fresh P9 Core validation: `WorldKitStreamingCoreTests` **11/11 PASS**.
- Added `StellarFramework.WorldGenKit.StreamingAdapter`, keeping WorldGen Core independent from WorldKit. It maps signed Chunk coordinates and Region/macro-region coordinates to absolute planar WorldGenerationRunKey/sample origins; adjacent chunks match monolithic generation and A->B vs B->A exploration produces identical data. Fresh `WorldGenStreamingAdapterTests`: **5/5 PASS**.
- Added `StellarFramework.WorldKit.Streaming.SaveKitAdapter`. Delta persistence uses explicit Stable-ID codecs (`IWorldDeltaCodec`) rather than reflection/polymorphic runtime serialization. Snapshot validation builds a fresh `WorldDeltaSet` and restore swaps atomically only after full decode succeeds. Fresh `WorldStreamingSaveKitAdapterTests`: **4/4 PASS**, including a real InMemory SaveKit round trip.
- Added `StellarFramework.WorldKit.Streaming.UnityAdapter` with `WorldFloatingOriginAdapter`: high-precision logical `WorldPoint2D` stays authoritative; Unity gets small relative `Vector3` coordinates and a snapped scene-shift delta when recentering is required. The adapter contains no Transform ownership. Fresh `WorldFloatingOriginAdapterTests`: **5/5 PASS**, including trillion-scale logical coordinates and repeated long-distance recentering.
- Added P9 E2E acceptance tests. Fresh `WorldStreamingEndToEndTests`: **2/2 PASS** proving (1) unmodified chunks can be discarded then regenerated identically and (2) modified chunks rebuild deterministic base data then recover the persisted modification by replaying the restored Delta.
- World Framework Boundary is now **24/24 PASS**. One intermediate 23/24 failure was a test-policy bug: the engine-free scan for Streaming Core recursively included `/Adapters/Unity/`. The boundary was corrected so Core remains engine-free while the Unity adapter is explicitly allowed UnityEngine and cannot pollute Core.
- Added four P9 distribution profiles/guides: `worldkit.streaming`, `worldgenkit.streaming`, `worldkit.streaming.savekit`, `worldkit.streaming.unity`. Catalog currently parses as **85 profiles = 20 Foundation / 9 Extension / 24 Adapter / 32 non-tier**, with **0 missing requiredProfileIds**. Fresh Metadata **5/5 PASS** and Standalone Source Export **30/30 PASS**.
- P9 streaming churn benchmark actually ran **1/1 PASS** on Unity 2022.3.62f3c1: one warmup + five measurements, metadataRadius=24, target resident=2,401, 200 movement steps, min/median **41.033 / 41.088 ms**, transitionChecksum=138,507,200, finalResident=2,401, coarse heap delta=24,576 bytes. Editor trend only, not device guarantee or strict allocation proof.
- Tooling issue found and fixed without touching PICOHands files: after Domain Reload the two Unity projects swapped ports (`8090=PICOHands`, `8091=StellarFramework`). The old temporary recovery hook hard-started StellarFramework on 8090 and produced a `Port 8090 is in use` Console error. `Assets/__StellarTempRecovery/Editor/UnitySkills8090Recovery.cs` now first respects an already-running server and otherwise starts 8090 with auto fallback, allowing both projects to coexist. Always verify `projectName`/instance before UnitySkills calls.
- Anti-hallucination recheck after user concern: 8091 explicitly reports `projectName=StellarFramework`, `instanceId=StellarFramework_DEEE9F8A`, compile/update/domain reload idle; Console query with the correct `type/filter/limit` contract returns **0 errors / 0 warnings**; repository-wide `git diff --check` exits 0 (line-ending notices only). P9 key suites were rerun rather than trusted from memory and total **86/86 PASS**: 11 Streaming Core + 5 WorldGen Streaming + 4 SaveKit Delta + 5 Floating Origin + 2 E2E + 24 Boundary + 5 Metadata + 30 Standalone.
- P9 is **ACTIVE, not frozen yet**. Remaining seal work: explicit small-suite P2-P8 frozen World Framework regression, then final Metadata/Standalone recheck after docs, Unity health, and repository-wide diff gate. Do not use the previous long batch PowerShell wrapper because it can wait without streaming Test Runner results; run suites in smaller groups and record exact counts.
- No commit/push was performed. Preserve the unrelated dirty baseline exactly.

### 2026-09-17 — P9 frozen / P10 opened

- User explicitly asked for an anti-hallucination verification before continuing. P9 evidence was therefore rechecked from live Unity rather than trusted from prior notes.
- Fresh P9 key suites: Streaming Core **11/11**, WorldGen Streaming **5/5**, SaveKit Delta **4/4**, Floating Origin **5/5**, E2E **2/2**, Boundary **24/24**, Metadata **5/5**, Standalone **30/30** = **86/86 PASS**.
- Frozen P2-P8 regression was then rerun in explicit small suites: P2 WorldKit 15 + P3 WorldGen Core 18 + P4 Builtins 12 + P5 Authoring 12 + P6 Resources 25 + P7 Feature/Placement 48 + P8 Presentation 9 = **139/139 PASS, 0 failed, 0 skipped**.
- Final post-document seal rerun kept Metadata **5/5** and Standalone **30/30** green. Live endpoint identity: `http://127.0.0.1:8091`, `projectName=StellarFramework`, `instanceId=StellarFramework_DEEE9F8A`, Unity 2022.3.62f3c1. Compilation/update idle; `unity_diagnose` healthy=true with 0 errors / 0 warnings; direct Console Error/Warning queries both 0; repository-wide `git diff --check` exit 0 with only line-ending notices.
- One Unity Test Runner progress response briefly exposed a 1,443-test discovery total while a six-test job was still running; the same concrete job later completed **6/6**. That transient progress count was not used as verification evidence. The reliable per-class outputs and completed job results are the recorded evidence.
- Intentional P9 streaming benchmark remains the seal benchmark: **1/1 PASS**, metadataRadius=24, 2,401 target residents, 200 movement steps, min/median **41.033 / 41.088 ms**, checksum 138,507,200, final resident 2,401, coarse heap delta 24,576 bytes. A later incidental broad Test Runner pass also executed the same benchmark and logged 40.487/40.653 ms with exact hot-path allocated bytes 0 / coarse heap delta 4,096; this incidental rerun is trend-only and does not replace the intentional seal measurement.
- **P9 — Infinite World / Streaming is now FROZEN / PASS.** Do not reopen its semantics for P10 unless an actual regression is demonstrated.
- Active milestone is **P10 — ToolsHub Production Authoring**. Frozen plan: WorldKit diagnostics; WorldGen Profile/Pipeline/Channel/Rule/Biome/Resource/Feature editors; candidate heatmap; accepted/rejected diagnostics; memory report; validator. Acceptance: common maps configurable without Core changes, advanced extensions still code-registerable, runtime assemblies must not depend on Editor.
- Immediate P10 task: audit existing StellarToolsHub architecture/module patterns and the current WorldKit/WorldGen authoring/diagnostic surfaces, then design the smallest Editor-only integration boundary. Preserve all P0-P9 frozen runtime semantics.
- No commit/push was performed.

### 2026-09-18 — P10 Batch 1 ToolsHub foundation

- Audited \`StellarToolsHub\` and confirmed the established pattern is a zero-business-reference Hub core plus per-Kit Editor asmdefs (for example FlowKit/SaveKit). P10 follows the same boundary instead of adding World Framework references to \`StellarFramework.ToolsHub.Editor\`.
- Added Editor-only \`StellarFramework.ToolsHub.WorldFramework.Editor\` with an explicit \`IWorldFrameworkDiagnosticsSource\` registry, immutable diagnostic snapshots, a WorldGen Plan/Channel/Stage/Report inspector model, and a ToolsHub module. Runtime Core receives no ToolsHub singleton and no Editor dependency.
- Added \`StellarFramework.ToolsHub.WorldFramework.Editor.Tests\`; fresh \`WorldFrameworkToolsHubTests\` are **4/4 PASS**.
- Added a Framework boundary assertion that the WorldFramework ToolsHub assembly is Editor-only and that WorldKit / WorldKitStreaming / WorldGenKit / PlacementKit Runtime sources do not reference \`UnityEditor\` or \`StellarFramework.ToolsHub\`.
- Validation nuance: source contained 25 \`[Test]\` methods and the compiled \`StellarFramework.FrameworkValidation.Tests.dll\` contained the new method, but Unity Test Runner cached an old 24-test discovery. Explicit \`test_discover_start\` refreshed discovery to **25** and found \`WorldFrameworkToolsHubRemainsEditorOnlyAndRuntimeDoesNotDependOnIt\`; the subsequent Boundary run passed **25/25**. The stale 24/24 runs are not final P10 evidence.
- Unity compilation after Batch 1: **0 errors**. P10 remains ACTIVE; next work is Profile/Pipeline/Channel authoring followed by Biome/Resource/Feature authoring, heatmap/accepted-rejected diagnostics, memory report and validators.
- No commit/push was performed. Preserve unrelated dirty baseline changes.

### 2026-09-18 — P10 Batch 2 Profile / Pipeline / Channel authoring

- Added Editor-only \`WorldGenerationAuthoringProfile\` as the persistent authoring artifact for common terrain configuration. It owns profile ID/version, planar layout, typed Channel entries and Height/Moisture/WaterDepth/Slope settings; this does not make ScriptableObject a WorldGen Core requirement.
- Added explicit \`WorldGenerationAuthoringCompiler\`: typed float/int/byte handles are registered without reflection, Builtins stages are instantiated directly, and the frozen \`WorldGenerationPipelineBuilder\` remains the authority for dependency/producer/cycle validation.
- ToolsHub World Framework module now creates/edits Authoring Profile assets and performs real Validate/Compile, showing the compiled plan hash/channel/stage model. Runtime diagnostics source registration remains independent.
- Editor asmdef now additionally references \`StellarFramework.WorldGenKit.Builtins\`; no Runtime assembly gained an Editor/ToolsHub dependency.
- Unity compile: **0 errors**. Explicit fresh Test Discovery found 8 \`WorldFrameworkToolsHubTests\`; **8/8 PASS** including deterministic default profile compilation, duplicate ID, wrong typed Channel and missing-producer cases.
- P10 remains ACTIVE. Next: Biome/Surface authoring, then Resource/Feature/Placement authoring and diagnostic visualization.

### 2026-09-18 — P10 Batch 3 Biome / Surface / Buildable authoring

- Extended the Editor-only WorldGenerationAuthoringProfile with Surface catalog IDs, Biome entries and range criteria, fallback Biome, Surface output and Buildable thresholds/blocked-Biome policy.
- Compiler delegates to the existing Builtins catalogs/stages/settings rather than reproducing runtime selection logic. Default authoring Profile now compiles to **7 Channels / 7 Stages** with stable plan identity.
- Initial compile found five Editor-only CS0117 errors because the authoring compiler assumed \`WorldBiomeId/WorldSurfaceId.TryCreate\`; frozen Runtime only exposes \`From\`. The Editor compiler was corrected to wrap \`From\` and Runtime remained untouched.
- Fresh Unity compile **0 errors**; explicit fresh discovery found 11 ToolsHub tests; \`WorldFrameworkToolsHubTests\` **11/11 PASS** including fallback-Biome, Surface mapping and blocked-Biome validation.
- P10 remains ACTIVE; next batch is Resource/Feature/Placement authoring plus preview/diagnostics.

### 2026-09-18 — P10 Batch 4 Resource / Feature / Placement authoring

- Added occupancy/resource, feature and Placement probe authoring fields to the Editor-only WorldGenerationAuthoringProfile.
- Added \`WorldSemanticAuthoringCompiler\`: occupancy/resource/feature authoring compiles into the existing frozen runtime registries/catalogs; Placement probe evaluation delegates to existing PlacementKit built-in rules and evaluator.
- Added \`WorldSemanticPreviewModel\`: Resource accepted/rejected diagnostics come from \`WorldResourceScatterResolver\`; Feature accepted/quota/reservation diagnostics come from \`WorldFeatureResolver\`. Editor does not reimplement resolution semantics.
- ToolsHub displays compiled occupancy/resource/feature counts and can run Placement probes with exact rule/failure IDs.
- Fresh Unity compilation: **0 errors**. Explicit discovery found 17 ToolsHub tests and \`WorldFrameworkToolsHubTests\` passed **17/17**. World Framework boundary remained **25/25 PASS**.
- P10 remains ACTIVE. Next: heatmap/preview surface, memory report and consolidated validator, followed by formal docs/distribution and final regression/health gates.

### 2026-09-18 — P10 Batch 5 + final freeze

- Added deterministic Resource Candidate Heatmap backed by the real WorldResourceCandidateGenerator + WorldResourceScatterResolver path, with generated/accepted and occupancy/budget/spacing rejection counts plus an explicit Editor preview candidate safety limit.
- Added consolidated WorldAuthoringDiagnosticsModel validation, unused-Channel warnings, Placement probe configuration validation and a Memory Report. Dense/Constant storage has an explicit fixed-byte lower bound; Sparse/Chunked/Computed/External storage is deliberately reported as variable instead of guessed.
- Added optional Editor-only IWorldFrameworkDetailDiagnosticsSource with immutable Chunk / DataLayer / Delta DTO snapshots so projects can expose detailed diagnostics without changing WorldKit Core or introducing a Runtime singleton.
- ToolsHub Profile now covers common Profile/Pipeline/Channel/Rule/Biome/Surface/Buildable/Resource/Feature/Placement authoring. Advanced projects remain code-extensible through existing Stage/Rule/Adapter APIs and project Editor bridges.
- Fresh discovery + test evidence after the full P10 implementation: WorldFrameworkToolsHubTests **24/24 PASS**, WorldFrameworkFoundationBoundaryTests **25/25 PASS**, KitArchitectureMetadataPolicyTests **6/6 PASS**, StandaloneSourceExportPolicyTests **30/30 PASS**.
- P2-P9 frozen runtime was rerun in explicit small suites after P10: P2-P6 **82/82 PASS** and P7/P8/P9 **84/84 PASS**, therefore frozen Runtime regression **166/166 PASS, 0 failed, 0 skipped**.
- P10 relevant non-benchmark seal total is **251/251 PASS** = 166 frozen Runtime + 24 ToolsHub + 25 Boundary + 6 Metadata + 30 Standalone. The frozen plan does not require a P10 performance benchmark; performance/release benchmarks are P12 scope.
- Distribution: added worldframework.tools tooling profile and production guide. Catalog parses as **86 profiles**, requiredProfileIds missing **0**; its dependency closure matches the Editor asmdef and does not force WorldKit.Streaming, SaveKit or Presentation adapters.
- Final pre-document health: StellarFramework_DEEE9F8A, compile/update idle, unity_diagnose healthy=true, Console 0 errors / 0 warnings, repository-wide git diff --check exit 0. Existing unrelated dirty baseline remains protected; no reset/clean/commit/push was performed.
- **P10 ToolsHub Production Authoring = FROZEN / PASS.**
- Post-document lightweight seal rerun also passed: KitArchitectureMetadataPolicyTests **6/6**, StandaloneSourceExportPolicyTests **30/30**, diagnose healthy=true, Console 0 errors / 0 warnings, compile idle.
- Next frozen-plan milestone: **P11 Integration Samples**. Required stress samples are Farm2D, HexStrategy, Survival3D, InfiniteFactory, StellarGridMap Migration Sample and TerrainGridNavigation. P11 starts with an audit of existing Samples/Integration assets and a shared verification harness; do not create a runtime mega-dependency just to make samples convenient.

### 2026-09-18 — P11 Batch 1 Farm2D

- Audited Assets/StellarFramework/Samples/Integration: before P11 it contained only FlowKitMsvIntegration; none of the six required World Framework integration stress samples existed.
- Added independent StellarFramework.Samples.WorldFramework.Farm2D rather than a shared mega-sample assembly. Direct dependencies are GridKit.Core + WorldGenKit.Core/Builtins/Authoring + TilemapAdapter only.
- Farm2D builds a deterministic **96×64 = 6,144-cell** generated base at logical origin **(-48,-32)** using real Height/Moisture/WaterDepth/Slope/Biome/Surface stages.
- A **32×20 = 640-cell** farm plot is manually authored through WorldDenseOverrideLayer<int> + WorldSemanticAuthoringPaint.PaintSurface, then composed to Final without mutating Generated Base.
- GridKit owns the fixed square logical bounds and a 2×2 barn occupancy footprint. A second overlapping occupant is rejected atomically.
- Final Dense semantic Surface is consumed by the real TilemapAdapter. Fresh Farm2DIntegrationSampleTests discovery found 4 tests and **4/4 PASS**, including actual Unity Grid + Tilemap projection.
- Added Editor SceneBuilder and actually invoked StellarFramework/Samples/World Framework/Build Farm2D Scene; generated Farm2D_Playable.unity.
- Actual PlayMode smoke passed: Farm2D_Tilemap existed at runtime, live Tilemap cellBounds were (-48,-32,0) with size (96,64,1), Rectangle layout, Console error count 0; exited PlayMode normally.
- Added samples.worldframework.farm2d sample distribution profile. Catalog after the profile = **87 profiles**, missing requiredProfileIds = **0**.
- Final Farm2D gate actually executed: Farm2DIntegrationSampleTests **4/4**, KitArchitectureMetadataPolicyTests **6/6**, StandaloneSourceExportPolicyTests **30/30**, Unity diagnose healthy=true with 0 errors / 0 warnings and compile idle, repository-wide git diff --check exit 0.
- **Farm2D P11 sample = COMPLETE / PASS; P11 progress 1/6.** Next: HexStrategy. No commit/push.

### 2026-09-18 — P13 canonical checkpoint / F1 frozen

- This tail section is the canonical current checkpoint. Earlier P13 notes exist near the top of this file due historical insertion; future updates append at EOF only.
- `FrameworkDoc` is now the formal documentation center. Initial files: `FrameworkDoc/README.md`, `00-Overview/P13-Completion-Plan.md`, `09-Development/Documentation-Migration-Map.md`, `02-Kits/LocalizationKit/LocalizationKit-Guide.md`.
- LocalizationKit formal audit: **PASS**. Core stays `references=[]`, `noEngineReferences=true`, with no Runtime reflection/assembly scan; Settings and UnityUGUI remain one-way adapters; Editor validator is tooling-only; Sample remains separately distributable.
- LocalizationKit validates cross-locale placeholder contracts with Core parser semantics. Fresh tests: Core **15/15**, Adapter/Validator **15/15**, Sample **5/5**.
- Language selector rule is frozen: labels always display `中文` and `English`; selector labels do not localize themselves. Clicking either switches all other localizable UI to zh-CN / en-US.
- P13 F1 productization is **COMPLETE / PASS** for ActionKit / BindableKit / EventKit / SingletonKit / ConfigKit / LogKit.
- Current gates: F1 Scene **9/9 PASS**, SampleManifest **4/4 PASS**, README bilingual policy **2/2 PASS**, Metadata incl. F1 distribution closure **11/11 PASS**, Localization Adapter **15/15 PASS**.
- All framework-owned README files in repository root + `Assets/StellarFramework/**` now require complete `## 中文` and `## English` sections through `SampleDocumentationPolicyTests`.
- F1 sample distribution profiles explicitly include `localizationkit.ugui`, `Samples/Common/P13Runtime`, Common Generated visual assets, Source Han Sans closure, and original stable scene paths.
- Catalog remains **98 profiles / 0 missing requiredProfileIds**. Compile/Console latest **0 errors / 0 warnings**; repository `git diff --check` **0**.
- `.gitattributes` only relaxes `blank-at-eol` for `*.unity`; other source/docs remain under strict whitespace checking.
- Next execution batch: P13 F2 = TimeKit / SaveKit / PoolKit / ResKit / AudioKit / FSMKit.
- No commit/push/reset/clean. Preserve unrelated dirty baseline.

### 2026-09-18 — P13 F2 complete / UnitySkills worker-port fix

- P13 F2 productization is **COMPLETE / PASS** for TimeKit / SaveKit / PoolKit / ResKit / AudioKit / FSMKit.
- Added `P13F2ExampleSceneBuilder`; all six original scene paths were rebuilt in place and GUID idempotence is enforced by tests.
- TimeKit: legacy OnGUI remains compatibility-only; P13 scene uses real clock/workshop/periodic 3D evidence and public control methods. PlayMode confirmed two-hour Workshop completes (`working=false`, `completed=true`).
- SaveKit: P13 uses real Main/Legacy Slot indicators plus Level/Money bars; real file Save made MainSlot true and Delete cleanup was executed.
- PoolKit: added dedicated serializable `ExamplePoolKitSample` wrapper to avoid file/type-name MonoScript ambiguity; in-scene `ExamplePoolBullet` template replaces legacy Generated prefab. PlayMode confirmed message reuse indicator and bullet pool actions.
- ResKit: public P13 API/visual state added for Resources/AB/AA/RawText; Resources and RawText were actually loaded in PlayMode with `ResourcesLoaded=true`, `RawTextLoaded=true`, physical StreamingAssets text content returned; AB/AA keep explicit external-build prerequisites and are not faked as success.
- AudioKit: public P13 controls for 2D/3D/follow SFX, BGM, mute and volume; PlayMode confirmed BGM indicator and SoundOn true->false toggle with Console clean.
- FSMKit: real Actor/Target + Idle/Chase indicators; Chase animation maps to Common `Move` state so sample no longer depends on legacy `Example_FSM.controller`; PlayMode confirmed Idle->Chase->Idle.
- F2 Scene Gate **9/9 PASS**; Manifest **4/4**; README bilingual policy **2/2**; Metadata incl. F2 distribution closure **12/12**; Standalone **30/30**; F1 regression **9/9**; Localization Adapter **15/15**.
- F2 distribution profiles now include `localizationkit.ugui`, P13Runtime, Common generated visuals, Source Han Sans; Pool/FSM no longer export `KitSamples/Generated`; Res explicitly exports its StreamingAssets RawText; Audio keeps Resources/Audio.
- UnitySkills recovery bug fixed: `Assets/__StellarTempRecovery/Editor/UnitySkills8090Recovery.cs` now ignores `AssetImportWorker` processes. Previously workers occupied 8090/8091 and main Editor fell back to 8092. After fix only main Editor owns the service; always dynamically probe current healthy port.
- Final F2 seal: Catalog **98 profiles / 0 missing requiredProfileIds**, compile/Console **0 errors / 0 warnings**, `git diff --check` **0**.
- Next execution batch: P13 F3 = GridKit / SpatialKit / SimulationKit / PathKit / PathKit.GridKitAdapter.
- No commit/push/reset/clean.

### 2026-09-18 — P13 F3 complete / 19 of 31 Manifest entrypoints ready

- P13 F3 productization is **COMPLETE / PASS** for GridKit / SpatialKit / SimulationKit / PathKit / PathKit.GridKitAdapter.
- Added `P13F3ExampleSceneBuilder`; all five original scene paths are rebuilt in place and the Scene Gate verifies GUID idempotence across repeated Builder runs.
- GridKit: P13 scene now uses a real 12x8 negative-coordinate 3D grid, selected-cell marker, L-shaped footprint markers, Occupant A indicator and explicit atomic-conflict indicator. Existing Core occupancy semantics were not changed.
- SpatialKit: P13 scene maps the Core index to reusable 3D point views and real Rect/Circle/Nearest indicators. Query highlights come only from IDs returned by SpatialKit.
- SimulationKit: 20 scheduler IDs map to real scene entities. Entries returned by `CollectDue` visibly lift and a dedicated object reflects `HasBacklog`, so frame-budget spreading is no longer GUI-only evidence.
- PathKit: 16 real nodes + `LineRenderer` + Agent visualize the actual A*/Dijkstra result; default A* still proves cost 8 / five nodes and the Agent moves along Start -> Goal.
- PathKit.GridKitAdapter: 96 real grid cells visualize blocked/mud traversal state; the Adapter result drives a real `LineRenderer` and moving Agent.
- Scene deserialization exposed a real tooling boundary: pure C# state such as `GridOccupancy`, `SpatialIndex2D`, and `GridPathGraph` is not serialized. Public P13 operation entrypoints now explicitly initialize missing runtime state before executing; Core algorithms remain unchanged.
- Initial Path PlayMode checks incorrectly assumed a fixed number of very fast Test Runner frames implied enough elapsed `Time.deltaTime`. The smoke was corrected to assert real Agent displacement after elapsed time instead of relying on machine-dependent frame count.
- Existing publication contracts were preserved: P13 Builder roots for Spatial/Simulation retain legacy names required by standalone/policy gates.
- F3 distribution profiles now explicitly close over `localizationkit.ugui`, `Samples/Common/P13Runtime`, Common Generated visual assets, Source Han Sans, and the original stable scenes.
- Final F3 gates actually executed: F3 Scene **8/8 PASS**, F3 PlayMode **5/5 PASS**, F2 regression **9/9 PASS**, F1 regression **9/9 PASS**, SampleManifest **4/4 PASS**, README bilingual **2/2 PASS**, Metadata **13/13 PASS**, Standalone Source Export **30/30 PASS**, SimulationKit legacy policy **2/2 PASS**.
- Final F3 health: Catalog **98 profiles / 0 missing requiredProfileIds**, compile **0 errors / 0 warnings**, Console **0 errors / 0 warnings**.
- Manifest progress after F3: **19 ready / 31 total**, **12 pending** = Flow / HotUpdate / Http / Settings / UIKit, six World Framework integrations, and ArchitectureDemo.
- Next execution batch: P13 F4 = Settings / UIKit / Http / HotUpdate / Flow. After F4, remaining work is six integration entrypoints + ArchitectureDemo, then documentation centralization, legacy Generated/template cleanup, and the final P13 seal.
- No commit/push/reset/clean. Preserve unrelated dirty baseline.

### 2026-09-18 — P13 F4 complete / 24 of 31 Manifest entrypoints ready

- P13 F4 productization is **COMPLETE / PASS** for SettingsKit / UIKit / HttpKit / HotUpdateKit / FlowKit.
- Added `P13F4ExampleSceneBuilder`; all five original scene paths are rebuilt in place and Scene Gate verifies GUID idempotence across repeated Builder runs.
- SettingsKit: UGUI buttons call real SettingsKit APIs for subtitles, HUD scale, theme, language, save and reset; the 3D preview reacts to real runtime state. Legacy OnGUI/overlay remains compatibility-only.
- UIKit: real `Open / Push / Pop / Close` and stress operations continue to use Resources `UIRoot.prefab` + `ExamplePanel.prefab`; P13 exposes runtime snapshot/operation state for tests without replacing the real panel lifecycle.
- HttpKit: network execution is explicit instead of forced on scene entry; Request / Offline / Success remain distinct states. Manual offline fallback is normal Log-level behavior, while genuine network failure still logs Warning and never fakes online success.
- HttpKit scene serialization bug fixed with `ExampleHttpKitSample`: legacy file `Example_Httpkit.cs` does not exactly match type `Example_HttpKit`, so direct Builder attachment produced an embedded MonoScript. The wrapper is scene-facing only and leaves HttpKit/original sample behavior unchanged.
- HotUpdateKit: P13 shows Available / Prerequisite / Failure explicitly. Missing dll/AOT artifacts remain prerequisite state; no fake success. Existing `Example_HybridCLRAAStartup` on `Example_HotUpdateKit_Runner` remains preserved for the Addressables + HybridCLR startup path.
- FlowKit: stable P13 scene now executes the minimal real `FlowKitSample.json` chain `Entry -> Delay(0.25s) -> Complete`, with Running / Completed / Failed scene indicators driven by `FlowRunStatus`. Rich FireDrill/SchoolTraining teaching assets remain in the sample directory but are not mixed into the stable entry smoke.
- F4 distribution profiles explicitly close over `localizationkit.ugui`, P13Runtime, Common Generated visual assets, Source Han Sans, and original stable scenes. Settings retains Audio resources without legacy `KitSamples/Generated`; UIKit retains its two Resources UI prefabs.
- Added complete bilingual README files for Settings / UIKit / Http / HotUpdate and updated Flow README so documentation matches the P13 scene instead of claiming that the fire-drill graph auto-runs.
- Final F4 gates actually executed: F4 Scene **8/8 PASS**, F4 PlayMode **5/5 PASS**, F3 PlayMode regression **5/5 PASS**, F1/F2/F3 Scene regression **9/9 / 9/9 / 8/8 PASS**, SampleManifest **4/4 PASS**, README bilingual **2/2 PASS**, Metadata **13/13 PASS**, Standalone Source Export **30/30 PASS**, HotUpdate legacy policy **3/3 PASS**.
- Final F4 health: Manifest **24 ready / 31 total**, Catalog **98 profiles / 0 missing requiredProfileIds**, compile **0 errors / 0 warnings**, Console **0 errors / 0 warnings**, `git diff --check = 0`.
- Remaining P13 entrypoints: six World Framework integrations + ArchitectureDemo. After those, finish FrameworkDoc migration, legacy Generated/template cleanup, and final 31-entry seal.
- No commit/push/reset/clean. Preserve unrelated dirty baseline.

### 2026-09-18 — P13 World Framework integrations complete / 30 of 31 ready

- Farm2D / HexStrategy / Survival3D / InfiniteFactory / StellarGridMapMigration / TerrainGridNavigation are COMPLETE / PASS under the P13 productization gates.
- Existing P11/P12 Scenario/Core logic was preserved. Each original integration SceneBuilder now adds the shared P13 localization header instead of replacing frozen world logic.
- Added shared runtime assembly StellarFramework.Samples.WorldFramework.P13Visuals so runtime Mesh/LineRenderer evidence is generated once instead of duplicating rendering code in six samples.
- Runtime evidence is now Player-build visible rather than relying on OnDrawGizmos: HexStrategy uses runtime mesh cells/features; Survival3D keeps real Unity Terrain plus runtime village markers; InfiniteFactory shows streamed chunk tiers as runtime mesh; Migration and TerrainGridNavigation use runtime grid mesh + LineRenderer path. Farm2D keeps its existing real Tilemap projection.
- All six scenes now have zh-CN/en-US catalogs, fixed 中文 / English buttons, LocalizationContext, Source Han Sans, GUID-idempotent Builder output, and explicit distribution closure.
- Actual gates: integration P13 Scene/Productization 3/3 PASS; six frozen integration suites each 4/4 PASS; runtime visual PlayMode 6/6 PASS; Manifest 4/4, README bilingual 2/2, Metadata 13/13, Standalone 30/30.
- Manifest advanced to 30/31 ready; only architecture.demo remained pending.

### 2026-09-18 — P13 ArchitectureDemo complete / 31 of 31 ready

- architecture.demo is now COMPLETE / PASS and all 31/31 Manifest entrypoints are ready.
- Added ExamplePlayableSceneBuilder.BuildArchitectureDemoOnly() so the architecture demo can regenerate its legacy template/support assets without invoking the old Build-All path that would overwrite P13 Kit scenes.
- Added P13ArchitectureDemoBuilder, which rebuilds only FrameworkArchitecture_Playable.unity, generates a five-key zh-CN/en-US catalog, adds fixed language controls and Source Han Sans UI, creates Model / Service / View flow objects, and preserves the original scene GUID.
- Panel_Main now consumes the scene LocalizationContext. Coin text, Mine button, and Close button switch locale at runtime while the MSV rule remains intact: View -> CoinService -> CoinModel -> BindableProperty -> View.
- Architecture PlayMode Gate performs the real loop: switch to English, click Mine, verify the read-only model moves from 0 to 10 and Panel_Main updates to Coins: 10.
- Architecture EditMode Gate 4/4 PASS; Architecture PlayMode 1/1 PASS; Manifest 4/4 PASS after adding the 31st ready entry; Metadata expanded to 15/15 PASS; Standalone 30/30 PASS; legacy SampleGeneration 7/7 PASS; PackagePublisher 23/23 PASS.
- Catalog closure for samples.architecture now explicitly includes localizationkit.ugui, P13Runtime, Common Generated art, Source Han Sans, the stable Architecture scene, and the existing Resources UIRoot.
- P13 entrypoint work is finished. Remaining work is horizontal only: FrameworkDoc migration/centralization, removal of legacy KitSamples/Generated and obsolete .unity.txt templates only after reference gates are updated, then the final 31-entry regression seal.
- No commit/push/reset/clean. Preserve unrelated dirty baseline.

### 2026-09-19 — P13 Final Seal complete

- P13 is now **COMPLETE / PASS**. P0-P13 are frozen; future work must open a new milestone instead of extending P13.
- FrameworkDoc centralization is complete. Formal Markdown outside FrameworkDoc is 0 apart from allowed local README / LICENSE / SOURCE files and this fixed collaboration-memory file.
- Legacy `Assets/StellarFramework/Samples/KitSamples/Generated` and `SampleTemplates/*.unity.txt` were physically removed after all source/catalog/publisher/test references were migrated.
- Sample Manifest final state: **31/31 ready**.
- Distribution Catalog final state: **98 profiles / 0 missing requiredProfileIds**.
- Final static closure confirms legacy Generated path absent, SampleTemplates path absent, stale documentation links 0, and FrameworkDoc as the canonical documentation center.
- ResKit final regression fix: a caller joining an existing same-path pending load now uses `AttachExternalCancellation(cancellationToken)`, so it can cancel its own wait. Dedicated regression **1/1 PASS**.
- Addressables / HotUpdate final regression fix: Unity Addressables Fast Mode is AssetDatabase-backed and Unity's own tests skip physical `DownloadDependenciesAsync` in Fast Mode. `AddressableHotUpdateManager` now returns an explicit 0-byte no-download success in Editor Fast Mode; Packed / Player behavior is unchanged.
- Dedicated integration evidence after that fix: Fast Mode prefab **1/1 PASS**, HotUpdate DLL + SHA **1/1 PASS**, ResKit AddressableLoader DLL **1/1 PASS**, HotUpdateManager FastMode no-op preservation **1/1 PASS**, HotUpdateManager/ResKit prefab integration **1/1 PASS**, AOT metadata AssetDatabase import + Addressables address/label contract **1/1 PASS**.
- Full AOT metadata runtime loading remains a **Packed / Player explicit integration gate**. Fast Mode is not used to fake proof for those large metadata `.dll.bytes` assets because direct Fast Mode metadata loads can stall.
- The environment-wide Unity Test Runner is not the P13 release authority because `com.besty.unity-skills` is configured under `testables` and contributes its own tests; five observed `UnitySkills.Tests.Core.ReviewFixRouterTests` failures are external to StellarFramework.
- Unity GUI startup on this machine can also be blocked by an external `Sentinel LDK Protection System` modal, which prevents the UnitySkills 8090 service from starting. This is an environment/tooling issue, not a StellarFramework Runtime/Editor source failure.
- Latest source compiles with existing Unity Bee/Roslyn response inputs for `StellarFramework.ResKit`, `StellarFramework.HotUpdateKit.Addressables`, and `StellarFramework.FrameworkValidation.Addressables.Tests` — all **exit 0**.
- P13 Final Seal is based on the framework-owned Scene / PlayMode / policy / distribution gates already recorded above, P0-P12 frozen regression evidence, the dedicated Addressables integration gates, and final static repository closure.
- No commit/push/reset/clean was performed. Preserve unrelated dirty baseline.

### 2026-09-19 — Post-seal milestone-label cleanup / EventKit lifecycle regression fix

- Formal product documentation is now milestone-neutral: outside `FrameworkDoc/09-Development`, P0-P13 / F1-F4 labels are no longer used to describe current framework capabilities. Historical plans, freeze records, handoffs, and review evidence remain archived under `09-Development`.
- Long-lived World Framework docs now use stable names: `WorldFramework-Core-API-Contracts.md`, `WorldFramework-GridKit-Projection-Topology-Contract.md`, and `WorldFramework-Performance-Release-Matrix.md`. Root README, FrameworkDoc index, Kit guides, validation docs, and archived links were updated accordingly.
- Product/runtime/test cleanup removed remaining construction-era names such as P7/P8/P9 benchmark labels, `P13Runtime`, P13 sample builder/test names, and the obsolete empty `Samples/Common/P13Runtime` directory. Current `SampleRuntime` / WorldFramework visual assemblies and builder/test names are milestone-neutral while preserving Unity `.meta` identity where assets were moved/renamed.
- Static closure after cleanup: Distribution Catalog **98 profiles / 0 duplicate IDs / 0 missing sourcePaths / 0 missing requiredProfileIds**; Sample Manifest **31/31 ready / 0 missing sourceRoot or scenePath**; **133 asmdef JSON files valid**; 13 checked renamed asset/meta pairs complete; checked old paths absent; formal docs milestone-token scan 0. Unity regenerates empty scalar values in both scene and prefab YAML as `key: `, so `.gitattributes` now applies the existing `whitespace=-blank-at-eol` rule to both `*.unity` and `*.prefab`; all other whitespace checks remain strict.
- UnitySkills 8090 was verified against the correct instance `StellarFramework_DEEE9F8A`. Forced Unity compilation completed successfully on 2026-09-19 14:55:19Z with **0 errors / 0 warnings**.
- Real Unity Test Runner evidence after cleanup: ArchitectureDemo Productization **4/4 PASS**; Foundation Example **9/9**; RuntimeSystems **9/9**; SpatialSimulation **8/8**; ApplicationServices **8/8**; SampleManifest Policy **4/4**; SampleDocumentation Policy **2/2**; WorldFramework Integration Productization **3/3**; DocumentationHub **3/3**; QuickStartCatalog **19/19**; SampleGeneration **7/7**; WorldFramework Foundation Boundary **29/29**; Kit Architecture Metadata **16/16**; Standalone Source Export **30/30**; PackagePublisher **23/23**. The nine-fixture policy/productization batch totaled **139/139 PASS**.
- Real PlayMode evidence: ArchitectureDemo **1/1 PASS**; SpatialSimulation **5/5 PASS**; ApplicationServices **5/5 PASS**; WorldFramework Integration **6/6 PASS**.
- Final PlayMode diagnostics exposed a pre-existing EventKit lifecycle bug: `EventUnregisterTrigger.OnDestroy()` / `EventUnregisterOnDisableTrigger.OnDisable()` iterated a `HashSet<IUnRegister>` while pooled event tokens removed themselves during `UnRegister()`, causing `InvalidOperationException: Collection was modified`.
- EventKit fix is minimal and zero-allocation on the normal path: both lifecycle triggers now guard their active unregister pass, ignore re-entrant `Remove` calls during that pass, and clear the set in `finally`. This preserves explicit exception behavior while preventing iterator invalidation from token recycling.
- Added two PlayMode regressions for multiple tokens bound to the same destroy/disable host. `EventKitPlayModeTests` expanded to **4/4 PASS**. After clearing Console and rerunning, UnitySkills reported **0 Error logs**.
- Existing unrelated dirty working-tree baseline remains intentionally untouched. No commit/push/reset/clean was performed.

### 2026-09-20 — Full Kit / Sample audit resumed

- Resumed the post-P13 user-requested audit of every Kit and published Sample rather than reopening the sealed P13 milestone.
- UnitySkills `http://127.0.0.1:8090` is healthy and bound to `StellarFramework_DEEE9F8A` / Unity 2022.3.62f3c1, Bypass + full surface, compile/update idle.
- Current source of truth remains SampleManifest **31 ready entries** and Distribution Catalog **98 profiles**. `FrameworkDoc/08-Validation/KitExportValidationMatrix.md` still says 97 profiles near its header and is therefore stale relative to the live Catalog; keep this on the audit correction list.
- Today-added audit work is currently untracked and must be preserved: `Tests/PlayMode/SampleSceneSmoke/`, `SampleManifestPolicyTests.cs`, and `FlowKitMsvIntegrationSceneTests.cs`.
- Added manifest-driven PlayMode smoke `SampleManifestSceneSmokeTests`: sequentially loads all 31 ready sample scenes and checks normal startup for unexpected logs. First execution did not produce a framework result because UnitySkills 2.8.3 lost the Test Runner job across PlayMode domain reload (`original Unity Test Runner job ... was not restored`); Console stayed at 0 errors / 0 warnings. Treat this as test transport infrastructure failure, not a Sample FAIL.
- Fresh EditMode discovery sees **1617 tests** total including third-party UnitySkills package tests, so release/audit runs must continue targeting framework-owned fixtures instead of using environment-wide totals.
- `SampleManifestPolicyTests` was extended with a fifth policy ensuring every ready bilingual Sample distribution closes over `localizationkit.ugui`. Fresh discovery sees all five methods.
- The fifth policy initially failed on `hotupdatekit`, but Catalog inspection proved the product profile exists as `samples.hotupdate.hybridclr` and correctly depends on `localizationkit.ugui`; `architecture.demo` likewise maps to `samples.architecture`. The failure was an incomplete test ID mapping, not a missing distribution profile.
- Updated `ProfileMatchesEntry` with the two intentional legacy/special mappings (`hotupdatekit -> samples.hotupdate.hybridclr`, `architecture.demo -> samples.architecture`) instead of renaming stable Catalog IDs.
- No commit/push/reset/clean. Preserve the existing dirty baseline and continue from this audit checkpoint.

### 2026-09-20 — Full Kit / Sample audit runtime + core closure

- Completed a direct Editor-controlled startup smoke for **all 31/31 ready SampleManifest scenes**. Each scene was loaded in EditMode, entered independently into PlayMode, given a startup window, then checked for Console warnings/errors and live EventSystem count. This bypasses the UnitySkills Test Runner domain-reload job-loss issue without weakening runtime validation.
- The direct smoke exposed two real sample bugs: `architecture.demo` and `uikit` each serialized a SampleSceneUtility EventSystem while UIKit's persistent `Resources/UIPanel/UIRoot.prefab` created its own runtime EventSystem, causing repeated `There are 2 event systems in the scene` warnings.
- Fixed the duplicate EventSystem issue without changing normal Sample defaults: `SampleSceneUtility.BeginScene` and `DecorateExistingScene` now accept optional `createEventSystem=true`; only ArchitectureDemo and UIKit explicitly pass `false`. Their existing UIKit UIRoot remains the single runtime EventSystem owner.
- Added regression assertions: ArchitectureDemo scene must serialize zero EventSystem components; UIKit sample scene must also serialize zero and rely on UIRoot at runtime. Fresh `ArchitectureDemoProductizationTests` **4/4 PASS** and `ApplicationServicesExampleSceneTests` **8/8 PASS**. Direct runtime recheck for both samples = **1 EventSystem / 0 warnings / 0 errors**.
- After those fixes, the full 31-scene direct startup sweep is clean: **31/31 entered PlayMode, 0 unexpected warnings, 0 errors, and one runtime EventSystem per scene**.
- `SampleManifestPolicyTests` special distribution mapping was corrected for stable IDs `hotupdatekit -> samples.hotupdate.hybridclr` and `architecture.demo -> samples.architecture`; fresh policy result **5/5 PASS**. Catalog IDs were intentionally not renamed.
- Corrected `FrameworkDoc/08-Validation/KitExportValidationMatrix.md` live Catalog summary from stale **97 / 30 sample / 40 non-tier** to **98 profiles / 31 sample / 41 non-tier**. Other live counts remain 2 single-file / 2 shared-runtime / 5 tooling / 1 generated-support / 19 kit / 38 kit-with-dependencies and 21 Foundation / 9 Extension / 27 Adapter.
- `KitArchitectureMetadataPolicyTests` exposed one stale AudioKit Sample expectation. Current Catalog correctly models `samples.audiokit -> audiokit.reskit -> audiokit.core + reskit.core`; updated the test from redundant direct Core IDs to `audiokit.reskit + localizationkit.ugui`. Fresh metadata policy **16/16 PASS**.
- Fresh productization/policy gates: Foundation Example **9/9**, RuntimeSystems **9/9**, SpatialSimulation **8/8**, SampleDocumentation **2/2**, WorldFramework Integration Productization **3/3**, DocumentationHub **3/3**, QuickStart Catalog **19/19**, SampleGeneration **7/7**, Standalone Source Export **30/30**, PackagePublisher **23/23**, WorldFramework Foundation Boundary **29/29**.
- Fresh PlayMode behavior gates that completed normally: Architecture **1/1**, ApplicationServices **5/5**, SpatialSimulation **5/5**, WorldFramework Integration **6/6**, EventKit **4/4**, TimeKit **1/1**, SaveKit **1/1**, BindableKit **4/4**, UIKitResKit **3/3**. When several PlayMode jobs were launched too quickly, UnitySkills 2.8.3 intermittently reported `batch_state.json` sharing violations / callback reconnect errors; the affected fixtures all passed when rerun individually with recovery spacing, so these were tooling transport failures rather than framework test failures.
- Fresh non-benchmark Kit behavior sweep over `Tests/EditMode/FrameworkValidation/Kits`: **39 fixtures / 327 tests / 327 PASS / 0 failed**. Coverage includes GridKit + projection/topology, Localization Core/Adapters/Sample, PathKit + Grid adapter, PlacementKit, SaveKit, SimulationKit, SpatialKit, TimeKit, WorldKit, WorldGen Core/Builtins/Authoring/Resources/Feature/Presentation adapters, Streaming/FloatingOrigin/SaveKit adapters, and streaming E2E.
- Repository-wide `git diff --check` exits **0**. Git reports existing LF->CRLF conversion warnings for many dirty baseline files, but no whitespace errors were reported by `diff --check`; do not normalize unrelated files just to remove those warnings.
- The Kit sweep intentionally executes TimeKit invalid-argument tests that emit three expected Error logs (`ScheduleAt` invalid trigger/callback, `ScheduleEvery` invalid args, `ScheduleAfter -1`). After identifying them as negative-test evidence, Console was cleared and rechecked at **0 warnings / 0 errors**; UnitySkills health is idle with no compilation/update pending.
- The manifest-driven `SampleManifestSceneSmokeTests` remains useful for a normal Unity Test Runner, but UnitySkills 2.8.3 can lose that single long PlayMode Test Runner job across domain reload. The direct 31-scene Editor-controlled smoke above is the current reliable evidence for this audit.
- No commit/push/reset/clean. Preserve unrelated existing dirty worktree changes.

### 2026-09-20 — Human-readable source standard added

- User clarified a first-class requirement: StellarFramework must not merely be easy for AI to parse; a human developer who did not participate in the original implementation must be able to understand Kit responsibilities, public contracts, important internal invariants, samples, and usage docs without relying on chat history or hidden assumptions.
- Added formal `FrameworkDoc/01-Architecture/CodeReadabilityAndDocumentationStandard.md` and linked it from `KitArchitectureGuide.md` + FrameworkDoc index. The standard requires meaningful XML docs for public/protected contracts, human-readable comments for complex internal algorithms/lifecycle/ownership/failure/GC decisions, explicit units/thread-safety/atomicity/determinism where applicable, and rejects line-by-line/comment-coverage filler.
- Readability is now a release/Kit-delivery concern alongside behavior, architecture, distribution, and validation. Existing Kits will be remediated progressively: Foundation public contracts + complex internals first, then Extension, Adapter, Sample/Guide, and Editor/ToolsHub workflows. Automated scans are only candidate-finders, never semantic proof.
- Initial heuristic scan showed TimeKit as the strongest current XML-doc baseline (115/115 public-like declarations detected), while many older Kits have substantial documentation gaps. The scan intentionally remains advisory because regex-based counts can over/under-count declarations.
- PathKit first remediation batch: documented graph ownership/heuristic contract, caller-owned output buffer semantics, buffer-too-small failure atomicity, expansion limits, path result fields/statuses, workspace reuse/thread-safety, A* closed-node reopen rationale, and overflow behavior. No behavior changes. Fresh tests: PathKit Core **15/15 PASS**, GridKit Adapter **11/11 PASS**. Advisory PathKit XML scan improved to **39/52 documented public-like declarations**.
- SaveKit first remediation batch: documented `SaveKitBuilder`, the `SaveKit` facade, initialization/registration/save-load-delete/diagnostic surfaces, and added internal `SaveCoordinator` responsibility/invariant comments covering operation serialization, validation/commit ordering, and unknown-section preservation. No behavior changes. Fresh tests: SaveKit Core **32/32 PASS**, SaveKit PlayMode **1/1 PASS**. SaveKit still has a large remaining public-contract documentation backlog; do not generate filler to clear it mechanically.
- Continue human-readability remediation without changing stable behavior or published dependency boundaries, and record each completed batch + regression evidence here.
- GridKit readability batch completed for the main geometry/storage/topology/occupancy contracts: GridCoord/GridSize/GridOffset/GridRect, IReadOnlyGrid/IGrid/DenseGrid, IGridTopology + Orthogonal4/8, HexCoord, GridOccupantId/GridOccupancyResult/GridOccupancy. Added explicit docs for negative coordinates, half-open bounds, Row-Major storage, Span/ref ownership, cell-count semantics, topology distance, and occupancy failure atomicity. Fresh regression: GridKit **17/17**, GridTopology **9/9**, UnityProjection Adapter **6/6** PASS. Advisory GridKit XML scan now reports **137/232 documented public-like declarations**.
- SimulationKit readability batch completed for scheduler construction, stable due ordering, single-thread ownership, registration/first-delay semantics, interval reset semantics, caller-owned Span dispatch, ID/result contracts. Fresh regression: SimulationKit **17/17 PASS**. Advisory XML scan now **31/46 documented public-like declarations**.
- SpatialKit readability batch completed for index ownership/thread-safety, BucketSize trade-off, caller-owned Span/truncation semantics, continuous point/half-open rect/query result contracts. Fresh regression: SpatialKit **13/13 PASS**. Advisory XML scan now **55/79 documented public-like declarations**.
- Current advisory XML-documentation checkpoint for the five actively remediated Kits: PathKit **39/52**, SaveKit **33/283**, GridKit **137/232**, SimulationKit **31/46**, SpatialKit **55/79**. These counts are regex heuristics only; semantic review remains the actual gate. SaveKit remains the largest outstanding public-contract backlog among these five.

### 2026-09-20 — Sample product line collapsed to one ArchitectureDemo

- User explicitly changed the sample strategy: StellarFramework no longer maintains one runnable Sample per Kit. The repository keeps exactly one user-facing onboarding Demo, `Assets/StellarFramework/Samples/ArchitectureDemo`, while each Kit's complete usage, dependency boundaries, code snippets, source notes, and troubleshooting live in `FrameworkDoc`.
- The single ArchitectureDemo remains intentionally small. It demonstrates `Architecture<T>` / MSV, BindableKit, ActionKit, UIKit, LocalizationKit, and LogKit through the real loop `Mine click -> CoinService.AddCoin -> CoinModel.CoinCount -> BindableProperty -> View refresh`. It is not expanded merely to claim coverage of every Kit.
- Removed the former Sample product line: `Samples/KitSamples`, `Samples/Examples`, `Samples/Integration`, `SampleManifest.json`, Sample-specific StreamingAssets, per-Sample scene builders/support assemblies, Sample productization/smoke tests, and all `samples.*` distribution profiles. Historical P13/Handoff documents keep their old 31-Sample evidence as history and are not rewritten.
- Distribution Catalog current live state after removal: **67 profiles / 0 missing requiredProfileIds** = 38 `kit-with-dependencies`, 19 `kit`, 5 tooling, 2 single-file, 2 shared-runtime, 1 generated-support. Architecture tiers are **21 Foundation / 9 Extension / 27 Adapter / 10 non-tier**. There are **0 sample profiles**.
- `StellarFrameworkPackagePublisher` no longer contains `OptionalSampleProfileIds`, `Export*SamplePackage`, or `ExportAllOptionalSamplePackages`. Full/base framework payloads exclude the whole `Assets/StellarFramework/Samples` tree, so the repository onboarding Demo is not a distribution product.
- Tools Hub onboarding was simplified from sample generation + UIKit/ResKit playable scenes to: **ArchitectureDemo -> Quick Start -> FrameworkDoc -> environment checks**. The old `样例支持 / 样例构建` module was removed. SettingsKit and FlowKit tooling no longer link to deleted KitSamples.
- The old maintainer-facing `StellarFrameworkVerification/Example_FrameworkValidation` Runner and `FrameworkValidation_Playable.unity` were also removed so Verification does not become a second Demo. `StellarFrameworkVerification` now keeps release/platform verification tooling and validation specifications only.
- Test/tool resources were separated from user Demo assets. Addressables integration uses `Assets/StellarFramework/Tests/Fixtures/Addressables/AddressablesTestPrefab.prefab`; AssetBundle tooling uses `Assets/StellarFramework/Generated/ToolingFixtures/AssetBundle`. Addressables/AB tooling no longer recreates or seeds deleted ResKit Sample assets.
- FlowKit editor regression tests no longer load the deleted FireDrill Sample JSON. The two-way Signal / State / Operation and operation-failure behavior is now built as a minimal in-code test graph. The framework source repository is explicitly allowed and expected to contain **0 bundled business Flow graphs**.
- Current user-facing docs were rewritten around the single-Demo model: Samples README, ArchitectureDemo README, Samples index/source/usage guides, Quick Start, ToolsHub guide, GridKit/SpatialKit/SaveKit/FlowKit/Resources guides, Kit architecture guide, PlayMode README, Verification README/architecture. Non-historical scans contain no positive dependency on deleted `Samples/KitSamples`, Integration Samples, SampleManifest, UIKit/ResKit playable scenes, or FrameworkValidation runner scenes; remaining occurrences are negative policy assertions or explicit statements that those surfaces are removed.
- Fresh post-convergence gates: QuickStartCatalog **19/19 PASS**; OnboardingSurface **1/1 PASS**; KitArchitectureMetadata **10/10 PASS**; StandaloneSourceExport **29/29 PASS**; PackagePublisher **23/23 PASS**; VerificationSurface **3/3 PASS**; DocumentationHub **3/3 PASS**; AAHotUpdatePublishTool **31/31 PASS**; FlowKitEditor **23/23 PASS**.
- Unique Demo behavior gate: `ArchitectureDemoPlayModeTests` **1/1 PASS**, including real Mine click, read-only model value `10`, and localized UI text `Coins: 10`. Independent direct scene run of `FrameworkArchitecture_Playable.unity` entered PlayMode successfully with **0 warnings / 0 errors**.
- Unity compilation after the convergence is clean. Repository `git diff --check` exits **0**. Existing unrelated dirty baseline remains preserved; no reset/clean/commit/push was performed.

### 2026-09-20 — ArchitectureDemo business + UI lifecycle closure

- User found that closing `Panel_Main` made the Demo impossible to continue. This was a real lifecycle gap: the business loop could repeat, but the UI had no scene-level reopen entry.
- ArchitectureDemo business loop is now explicitly repeatable: `Round N 0/30 -> Mine +10 -> 10/30 -> 20/30 -> 30/30 -> Complete Round -> Round N+1 0/30`. `CoinModel` owns both `CoinCount` and `RoundNumber`; `CoinService.AdvanceCycle()` owns the round transition; View remains presentation/input only.
- Added a real scene-level `OpenPanelButton` under `Sample_UI`, outside `Panel_Main`. `DemoEntry` subscribes to `UIPanelBase.OnPanelClosedGlobal`: initial successful open hides the launcher; closing `Panel_Main` shows it; clicking it reopens the cached panel through UIKit and hides the launcher again.
- Reopening the View does not dispose `DemoApp` or reset `CoinModel`, so business state persists across `Panel_Main` close/reopen. This intentionally demonstrates that Model/Architecture lifetime is independent from View lifetime.
- Added localized launcher key `architecture.open_panel` for zh-CN/en-US. ArchitectureDemo README now documents both the repeatable gameplay loop and UI lifecycle loop.
- `ArchitectureDemoPlayModeTests` expanded to **2/2 PASS**: (1) complete Round 1 and enter Round 2; (2) mine to 10, close Panel, verify launcher visible and model remains at 10, reopen, verify UI restores `10/30`, then continue to 20.
- Independent direct scene validation used the actual runtime `CloseButton` and scene `OpenPanelButton`: after Close => `Panel_Main.isActive=false`, `OpenPanelButton.isActive=true`; after launcher click => `Panel_Main.isActive=true`, `OpenPanelButton.isActive=false`.
- The deeper Sample cleanup audit also removed `Samples/Common` entirely. ArchitectureDemo now owns its fonts, materials and locale switcher directly. Current `Assets/StellarFramework/Samples` contains only `ArchitectureDemo` and `README.md`.
- Additional cleanup discovered and removed stale `EditorBuildSettings` reference to deleted `HotUpdateKit_Playable.unity`, removed stale `StreamingAssets/aa/Windows` built output, and moved AB/AA validation assets to Tooling/Test fixture locations.
- Addressables source configuration is clean and a fresh Addressables Build succeeds after hard-gating `StellarFrameworkKitPackageBootstrapInstaller.cs` with `#if UNITY_EDITOR`; this prevents SBP Player script compilation from seeing `UnityEditor.PackageManager` / `InitializeOnLoad` code.
- Final non-historical scan across current source/docs/ProjectSettings/Addressables/StreamingAssets reports no positive legacy references to deleted `KitSamples`, Integration Samples, Samples/Common, SampleManifest, old `*_Playable` sample scenes, FrameworkValidation runner scene, or FireDrill sample graph. Remaining occurrences are negative policy assertions or historical handoff records.

### 2026-09-21 — ResKit three-tier usability + YooAsset adapter

- User formally defined the ResKit product target as: **beginner can load safely in 5 minutes; experienced users can precisely manage lifecycle; advanced projects can swap Addressables / YooAsset without changing business loading code**.
- Added `ResScope : IDisposable` as the recommended ordinary-business API. A Scope owns one `IResLoader`; `Load/LoadAsync/PreloadAsync` references belong to that Scope; `Dispose()` cancels pending waits, releases all owned references and recycles the loader. Use after Dispose throws `ObjectDisposedException`; Dispose is idempotent.
- Added `ResKit.CreateScope(...)`, `CreateCustomScope(...)`, and request-based Scope creation. Missing/unregistered backend is treated as a startup/configuration error and fails fast instead of returning an unusable Scope.
- Fixed a pooled-loader async lifetime race: an old `LoadAsync` finally block now removes a pending record only when that record still points to the same `UniTaskCompletionSource`. A completed request from a previous pooled lifetime can no longer erase a new owner's same-path pending request.
- Fixed cancellation semantics in `ResMgr.LoadSharedAsync`: caller cancellation now propagates as `OperationCanceledException` instead of becoming `null`, so timeout/cancel is distinguishable from actual resource-not-found/load-failed.
- HybridCLR AOT metadata byte acquisition was changed from serial to parallel loading. Direct Addressables Fast Mode AOT metadata load and ResKit->AddressableLoader parallel AOT load both completed successfully in focused gates (about 10s / 9s editor observations respectively); subsequent HybridCLR metadata application ordering remains unchanged.
- Corrected repo default Addressables Local Built-in state: `BuildRemoteCatalog=false`, Local profile remote paths cleared, remote profile preserved. Added a policy assertion so selected Local Built-in workflow cannot silently leave a remote HTTP catalog state behind. Fresh Addressables Player Content build succeeds without remote catalog URLs.
- Packed Addressables runtime validation was moved out of EditMode into a dedicated Editor-only PlayMode test assembly. UnitySkills currently cannot provide assertion evidence for that gate because the PlayMode job is lost during domain reload (`0 tests / original job was not restored`). Treat this as a tooling evidence gap, not a PASS or product failure.
- Installed official YooAsset **2.3.19** via fixed Git UPM dependency `https://github.com/tuyoogame/YooAsset.git?path=Assets/YooAsset#2.3.19`. Package lock and PackageCache both confirm the dependency.
- Added independent `StellarFramework.ResKit.YooAsset` adapter assembly. `YooAssetLoader` maps ResMgr final release to `AssetHandle.Release()`; it does not own YooAsset package creation, initialization, version/manifest update or downloader workflow. These remain project startup/hot-update responsibilities.
- YooAsset multi-package cache isolation is explicit: LoaderName is `YooAsset:<PackageName>`, so identical locations in different ResourcePackages cannot collide inside ResMgr's `LoaderName + Path` shared cache key.
- Added `YooAssetResKitInstaller.Install(packageName)` / `Uninstall()`. Ordinary usage can now remain `ResScope.LoadAsync<T>()`; switching backend is an installer/default-backend concern rather than a business-code rewrite.
- Fresh focused tests: `ResScopeTests 3/3 PASS`; `YooAssetResKitAdapterTests 3/3 PASS`; `KitArchitectureMetadataPolicyTests 10/10 PASS`; `StandaloneSourceExportPolicyTests 29/29 PASS`; `PackagePublisherPolicyTests 23/23 PASS`. Latest cancellation fixture run discovered 1/1 and passed; do not overstate that as all source methods freshly discovered.
- Distribution Catalog gained independent `reskit.yooasset` adapter profile and `reskit.core` explicitly excludes YooAsset source. Current live post-Sample-removal Catalog is **68 profiles / 0 missing requiredProfileIds** = 21 Foundation / 9 Extension / 28 Adapter / 10 non-tier; 39 are `kit-with-dependencies`. Historical P13 98-profile records remain historical and are not rewritten.
- ResKit user/source guides were rewritten around the three-tier experience. Beginner docs start with `using ResScope`; direct `IResLoader` is the skilled-user layer; third-party adapters are the advanced layer. YooAsset package initialization/download/version responsibilities and cancellation semantics are explicitly documented.

### 2026-09-21 — YooAsset content-update helper + real resume / HybridCLR E2E

- Continued the HotUpdateKit -> `HybridCLRKit + ResKit/YooAsset` simplification. No new mega HotUpdate abstraction was introduced. The intended startup path is now two explicit phases: `YooAssetContentUpdater.UpdateHostPackageAsync(...)` for content, followed by `HybridCLRKit.RunAsync()` for code.
- Added `YooAssetContentUpdater` to the independent `StellarFramework.ResKit.YooAsset` adapter. It wraps YooAsset 2.3.x official HostPlayMode startup flow only: Package init -> request version -> update manifest -> create downloader -> download -> optional ResKit install. `ResKit Core` still has no YooAsset dependency and HybridCLRKit still has no YooAsset dependency.
- Fixed pure-remote HostPlayMode initialization: `BuildinFileSystemParameters` is now omitted when `BuildinPackageRoot` is empty. This prevents pure-remote packages from incorrectly probing `StreamingAssets/yoo/<Package>/BuildinCatalog.bytes`.
- Fixed first-install manifest lifecycle: the updater now calls `UnloadAllAssetsAsync()` only when `package.PackageValid == true`. A newly initialized package with no active manifest no longer throws `Can not found active package manifest` before its first `UpdatePackageManifestAsync`.
- Fixed verification cleanup to use YooAsset `DestroyAsync()` directly. This lets YooAsset itself decide whether unload is valid and avoids calling unload on a package without an active manifest.
- Fixed a real HybridCLR correctness bug: `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly` return values are now checked. Any non-`OK` `LoadImageErrorCode` or reflected invocation failure blocks startup instead of being silently treated as metadata success.
- Added a verification-only YooAsset package `StellarHotUpdateVerification` containing exactly Manifest + HotUpdate.dll + 4 AOT metadata assets. Fresh real build succeeded. Largest bundle is `0ed287fb0f830437ed5b5e42978a9fc6.bundle` = **1,052,914 bytes**. Current `HotUpdate.dll.bytes` SHA256 remains **e3d707b53fecb9a748d3fd837635c2c2e13a9ac126c517dd2a1fb5be89457544**, matching `HotUpdateManifest.json`.
- Remote-update verification is fully isolated from project content: built package output is copied to `Temp/StellarHotUpdateVerification/RemoteCDN` and served through a local HTTP Range server; client cache is separately stored in `Temp/StellarHotUpdateVerification/ClientCache`. `StreamingAssets` is not involved.
- Real runtime E2E **PASS**: first request was intentionally TCP-reset after **262,144 bytes**; YooAsset first update failed as expected while preserving its temp cache; package was destroyed/recreated; second request emitted HTTP `Range` and resumed at **262,144** exactly; all content then completed. Result: `packageVersion=verification-v1`, `interruptedBytes=262144`, `resumeOffset=262144`.
- The same runtime gate then loaded Manifest and `HotUpdate.dll.bytes` through `ResKit:YooAsset`, verified DLL SHA256, loaded AOT metadata through HybridCLR's Editor API, loaded assembly `HotUpdate, Version=0.0.0.0`, and executed `HotUpdate.HotUpdateMain.Main`. Recorded manifest source: `ResKit:YooAsset:Assets/GameHotUpdate/Manifest/HotUpdateManifest.json`.
- Important evidence boundary: HybridCLR `RuntimeApi.LoadMetadataForAOTAssembly` is an Editor stub that returns `OK` in Unity Editor. Therefore the above runtime gate proves real YooAsset download/cache/resume + ResKit + actual managed DLL load/entry execution, but **does not replace** a final StandaloneWindows64 IL2CPP Player verification of the HybridCLR interpreter/AOT metadata path.
- Fresh focused regressions after the change: `YooAssetResKitAdapterTests` **3/3 PASS**, `HybridCLRHotUpdateAssetExporterTests` **5/5 PASS**, fully-qualified `HotUpdateManifestTests` **5/5 PASS**, `VerificationSurfacePolicyTests` **3/3 PASS**, `StandaloneSourceExportPolicyTests` **25/25 PASS**. An earlier short-name Manifest filter returned 0 matches and is not counted as evidence.
- The intentional interrupted download emits YooAsset/cURL Error logs (`Curl error 18`, incomplete response) during the negative half of the test. Those logs are expected fault-injection evidence and must not be interpreted as post-test product errors; clear Console after the verification gate before final release health checks.
- User-facing ResKit and HybridCLRKit guides were updated to document the two-step startup flow, pure-remote `BuildinPackageRoot=null`, official YooAsset temp-file + HTTP Range resume semantics, and the Verification RemoteCDN/ClientCache separation.

### 2026-09-21 — Windows64 IL2CPP HybridCLR final runtime gate PASS

- Built a real **StandaloneWindows64 Release IL2CPP Player** from `FrameworkArchitecture_Playable.unity` into `Builds/HotUpdateVerification/StellarHotUpdateVerification.exe`. First full build completed successfully with generated `GameAssembly.dll`; Build Settings were intentionally left unchanged and the verification scene was passed explicitly to the build job.
- First real Player run proved the YooAsset half again: simulated RemoteCDN bundle size **1,062,593 bytes**, forced interruption **262,144 bytes**, resumed HTTP Range offset **262,144**. It then exposed a genuine Release-only HybridCLR defect: `HybridCLR.Runtime` was not available to `Type.GetType(...)` because `StellarFramework.HybridCLRKit` only referenced the Runtime API through string reflection, allowing IL2CPP stripping to treat the assembly as unreachable.
- Fixed the Release defect by adding an explicit asmdef dependency on `HybridCLR.Runtime` (GUID `13ba8ce62aa80c74598530029cb2d649`) and replacing reflective `RuntimeApi.LoadMetadataForAOTAssembly` invocation with the strong-typed official call `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(..., HomologousImageMode.SuperSet)`. Non-`OK` `LoadImageErrorCode` still fails startup explicitly.
- This strong dependency is intentional architecture: HybridCLRKit is the HybridCLR-specific code-update adapter and already requires the `com.code-philosophy.hybridclr` package. It remains independent from Addressables, YooAsset and HttpKit. Strong typing also prevents silent API drift on future HybridCLR upgrades.
- Unity recompiled after the fix with **0 errors**. Incremental Windows64 Release IL2CPP rebuild then completed successfully.
- Final real Player runtime E2E **PASS**: `packageVersion=verification-v1`, `largeBundleSize=1062593`, `interruptedBytes=262144`, `resumeOffset=262144`, `loadedAssemblyFullName=HotUpdate, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null`, `manifestSource=ResKit:YooAsset:Assets/GameHotUpdate/Manifest/HotUpdateManifest.json`.
- This final gate is the missing evidence from the previous checkpoint: metadata loading now runs through the actual HybridCLR Runtime in an IL2CPP Player, not the Unity Editor stub. Therefore the tested chain is now closed end-to-end: **YooAsset remote version/manifest/download/cache/resume -> ResKit -> Manifest/DLL SHA -> real HybridCLR AOT metadata load -> Assembly.Load HotUpdate.dll -> HotUpdate entry execution**.
- Fresh post-fix focused regressions: `YooAssetResKitAdapterTests` **3/3**, `HybridCLRHotUpdateAssetExporterTests` **5/5**, `HotUpdateManifestTests` **5/5**, `HotUpdateSourcePolicyTests` **2/2**, `KitArchitectureMetadataPolicyTests` **10/10**, `StandaloneSourceExportPolicyTests` **29/29**, `DocumentationHubPolicyTests` **3/3**, `QuickStartCatalogPolicyTests` **20/20** — all PASS. Unity Console final error query reports **0 errors**.
- `Builds/HotUpdateVerification` is ignored/not present in `git status`; the large IL2CPP verification Player and backup symbols are not being staged as repository content. Repository-wide `git diff --check` is not globally clean because the existing/generated `Assets/AddressableAssetsData/AddressableAssetSettings.asset` has two YAML blank-value trailing-space lines (`m_Value: `); this is unrelated to the HybridCLR runtime fix and was not silently rewritten during this gate.

### 2026-09-21 — ResKit post-freeze polish: AssetsMap, updater diagnostics, concurrency gates

- User approved three final ResKit polish items before freezing: keep `YooAssetContentUpdater` thin but improve diagnostics/retry semantics; eliminate hand-written resource strings with an automatically maintained AssetsMap; strengthen abnormal-path / concurrency tests.
- Added backend-neutral generated `StellarFramework.Generated.AssetsMap` at `Assets/StellarFramework/Generated/AssetMap/AssetsMap.cs`. It scans runtime-loadable project assets, emits nested folder classes + stable `const string` canonical `Assets/...` paths, sanitizes C# identifiers, deterministically disambiguates collisions, sorts output, and only writes when content changes.
- Added automatic maintenance through `AssetPostprocessor` plus manual menu `StellarFramework/ResKit/Regenerate AssetsMap`. Infrastructure is excluded (`Editor`, `Tests`, `FrameworkDoc`, `AddressableAssetsData`, `StreamingAssets`, Verification/Bootstrap, docs/temp/recovery, Unity `InitTestScene*`). Current clean generated result is about **199 lines / 11.6 KB**, so compile cost is negligible. Existing singular `AssetMap` remains the AB path->bundle internal map; plural `AssetsMap` is the business resource-key surface.
- `ResourceLoader` now accepts both legacy Resources-relative keys and canonical AssetsMap paths. Example `Assets/Resources/HotUpdateSettings.asset` is normalized to `HotUpdateSettings`; nested `Assets/.../Resources/Foo.prefab` becomes `Foo` relative to its Resources folder with extension removed. This makes the same generated key usable across Resources / AB / Addressables / YooAsset.
- Compatibility guard: Resources normalization only activates for paths beginning with canonical `Assets/`; legacy relative keys that merely contain a folder named `Resources` are returned unchanged.
- `YooAssetContentUpdater` now returns stable `YooAssetContentUpdateErrorCode`, `FailureStage`, `RetryCount`, and progress `Attempt`. Added pluggable `IYooAssetContentUpdateRetryPolicy`; conservative default retries version and Manifest control-plane requests only. Package initialization is not blindly retried and bundle retries remain owned by YooAsset `ResourceDownloaderOperation`, preserving the helper's thin-adapter boundary.
- Added `AssetsMapGeneratorTests` **5/5 PASS**: expected canonical constants, infrastructure exclusions, canonical Resources path actually loads `HotUpdateSettings`, no-op regeneration is stable, and code/Editor changes are rejected by the auto-generation trigger before any full AssetDatabase scan.
- Added `ResScopeConcurrencyTests` **3/3 PASS**: two scopes sharing one key produce one physical load and unload only after last owner; cancelling one waiter does not cancel another scope's shared load; disposing the last waiting scope propagates cancellation to the shared physical load.
- Added `YooAssetContentUpdaterPolicyTests` **3/3 PASS**: invalid config returns stable `InvalidOptions`, default retry policy scope is version/Manifest only, and projects can opt into fail-fast with `RetryPolicy=null`.
- Re-ran the real simulated RemoteCDN E2E after updater changes: **PASS**, bundle size `1,062,593`, forced interruption `262,144`, resumed HTTP Range `262,144`, HotUpdate assembly loaded and entry executed. Runtime verification now also requires the intentional first failure to be classified exactly as `DownloadFailed`.
- Final focused gates after the auto-trigger optimization: `AssetsMapGeneratorTests` **5/5**, `ResScopeConcurrencyTests` **3/3**, `YooAssetContentUpdaterPolicyTests` **3/3**, `KitArchitectureMetadataPolicyTests` **10/10**, `StandaloneSourceExportPolicyTests` **29/29**, `PackagePublisherPolicyTests` **23/23**, `DocumentationHubPolicyTests` **3/3**; Unity compilation/error query remained **0 errors**.

### 2026-09-21 — Distribution dependency truth / Recommended Profile refinement

- This pass intentionally refined distribution boundaries instead of adding more ResKit/UIKit features. The governing rule is now: atomic Kit profiles describe real implementation dependencies; higher-level convenience must be represented by Recommended Profiles, not by coupling unrelated runtime modules.
- `KitDistributionCatalog.json` moved to **schema v3** and now has **75 atomic distribution profiles + 2 Recommended Profiles**. Recommended Profiles are composition presets only; they reuse the existing atomic dependency resolver and do not create new Runtime assemblies.
- Added `uikit.standard` with roots `uikit.core + uikit.tools`. Its resolved closure is `logkit, runtime.core, singletonkit, toolshub.core, uikit.core, uikit.tools`. UIKit.Core no longer hard-depends on PoolKit, Newtonsoft.Json or ToolsHub; it still correctly depends on Runtime.Core + SingletonKit.
- Added `hotupdate.full` with roots `reskit.yooasset + reskit.tools + hybridclrkit.tools`. Its intended closure is `generated.assetmap, hybridclrkit, hybridclrkit.tools, logkit, poolkit, reskit.core, reskit.tools, reskit.yooasset, toolshub.core`. UPM resolution continues to supply UniTask, YooAsset and HybridCLR.
- Split optional ToolsHub/editor UX away from Runtime profiles: new tooling profiles include `actionkit.tools`, `eventkit.tools`, `configkit.tools`, `singletonkit.tools`, `reskit.tools`, `audiokit.tools`, `reskit.assetbundle.tools`, `hybridclrkit.tools` and `uikit.tools`.
- Current Catalog audit result: **0 Runtime profiles depend on `toolshub.core` and 0 Runtime profiles directly source `Assets/StellarFramework/Editor/StellarToolsHub/...`**. A policy test now locks this boundary.
- Dependency truth corrections: ResKit.Core keeps only its real framework runtime dependencies LogKit + PoolKit; SingletonKit and Generated.AssetMap are not Core dependencies. ResKit.AssetBundle owns SingletonKit + Generated.AssetMap because AssetBundleManager actually uses both. HybridCLRKit no longer carries stale PoolKit/SingletonKit asmdef references.
- UIKit asmdef audit removed stale PoolKit. Unity compilation proved the UIKit.ResKit adapter must still directly reference SingletonKit because its referenced UIKit type inherits `MonoSingleton<>`; that dependency was restored at the adapter layer. Unity also proved the AssetBundle adapter still needs generated singular `AssetMap`; Generated.AssetMap was restored there rather than returned to ResKit.Core.
- Editor-code boundary is semantic, not folder-name-only: build-essential editor infrastructure remains with its owning core when required for correctness. In particular `SingletonKit/Editor/SingletonGenerator` stays inside SingletonKit delivery because the runtime intentionally relies on generated static metadata instead of runtime reflection. Optional ToolsHub panels/audits/codegen UX are separate tooling profiles.
- Kit Package Exporter now has a first-class **推荐 Profile** tab. It displays each Recommended Profile's root atomic profiles, resolved final closure and output package, and exports through the existing combined-package path.
- Final validation after adding closure/asmdef regression gates: Unity compile/error query **0 errors**; `KitArchitectureMetadataPolicyTests` **14/14 PASS**, `StandaloneSourceExportPolicyTests` **30/30 PASS**, `PackagePublisherPolicyTests` **23/23 PASS**. The Recommended Profile closure tests verify exact `uikit.standard` and `hotupdate.full` closures, and asmdef policy tests lock the corrected ResKit/UIKit/HybridCLR dependency ownership.
- No commit/push/reset/clean was performed in this pass.

### 2026-09-21 — Unity top-menu consolidation

- User requested the Unity top-level `StellarFramework` menu to remain intentionally minimal. Final policy: the only visible `StellarFramework/...` menu paths are **`StellarFramework/Tools Hub`** and **`StellarFramework/Export`**.
- The previous source-project menu `StellarFramework/Framework Source/Kit Package Exporter` was renamed to the single `StellarFramework/Export` entry. The window title/header is now `StellarFramework Export` because it covers Recommended Profiles, Kit combinations, samples, standalone source and full-framework export.
- Removed duplicate top-level menus for `StellarFramework/ResKit/Regenerate AssetsMap` and `StellarFramework/Verification/Export HybridCLR Generated Assets`. AssetsMap manual rebuild + generated-file ping now live in Tools Hub -> `ResKit 资源审计`; HybridCLR DLL/AOT/Manifest export was already available in Tools Hub -> `HybridCLR DLL 导出`.
- The verification-only YooAsset/HybridCLR release actions were moved into a source-project-only ToolsHub module `HotUpdate 发布验证` under the `热更新` group. It provides buttons for verification package build and Runtime HotUpdate E2E preparation without creating additional top-level menus.
- Bootstrap is the one semantic exception to “put it in ToolsHub”: in a clean project ToolsHub does not exist before installation. The bootstrap window therefore auto-opens once after importing the public package and keeps a recovery entry under `Window/StellarFramework Bootstrap Installer`; it no longer contributes any `StellarFramework/...` menu path. Framework development projects are explicitly excluded from bootstrap auto-open.
- Added a repository-wide menu policy test that scans **all C# files under Assets**, extracts every `[MenuItem("StellarFramework/...]` path and only allows Tools Hub + Export. This prevents Verification/Bootstrap or future Kit tools from re-growing the menu hierarchy.
- README, Distribution Guide, ResKit/GridKit/SpatialKit/SaveKit guides, PlayMode verification error guidance and generated single-package dependency instructions were updated to the new entry paths. Historical memory records retain their original paths as historical evidence.
- Fresh validation: Unity compile/update idle and **0 errors**; `StandaloneSourceExportPolicyTests` **31/31 PASS**, `PackagePublisherPolicyTests` **23/23 PASS**, `BootstrapInstallerPolicyTests` **3/3 PASS**, `VerificationSurfacePolicyTests` **3/3 PASS**. UnitySkills executed both `StellarFramework/Tools Hub` and `StellarFramework/Export` successfully.
- No commit/push/reset/clean was performed.

### 2026-09-22 — Localization standalone delivery + Export UI unification

- User asked whether LocalizationKit can be used by an otherwise complete project that only lacks localization. Verified current Core architecture before changing export UX: `StellarFramework.LocalizationKit.Core.asmdef` has **zero references** and `noEngineReferences=true`. Therefore `LocalizationKit.Core` is a valid standalone domain package with no StellarFramework Kit or UPM dependency.
- Formalized three Localization delivery levels instead of forcing one mega package: `LocalizationKit.Core` for pure domain localization; `LocalizationKit.UnityUGUIAdapter` for optional Unity UGUI authoring/binding; `Localization Complete` Recommended Profile for the full Unity development experience. `LocalizationKit.SettingsAdapter` remains optional and is not pulled in unless the project intentionally uses SettingsKit for language selection.
- Added `localizationkit.tools` tooling Profile and new ToolsHub module `Localization 本地化`. It combines the existing Editor validator API with ToolsHub and exposes Catalog validation / coverage / fallback / placeholder checks plus Source Han Sans example-font maintenance. Runtime Core and UGUI Adapter do **not** depend on ToolsHub.
- Removed the two legacy `Tools/Stellar Framework/Localization/...` menu entries. Validator/font installer remain callable APIs; visual entry is now ToolsHub. Added policy coverage so legacy `Tools/Stellar Framework/` Kit menus cannot silently return.
- Improved validator UX: added explicit `ValidateAndShowReport(LocalizationCatalogAsset)`; the ToolsHub module owns an editable Catalog field, follows Unity Selection when appropriate, and validates the actual field value instead of pretending an ObjectField selection works while still reading global Selection.
- Added Recommended Profile `localization.complete` (order 10) -> `localizationkit.tools`; existing `uikit.standard` is displayed as **UIKit Complete** (order 20); `hotupdate.full` is order 30. Exact Localization closure is `localizationkit.core, localizationkit.editor, localizationkit.tools, localizationkit.ugui, toolshub.core`. It intentionally excludes SettingsKit/UIKit/ResKit/Addressables/HybridCLR.
- Reworked `StellarFramework Export` from a horizontal IMGUI tab window into the same UI Toolkit shell used by ToolsHub: matching dark palette, 288px left navigation, top title bar, right header/content card, footer and search field. Manual export navigation is now ordered **01 基础能力 -> 02 功能系统 -> 03 适配扩展 -> 04 开发工具**, with Recommended combinations above and Samples / Source & Full Framework below.
- Manual selections persist across export sections, allowing real cross-layer combination export. Footer now reports the global selected Profile count, offers Clear Selection, previews the deduplicated dependency closure and exports one combined package.
- Current Recommended Profiles: `Localization Complete`, `UIKit Complete`, `Hot Update Full`. They remain composition presets over atomic Profiles and do not create new Runtime assemblies.
- Documentation updated: README Chinese/English distribution guidance, LocalizationKit guide, ToolsHub guide, KitArchitectureGuide, KitDistributionGuide and KitExportValidationMatrix. Current Catalog baseline is **76 atomic Profiles + 3 Recommended Profiles** = 19 kit, 37 kit-with-dependencies, 15 tooling, 2 shared-runtime, 2 single-file, 1 generated-support; tier counts remain 21 Foundation / 9 Extension / 26 Adapter / 20 non-tier.
- Fresh final validation: Unity compile/error query **0 errors**; Localization Core **15/15 PASS**; Localization Adapter/Editor **15/15 PASS**; KitArchitectureMetadataPolicy **14/14 PASS**; StandaloneSourceExportPolicy **31/31 PASS**; PackagePublisherPolicy **23/23 PASS**; DocumentationHubPolicy **3/3 PASS**; QuickStartCatalogPolicy **20/20 PASS**. Unity successfully executed both `StellarFramework/Tools Hub` and `StellarFramework/Export`; `git diff --check` exit 0.
- No commit/push/reset/clean was performed.

### 2026-09-22 — Production delivery model + Localization Scanner/Exchange implementation

- Began the approved production-delivery plan. P1-P5 are implemented far enough to compile and pass focused regression; work continues into UIKit adaptation next.
- Export UI no longer treats internal architecture `tier=foundation/extension/adapter` as the primary user-facing model. Catalog tiers remain architecture-policy metadata, while Export is being converted to the delivery view **01 基础功能 / 02 完整功能 / 03 扩展功能**. Basic = atomic Runtime Kit with only real hard dependencies; Complete = composed production-ready feature; Extension = atomic Adapter/Tooling plus composed extension.
- Added Recommended Profile `reskit.complete` -> `reskit.tools`. Renamed the temporary complete UI profile id from `uikit.standard` to `uikit.complete`; UIKit Complete roots are now `uikit.reskit + uikit.tools + reskit.tools`, so “Complete” includes ResKit integration/audit/tooling instead of only UIKit Runtime. Recommended Profiles now carry `deliveryGroup=complete|extension`; `hotupdate.full` is a composed extension.
- `LocalizedTextView` now owns serialized stable `BindingId` plus `Key`; scanner-managed identity does not change on Rename/Reparent/Reorder. It can auto-resolve `LocalizationContext` from parents at runtime, so scanned prefabs do not need hard scene references. A custom inspector keeps normal usage simple and hides BindingId under Advanced.
- Added Editor-only `LocalizationSourceRegistry` recording BindingId, Key, prefab GUID, LocalFileId, current hierarchy, component type, source locale/text/hash and status. Runtime lookup does not depend on this registry.
- Added `LocalizationUiScanner` for UGUI Text prefab scanning. Scan is Preview-first, then Apply. It classifies New / Synced / SourceChanged / DuplicateBindingId / DynamicCandidate / Conflict; dynamic-looking text is not auto-selected. Apply writes/repairs `LocalizedTextView`, source table and registry with Undo support.
- Binding identity rules are implemented and tested: sibling reorder does not alter generated Key; existing BindingId/Key survive rescans; independent prefab copies with duplicated serialized BindingId Fork a new identity; Prefab Variant inherited identity is treated separately and conflicting source changes are blocked for explicit review.
- Initial key format is readable + stable short identity, e.g. `ui.panel_login.btn_confirm.c4729f11`; sibling indices are not used. Hierarchy/index data is transient scan location/metadata only.
- Added `LocalizationWorkspaceAsset` (Editor-only) for source locale plus arbitrary enabled language/table configuration.
- Added `LocalizationTranslationExchange`: JSON and CSV export/import for external manual/AI translation. Unity performs no remote AI calls. Files include key, BindingId, source, sourceHash, prefab/hierarchy/component context and per-locale translations. Import validates locale/table configuration and rejects stale source hashes so old translations cannot silently overwrite newer source strings.
- ToolsHub `Localization 本地化` now includes Workspace, Scan & Bind preview/apply, JSON/CSV Import/Export, Catalog validation and example-font maintenance.
- Focused validation after P1-P5: Unity compile **0 errors**; LocalizationUiScannerTests **4/4 PASS**; LocalizationTranslationExchangeTests **3/3 PASS**; Localization Core **15/15 PASS**; Localization Adapter **15/15 PASS**; KitArchitectureMetadataPolicy **14/14 PASS**; StandaloneSourceExportPolicy **31/31 PASS**.
- No commit/push/reset/clean was performed.

### 2026-09-22 — Clean-project delivery verification + UIKit/Singleton distribution handoff

- Dedicated clean verification project:
  - path: `C:\CodingToolsWorkerCenter\StellarFramework-test`
  - Unity: `2022.3.62f3c1`
  - UnitySkills: `http://127.0.0.1:8093/`
- Localization Complete clean import/function validation: **PASS / 0 Error**.
  - `Panel_Login` scanned three UGUI texts and produced stable keys:
    - 登录 -> `ui.panel_login.title.675d49a1`
    - 确认 -> `ui.panel_login.btn_confirm.325c4437`
    - 退出 -> `ui.panel_login.btn_quit.589345c0`
  - Reparent/reorder kept BindingId/Key stable.
  - Source text change kept identity but triggered SourceChanged/NeedsReview.
  - JSON/CSV external translation roundtrip worked for configured target locales.
  - stale `sourceHash` correctly blocked old translation overwrite.
  - TMP scanner/binding also passed; 用户名 -> `ui.panel_tmp.user_name_label.c624722e`.
- ResKit Complete clean import/function validation: **PASS / 0 Error**.
  - Bootstrap auto-installed UniTask from a clean baseline.
  - Two `ResScope` instances sharing one Resources TextAsset verified cache lifecycle `0 -> 1 -> 1 -> 0`.
  - AssetsMap support and ResKit ToolsHub module present.
- UIKit Complete clean import exposed and then verified two real mother-project-hidden distribution defects:
  1. clean project initially had no generated Singleton static registry.
  2. `uikit.core` initially omitted runtime-required `Assets/StellarFramework/Resources/Managers/UIKit.prefab`.
- UIKit fix state:
  - `uikit.core.sourcePaths` now includes `Assets/StellarFramework/Resources/Managers/UIKit.prefab`.
  - Singleton generated output moved away from the mother-project-owned fixed asmdef model to target-project output:
    `Assets/Generated/StellarFramework/SingletonRegister/SingletonRegister.cs`.
  - old fixed `Assets/StellarFramework/Generated/SingletonRegister` code/asmdef was removed.
  - `SingletonGenerator` now scans loaded assemblies through `AppDomain.CurrentDomain.GetAssemblies()`, writes only when content changes, and retains build-time generation gate.
  - Kit bootstrap calls Singleton registry generation after payload import.
- Real UIKit clean PlayMode validation after these fixes: **PASS / 0 Runtime Errors**.
  - generated register contains `StellarFramework.UI.UIKit`
  - default UI strategy = `ResKitUILoadStrategy`
  - UIRoot generator PASS
  - SafeArea Panel routing PASS
  - FullScreen Panel routing PASS
  - 20:9 `phone_tall` breakpoint PASS
  - 4:3 `tablet` breakpoint PASS
  - Static/Dynamic dual SafeAreaRoot PASS
  - orientation-independent Shape Aspect PASS
- Additional business-Singleton regression:
  - clean project added `CleanBusinessSingleton : ISingleton` with `[Singleton("", Global, true)]`.
  - manual `SingletonGenerator.Generate()` immediately found it, proving scanner/generator logic is correct.
  - several unreliable self-bootstrap/TypeCache timing ideas were rejected by clean-project evidence.
- Canonical Kit bootstrap installer was physically migrated in the mother project from:
  `Assets/StellarFramework/Editor/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs`
  to:
  `Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs`
  so it remains in Unity's predefined Editor assembly after payload import instead of being captured by `Assets/StellarFramework/StellarFramework.asmdef`.
- Publisher canonical paths and Policy tests were updated to the new bootstrap root.
- Mother-project validation after path migration:
  - Unity **0 Error**
  - Architecture **16/16 PASS**
  - Standalone Export **32/32 PASS**
  - Package Publisher **23/23 PASS**
- Final clean-project diagnostic (important): adding both
  1. `[DidReloadScripts]` scheduling on the bootstrap Installer, and
  2. fallback `Assembly.Load("StellarFramework.Singleton.Editor")` inside `TryGenerateSingletonRegistryIfAvailable()`
  made the clean project automatically update `SingletonRegister.cs` with `CleanBusinessSingleton` while keeping UIKit present; Unity remained **0 Error**.
- **Exact continuation point for next conversation:** those final two proven diagnostic changes currently exist in `StellarFramework-test` and still need to be ported into the mother project's canonical installer:
  `Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs`.
  After porting:
  1. compile mother project / check 0 errors;
  2. run Architecture + Standalone + Publisher policies;
  3. re-export `uikit.complete`;
  4. final 8093 clean import;
  5. confirm business Singleton auto-update still works without manual Generate;
  6. rerun UIKit PlayMode validation and confirm PASS.
- Other completed delivery state in this pass:
  - Localization Complete includes UGUI + TMP production workflow.
  - TMP Essential Resources are not copied into framework payloads.
  - Package Publisher maps `com.unity.textmeshpro -> com.unity.textmeshpro@3.0.7`.
  - real package inspection confirmed no accidental `Assets/TextMesh Pro`, Tests, Packaging implementation, or temporary Verification sources in payloads.
  - UIKit.Adaptation supports `PanelLayoutRegion.FullScreen/SafeArea`, standard UIRoot dual regions, long-edge/short-edge Shape Aspect, and multiple SafeAreaRoots.
- No commit/push/reset/clean was performed.

### 2026-09-22 — Clean-project delivery verification + UIKit/Singleton distribution handoff completed

- Continued exactly from the previous handoff point. The mother project's canonical bootstrap installer is:
  `Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs`.
- Synchronized the proven domain-reload production path back to the mother installer:
  - added `UnityEditor.Callbacks` + `[DidReloadScripts]`;
  - schedules a delayed Singleton registry refresh after script reload and retries while Unity is compiling/updating;
  - confirmed the mother installer already contained the required fallback `Assembly.Load("StellarFramework.Singleton.Editor")`, so no duplicate fallback path was added.
- Strengthened `KitArchitectureMetadataPolicyTests.SingletonKitDistributionOwnsBuildEssentialRegistryBootstrap` to assert `[DidReloadScripts]` and the escaped `Assembly.Load("StellarFramework.Singleton.Editor")` source contract. A previously latent bad string literal in that dirty test source was exposed by a fresh domain compile and fixed before delivery.
- Final mother-project verification after all temporary export helpers were removed:
  - compile: **0 errors / 0 warnings**;
  - `KitArchitectureMetadataPolicyTests`: **16/16 PASS**;
  - `StandaloneSourceExportPolicyTests`: **32/32 PASS**;
  - `PackagePublisherPolicyTests`: **23/23 PASS**;
  - Unity Console: **0 Error**.
- Re-exported `uikit.complete` from the current mother project. Final artifact:
  `BuildArtifacts/StellarFramework/Kits/StellarFramework-Profile-UIKit-Complete.unitypackage`
  with fresh timestamp `2026-09-22T05:47:33Z`; the one-shot exporter used only for this action was deleted immediately afterwards.
- Final 8093 verification used `C:\CodingToolsWorkerCenter\StellarFramework-test` and explicitly removed the previous framework payload, previous generated Singleton registry, and the earlier diagnostic-only `SingletonKitAutoBootstrap` before importing the newly exported UIKit Complete package. The clean validation helper/business source itself was preserved.
- **Business Singleton auto-refresh is now proven without manual Generate:** after the clean import, `Assets/Generated/StellarFramework/SingletonRegister/SingletonRegister.cs` was recreated automatically at `05:50:01` and contains both:
  - `CleanBusinessSingleton` metadata + pure singleton creator;
  - `StellarFramework.UI.UIKit` metadata.
  The old diagnostic `Assets/Editor/StellarFramework/SingletonKitAutoBootstrap.cs` is absent, so this result comes from the distributed production Installer + Singleton editor assembly path rather than the earlier test bridge.
- The freshly imported clean-project Installer was inspected and contains the production `[DidReloadScripts] -> delayCall -> TryGenerateSingletonRegistryIfAvailable()` path. Clean-project compile remained **0 errors / 0 warnings** and Console **0 Error**.
- Re-ran real UIKit PlayMode validation after regenerating the target-project UIRoot through `UIKitEditor.CreateUIRootPrefab()`:
  - PlayMode fixture: **1/1 PASS**;
  - runtime initialized with `ResKitUILoadStrategy`;
  - Root / StaticCanvas / DynamicCanvas present;
  - Static + Dynamic `FullScreenRoot` routing present;
  - Static + Dynamic `SafeAreaRoot` routing present;
  - 20:9 `phone_tall` breakpoint PASS;
  - 4:3 `tablet` breakpoint PASS;
  - portrait/landscape Shape Aspect equivalence PASS.
- Temporary clean-project PlayMode validation asmdef/source and temporary mother-project export trigger/test were removed after evidence was collected. Final 8093 cleanup compile: **0 errors / 0 warnings**, Console **0 Error**.
- The previous UIKit/Singleton distribution handoff is therefore closed. No commit/push/reset/clean was performed; unrelated mother-project dirty worktree state remains preserved.
- Final Git delivery was subsequently authorized by the user and completed on `main`: implementation commit `462b41c` (`feat: finalize localization and UIKit distribution`) was pushed to `origin/main`. Local-only `Assets/Generated/StellarFramework/SingletonRegister` output and `Assets/TextMesh Pro` Essential Resources were intentionally excluded from version control as generated/validation artifacts.

### 2026-09-22 — UIKit dual SafeArea / precise Cutout adaptation + device demo delivery

- User requested keeping the previously delivered conservative Safe Area solution and adding a second, precise solution that can use the screen around a punch-hole/notch/Dynamic Island. Final architecture keeps both instead of replacing one with the other:
  1. **SafeAreaRoot** — rectangular conservative avoidance for ordinary pages/forms/settings/navigation.
  2. **UICutoutAwareLayout** — precise per-target avoidance for HUD/top bars that should keep using the rest of the display edge.
- Added runtime display geometry transport:
  `Assets/StellarFramework/Runtime/Kits/UIKit/Adapters/Adaptation/UIDisplayGeometry.cs`.
  `UIAdaptationController` now samples `Screen.cutouts` together with width/height/safeArea, exposes `CurrentGeometry`, `HasCurrentGeometry`, and `DisplayGeometryChanged`, and only publishes when the geometry actually changes. Existing `SafeAreaRoot` behavior is preserved.
- Added precise runtime layout:
  `Assets/StellarFramework/Runtime/Kits/UIKit/Adapters/Adaptation/UICutoutAwareLayout.cs`.
  It supports:
  - `System`, `Manual`, `SystemAndManual` exclusion sources;
  - normalized manual exclusion zones with orientation filtering;
  - Top / Bottom / Any edge handling;
  - Auto / Horizontal / Vertical movement constraints;
  - reference-pixel cutout/edge padding;
  - cached screen-space solver data; no independent per-frame polling;
  - only configured RectTransform targets that actually overlap an exclusion zone are moved.
- `Tools Hub -> UIKit UI适配` now supports Cutout preview in addition to existing Safe Area / breakpoint preview:
  - None;
  - Center Punch;
  - Dynamic Island;
  - Left Punch;
  - explicit Cutout X/Y/W/H pixel rectangle.
  Preview applies LayoutVariant first and then precise Cutout avoidance, matching runtime ordering.
- `UIKit-界面系统-说明文档-Guide.md` and `KitDistributionCatalog.json` were updated with the two-scheme contract, System Cutouts, PreciseCutoutAvoidance, ManualExclusionZones, CutoutPreview and validation capabilities. `UIKit Complete` continues to include the adaptation package.
- UIKit adaptation EditMode fixture is now **12/12 PASS**, including new evidence for injected display geometry, center-cutout selective movement, corner-punch selective movement, and manual normalized exclusion conversion.
- During clean-project runtime validation, the demo scene exposed an existing ResKit generator edge case: a folder and asset with the same sanitized C# name could produce an illegal member matching its enclosing generated type (`CS0542`). `AssetsMapGenerator` was fixed to reserve the enclosing type name and append an extension/hash when needed. Added `GeneratedMemberNeverMatchesItsEnclosingFolderTypeName`; final `AssetsMapGeneratorTests` are **6/6 PASS**.
- Final mother verification after removing all temporary export helpers:
  - compile: **0 errors / 0 warnings**;
  - `UIKitAdaptationTests`: **12/12 PASS**;
  - `KitArchitectureMetadataPolicyTests`: **16/16 PASS**;
  - `StandaloneSourceExportPolicyTests`: **32/32 PASS**;
  - `PackagePublisherPolicyTests`: **23/23 PASS**.
- Re-exported final `uikit.complete` package containing both Cutout support and the AssetsMap generator fix:
  `BuildArtifacts/StellarFramework/Kits/StellarFramework-Profile-UIKit-Complete.unitypackage`
  - timestamp: `2026-09-22T06:41:17.8735080Z`;
  - size: `167441` bytes.
- Imported that final package into `C:\CodingToolsWorkerCenter\StellarFramework-test`. Final test-project compile and Console are **0 errors / 0 warnings / 0 Error**.
- Added the persistent visual/device validation deliverable in the test project:
  - runtime demo: `Assets/UIKitAdaptationDeviceDemo/UIKitAdaptationDeviceDemo.cs`;
  - scene builder: `Assets/UIKitAdaptationDeviceDemo/Editor/UIKitAdaptationDeviceDemoSceneBuilder.cs`;
  - scene: `Assets/UIKitAdaptationDeviceDemo/UIKitAdaptationDeviceDemo.unity`;
  - scene is enabled in Build Settings;
  - Android `renderOutsideSafeArea` is enabled (`androidRenderOutsideSafeArea: 1`).
- Demo runtime provides buttons for:
  - `A  SafeArea`;
  - `B  Precise Cutout`;
  - `System` (real `Screen.safeArea` + `Screen.cutouts` on device);
  - `Center Punch`;
  - `Dynamic Island`;
  - `Left Punch`.
- Final runtime A/B evidence under simulated Dynamic Island:
  - **Precise Cutout:** Back stayed `(54, -32)`, Coins stayed `(-54, -32)`, while only center Title moved from baseline `Y=-32` to `Y=-182.87085`; Console remained 0 Error.
  - **Safe Area:** `SafeAreaRoot.anchorMax` became `(1, 0.8454797)`, proving the whole top safe rectangle moved below the simulated island; Console remained 0 Error.
  - runtime screenshots were captured under `Assets/Screenshots/` for both final states.
- A real Android Development build was executed successfully from the test project:
  `C:\CodingToolsWorkerCenter\StellarFramework-test\Builds\UIKitAdaptationDeviceDemo.apk`
  - size: `38,966,373` bytes (`37.16 MB`);
  - timestamp: `2026-09-22T06:49:14.5422282Z`;
  - BuildReport: **Player build succeeded for Android**.
- iOS uses the same `Screen.safeArea` / `Screen.cutouts` runtime contract, but no iOS/Xcode build was executed in this Windows validation pass.
- Temporary mother export helper and temporary clean-project AssetsMap regeneration helper were removed. This Cutout task has **not** been committed or pushed yet; previous intentional local generated/TMP artifacts remain outside the commit scope.

### 2026-09-22 — UIKit selectable avoidance policy + production fallback chain + usage docs

- User clarified the production requirement: UI authors must be able to **choose** between whole-safe-region avoidance and precise hazard-only avoidance, while the framework must automatically degrade for old devices, unusual ROMs and platforms that cannot provide reliable Cutout/SafeArea information. This must not require per-brand/per-model UI logic.
- `UICutoutAwareLayout` was extended without renaming the serialized component (to avoid breaking existing Prefab/Scene references). It now exposes author intent:
  - `UIDisplayAvoidanceMode.None` — no hazard avoidance;
  - `UIDisplayAvoidanceMode.SafeArea` — configured Targets are contained inside the rectangular SafeArea (whole-region avoidance intent);
  - `UIDisplayAvoidanceMode.PreciseCutout` — only Targets that intersect a relevant exclusion zone move.
- Added fallback selection:
  - `UIDisplayFallbackMode.Automatic` — production default;
  - `SafeArea`;
  - `EdgePadding`;
  - `None`.
- Added `UIDisplayResolvedMode` for diagnostics and tests:
  - `None`;
  - `SafeArea`;
  - `PreciseCutout`;
  - `EdgePadding`.
  `UICutoutAwareLayout.EffectiveMode` reports the actual strategy selected for the current device geometry.
- Final Automatic chain for `Mode=PreciseCutout`:
  `PreciseCutout -> non-full valid SafeArea -> reference-pixel EdgePadding`.
  If a precise solve fails for a particular Target, the same fallback rules are applied instead of leaving the control intersecting the hazard.
- SafeArea validity uses the existing provider-level `UIDisplayGeometry.SafeAreaDataValid` / `HasSafeAreaInsets` state rather than guessing solely from rectangle shape. Invalid provider data is normalized by `UIAdaptationController` to a safe full-screen rectangle while preserving `SafeAreaDataValid=false`, allowing downstream fallback to select `EdgePadding` reliably.
- Added pure `UICutoutLayoutSolver.CalculateContainmentOffset(...)` for SafeArea / EdgePadding containment. Precise `CalculateOffset(...)` remains the exclusion-zone solver.
- ToolsHub now surfaces every `UICutoutAwareLayout` under the selected root as:
  `Mode / Fallback / Effective`.
  Validator now warns for:
  - active avoidance with zero Targets;
  - `Fallback=None` outside controlled-hardware scenarios;
  - `Source=Manual` with no manual exclusion zones;
  - the existing SafeAreaRoot / CanvasScaler / anchor / breakpoint risks.
- UIKit adaptation EditMode fixture expanded to **17/17 PASS**. New tests cover:
  - SafeArea containment solver;
  - Precise + Automatic -> SafeArea when Cutout data is unavailable but a non-full SafeArea exists;
  - Precise + Automatic -> EdgePadding when only full-screen geometry is available;
  - Precise + Automatic -> EdgePadding when SafeArea provider data is marked invalid;
  - explicit `Mode=SafeArea` selection.
- Final mother regression after policy changes:
  - `UIKitAdaptationTests`: **17/17 PASS**;
  - `AssetsMapGeneratorTests`: **6/6 PASS**;
  - `KitArchitectureMetadataPolicyTests`: **16/16 PASS**;
  - `StandaloneSourceExportPolicyTests`: **32/32 PASS**;
  - `PackagePublisherPolicyTests`: **23/23 PASS**;
  - compile / Console: **0 errors / 0 warnings / 0 Error**.
- Test project demo `Assets/UIKitAdaptationDeviceDemo/UIKitAdaptationDeviceDemo.cs` now explicitly configures `PreciseCutout + Automatic` and adds two old/unsupported-device simulations:
  - `Legacy SafeArea`: no Cutout data, valid non-full SafeArea;
  - `Legacy Unknown`: no Cutout data and invalid SafeArea provider input.
- Real PlayMode verification in `C:\CodingToolsWorkerCenter\StellarFramework-test`:
  - `B Precise Cutout + Legacy SafeArea` => `Mode=PreciseCutout`, `Fallback=Automatic`, **EffectiveMode=SafeArea**;
  - `B Precise Cutout + Legacy Unknown` => **EffectiveMode=EdgePadding**;
  - runtime Console: **0 Error**.
- Rebuilt Android Development APK successfully after the fallback/demo changes:
  `C:\CodingToolsWorkerCenter\StellarFramework-test\Builds\UIKitAdaptationDeviceDemo.apk`
  - size: `39,053,109` bytes (`37.24 MB`);
  - timestamp: `2026-09-22T07:39:08.9495872Z`;
  - BuildReport: **Player build succeeded for Android**;
  - test-project compile: **0 errors / 0 warnings**, Console **0 Error**.
- Documentation was split into three explicit deliverables under `FrameworkDoc/02-Kits/UIKit`:
  - `UIKit-界面系统-使用文档-Guide.md` — new step-by-step author workflow, Mode/Fallback selection, Inspector examples, ToolsHub usage, test-scene instructions and cross-platform rules;
  - `UIKit-界面系统-说明文档-Guide.md` — product/design rules, recommended UI categories and fallback contract;
  - `UIKit-界面系统-源码文档-Guide.md` — implementation types, solver/fallback pipeline, geometry validity and test expectations.
- `uikit.core` distribution now explicitly includes all three UIKit docs. Final `UIKit Complete` export:
  `BuildArtifacts/StellarFramework/Kits/StellarFramework-Profile-UIKit-Complete.unitypackage`
  - timestamp: `2026-09-22T07:45:07.8910618Z`;
  - size: `182,490` bytes.
  The outer package is a Bootstrap wrapper; the embedded payload was inspected directly and contains all three `Assets/StellarFramework/FrameworkDoc/02-Kits/UIKit/...Guide.md` paths.
- Temporary export helpers were deleted after use. No commit/push/reset/clean was performed for this batch.
- Before final Git delivery, an explicit orientation regression was added for precise top-cutout avoidance using both `2400x1080` landscape and `1080x2400` portrait geometry. Final `UIKitAdaptationTests` are **18/18 PASS**, and mother-project compile remains **0 errors / 0 warnings**.
