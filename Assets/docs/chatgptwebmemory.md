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
- User authorized final Git delivery after confirming real-device behavior. Implementation commit `51113cd` (`feat: add robust UIKit display avoidance`) was pushed to `origin/main`. The commit includes the selectable SafeArea/PreciseCutout policy, Automatic fallback pipeline, orientation regression, ToolsHub validation, AssetsMap collision fix, three UIKit docs and distribution metadata. Local-only `Assets/Generated` and `Assets/TextMesh Pro` validation artifacts remain intentionally untracked.

### 2026-09-22 — UIAdaptationKit split from UIKit and standalone delivery

- User clarified the product goal: developers must be able to take only the display/UI adaptation capability without importing UIKit, while end users should receive automatic device adaptation with no model-specific setup.
- The former `Runtime/Kits/UIKit/Adapters/Adaptation` implementation was physically split into the independent `Runtime/Kits/UIAdaptationKit` Kit.
- Existing runtime script `.meta` GUIDs and namespace `StellarFramework.UI.Adaptation` were preserved during the move so existing scenes/prefabs/code do not lose MonoScript references merely because the Kit changed ownership.
- Runtime assembly is now `StellarFramework.UIAdaptationKit` and references only `UnityEngine.UI`; it no longer references `StellarFramework.UIKit`, ResKit, SingletonKit, ToolsHub, or other StellarFramework runtime Kits.
- ToolsHub integration moved to `Editor/StellarToolsHub/Modules/UIAdaptationKit` with assembly `StellarFramework.ToolsHub.UIAdaptationKit.Editor`. It depends on ToolsHub + UIAdaptationKit, not UIKit.
- ToolsHub module was renamed to `UIAdaptationKit` and gained a one-click standalone authoring path that creates `Canvas + CanvasScaler + UIAdaptationController + FullScreenRoot + SafeAreaRoot` and a recommended profile when needed.
- Distribution catalog now exposes:
  - `uiadaptation.core` -> `StellarFramework-UIAdaptationKit-Core.unitypackage`;
  - `uiadaptation.tools` -> runtime + ToolsHub preview/validator;
  - `uiadaptation.complete` -> `StellarFramework-Profile-UIAdaptationKit-Complete.unitypackage`.
- `UIKit.Core` remains independently usable and does not require UIAdaptationKit. `UIKit Complete` now composes `uiadaptation.tools` rather than owning the adaptation implementation.
- Legacy catalog IDs `uikit.adaptation` / `uikit.adaptation.tools` remain as compatibility aliases, but their source/dependency closure points to the independent UIAdaptationKit and no longer requires UIKit.Core.
- UIAdaptationKit documentation now lives under `FrameworkDoc/02-Kits/UIAdaptationKit` with separate usage, design/description, and source documents. UIKit docs now describe UIAdaptationKit only as an optional integration.
- Distribution/architecture/ToolsHub/validation docs and policy tests were updated to use the new independent Kit semantics.
- Mother-project validation after the split:
  - compile: **0 errors / 0 warnings**;
  - `UIKitAdaptationTests`: **18/18 PASS**;
  - `KitArchitectureMetadataPolicyTests`: **16/16 PASS**;
  - `StandaloneSourceExportPolicyTests`: **32/32 PASS**;
  - `PackagePublisherPolicyTests`: **23/23 PASS**.
- Clean-project standalone proof in `C:\CodingToolsWorkerCenter\StellarFramework-test`:
  - imported only `UIAdaptationKit Complete` after deleting `Assets/StellarFramework`;
  - verified `UIAdaptationKit=true` while `UIKit=false`, `UIKit.prefab=false`, `SingletonKit=false`, `ResKit=false`;
  - compile **0 errors / 0 warnings**, Console **0 Error**;
  - PlayMode: Dynamic Island => `EffectiveMode=PreciseCutout`; Legacy SafeArea => `SafeArea`; Legacy Unknown => `EdgePadding`;
  - Android standalone build succeeded: `Builds/UIAdaptationKitStandalone.apk`.
- Clean-project `UIKit.Core` proof:
  - `UIKit=true`, `UIAdaptationKit=false`, `ResKit=false`;
  - compile **0 errors / 0 warnings**, Console **0 Error**.
- Clean-project `UIKit Complete` proof:
  - UIKit + UIAdaptationKit + ResKit closure imported together;
  - compile **0 errors / 0 warnings**, Console **0 Error**;
  - Dynamic Island PlayMode still resolved `PreciseCutout + Automatic -> EffectiveMode=PreciseCutout`.
- Final test-project state was intentionally returned to standalone UIAdaptationKit-only so opening `StellarFramework-test` demonstrates the independent Kit rather than the UIKit combination.
- Final exported artifacts after the split:
  - `StellarFramework-UIAdaptationKit-Core.unitypackage` = `24,947` bytes;
  - `StellarFramework-Profile-UIAdaptationKit-Complete.unitypackage` = `87,783` bytes;
  - `StellarFramework-UIKit-Core.unitypackage` = `62,935` bytes;
  - `StellarFramework-Profile-UIKit-Complete.unitypackage` = `186,163` bytes.
- Temporary export helpers were removed.
- Final delivery was authorized after re-checking both integration directions:
  - standalone dependency guide contains only `ToolsHub.Core + UIAdaptationKit.Core + UIAdaptationKit.Tools + com.unity.ugui` and no UIKit/SingletonKit/ResKit;
  - `UIKit Complete` dependency guide explicitly contains both UIKit and UIAdaptationKit closures;
  - current standalone test project remained `UIAdaptationKit=true / UIKit=false / SingletonKit=false / ResKit=false` with **0 errors / 0 warnings**;
  - mother project remained **0 errors / 0 warnings**.
- Implementation commit `8ce7a13` (`feat: split UIAdaptationKit from UIKit`) was pushed to `origin/main`.

### 2026-09-22 — Final UIKit Complete clean-project revalidation and Singleton docs sync

- Revalidated the latest `StellarFramework-Profile-UIKit-Complete.unitypackage` from a clean `C:\CodingToolsWorkerCenter\StellarFramework-test` state after the Bootstrap/Singleton delivery refinements.
- Fresh import proved all required first-install pieces together: `UIKit.cs=true`, `Resources/Managers/UIKit.prefab=true`, `Assets/Generated/StellarFramework/SingletonRegister/SingletonRegister.cs=true`, UniTask restored in the target manifest, Unity compile/Console **0 Error**.
- Added a target-project-only `CleanBusinessSingleton : ISingleton` with `[Singleton("", Global, true)]` and did **not** call the manual Generate action. After script reload the generated registry automatically added both its metadata and pure creator while retaining UIKit metadata; Unity remained **0 Error**.
- Final UIKit PlayMode validation ran for 6 seconds with **0 runtime errors** and wrote PASS for: SingletonRegister, business Singleton auto-refresh, `ResKitUILoadStrategy`, UIRoot generation, SafeArea/FullScreen panel routing, 20:9 phone_tall breakpoint, 4:3 tablet breakpoint, dual SafeArea roots, and orientation-independent shape aspect.
- Corrected formal Singleton/Generated documentation to the real output path `Assets/Generated/StellarFramework/SingletonRegister/SingletonRegister.cs` and the current lifecycle: first Kit import generation, `DidReloadScripts` automatic refresh, build-preprocess final generation, ToolsHub manual fallback, no fixed `StellarFramework.Generated.SingletonRegister.asmdef`.
- This documentation-sync batch did **not** commit, push, reset, clean, or delete the local generated/TMP validation artifacts.

### 2026-09-22 — Framework consolidation + maturity model + full Catalog audit

- User approved the next framework phase in this order: **consolidation cleanup -> maturity model -> full Catalog audit**. Work was performed without changing Runtime feature behavior.
- Consolidation cleanup:
  - `KitExportValidationMatrix.md` was reframed as the historical Evidence Ledger; current status now lives in `FrameworkDoc/08-Validation/ValidationCurrentStatus.md`.
  - Removed stale/empty FrameworkDoc folder metas left by retired layouts (`05-Tests`, `06-Resources`, `06-RuntimeTools`, obsolete `03-Samples/FlowKit`).
  - Export window no longer exposes an empty `Sample` page; `GetSourceProjectSampleProfiles()` was removed because Catalog currently has zero sample Profiles.
  - `KitDistributionGuide.md` no longer documents the retired `Export -> 样例包` workflow. Current teaching strategy is one ArchitectureDemo + per-Kit Guide + automated tests/Verification.
  - Added `FrameworkDoc/07-Distribution/BuildArtifactsGuide.md` and safe Export-window cleanup for clearly named Legacy/Validation artifacts while protecting current Catalog/Recommended outputs.
- Maturity model:
  - Catalog schema upgraded to **v4**.
  - Every atomic Profile now declares `maturity = stable / rc / experimental`, independent from `availability`.
  - Publisher validates maturity, prevents a Profile from outranking a dependency, and Recommended Profiles derive the worst maturity across their full closure.
  - Export UI shows maturity badges and allows maturity search.
  - Current conservative distribution: **78 Stable / 3 RC / 2 Experimental**.
  - RC: `httpkit`, `reskit.addressables`, `reskit.yooasset`.
  - Experimental: `hybridclrkit`, `hybridclrkit.tools`.
  - Recommended maturity: Localization / ResKit / UIAdaptationKit / UIKit Complete = Stable; Hot Update Full = Experimental.
- Full Catalog audit fixes:
  - Removed stale `toolshub.core` source path for deleted `WorkflowHubModules.cs`.
  - Added missing YooAsset bootstrap install source using the mother project's YooAsset `2.3.19` Git UPM source.
  - Added explicit `documentationPaths` to Runtime Profiles; all **59 Runtime Profiles** now ship formal documentation with standalone packages.
  - `toolshub.core` now also ships `FrameworkDoc/04-ToolsHub`, so standalone ToolsHub delivery has a formal Guide.
  - Legacy `uikit.adaptation*` IDs remain resolvable but are hidden from normal Export picker UX.
  - All Tooling Profiles now explicitly exclude `PlayerRuntime`; audit found and fixed missing metadata on `toolshub.core`, `flowkit.tools`, and `savekit.tools`.
- New `KitCatalogAuditPolicyTests` permanently gate:
  - source/documentation path existence;
  - duplicate profile IDs / outputs;
  - missing requiredProfileIds;
  - required UPM install-source coverage;
  - maturity inversion;
  - legacy alias picker hiding;
  - internal asmdef references covered by Catalog dependency closure;
  - human-readable `requiredKits` exactly matching machine `requiredProfileIds`;
  - Tooling source paths staying Editor-only and excluding PlayerRuntime;
  - Runtime/Tooling Profiles having formal documentation somewhere in their closure.
- Full audit result: no current P0 Catalog structure gap. `runtime.tools` / `StellarFramework.Runtime.Tools` was identified as a **P2 Legacy Candidate**: it contains only `CoroutineRunner`, has no framework caller and no dependent Profile, but was intentionally not deleted because its Catalog ID may still be used externally.
- Current final structural counts remain:
  - atomic Profiles: **83**;
  - Recommended Profiles: **5**;
  - kind: 38 kit-with-dependencies / 19 tooling / 21 kit / 2 single-file / 2 shared-runtime / 1 generated-support;
  - tier: 21 Foundation / 11 Extension / 27 Adapter / 24 non-tier.
- Final regression after consolidation/maturity/audit:
  - Unity compile: **0 errors / 0 warnings**;
  - FrameworkValidation EditMode: **583/583 PASS**, 0 failed, 0 skipped;
  - PlayMode `StellarFramework`: **13/13 PASS**, 0 failed, 0 skipped;
  - Catalog Audit policy: **7/7 PASS**;
  - PackagePublisher policy: **25/25 PASS**;
  - Architecture policy: **16/16 PASS**;
  - Standalone source/export policy: **32/32 PASS**.
- External release gates are intentionally not promoted by mother-project tests: Http real network environments, Addressables remote catalog/bundles, YooAsset Host/Offline/update/download flows, HybridCLR IL2CPP/AOT/hot DLL execution, and platform-specific Android/iOS/HarmonyOS builds still require target-environment evidence.
- This batch has **not** been committed or pushed yet. Existing local `Assets/Generated` and `Assets/TextMesh Pro` artifacts remain intentionally excluded from source-control delivery.

### 2026-09-22 — RuntimeTools.Core first production batch

- User asked to evolve StellarFramework's tool layer using `https://github.com/StarrDream/Utils` as a candidate pool, with explicit constraints: do **not** duplicate existing framework Kits, reject low-value/chicken-rib utilities, only accept tools that are valuable, implementable well, usable well, and document/comment them completely; add ToolsHub authoring/diagnostics only where it materially helps.
- Audited the current Utils remote `main` (`ccd205e...`) and chose **not** to copy the repository wholesale. Rejected direct migration of ObjectPool (PoolKit), Timer/DelayedAction (TimeKit), ScreenFader (UIKit/flow), MiniFPSController/AimRaycast/DynamicScaleLimiter/TriangularScanMesh (too demo/business-specific), URPTransparencyController (future optional adapter only), and CombinedMeshCollider (framework already owns Editor-side tooling).
- Preserved historical Catalog id/output compatibility: `runtime.tools` still resolves and still outputs `StellarFramework-Runtime-Tools.unitypackage`, but it is now formally `RuntimeTools.Core` with `tier=extension`, `category=infrastructure`, `maturity=stable`. Added `runtimetools.tools` / `RuntimeTools.Tools` as the Editor-only ToolsHub Profile.
- RuntimeTools.Core remains zero-dependency relative to StellarFramework Kits: `StellarFramework.Runtime.Tools.asmdef` still has `references=[]`. New runtime APIs use namespace `StellarFramework.RuntimeTools`; historical `CoroutineRunner` remains in namespace `StellarFramework` for source compatibility.
- First accepted tool set:
  - Core: `WeightedRandom`, `TransformSnapshot`, `TransformUtil`, `RandomPointUtil`;
  - Transform: `FollowTarget`, `Rotator`, `UniversalBillboard`;
  - Physics: `PhysicsProbe`, `GroundChecker`, `BoundsUtility`, `PhysicsRelayFilter`, `TriggerRelay`, `CollisionRelay`;
  - compatibility: existing `CoroutineRunner`.
- Implementation quality details:
  - WeightedRandom avoids LINQ/temp work lists, validates invalid weights, accumulates with double, and exposes deterministic sample input for tests/replay;
  - FollowTarget uses frame-rate-independent exponential smoothing and explicit position/rotation/scale channels;
  - PhysicsProbe unifies Ray/Sphere/Box/Capsule Casts without hit-list allocation;
  - GroundChecker reuses PhysicsProbe and supports stableFrames plus explicit Probe/Evaluate APIs;
  - BoundsUtility provides convenience overloads and reusable scratch-list overloads for low-GC repeated calls;
  - Trigger/Collision Relay directly forwards Unity Collider/Collision objects instead of allocating custom EventData.
- ToolsHub `Runtime Tools` module added under its own Editor asmdef. It provides Transform Snapshot capture/restore, Reset Local, quick-add for common RuntimeTools components, Renderer/Collider Bounds Diagnostics, and basic selection risk checks (missing FollowTarget target, TriggerRelay without trigger Collider, CollisionRelay without Collider). Runtime never references ToolsHub.
- Formal RuntimeTools documentation now lives in `FrameworkDoc/02-Kits/RuntimeTools`:
  - `RuntimeTools-使用文档-Guide.md`;
  - `RuntimeTools-说明文档-Guide.md`;
  - `RuntimeTools-源码文档-Guide.md`.
  The old architecture-corner RuntimeTools doc was moved into the Kit documentation area while preserving its meta GUID.
- Tests/evidence:
  - focused RuntimeTools EditMode: **11/11 PASS**;
  - focused RuntimeTools PlayMode real physics: **2/2 PASS** (Trigger Enter/Exit + Collision Enter);
  - full FrameworkValidation after productization: **594/594 PASS**, 0 failed, 0 skipped;
  - full PlayMode after test-hardening: **15/15 PASS**, 0 failed, 0 skipped;
  - Unity compile: **0 errors / 0 warnings**, Console **0 Error**.
- Full PlayMode initially exposed an old ArchitectureDemo test fragility: `MainPanelCanCloseReopenAndKeepModelState` assumed UIKit async open completes in exactly two frames. Single rerun passed. The test was changed only to wait up to 60 frames for the panel to appear, preserving timeout failure and leaving UIKit/Demo runtime logic unchanged. Full suite then passed 15/15.
- Formal exported artifacts:
  - `StellarFramework-Runtime-Tools.unitypackage` = **24,187 bytes**;
  - `StellarFramework-Runtime-Tools-Tools.unitypackage` = **92,196 bytes**.
- Actual payload audit:
  - Core payload = 18 paths, contains RuntimeTools + formal docs and **does not contain TimeKit / PoolKit / UIKit / ResKit / SingletonKit / ToolsHub**;
  - Tools payload = 42 paths, contains RuntimeTools + ToolsHub.Core + RuntimeTools Tools module and still does **not** contain TimeKit / PoolKit / UIKit / ResKit.
- Catalog after RuntimeTools productization: **84 atomic Profiles + 5 Recommended**; kind = 38 kit-with-dependencies / 20 tooling / 22 kit / 2 single-file / 1 shared-runtime / 1 generated-support; tier = 21 Foundation / 12 Extension / 27 Adapter / 24 non-tier; maturity = **79 Stable / 3 RC / 2 Experimental**.
- Clean-project RuntimeTools.Core import remains **NOT RUN** for this batch. `StellarFramework-test` is currently a mixed UIKit/AngryBirds validation project, and its UnitySkills 8093 listener did not restore even though the UnitySkills package itself exists. We did not disable/delete unrelated tests to manufacture a clean-import PASS. Formal payload closure plus mother-project policies are proven; a true clean import still requires a dedicated minimal validation project or a restored isolated test instance.
- This RuntimeTools batch is **not committed or pushed yet**. Preserve local `Assets/Generated` and `Assets/TextMesh Pro` validation artifacts as intentionally untracked.

### 2026-09-22 — RuntimeTools.Core second production batch

- Continued the RuntimeTools audit against current `https://github.com/StarrDream/Utils` instead of bulk-copying utilities. The second batch was deliberately capped at four additions after re-checking overlap with existing StellarFramework Kits.
- Added `FrameRateSampler` + `FrameRateMonitor`:
  - fixed-size ring-buffer FPS statistics;
  - no OnGUI / Texture / presentation ownership;
  - invalid/non-positive delta times are ignored;
  - `FrameRateMonitor` only provides Unity lifecycle driving plus an `Updated` event.
- Added `PhysicsOverlap`:
  - Sphere / Box / Capsule factories;
  - unified `Overlap*NonAlloc` calls;
  - caller-owned `Collider[]` result buffer;
  - no internal resizing or hidden collection allocation.
- Added `TransformShake` as the production replacement for the Utils CameraShake idea:
  - generalized to any visual Transform/Pivot instead of requiring Camera;
  - deterministic fixed seed and local noise time, no consumption of UnityEngine.Random global state;
  - position + rotation trauma shake, configurable decay/exponent;
  - public Tick / AddTrauma / SetTrauma / Stop / RecaptureBaseTransform;
  - docs explicitly recommend a dedicated child ShakePivot when movement logic also owns transforms.
- Added `RendererPropertyBlockController`:
  - updates per-renderer Float/Int/Color/Vector/Texture through reusable `MaterialPropertyBlock`;
  - reads existing property block before mutation so unrelated properties are preserved;
  - never touches `renderer.material`;
  - material-slot validation uses reusable `List<Material>` + `GetSharedMaterials`, avoiding `sharedMaterials` array allocation in repeated calls.
- RuntimeTools ToolsHub now additionally provides:
  - Quick Add for TransformShake / FrameRateMonitor / RendererPropertyBlockController;
  - live PlayMode Current/Avg/Min/Max FPS diagnostics;
  - PropertyBlock material-slot validation.
- Explicitly rejected remaining Utils candidates for Core migration after source audit:
  - CameraFreeLook / SimpleDragTrigger3D / MouseFollower: hard-coded legacy Input and project interaction policy;
  - CameraScreenshot: filesystem/platform/debug workflow; Unity already provides ScreenCapture primitives;
  - GizmoDrawer: Editor visualization rather than Player Runtime infrastructure;
  - RaycastTool: duplicates PhysicsProbe while mixing Gizmo, logs, tag list and state machine;
  - ColliderEventObj: mixes legacy input, UI blocking, 2D/3D raycasts and allocates event data objects per interaction;
  - ParallaxEffect: specific visual presentation policy;
  - TextTypewriter: legacy UGUI Text/AudioSource coupling plus per-character string concatenation GC;
  - CollapsibleItem / UIDragger / UIInputTrigger: concrete UGUI widget/interaction policy;
  - UGUIFollowTarget/Manager: the need is valid, but current design uses a global manager singleton and mixes visibility, smoothing and Canvas mapping; only reconsider later as a redesigned optional UI adapter;
  - MathUtil: mostly thin Unity API aliases with insufficient value-to-API-surface ratio.
- RuntimeTools focused EditMode is now **17/17 PASS**.
- Added exact managed-allocation gates after warmup:
  - 1000 x `FrameRateSampler.PushFrame` = **0 bytes** via `GC.GetAllocatedBytesForCurrentThread()`;
  - 1000 x `PhysicsOverlap.QueryNonAlloc` = **0 bytes**.
- Targeted policy regression remains fully green:
  - Catalog Audit **7/7**;
  - Architecture Metadata **16/16**;
  - Package Publisher **25/25**;
  - Standalone Source Export **32/32**.
- Full final mother-project regression after batch 2:
  - compile **0 errors / 0 warnings**;
  - FrameworkValidation EditMode **600/600 PASS**, 0 failed, 0 skipped;
  - full StellarFramework PlayMode **15/15 PASS**, 0 failed, 0 skipped;
  - Console Error **0**.
- Final exported artifacts after re-exporting with the matching latest RuntimeTools Guides:
  - `StellarFramework-Runtime-Tools.unitypackage` = **32,681 bytes**;
  - `StellarFramework-Runtime-Tools-Tools.unitypackage` = **103,243 bytes**.
- Final payload closure remains clean:
  - Core payload = **22 paths**, includes RuntimeTools + RuntimeTools Guides and excludes TimeKit / PoolKit / UIKit / ResKit / SingletonKit / ToolsHub;
  - Tools payload = **46 paths**, adds only ToolsHub.Core + RuntimeTools Tools and still excludes those unrelated Runtime Kits.
- Clean-project RuntimeTools import remains **NOT RUN** because the existing `StellarFramework-test` is a mixed UIKit/AngryBirds validation workspace and its 8093 UnitySkills listener did not restore. No unrelated validation scripts were disabled/deleted to manufacture a clean-import PASS.
- Batch 2 is **not committed or pushed yet**. Keep `Assets/Generated` and `Assets/TextMesh Pro` out of source-control delivery as before.

### 2026-09-23 — Android Release Verification Pipeline complete

- Continued the dedicated Android automation setup without changing Unity Hub's embedded SDK/JDK. Android CLI uses `C:\Android\Sdk`; user environment is persisted as `JAVA_HOME=C:\Program Files\Microsoft\jdk-17.0.20.101-hotspot`, `ANDROID_SDK_ROOT=C:\Android\Sdk`, `ANDROID_HOME=C:\Android\Sdk`, with platform-tools/emulator/cmdline-tools on the user PATH.
- Android SDK licenses are accepted. Installed dedicated packages are platform-tools 37.0.1, emulator 37.1.11, build-tools 35.0.0, platform android-35 and `system-images;android-35;google_apis;x86_64`.
- Dedicated AVD `StellarFramework_API35` is Android 15 / API 35 / Google APIs / x86_64, 1080x2400 @ 420 dpi, 2 GB requested RAM, 4 cores, 10 GB data partition. `emulator-check accel` reports `WHPX(10.0.28000) is installed and usable`; real boot reached `sys.boot_completed=1`. No further Windows reboot is required for the current machine state.
- Added/finished `Tools/AndroidVerification/`: `Common.ps1`, environment/start/stop helpers, `Invoke-StellarApkSmoke.ps1`, `Invoke-StellarAndroidReleaseVerification.ps1`, and README. The scripts identify the emulator by AVD name instead of targeting the first ADB device, so attached PICO/phones are not selected accidentally.
- Added project-local `Assets/Editor/StellarFrameworkAndroidReleaseVerificationBuild.cs`. It builds only `FrameworkArchitecture_Playable.unity` as a non-Development IL2CPP x86_64 APK, restores the previous Android backend/architecture/development/AppBundle settings in `finally`, and writes build handshake state to `Library/StellarFramework/AndroidVerification/android-build-state.json` rather than beside the APK.
- Two integration bugs were found and fixed during live validation: build-state DTO used `uint` while Unity 2022.3 `BuildSummary.totalErrors/totalWarnings` are `int` (CS0266), and an Editor `delayCall` wrapper did not reliably fire through UnitySkills. Final menu entry executes `BuildRelease()` synchronously; the PowerShell UnitySkills request timeout is tied to the configured build timeout.
- After fixing CS0266, forced `Assets/Refresh` produced Tundra build success + assembly/domain reload. Unity Console was cleared and read back as **0 logs / 0 warnings / 0 errors** before the final pipeline run.
- Final end-to-end run used the open UnitySkills `http://localhost:8090` instance `StellarFramework_DEEE9F8A` / Unity 2022.3.62f3c1 and passed: Unity Release Build -> API35 Emulator -> ADB install -> clear app data -> cold launch -> process survival -> logcat -> screenshot -> force-stop/restart -> second process/log validation -> PASS.
- Final build state: **PASS / Succeeded / 0 errors / 0 warnings**, output `Builds/AndroidVerification/StellarFramework-ArchitectureDemo-x86_64-release.apk`; APK contains x86_64 `libil2cpp.so`/`libunity.so` and is not marked debuggable.
- Final pipeline result: `Tools/AndroidVerification/Results/20260923-110122/pipeline-result.json` = **PASS**, `buildMode=UnitySkills`, `smokeResult=PASS`; smoke `result.json` has no failures, first PID 7895, restart PID 8028. Artifacts include full/app-scoped logcat before and after restart, package/activity dumps, and `screen.png`.
- Existing dirty working tree was preserved. No reset/clean/commit/push was performed.

### 2026-09-23 — Android Verification Harness productization plan + environment shutdown

- Added formal plan `Assets/StellarFramework/FrameworkDoc/09-Development/Plans/2026-09-23-android-release-verification-harness-plan.md` instead of reopening the already sealed P13 milestone.
- The plan keeps the proven Android Release Pipeline as the baseline and schedules productization into a reusable Android Verification Harness: configurable profiles, Runtime Verification API, Android Kit gates, ToolsHub/Editor entry points, cross-project UPM-style reuse, CI/release integration, and explicit real-device boundaries.
- Cleanup/shutdown is now a required first-class feature in the plan. It must distinguish: `Stop Target Emulator`, `Stop ADB Server`, `Clean Generated Verification Data`, and one-click `Shutdown & Clean Android Verification Environment`.
- Safety requirements recorded: target emulator is resolved by AVD name; ADB shutdown is explicit because the ADB server is machine-global; post-`adb kill-server` verification must not call `adb devices` and accidentally restart the server; generated-data cleanup is strictly scoped to Harness-owned build/state/result paths and must never use broad `git clean`/project deletion.
- Per user request, after writing the plan the current Android test environment was shut down immediately. `StellarFramework_API35` was stopped; two lingering emulator helper processes were terminated; the Android SDK ADB server was killed and a final process-level check reported `ANDROID_ADB_AND_EMULATOR_STOPPED` with no `adb.exe`, `emulator.exe`, or `qemu-system-x86_64.exe` remaining.
- No commit/push was performed.

### 2026-09-23 — Reusable UIAdaptation / HotUpdate hardening Agent plan

- After a fresh 8090 audit, user accepted the conclusion that LocalizationKit and UIAdaptationKit are already suitable for independent reuse, while Hot Update Full needs final cross-project/platform hardening before being treated as long-term Stable.
- Added formal Agent execution plan: `Assets/StellarFramework/FrameworkDoc/09-Development/Plans/2026-09-23-reusable-capabilities-hardening-agent-plan.md`.
- UIAdaptation action is intentionally narrow: fix the cutout-only automatic refresh gap without polling `Screen.cutouts` every frame; use a throttled/low-GC detector, preserve manual refresh, add regression coverage, and keep the Kit independent from UIKit/ResKit/SingletonKit.
- HotUpdate hardening keeps the existing architecture (`ResKit.YooAsset` content phase -> `HybridCLRKit` code phase). Do not restore a mega HotUpdateKit or couple HybridCLRKit directly to YooAsset.
- Distribution reproducibility action: current UniTask resolves to 2.5.11 / commit `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`, but manifest + Package Publisher currently use an unpinned Git URL. Plan requires pinning the verified commit/tag and adding a policy that exported third-party Git UPM dependencies cannot float.
- PlayMode release-gate action: current `YooAssetHotUpdateEndToEndTests` is not present in PlayMode discovery because its assembly is coupled to Editor-only verification/YooAsset.Editor. Plan requires splitting Editor preparation from Runtime PlayMode verification so the release gate is discoverable and can be run explicitly without contaminating the normal 15-test PlayMode suite.
- Android action extends the already-proven `Tools/AndroidVerification` harness instead of creating another ADB stack. It must regenerate Android-target HybridCLR/AOT artifacts, build an Android YooAsset verification package, run a Release IL2CPP APK through Emulator + local verification CDN/adb reverse, and prove real Android metadata load + HotUpdate DLL load + entry execution with structured evidence.
- Stable promotion remains evidence-driven: UIAdaptation stays Stable after the focused fix; ResKit.YooAsset can be considered for Stable after Android + clean-consumer evidence; HybridCLRKit should move Experimental -> RC first after Android/PlayMode closure, then RC -> Stable only after Windows64 + Android Release gates and clean consumer import all pass.
- Plan also requires clean consumer import validation for `UIAdaptationKit Complete`, `Localization Complete`, and `Hot Update Full`; mother-project policy tests alone are not treated as final proof that another project can import and run the packages.
- No implementation of these hardening items has been started in this entry; this checkpoint only records the agreed execution plan. Existing dirty worktree must be preserved; no reset/clean/commit/push unless explicitly requested.

### 2026-09-23 — Reusable capability hardening P1 sealed

- Continued directly from `2026-09-23-reusable-capabilities-hardening-agent-plan.md`; preserved existing dirty worktree. Current UnitySkills instance `StellarFramework_DEEE9F8A` on Unity 2022.3.62f3c1 is healthy at `http://localhost:8090/`.
- Pre-change UIAdaptation focused baseline was run through Unity Test Runner: **18/18 PASS**, 0 failed/skipped (`UIKitAdaptationTests`, EditMode).
- P1 implementation now throttles system `Screen.cutouts` probes to one every 0.5 seconds using `Time.realtimeSinceStartupAsDouble`; width/height/raw-safeArea comparisons remain per-frame and trigger an immediate single screen snapshot. The cutout detector compares each normalized rectangle against cached geometry without an intermediate collection, ignores invalid/fully clipped inputs, and passes the one probed array directly into geometry application when changed.
- Preserved explicit `Apply(...)` input for external geometry adapters and retained `RefreshDisplayGeometry()` as the immediate system refresh path; refreshing switches back to system cutout polling.
- Added focused detector and invalid/clipped geometry regression tests; updated UIAdaptationKit usage/overview/source Guides and the UIKit overview to describe the actual sampling cadence.
- UnitySkills discovery was explicitly refreshed after the first cached listing remained stale. Fresh EditMode discovery reported **1,558 tests** and included all 3 new cutout regressions.
- Post-change validation: UIAdaptation focused **21/21 PASS**; FrameworkValidation namespace release set **603/603 PASS**; regular StellarFramework PlayMode **15/15 PASS**; all with 0 failed and 0 skipped. Unity compile/update idle with **0 compilation errors**.
- Post-test Console inspection found one UnitySkills internal `Thread was being aborted` response-fallback log; the completed Unity Test Runner results were all present and complete. After reviewing that tool-only history, Console was cleared and read back as **0 errors / 0 warnings**. This did not create a `TOOLING EVIDENCE GAP` because every requested test result was obtained.
- Code review confirms `Screen.cutouts` appears only in immediate screen snapshot collection and the `LateUpdate` branch guarded by the 0.5-second realtime deadline; cutout-only probes read once, compare in place against cached normalized geometry, and reuse the same snapshot when applying. No per-frame cutout read was introduced.
- P1 is **SEALED**. UIAdaptationKit remains Stable. Next authorized phase is P2; do not skip phase ordering.

### 2026-09-23 — Reusable capability hardening P2 sealed

- Began P2 only after the P1 focused, FrameworkValidation, PlayMode, compile, and Console seals passed. Existing dirty Publisher/Catalog/test changes are preserved; edits are limited to the UniTask source entries and pin policy.
- UniTask in `Packages/manifest.json`, Package Publisher dependency sources, and the separately shipped Bootstrap installer now target the already-resolved commit `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`; current `packages-lock.json` already records that hash. No package upgrade was selected.
- Added Publisher source classification and validation for Git UPM refs: Git URLs must use a semantic version tag or commit hash; absent/branch refs fail. Unity Registry `package@version` specs are classified separately. Catalog loading now rejects configured floating Git UPM sources.
- Added policy coverage for floating/moving refs, tag and commit pins, Registry non-Git versions, Publisher configuration, Bootstrap GitUrl entries, and the exact UniTask pin at each install entry point.
- Documented the tag/commit rule, Registry distinction, UniTask version/commit, and Bootstrap consistency in `FrameworkDoc/07-Distribution/KitDistributionGuide.md`.
- Unity Package Manager resolved the edited manifest: `com.cysharp.unitask` is installed as **2.5.11**, direct dependency, with lock hash **e5acc106ee196bc5a32fb14cdf2987b0f96d11e0**. Lock/manifest comparison against the starting tree confirms **only com.cysharp.unitask changed**; no other package was upgraded.
- Fresh EditMode discovery = **1,565 tests**. Focused `PackagePublisherPolicyTests` = **32/32 PASS**; FrameworkValidation = **610/610 PASS**; regular PlayMode = **15/15 PASS**. Compile idle, Console **0 errors / 0 warnings**.
- The full FrameworkValidation/PlayMode regression exercised the existing ResKit/SaveKit paths against the unchanged UniTask 2.5.11 package; ActionKit/HttpKit also compiled against it. No separate ActionKit or HttpKit test scripts exist in this workspace.
- P2 is **SEALED**. Next phase is P3; do not skip phase ordering.

### 2026-09-23 — Reusable capability hardening P3 implementation in progress

- Began P3 only after P2's focused, FrameworkValidation, PlayMode, compile, and Console seals passed. Existing dirty worktree remains preserved.
- Added shared Runtime verification constants and project-local paths in `Assets/StellarFrameworkVerification/Runtime/HotUpdateVerificationPaths.cs`; Editor Prepare and PlayMode Runtime Gate now share package/version/config/result paths without a Runtime-to-Editor dependency.
- Split Editor preparation from the Runtime gate. The builder exposes `Tools/StellarFramework/Verification/Prepare HotUpdate PlayMode Release Gate`; the Runtime bootstrap no longer consumes the Editor Test Runner's prepared config while in Editor PlayMode.
- Replaced the previous Editor-coupled gate source with a Runtime-only `StellarFramework.Tests.ReleaseGate` test and changed its asmdef to include all platforms while referencing only Verification.Runtime and UniTask. Missing prepared config/package reports `precondition missing`.
- Added a FrameworkValidation policy test for the Runtime-only PlayMode assembly boundary, shared constants, and the Prepare menu contract. Updated Verification README with the Prepare → fresh PlayMode discovery → exact Gate → result polling procedure.
- Unity compilation is idle with no reported compile failure; focused `VerificationSurfacePolicyTests` passed **4/4**. Editor `project_get_build_settings` confirmed the active target was **Android**, and Prepare wrote the package under `Temp/StellarHotUpdateVerification/Bundles/Android`; the RemoteCDN copy had 13 files / 6 bundles, including a **1,062,593-byte** bundle needed to exercise Range resume. This Editor PlayMode result does not substitute for P4's required Android HybridCLR/AOT metadata, Manifest, verification package regeneration, or Release IL2CPP Player E2E.
- Fresh Unity Test Runner PlayMode discovery reported **17 tests** and contained the exact `StellarFramework.Tests.ReleaseGate.YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate` test as Runnable.
- The next exact Gate run completed the entire runtime path (forced download failure, resumed package download, HybridCLR entry log) before its test-side diagnostic assertion failed. Inspection of the actual `Application.logMessageReceivedThreaded` condition showed it starts with `URL :`; `[Error]` is only Unity Test Runner's display prefix. The expected-log regex mistakenly included that display prefix. Removed the unnecessary log-capture workaround and restored `LogAssert.Expect` with a regex matching the raw URL/error condition; no log suppression or exception handling remains. The exact Gate must pass with this corrected matcher before P3 can be sealed.
- The following exact-run job returned a stack trace from the removed `ExpectedFailureLogCapture` code and its `IgnoreFailingMessages:true` markers. File timestamps confirmed the PlayMode test DLL was older than the edited source (`assembly 16:19:43`, source `16:27:57` local time). That job executed stale compiled code and is not evidence for the current source. UnitySkills `asset_refresh` returned success but did not regenerate this script assembly; force-reimport the exact PlayMode `.cs` with UnitySkills `asset_reimport` (`ImportAssetOptions.ForceUpdate`), then verify the assembly timestamp and perform fresh discovery before the next exact run.
- UnitySkills `asset_reimport` did recompile the current PlayMode source: assembly timestamp advanced beyond source, the updated regex was present in the compiled type, and the legacy capture helper / `ignoreFailingMessages` were absent from source. The corrected exact Gate then passed **1/1**.
- After the Gate, regular `StellarFramework.Tests.PlayMode` passed **15/15** independently. Fresh EditMode discovery returned 1,565/1,565 (not truncated) and contained the new VerificationSurface policy test; full FrameworkValidation passed **611/611**, 0 failed/skipped. A subsequent Console clear + `unity_diagnose` reported **healthy=true, 0 errors, 0 warnings, compile/update idle**.
- Added and executed `Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1`, an orchestration-only one-command path over the existing Editor Prepare menu and UnitySkills Test Runner APIs. It verifies current-project identity, prepared config/package, exact fresh discovery, exact Gate pass, and writes JSON evidence under `Temp/StellarHotUpdateVerification/playmode-gate-result.json`. The one-shot run passed: discovery **17/17**, exact Gate **1/1**, job `d55207bd`, evidence status `PASS`, elapsed **7 seconds**. README documents the command and REST sequence.
- Final P3 Console clear + `console_get_stats` + `unity_diagnose`: **healthy=true, 0 errors, 0 warnings, compile/update idle**, UnitySkills server running on port 8090. The stale-assembly attempt was retained as tooling/debug history and not counted as a product result; exact compiled-source gate and all final regressions passed, so no `TOOLING EVIDENCE GAP` remains.
- P3 is **SEALED**. Next authorized phase is P4: extend the existing `Tools/AndroidVerification/` harness and regenerate true Android HybridCLR/AOT metadata, Manifest, and YooAsset verification package before Android Release IL2CPP E2E. Preserve `ResKit.YooAsset -> YooAssetContentUpdater -> HybridCLRKit`; do not reintroduce HotUpdateKit or couple HybridCLRKit to YooAsset.

### 2026-09-23 — Reusable capability hardening P4 implementation in progress

- Began P4 only after the P3 exact Release Gate, ordinary PlayMode, FrameworkValidation, compile, and Console seals passed. The active Unity target is Android; the current Android scripting backend is Mono2x and the checked-in HotUpdate Manifest still targets StandaloneWindows64, so neither is being treated as Android proof.
- Added an Android preparation menu that asserts active Android target, temporarily selects IL2CPP + x86_64 + non-Development settings, invokes HybridCLR `PrebuildCommand.GenerateAll()` (which compiles `HotUpdate.dll` and performs an Android scripts-only IL2CPP strip build), exports Android AOT metadata through the existing `HybridCLRHotUpdateAssetExporter`, checks the generated Manifest/SHA/AOT files, builds the YooAsset verification package, and restores Player settings in `finally`.
- Added an Android Player verification route configured only by launch Intent extras. It uses `YooAssetContentUpdater -> ResKit.YooAsset -> HybridCLRKit`, keeps the device persistent cache, validates Android Manifest target and DLL SHA, reports downloaded-file/cache counts, checks HybridCLR's successful metadata/assembly/entry state, and observes the existing HotUpdateMain log marker. It emits a structured `[StellarHotUpdateVerification]` JSON record; no production `HotUpdateSettings` asset is rewritten.
- Extended the existing `Tools/AndroidVerification/Invoke-StellarAndroidReleaseVerification.ps1` / `Invoke-StellarApkSmoke.ps1` path with a HotUpdate profile: loopback Python standard-library CDN with byte-range support, `adb reverse`, Release IL2CPP x86_64 APK build, cold launch plus force-stop/restart, and machine assertions for first-run download and warm-cache zero-redownload behavior. No separate ADB, emulator, or result-aggregation system was introduced.
- Added a FrameworkValidation policy test for the Runtime/Editor boundary, Android target preparation, Intent configuration, CDN/ADB flow, and structured runtime proof. These source changes have not yet been compiled or run; P4 remains **IN PROGRESS** and no Android product result is claimed. Next: verify PowerShell/Python syntax, compile through UnitySkills, run focused policy validation, then execute the real Android Release IL2CPP gate and record its result before proceeding.
- PowerShell parser checks passed for the extended Android pipeline and smoke runner; Python AST parse and targeted `git diff --check` passed. The first asset-reimport batch raced Unity compilation, so I waited for the domain to settle and force-reimported the changed Verification Editor asmdef before using the resulting compile.
- UnitySkills compile is idle with **0 compiler errors**; fresh EditMode discovery returned **1,566 tests** and marked the new `AndroidHotUpdateReleaseGateUsesAndroidArtifactsIntentAndStructuredRuntimeEvidence` test Runnable. Focused run job `8f07e482` passed **1/1** in 9 seconds. The Windows Editor compile does not validate `UNITY_ANDROID` runtime code or device behavior; those remain pending the Release APK gate.
- Real P4 Android preparation completed with state `Temp/StellarHotUpdateVerification/android-release-preparation.json` = **PASS** (2026-09-23 17:48 local). It records Android + IL2CPP, `HotUpdate.dll` SHA256 `00b27e04a3de8f6995e9fe316fe86573d33c69eae8309c22e3f3a2148cd41f17`, matching Android Manifest entry, four AOT metadata sources regenerated under `HybridCLRData/AssembliesPostIl2CppStrip/Android`, and a rebuilt Android verification package (13 files / 6 bundles). This is true Android generation evidence, not the prior Windows target.
- The existing UnitySkills instance completed the non-Development Android Release HotUpdate APK build. `Library/StellarFramework/AndroidVerification/android-hotupdate-build-state.json` = **PASS / Succeeded**, profile `HotUpdate`, target `Android`, backend `IL2CPP`, architecture `X86_64`, Development=false, 0 errors / 1 warning, output `Builds/AndroidVerification/StellarFramework-HotUpdate-x86_64-release.apk`. The prior `C_Android_x64` build compiled 1,776 objects. Player settings restored to Mono2x / ARMv7 / Development=false / no Internet permission; `insecureHttpOption` was unavailable via reflection and is recorded `Unavailable` (the build warning), so runtime CDN communication still requires proof.
- The first full Android run built the APK successfully (**Succeeded, 0 errors, 1 warning; IL2CPP / x86_64 / Development=false**) and installed/started it on the existing API 35 AVD, but the smoke gate failed because `app-logcat.txt` contained **0** structured HotUpdate results and the CDN access log contained only its health check (no package request). The retained screenshot shows “System UI isn't responding”; device logcat shows `lowmemorykiller` evicting Google system processes. No product PASS is claimed; the app process was alive but never produced runtime evidence.
- Root-cause evidence showed the existing AVD config requested 2 GB and `Start-StellarAndroid.ps1` explicitly overrode RAM with `-memory 2048`; host had about 10 GB free. The immediate remediation is to keep using the same AVD and launch it with 4096 MB for the HotUpdate profile, then verify at least 3584 MB visible in `/proc/meminfo`. Ordinary Android smoke retains its 2048 MB default. This addresses observed device memory pressure without adding another emulator or ADB system.
- Extended the existing launcher/pipeline/smoke path to parameterize and record emulator memory and fail before installation when a pre-running AVD is undersized; documented the HotUpdate-specific 4 GB requirement and added it to the P4 policy assertions. PowerShell parser checks pass for all four modified scripts; `git diff --check` is clean apart from existing LF-to-CRLF warnings. Fresh EditMode discovery job `214f8f78` returned **1,566/1,566**, not truncated, with the updated HotUpdate policy test Runnable; exact focused test job `0115ca7b` passed **1/1** in 13 seconds. The initial 4 GB rerun also failed at runtime; P4 remains **IN PROGRESS**.
- Second full P4 run `Tools/AndroidVerification/Results/20260923-181937/pipeline-result.json` again failed only at the missing runtime JSON marker; build and preparation both passed, and cleanup passed. The AVD actually reported **4,013,928 kB** total RAM, proving the 4 GB launch took effect. Its app log still had no Unity managed/player messages or CDN package request, and the captured screen showed “Pixel Launcher isn't responding”; adding memory alone did not resolve the gate.
- Isolated the cause with the same 4 GB AVD: the existing ArchitectureDemo Release APK smoke passed cold-start + restart (**30s/10s**, PIDs 2870/3130), and the HotUpdate Release APK also cold-started + restarted (**30s/10s**) when launched **without verification Intent extras**, with normal Unity `SystemInfo` and Singleton logs. These controlled checks show the emulator and player baseline work; the failure is isolated to the Intent-enabled Android verification path. They are diagnostics only and do not count as P4 PASS.
- The HotUpdate APK launched without verification extras and passed cold-start + restart (**30s/10s**, PIDs 3604/3730), emitting normal Unity initialization logs; this rules out a general IL2CPP Player startup defect. The Intent-enabled path remains the isolated failure.
- Added one-time Android-only `[StellarHotUpdateVerificationStage]` logs for bootstrap/Intent, package cleanup, updater, ResKit, and HybridCLR stages. Wrapped the top-level async gate so unexpected exceptions are serialized as a structured `FAIL` result with full exception context rather than escaping `UniTaskVoid`; no exception is swallowed and the PASS criteria are unchanged. README and policy coverage now preserve this diagnostic contract.
- Forced UnitySkills reimport of Runtime and policy test sources; `StellarFramework.Verification.Runtime.dll` and `StellarFramework.FrameworkValidation.Tests.dll` both advanced to 18:54:40 local. UnitySkills reports compile/update idle. Fresh EditMode discovery job `5bc73f7a`: **1,566/1,566**, not truncated, exact updated policy test Runnable. Focused run job `5699a758`: **1/1 PASS**, 14 seconds. `git diff --check` has no whitespace errors (only Git line-ending warnings). Next is another full Android target preparation + Release IL2CPP build + 4 GB device run; P4 remains **IN PROGRESS**.
- Third full P4 run `20260923-185711` regenerated true Android outputs and built the Release IL2CPP x86_64 APK successfully (0 errors / 1 warning); the 4 GB AVD made all six CDN content GETs (HTTP 200). Full logcat proves `ContentUpdateCompleted:True`, ResKit Manifest + assembly load, Android target and expected/actual SHA match, all four AOT metadata loads, HybridCLR run success, `HotUpdate.HotUpdateMain.Main()` execution, and runtime JSON `status=PASS`. The smoke runner still reported failure before restart because Unity Android logger truncated the single JSON message at about 1 KB, cutting the app-scoped logcat record mid-array; this is a tooling evidence transport/parser defect, not a product FAIL, and does not close P4.
- Fixed Android result transport without relaxing the gate: Runtime now emits the complete UTF-8 JSON as ordered 512-character Base64 chunks; `Invoke-StellarApkSmoke.ps1` rejects missing/duplicate/inconsistent chunks and invalid Base64/JSON before applying its existing result assertions. Updated Android Verification docs and FrameworkValidation policy assertions. Direct PowerShell syntax validation passed; executing the actual parser function against synthetic out-of-order chunks passed, while an incomplete payload was rejected. `git diff --check` passed (only pre-existing LF-to-CRLF warnings).
- UnitySkills reimported the Android Runtime and policy test source. Fresh EditMode discovery job `0096bf94` returned **1,566/1,566**, not truncated. Focused Android release-gate policy job `51e8261c` passed **1/1** in 11 seconds. Current compile/update is idle. Next: run the complete P4 Android preparation + Release APK + cold-download + force-stop/restart pipeline against the chunked evidence path; P4 remains **IN PROGRESS**.
- The next fresh Android pipeline run `20260923-193040` again passed Android Prepare (Android / IL2CPP, matching DLL SHA and Manifest, 13 package files / 6 bundles) and non-Development Release APK build (Android / IL2CPP, build Succeeded), but cold boot surfaced an Android `Pixel Launcher isn't responding` system dialog before Unity Player initialization. The retained app log had JNI load only and no Unity bootstrap; device log had no HotUpdate request. This was AVD startup state, not product PASS/FAIL.
- Added UIAutomator-based handling to the existing HotUpdate smoke runner: it identifies the Android system `Wait` button by `android:id/aerr_wait`, clicks the accessible bounds (no fixed screen coordinates), and waits up to 180 seconds for `[StellarHotUpdateVerificationStage] BootstrapEntered` before the runtime window. The number of handled system prompts is recorded for cold start and restart; missing bootstrap fails early. Docs and the verification policy now cover this startup evidence. PowerShell AST parsing passed; a real AVD prompt produced action `Wait` at bounds center `(540,1363)`, and the actual runner helper selected it and observed the Unity bootstrap. Ordinary non-HotUpdate smoke behavior is unchanged.
- Fresh UnitySkills EditMode discovery job `3f137dbc`: **1,566/1,566**, not truncated; updated Android policy is Runnable. Focused policy job `cb9b9352`: **1/1 PASS** in 7 seconds. PowerShell smoke script syntax passed.
- Using the just-generated Android package and APK from `20260923-193040`, the repaired strict smoke runner completed `retry-chunked-ui/result.json` = **PASS** after 90-second cold runtime + 30-second force-stop/restart. Cold run: Android Manifest, ResKit Manifest/DLL, expected/actual SHA256 `00b27e04a3de8f6995e9fe316fe86573d33c69eae8309c22e3f3a2148cd41f17`, all four Android AOT metadata keys loaded, `HotUpdate` Assembly loaded, entry point and marker true; 6 files / 1,838,760 bytes downloaded, cache 0→15. Restart: cache 15→15, 0 redownloads, all four metadata keys and entry evidence true. Both system prompts were detected/handled, CDN content requests were HTTP 200, and the machine record has no failures. The Android artifacts are true target-generated outputs; no Windows metadata was used. Next: rerun the official full Android verification pipeline once with the corrected startup/evidence handling to seal P4 in its aggregate result, then continue P5.
- Official full rerun `20260923-201227` independently regenerated Android Prepare **PASS** and completed the fresh non-Development Android/IL2CPP APK build. UnitySkills' 15-minute gateway limit returned HTTP **504** just before its state file write completed; six seconds later the Editor-written state was **PASS / Succeeded** and the APK was present (37,719,929 bytes). The pipeline aggregate therefore stopped before smoke (`productVerificationStatus=NOT_RUN`); this is a transport timeout, not a product result.
- Re-ran the repaired strict smoke against that latest APK and latest Android package: `smoke-after-504/result.json` = **PASS** (90s cold + 30s restart), same SHA `00b27e...41f17`, 6 files / 1,838,760 bytes, cache 0→15; restart cache 15→15 with 0 redownloads. Four AOT metadata keys loaded, ResKit Manifest/DLL true, loaded assembly `HotUpdate`, entry invocation + marker true. Both cold/restart startup prompts were handled; CDN package/manifest requests returned HTTP 200.
- Hardened `Invoke-StellarAndroidReleaseVerification.ps1`: on HTTP 504 only, it now waits for the authoritative Editor state and continues only if `status=PASS`, `buildResult=Succeeded`, and the recorded APK file exists. It records `buildRequestTransportStatus` and the original transport warning in `pipeline-result.json`; all other HTTP errors remain hard failures. README and policy coverage updated. PowerShell parse and synthetic fallback checks passed for both 504+valid state recovery and 504+failed-state rejection.
- UnitySkills reimported the final P4 policy test. Fresh EditMode discovery `ccab3892`: **1,566/1,566**, untruncated, exact target Runnable. Focused Android release-gate policy `300e59df`: **1/1 PASS**, 7 seconds. Next: rerun full pipeline with the gateway-state recovery path; prior APK build is now fully incremental. P4 remains **IN PROGRESS** until aggregate and necessary regression evidence seal.
- Official aggregate Android Release IL2CPP run `Tools/AndroidVerification/Results/20260923-204313/pipeline-result.json` is **PASS** (`productVerificationStatus=PASS`, cleanup PASS). It used newly regenerated Android/IL2CPP HybridCLR outputs, Android Manifest/DLL SHA `00b27e04a3de8f6995e9fe316fe86573d33c69eae8309c22e3f3a2148cd41f17`, four Android AOT metadata files, and a rebuilt YooAsset verification package (13 files / 6 bundles). The non-Development Android IL2CPP x86_64 APK built with 0 errors / 1 warning. UnitySkills REST returned 504 at its gateway timeout, but the pipeline records `GatewayTimeoutRecoveredFromBuildState` and continued only after the Editor state was PASS/Succeeded and the APK existed; the transport warning remains visible in the evidence.
- The retained Android machine result `Tools/AndroidVerification/Results/20260923-204313/result.json` proves cold update and force-stop restart. Cold run: Android `verification-v1`, YooAsset update true, 6 files / 1,838,760 bytes, cache 0→15; ResKit Manifest + DLL true; SHA256 matched; all four Android AOT metadata entries loaded; `HotUpdate` Assembly loaded; `HotUpdate.HotUpdateMain.Main` invoked and marker observed. Restart: cache 15→15, 0 files / 0 bytes redownloaded, with the same Manifest/SHA/AOT/Assembly/entry proofs. Both runs have structured PASS; app logcats retain five ordered evidence chunks each and the HotUpdateMain marker. The smoke runner strictly reconstructs/rejects malformed chunk payloads.
- Post-P4 regression seal: `FrameworkValidation` EditMode filter job `e3e6e9bc` **615/615 PASS** (0 failed/skipped); complete PlayMode job `a71d9ad2` **17/17 PASS**; official `Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1` **PASS** at `Temp/StellarHotUpdateVerification/playmode-gate-result.json` (fresh PlayMode discovery found the exact gate Runnable; exact test 1/1, 0 failed/skipped); UnitySkills `unity_diagnose` reports healthy, compile/update idle, 0 console errors / 0 warnings.
- A separate unfiltered `EditMode` run `b713d146` is **not** the FrameworkValidation gate: it enumerated 1,566 tests and reported 1,561 pass / 1 fail / 4 skipped. Its only failure was `PackedAddressablesPlayModeTests.PackedModeLoadsPrefabThroughResKit`, a PlayMode-only test accidentally run through the EditMode entry; `AddressablesDataBuilderInput` then rejected `EditorUserBuildSettings.activeBuildTarget` from the async continuation. That same test passed in the correctly scoped complete PlayMode suite. This mixed-mode run is retained as a Test Runner mode-selection anomaly and is not counted as product PASS/FAIL evidence for P4.
- P4 is **SEALED / PASS**. Next authorized phase is P5: clean consumer import verification for UIAdaptationKit Complete, Localization Complete, and Hot Update Full using isolated fresh Unity 2022.3.62f3c1 consumers.

### 2026-09-23 — P5 clean consumer import in progress

- Regenerated the three Catalog Recommended Profile closures through the existing package-group exporter from the current source tree, using date-stamped output names so older BuildArtifacts remain untouched. Bootstrap wrappers and dependency guides exist for each export:
  - UIAdaptationKit Complete: `BuildArtifacts/StellarFramework/Kits/StellarFramework-Profile-UIAdaptationKit-Complete-P5-20260923.unitypackage`, 98,196 bytes, SHA256 `1d5e7a5105b4cbb5f869175e69884d6bfe118bf416198592886574e0cc44a49f`; auto UPM `com.unity.ugui`.
  - Localization Complete: `BuildArtifacts/StellarFramework/Kits/StellarFramework-Profile-Localization-Complete-P5-20260923.unitypackage`, 118,055 bytes, SHA256 `9d40fb0f08703018b1ed5dfa15bba0faa4694512144490de06ae8ecf9e306701`; auto UPM `com.unity.textmeshpro`, `com.unity.ugui`.
  - Hot Update Full: `BuildArtifacts/StellarFramework/Kits/StellarFramework-Profile-HotUpdate-Full-P5-20260923.unitypackage`, 150,451 bytes, SHA256 `3bf8190b6544d8a7a1b8e4109c2db14ad3d7dbfbc11eac7570f10aefcbf6e812`; auto UPM HybridCLR, pinned UniTask commit `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`, and YooAsset.
- Export manifest is `Temp/StellarFrameworkConsumerValidation/P5/export-manifest.json`; matching `*-Dependencies.md` files list the closures. The one-shot Editor export helper and its generated `.meta` were removed after export; no existing package artifact was overwritten. The three clean consumer imports/runtime gates remain pending, so P5 is **IN PROGRESS**.
- First fresh `UIAdaptationKit Complete` consumer was created at `D:\StellarFramework-ConsumerValidation-P5-20260923-2215\UIAdaptation`. Bootstrap auto-installed `com.unity.ugui`, imported its payload, and the required UIAdaptation runtime/ToolsHub asmdefs were present with excluded Kit paths absent. Its clean Unity log exposed **4 compile errors** because `ToolsHub.Core` exported `ListSerializerWindow.cs`, which unconditionally referenced `Newtonsoft.Json` although this profile correctly does not install Newtonsoft. P5 is not passing yet; this is a real package-source boundary defect, not a tool gap.
- Fixed the root cause by adding an asmdef `versionDefines` guard for `com.unity.nuget.newtonsoft-json` and compiling the Newtonsoft-specific `ListSerializerWindow` only when that optional UPM package is present. Added `PackagePublisherPolicyTests.NewtonsoftDependentToolsHubEditorIsBehindOptionalUpmVersionDefine` to prevent regression. The failed first consumer is retained as evidence; its result is not reused. Focused Unity validation and a newly exported package/clean consumer rerun are pending.
- The first P5-Fix1 clean consumer import exposed a second compile error: BuiltinModules.cs still registered ListSerializerWindowHubModule while the guarded Newtonsoft panel was absent (CS0246). The import monitor PASS only proved bootstrap/dependency/assets and did not prove compilation; the independent Unity log check correctly records the consumer as failed. The source root cause is now fixed by putting the registration behind the same STELLARFRAMEWORK_NEWTONSOFT_JSON define, and the policy assertion checks both implementation and registration.
- Focused PackagePublisherPolicyTests after the guard fix: 33/33 PASS. The next FrameworkValidation run completed 615/616, with its sole failure StellarFrameworkTopMenuOnlyExposesToolsHubAndExport; inspection traced that failure to the task-owned temporary P5 export runner's extra top-level menu. It is validation scaffolding, not a product menu, and must be removed after the non-overwriting P5-Fix2 re-export before repeating FrameworkValidation. P5 remains IN PROGRESS.
- Re-exported the three Catalog Recommended Profile closures after guarding the ToolsHub registration. Non-overwriting Fix2 artifacts and dependency guides: UIAdaptation 98,447 bytes / SHA256 f255acf5933369feabd31c9d506e7ca2fda34fd954a88a81bb29efa3cffcff8f; Localization 117,962 bytes / SHA256 91a86725b4edce13e2657f80c370d26b2c55646b34e1bcdb0a1cb0bb3cc80081; Hot Update Full 150,803 bytes / SHA256 83fce4e8be0c733114b2cea652423a1179e39f1fe4639f56353a4e0c8f0a3e2b. Export record: Temp/StellarFrameworkConsumerValidation/P5/export-manifest-fix2.json. The temporary exporter source and .meta were removed and the top-menu FrameworkValidation policy then passed.
- UIAdaptation Fix2 was imported through Bootstrap in fresh project D:\StellarFramework-ConsumerValidation-P5-Fix2-20260923-2308\UIAdaptation. UGUI auto-install, required Runtime/ToolsHub asmdefs, exclusions, and compile-log audit all passed (0 C# errors). PlayMode smoke XML P5ConsumerSmoke-PlayMode.xml = **1/1 PASS**, exercising breakpoint selection, CanvasScaler resolution, safe-area anchors, and cutout geometry.
- Localization consumer harness sequencing had two retained non-product failures: one first staged its test source before package import, producing missing-reference compile errors; a later fresh import compiled cleanly but the test fixture requested obsolete built-in Arial.ttf. A separate fresh consumer D:\StellarFramework-ConsumerValidation-P5-Fix4-20260923-2348\Localization imported both TMP and UGUI, all required asmdefs were present, exclusions absent, and compile log had 0 C# errors. Its supported LegacyRuntime.ttf PlayMode smoke XML = **1/1 PASS**, including translation lookup and live locale-change refresh.
- Hot Update Full clean consumer import and the YooAsset/HybridCLR PlayMode gate remain **IN PROGRESS**; no maturity change has been made.
- Hot Update Full Fix2 Bootstrap resolved all three declared UPM dependencies; clean packages/manifest.json and packages-lock.json preserve HybridCLR commit 4feac30cb2e105992986c737f7f54992b8300e1a, UniTask 2.5.11 commit e5acc106ee196bc5a32fb14cdf2987b0f96d11e0, and YooAsset tag 2.3.19. The external compile-log audit found a real optional UGUI boundary error in ToolsHub.Editor: FindUsedAssetsTool.cs imported UnityEngine.UI although Hot Update Full correctly omits UGUI.
- Fixed the optional UGUI boundary with the ToolsHub.Editor asmdef version define for com.unity.ugui, guarded the UGUI-only static helper and SmartMaterialModule's Image controls, and added a policy test. Focused PackagePublisherPolicyTests = 34/34 PASS; FrameworkValidation = 617/617 PASS. The P5 exporter runner was removed after use.
- Re-exported all three recommended closures as non-overwriting Fix3 artifacts after this source fix. Temp/StellarFrameworkConsumerValidation/P5/export-manifest-fix3.json records: UIAdaptation 98,699 bytes / SHA256 4ff67573edbba2bd87d5403367f2bcdec1da87fe4b808541634240ab59c159a8; Localization 118,624 bytes / SHA256 166b569a6d53ddabcb42db28370a369c67645cc26d5a7d2ed6d76bb8dec95b73; Hot Update Full 150,969 bytes / SHA256 1025703c01acc15a7eabfbe82e2888ea27aaf932db07b2f2d29131ac48e687d2. Their consumer re-imports remain pending; P5 stays IN PROGRESS.

### 2026-09-24 — Reusable capability hardening P5 sealed

- P5 最终 Fix3 包在三个全新 Unity 2022.3.62f3c1 consumer 项目完成 Bootstrap 导入、UPM 自动依赖、asmdef/exclusion 检查、0 C# 编译错误及 PlayMode Runtime Smoke：UIAdaptation **1/1 PASS**（Breakpoint、CanvasScaler、安全区 anchors、cutout geometry），Localization **1/1 PASS**（表查找、UGUI 绑定、locale 切换刷新），Hot Update Full **1/1 PASS**（content update + HTTP Range resume + ResKit Manifest/DLL + HybridCLR metadata + Assembly.Load + HotUpdateMain.Main）。
- 最终包 SHA256：UIAdaptation `4ff67573edbba2bd87d5403367f2bcdec1da87fe4b808541634240ab59c159a8`；Localization `166b569a6d53ddabcb42db28370a369c67645cc26d5a7d2ed6d76bb8dec95b73`；Hot Update Full `1025703c01acc15a7eabfbe82e2888ea27aaf932db07b2f2d29131ac48e687d2`。Consumer 机器证据：`D:\SF-P5-UI-Fix3-Clean\P5ConsumerEvidence.json`、`D:\SF-P5-Loc-Fix3-Clean\P5ConsumerEvidence.json`、`D:\StellarFramework-ConsumerValidation-P5-Fix3-20260924-0015\HotUpdate\P5ConsumerHotUpdate-Evidence.json`。
- Hot Update Full fresh lock 仍是 HybridCLR commit `4feac30cb2e105992986c737f7f54992b8300e1a`、UniTask 2.5.11 commit `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`、YooAsset 2.3.19 hash `5df594f7dc2383735796d8816d636a9b82976ea2`。Consumer 生成的 Manifest 明确为 `StandaloneWindows64`，`HotUpdate.dll` SHA256 `170f24354ad67cc09b86ce4cc6ddd2fc0feed250195b7362954c1d63370d3fd0`，并列出四份对应平台 AOT metadata。
- 运行夹具注意：Windows 临时 consumer 的长路径曾让 YooAsset `OutputCache` 文件路径超过 MAX_PATH；通过该项目的临时 `P:` drive alias运行（路径缩短到 198 字符），没有修改框架代码。另一个分进程尝试因 Unity 重启清除 `Temp/StellarHotUpdateVerification` 而缺少 runtime config；最终在同一个 Editor/Test Runner 启动中先 Prepare 再运行 Gate，XML 正式 **PASS**。这两个 harness/setup 失败已保留日志，不是产品失败，也没有留下 `TOOLING EVIDENCE GAP`。
- UnitySkills health endpoint 回报服务在线，但本会话未暴露 Unity MCP tool；最终证据由 Unity 2022.3.62f3c1 batchmode Test Runner 取得，Gate discovery 可见并执行，结果 XML 与日志完整。
- P5 **SEALED**，尚未调整 maturity。下一阶段 P6：基于已收集的 Android Release IL2CPP、Windows/PlayMode、导出依赖闭包与 clean consumer 证据评估 `ResKit.YooAsset` 与 `HybridCLRKit` 的 maturity，并保持 Hot Update Full Recommended Profile 从依赖闭包派生。
### 2026-09-24 — P6 maturity evidence decision (verification pending)

- Reviewed sealed P3–P5 evidence against the P6 gate. Updated `KitDistributionCatalog.json`: `ResKit.YooAsset` RC→Stable after YooAsset focused policy/adapter tests, Range/cache Gate, the true Android Release IL2CPP content-update E2E, and Hot Update Full clean-consumer import/runtime PASS; `HybridCLRKit` and `HybridCLRKit.Tools` Experimental→RC after Android Release IL2CPP, the discoverable PlayMode Release Gate, and clean-consumer PASS. The P5 Windows64 consumer ran the Editor PlayMode Gate and does not count as a Windows64 Release Player Gate, so HybridCLR Stable is deferred to P7.
- `Hot Update Full` remains closure-derived and is expected to resolve to RC. Updated the catalog architecture/maturity assertions, Publisher closure expectation, maturity audit/current-status docs, and this Agent Plan. Expected catalog maturity totals are Stable 80 / RC 4 / Experimental 0.
- Evidence sources: `Tools/AndroidVerification/Results/20260923-204313/pipeline-result.json` + `result.json`; P5 clean consumer evidence paths and package/dependency hashes in the Agent Plan. P6 focused policy tests and FrameworkValidation regression are pending; no maturity phase is sealed until they pass.

### 2026-09-24 — P6 maturity sealed / PASS

- P6 已封存：Catalog maturity totals 为 Stable **80** / RC **4** / Experimental **0**。`ResKit.YooAsset`=Stable；`HybridCLRKit` 与 `HybridCLRKit.Tools`=RC；`Hot Update Full` 由 closure resolver 得到 RC。HttpKit 与 ResKit.Addressables 仍保留 RC。未把 HybridCLR 升到 Stable，因 P7 Windows64 Release IL2CPP Gate 需要在真实 Player 形态复跑。
- Focused Unity Test Runner: `RuntimeKitProfilesUseSchemaV4ArchitectureAndMaturityMetadata` job `1f36734a` **1/1 PASS**；`RecommendedProfileMaturityUsesWorstDependencyInClosure` job `4427a6e5` **1/1 PASS**。
- P6 全回归：主 `StellarFramework.Tests.FrameworkValidation` filter job `b0df19cd` **614/614 PASS**；独立 Addressables validation class `AddressablesBuildToolTests` job `ee4abe7d` **3/3 PASS**；合计 **617/617 PASS**, 0 failed / 0 skipped。UnitySkills `unity_diagnose`: healthy=true, compile/update idle, Console 0 errors / 0 warnings。
- 调试历史保留：外部编辑后的首轮 Run 仍命中旧程序集；AssetDatabase Refresh 后源码程序集更新，下一次运行暴露首个宽泛 JSON patch 命中了 HttpKit 而不是 ResKit.YooAsset。Catalog 已按 profile id 修正（HttpKit 恢复 RC、ResKit.YooAsset 升 Stable），两项 maturity focused test 与全部 617 项回归随后 PASS。早期失败未被当作 Gate PASS，也未通过放宽断言/删测试绕过。
- P6 计划记录及 `KitCatalogAudit.md`、`ValidationCurrentStatus.md` 已同步。P7 进行中：需保留 Android Release IL2CPP PASS，并重新取得 Windows64 Release IL2CPP HotUpdate Gate 与最终全量回归证据。

### 2026-09-24 — P7 Windows64 Release Gate execution in progress

- P7 按计划只复跑 Windows64 Release IL2CPP HotUpdate Gate 与最终回归，不重开架构设计。主工作区当前 Editor Active Target 为 Android/Mono；Standalone 的 IL2CPP 设置已存在，但 HybridCLR 预检接口错误读取 Active Target。该预检结果不作为产品失败或 PASS。
- 为保留主工程 `Assets/GameHotUpdate` 的 Android P4 产物和所有既有 dirty changes，本轮 Windows Player 使用 P5 Hot Update Full clean consumer 的独立副本；不会对主工程做 Windows 导出或平台切换。P5 consumer 中 `HotUpdateRuntimeVerification.cs`、`HotUpdateVerificationPaths.cs`、`YooAssetHotUpdateVerificationBuilder.cs` 与本工作区当前文件 SHA256 分别一致：`CAE3CAAC872B63029523CBDE15E6162B5859C7E829AE8C7BCDC7B449BFAB7D0A`、`D0F2DD43681A51B3C7959C3351F4E4F84A40EC24555D08F9D0017A5AE83D63BA`、`97CE5312C79CD00FB9A1E4972BB128FD1E91AD365AFBF6DF2945589F93FBACA4`。
- P5 evidence 已确认 Windows64 Manifest 指向 `StandaloneWindows64`、HotUpdate DLL SHA256 `170f24354ad67cc09b86ce4cc6ddd2fc0feed250195b7362954c1d63370d3fd0`，并有四份 Windows64 AOT metadata。P7 将在隔离副本重新导出 Windows64 Assets、重建 YooAsset verification package、构建新的 Release IL2CPP Player 并采集机器结果；当前尚无 P7 产品 PASS，focused/full regression 待执行。
- P7 首轮 batchmode import/build 只编译验证辅助脚本，报 2 个 CS0246/CS0103：该临时 builder 漏引入 `StellarFramework.HybridCLR` 中的 `HotUpdateManifest`。这是本轮 runner 编译缺陷，不是产品程序集结果；失败日志 `D:\SF-P7-Win64-20260924\P7-Windows64-Unity-Build.log` 保留，修正 runner 后重跑，当前不记产品 PASS/FAIL。
- 修正 runner 后，隔离副本的 P5 Windows64 Export + YooAsset package prepare 已真实 PASS（Manifest `StandaloneWindows64`、DLL 1 项、AOT metadata 4 项）；Player 构建前的 HybridCLR `CheckSettings` 明确拒绝未初始化的 clean consumer，缺少项目本地 `HybridCLRData/LocalIl2CppData-WindowsEditor/il2cpp/libil2cpp/hybridclr`。这说明 P5 Editor PlayMode consumer 并未覆盖 IL2CPP Player 安装前置，不是产品 Gate 失败或 PASS。日志 `D:\SF-P7-Win64-20260924\P7-Windows64-Unity-Build-Retry.log` 保留；下一轮给隔离副本提供与同一 Unity/HybridCLR 版本匹配的本机已安装 Windows IL2CPP toolchain，再重建到新唯一输出路径。
- 已从主工程 HybridCLR 安装输出的 `il2cpp/libil2cpp/hybridclr/generated/libil2cpp-version.txt` 实读版本 `8.11.0`，并将 `HybridCLRData/LocalIl2CppData-WindowsEditor`（843,348,025 bytes）复制到 P7 隔离副本；这是项目本地 IL2CPP toolchain data，不改 Unity Editor 安装，也不触碰主工程。下一轮 Build 输出使用新的 `Windows64ReleasePlayer-InstalledHybridCLR` 子目录。
- 提供已安装 toolchain 后，第二次隔离启动发现 P5 consumer 副本内的 `HybridCLRData/AssembliesPostIl2CppStrip/StandaloneWindows64` 源目录为空，P5 导出 prepare 因缺少源 AOT metadata 停止；该轮没有生成/运行 Player，日志 `D:\SF-P7-Win64-20260924\P7-Windows64-Unity-Build-InstalledHybridCLR.log` 保留。为避免把缺失的源文件或 P5 的已导出 Assets 冒充新生成结果，runner 下一轮会在 active `StandaloneWindows64` 下调用 HybridCLR 官方 `PrebuildCommand.GenerateAll()`，再执行 Export + YooAsset package + Player。
- Windows64 `PrebuildCommand.GenerateAll()` 已确实编译并生成当前 StandaloneWindows64 HotUpdate DLL，但 AOT strip 阶段因 consumer 的 `EditorBuildSettings.scenes` 为空而报 `Cannot build untitled scene`，所以 Gate 未继续，日志 `D:\SF-P7-Win64-20260924\P7-Windows64-GenerateAll-ReleaseBuild.log` 保留。P7 runner 将在 GenerateAll 前创建并保存独立 Gate scene，再把它加入该隔离 consumer 的 Build Settings。
- 加入保存的 Gate scene 后，官方 `GenerateAll()` 已完成 Windows64 scripts-only strip、MethodBridge/AOT reference 生成及四份 Windows64 AOT metadata；流程随后因 clean consumer 未配置 HotUpdate source asmdef，重建输出中没有 `HotUpdate.dll`，在 Export 前停止，日志 `D:\SF-P7-Win64-20260924\P7-Windows64-GenerateAll-ReleaseBuild-GateScene.log` 保留。隔离副本原有 Windows64 `HotUpdate.dll.bytes` 与 Manifest SHA 仍匹配 P5 已审计值 `170f24354ad67cc09b86ce4cc6ddd2fc0feed250195b7362954c1d63370d3fd0`。下一轮只把这份已验证 Windows64 payload 复制回 HybridCLR 导出源目录，再由 Exporter 与新生成的 Windows64 AOT metadata 重建 Manifest/YooAsset 包；不会把这份 P5 DLL标记成 P7 新编译产物。
- P7 随后完成了 Windows64 target metadata 的重新生成、Export 和 YooAsset verification package build，三个阶段日志均通过；但 Unity build helper 在 HybridCLR scripts-only 生成后继承了 `EditorUserBuildSettings.buildScriptsOnly=true`。BuildReport 虽返回 Succeeded，仍报告 `Run script only build`，没有完整 Player/GameAssembly；helper 的独立产物断言拒绝将其记作 Gate PASS。日志 `D:\SF-P7-Win64-20260924\P7-Windows64-FinalBuild.log` 保留。下一轮在 full Player build 前显式设 `buildScriptsOnly=false`，并用新输出目录重建。
- 下一次独立 full-build 尝试在 `GenerateAll` 之后显式设置了 `buildScriptsOnly=false`，但 Unity BuildReport 仍为 `Run script only build` 且没有 Player/GameAssembly，断言再次拒绝 PASS；证据日志 `D:\SF-P7-Win64-20260924\P7-Windows64-FullPlayerBuild-01.log`。下一轮将在 BuildPipeline 调用正前记录有效 `EditorUserBuildSettings.buildScriptsOnly` / `BuildOptions` 并再次置 false，以区分 Unity 状态继承与调用配置。
- `FullBuild-02` 日志确认 BuildPipeline 调用前 Unity API 返回 `buildScriptsOnly=False`、`BuildOptions=0`、`target=StandaloneWindows64`，但实际 BuildReport 仍执行 `Run script only build`。据此把 GenerateAll + package preparation 与正式 Player Build 拆为两个 Unity batchmode 进程，避免同一 Editor 构建状态污染；原始日志保留，仍不视作 PASS/产品 FAIL。
- P7 隔离 runner 已拆为 `Prepare()` 与 `BuildPlayer()` 两个独立 Unity 进程入口。Prepare 在 StandaloneWindows64 下生成 AOT metadata、核对并注明复用的 P5 Windows DLL 来源、导出 Manifest / YooAsset 包，并写 `P7-Windows64-PreparationEvidence.json`；BuildPlayer 再以 Release IL2CPP、full build 和 `GameAssembly.dll` 断言构建新 Player。此 runner 只在 `D:\SF-P7-Win64-20260924` 隔离验证项目内，不改工作区产品代码。
- 分离后的 Prepare 进程 **PASS**：StandaloneWindows64 / IL2CPP / HybridCLR 8.11.0；使用 P5 Windows DLL SHA `170f24354ad67cc09b86ce4cc6ddd2fc0feed250195b7362954c1d63370d3fd0`，新 Manifest SHA `39961b93d526e286de6b8cff78889a3648bdde73cec2de4726b164dbe6cc5681`，四份 AOT 源 metadata 在 Windows64 目标目录重新生成并记录 SHA；YooAsset verification package 13 files / 6 bundles。Evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\P7-Windows64-PreparationEvidence.json`，Prepare log=`D:\SF-P7-Win64-20260924\P7-Windows64-Preparation-Separated.log`。
- 新发现 Unity 在 Editor 退出时清理项目 `Temp`，所以第一份 Prepare evidence 引用的 `Temp/.../RemoteCDN` 在跨进程后不存在。该生命周期问题已定位，产品 Prepare 本身已通过；这不是 `TOOLING EVIDENCE GAP`。重跑将先把 package 和 config 复制到 P7Artifacts 的持久验证目录，再启动独立 Player Build 进程。
- 持久化后的第二次 Prepare **PASS**：package/config 位于 `D:\SF-P7-Win64-20260924\P7Artifacts\Windows64-P7-Run02`，退出 Editor 后 RemoteCDN 仍存在，13 files，manifest/DLL/AOT 证据与首轮一致。随后检查发现新建场景写进 EditorBuildSettings 时用了绝对路径，导致 scene GUID 为零；在 Player-only 进程里改用 `Assets/...` asset path 并刷新 AssetDatabase 后再构建。日志 `D:\SF-P7-Win64-20260924\P7-Windows64-Preparation-Persistent.log` 保留。
- Player-only 进程的 Unity BuildReport 返回 Succeeded、已进入 Scene/Assets build，但 Windows build option createSolution 保持 true，结果只有 Visual Studio solution 而没有游戏 EXE / GameAssembly.dll；产物断言拒绝 PASS。输出 D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-03 与日志 D:\SF-P7-Win64-20260924\P7-Windows64-FullPlayerBuild-Separated.log 保留。下一轮在 Player-only 进程显式设并验证 UnityEditor.WindowsStandalone.UserBuildSettings.createSolution=false，再用全新目录生成游戏 Player。
- P7 首个真实 Windows64 Release IL2CPP Player 已启动并执行了 Runtime Verification bootstrap，但 Gate 按既有 `>1 MiB` bundle 前置断言返回失败，尚未执行 Range 下载中断 / 恢复或热更链路。机器结果 `D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-04\Temp\StellarHotUpdateVerification\runtime-result.json` 与 Player log 保留；结果是验证 fixture 的所有 bundle 都经 LZ4 后小于阈值，不是 Gate PASS，也尚不足以判断产品链路 FAIL。未改变断言、测试或产品代码。P7 隔离副本第三次准备改用 YooAsset 已有 `ECompressOption.Uncompressed` 构建验证包，确保大 bundle fixture 达到 Gate 原阈值；输出改到全新 Run03 / FullBuild-05，保留旧失败产物以便审计，然后重新执行同一 Release IL2CPP Player Gate。
- P7 Windows64 Run03 Prepare **PASS**：YooAsset fixture 改为内置 Uncompressed 后，Windows64/IL2CPP package 仍 13 files / 6 bundles，最大 bundle 为 1,877,008 bytes（原 Gate 需要 >1 MiB），其余验证条件未放宽。官方 `GenerateAll()` 在 StandaloneWindows64 下重新生成四份 AOT metadata；Manifest target `StandaloneWindows64`，Manifest SHA `39961b93d526e286de6b8cff78889a3648bdde73cec2de4726b164dbe6cc5681`；HotUpdate DLL 来源继续注明为 P5 已核实 Windows payload（SHA `170f24354ad67cc09b86ce4cc6ddd2fc0feed250195b7362954c1d63370d3fd0`），未伪称 P7 新编译。Evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\Windows64-P7-Run03\P7-Windows64-PreparationEvidence.json`，日志=`D:\SF-P7-Win64-20260924\P7-Windows64-Preparation-Uncompressed-Run03.log`。下一步以独立 Unity 进程构建新的 Windows64 Release IL2CPP Player。
- P7 Windows64 Release IL2CPP Player 全链路 **PASS**（隔离 P5 clean consumer 副本）：Run03 官方 `GenerateAll()` 重生成四份 StandaloneWindows64 AOT metadata；Manifest target Win64、6 个 bundle，未压缩最大包 1,877,008 bytes。FullBuild-05 构建证据为 IL2CPP / Release / Development=false / scriptsOnly=false / createSolution=false / errors=0，生成真实 EXE 与 GameAssembly.dll。Player result `success=true`，Range 中断和恢复 offset 均 262,144 bytes，已加载程序集 `HotUpdate, Version=0.0.0.0...`，Manifest source `ResKit:YooAsset:...`；Player.log 有 `[HotUpdateVerification] PASS`、`HotUpdate.HotUpdateMain:Main()` 及热更成功标记。Strict aggregate gate evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-05\P7-Windows64-Release-IL2CPP-GateEvidence.json`，同时引用原始 Prepare/Build/Runtime JSON、Player log 与 Run02 失败尝试。Runtime 原 JSON 的 `status` 空、Android 专用 telemetry 默认值保持原样；aggregate 对原始 JSON、构建/准备证据、DLL/Manifest hash、四份新 AOT hash、Player 主函数日志逐项做了断言后产出顶层 PASS。Build 有 1 个 HybridCLR `CheckSettings` warning（clean consumer 未配置 source hot-update module；运行使用 P5 已验证 payload），0 errors，不隐去此信息。Windows Player P7 Gate 已闭环，P7 最终回归尚待执行。
- P7 maturity 决策：结合已有 P4 Android Release IL2CPP 完整热更证据、P5 Hot Update Full clean consumer Gate、P7 Windows64 Release IL2CPP Player 全链路 PASS，以及本轮 UI/HotUpdate focused suites 44/44 PASS，P6 保留的 HybridCLR Windows Player Stable 门槛已满足。将 `HybridCLRKit` 与 `HybridCLRKit.Tools` 从 RC 提升到 Stable；`Hot Update Full` 继续由依赖闭包计算成熟度，不手写 profile 等级。已只修改 Catalog 中这两个 maturity 字段，正在执行 maturity/publisher/catalog focused policies 与最终全回归；在这些验证通过之前，不宣称 maturity 决策已验证完成。
- P7 maturity focused run `KitArchitectureMetadataPolicyTests` job `7cae486e` surfaced 1/16 failure: the Catalog correctly reports the evidence-backed `hybridclrkit=stable`, while an existing test still hard-coded `rc`; no other failure/skips. This test result is retained, not reclassified. I checked the relevant maturity assertions: only the architecture metadata expectations for HybridCLRKit/Tools and the Recommended `hotupdate.full` closure expectation encoded the superseded RC state. Updated those exact expectations to Stable (without changing conditions, removing tests, or weakening dependency-order assertions); next rerun architecture/catalog/publisher/export policy classes against the new maturity.
- P7 maturity policy 15/16 failure was stale-assembly evidence: source files were modified at 2026-09-24 02:06, but `Library/ScriptAssemblies/StellarFramework.FrameworkValidation.Tests.dll` was last written 2026-09-24 00:16. UnitySkills health/compilation was idle and did not auto-import these externally patched test sources. After a successful UnitySkills `asset_reimport?dryRun=true` validation of each exact path, force-reimporting only the two updated maturity assertion files so final focused tests execute current source; the 15/16 result remains recorded as stale and will not be counted as a product failure or pass.
- P7 post-maturity whole `StellarFramework.Tests.FrameworkValidation` namespace filter **PASS 614/614**, 0 failed / 0 skipped / 0 inconclusive, UnitySkills job `faea6765`; fresh EditMode discovery job `c648bcb3` returned 1,568/1,568 without truncation and measured the exact framework filter as 614 tests. Machine summary=`D:\SF-P7-Win64-20260924\P7Artifacts\P7-FrameworkValidation-Results.json`. This validates current Stable catalog metadata along with the rest of FrameworkValidation.
- P7 standalone Addressables policy gate **PASS 3/3** (`AddressablesBuildToolTests`, job `c05725bd`). Later independent audit clarified the PlayMode accounting: `StellarFramework.Tests.PlayMode` contains **15 product tests**, all **15/15 PASS**. Fresh Test Runner discovery totals **17** because it additionally contains 1 HotUpdate ReleaseGate test and 1 UnitySkills PlayModeRecovery tooling test. The historical full-discovery job returned 17/17, but it should not be labeled “ordinary product PlayMode 17/17”. Evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\P7-AddressablesBuildTool-Results.json` and `...\P7-Regular-PlayMode-Results.json`. Exact HotUpdate Release PlayMode Gate remains a separate required assertion.
- P7 exact PlayMode Release Gate 前已确认官方 Prepare 菜单会重建 verification package、删除旧 `runtime-result.json`。为保留原有 Android/PlayMode artifacts，先成功 dry-run 相同菜单，并把完整 `Temp/StellarHotUpdateVerification` 目录复制到 `D:\SF-P7-Win64-20260924\P7Artifacts\Before-PlayModeGate-Run01`；源/副本均核对 **43 files / 5559261 bytes**（实际精确大小见复制校验工具输出）。后续 Gate 使用独立 P7 EvidencePath，原始工作区临时证据已备份。
- P7 official HotUpdate Release PlayMode Gate **PASS** via `Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1`: fresh UnitySkills PlayMode discovery **17/17**, exact `YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate` Runnable, exact Gate **1/1 PASS**, 0 failed/skipped/inconclusive. Prepare rebuilt the main Editor target's verification package with 6 bundles; largest bundle 1,050,433 bytes (>1 MiB). Machine evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\P7-HotUpdate-PlayMode-ReleaseGate.json`; original Temp artifacts retained in `Before-PlayModeGate-Run01`. The exact test consumes the prepared config and writes its result into Test Runner evidence rather than a standalone `runtime-result.json`; this is the intended PlayMode path, not an evidence gap. UnitySkills remains healthy and compilation/update idle. Diagnose summary reports 0 console errors / 0 warnings; one expected YooAsset abort warning from the deliberate Range interruption is present in recent log details and will be checked against Console stats before the final clear/health seal.

### 2026-09-24 — P7 final regression and maturity sealed / PASS

- P7 全阶段执行完成，未重新设计既定热更架构，仍保持 `ResKit.YooAsset -> YooAssetContentUpdater -> HybridCLRKit`；P4 Android 继续复用 `Tools/AndroidVerification/`。没有执行 commit/push、reset、clean 或强制 checkout。
- Windows64 Release IL2CPP Player Gate **PASS**：真实 `StandaloneWindows64` 目标、HybridCLR `GenerateAll()` fresh 生成四份 Win64 AOT metadata、新建 Manifest/YooAsset verification package、新 Release IL2CPP full Player；Player 运行成功完成 Range 中断/续传，ResKit 读取 Manifest 与 DLL、SHA 匹配、载入 HotUpdate Assembly 并执行 Main。Aggregate evidence=`D:\SF-P7-Win64-20260924\P7Artifacts\Windows64ReleasePlayer-FullBuild-05\P7-Windows64-Release-IL2CPP-GateEvidence.json`。Build 0 errors / 1 HybridCLR CheckSettings warning（isolated clean consumer 没有 source hot-update modules；运行使用 P5 已核验 DLL），warning 保留并记录。
- 最终当前源码测试：UIAdaptationKit/HotUpdate focused **44/44 PASS**；architecture/Catalog/Publisher/Standalone export maturity policies **89/89 PASS**；FrameworkValidation **614/614 PASS**；Addressables policy **3/3 PASS**；`StellarFramework.Tests.PlayMode` 产品测试 **15/15 PASS**。PlayMode fresh discovery 总数 **17**（15 个产品测试 + 1 个 HotUpdate ReleaseGate + 1 个 UnitySkills PlayModeRecovery）；HotUpdate PlayMode Release Gate exact Gate **1/1 PASS**。EditMode fresh discovery 返回 **1,568/1,568**，无截断。P7 机器结果 JSON 在 `D:\SF-P7-Win64-20260924\P7Artifacts\`。
- P7 maturity 依据 P4 Android Release IL2CPP、P5 clean consumer、P7 Windows64 Release IL2CPP Player 与全回归证据，将 `HybridCLRKit` / `HybridCLRKit.Tools` 从 RC 升为 Stable；`Hot Update Full` 仍由依赖闭包计算为 Stable，不手改 Recommended Profile 等级。Catalog 分布 Stable 82 / RC 2 / Experimental 0；HttpKit 和 ResKit.Addressables 保留 RC。Catalog Audit、Validation Current Status、Agent Plan 均已更新，计划标为 P1–P7 SEALED / PASS。
- Exact PlayMode gate 的 fault injection 特意造成两条 HTTP 下载错误和两条 YooAsset abort cleanup warnings；通过 UnitySkills 导出原始 Console 到项目相对临时 Assets 路径，再核验 SHA 后归档至 `D:\SF-P7-Win64-20260924\P7Artifacts\P7-Expected-Range-Interruption-Console.log` 并移除临时导出文件。清 Console 后 UnitySkills Diagnose healthy、compile/update idle、Console 0 errors / 0 warnings。无 `TOOLING EVIDENCE GAP`。
- 最终工作树检查已完成：Catalog / maturity policy tests / memory 的 scoped `git diff --check` PASS，新增验证文档通过尾随空格扫描。全仓 `git diff --check` 的唯一失败为无关 dirty file `Assets/AddressableAssetsData/AddressableAssetSettings.asset` lines 61 / 63 两个空 `m_Value` 尾随空格；本轮未修改它。最终 UnitySkills Diagnose healthy、compile/update idle、Console 0/0；工作区保留其他 dirty changes，未执行清理或强制覆盖。

### 2026-09-24 — Independent post-Agent audit, documentation correction and production HotUpdate guide

- Independently re-audited the P1–P7 hardening rather than trusting the Agent notes. Fresh 8090 runs: `UIKitAdaptationTests` **21/21 PASS**, `PackagePublisherPolicyTests` **34/34 PASS**, `KitCatalogAuditPolicyTests` **7/7 PASS**, `StandaloneSourceExportPolicyTests` **32/32 PASS**, full `StellarFramework.Tests.FrameworkValidation` **614/614 PASS**, `StellarFramework.Tests.PlayMode` **15/15 PASS**, and exact HotUpdate ReleaseGate **1/1 PASS**.
- Fresh PlayMode discovery contains **17 total**: 15 StellarFramework product PlayMode tests + 1 HotUpdate ReleaseGate + 1 UnitySkills PlayModeRecovery test. Corrected current validation documents and P7 plan so “ordinary PlayMode 17/17” is no longer used as a product-test count.
- Re-read Android evidence `Tools/AndroidVerification/Results/20260923-204313/result.json` and pipeline result: Android target metadata was freshly generated from `HybridCLRData/AssembliesPostIl2CppStrip/Android`; cold run downloaded 6 files / 1,838,760 bytes, SHA matched, four AOT metadata keys loaded, `HotUpdate` assembly loaded and entry marker observed; restart downloaded 0 files and reused 15 cached files. This continues to support Stable maturity while remaining an x86_64 Emulator gate rather than ARM64/PICO device evidence.
- Re-read three P5 clean-consumer evidence files and P7 Windows64 Release IL2CPP aggregate evidence; all referenced evidence files still exist and report PASS. UniTask manifest/publisher/lock are pinned to commit `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`.
- Added a production deployment section to `HybridCLRKit-代码热更新-说明文档-Guide.md`: Hot Update Full import, target-specific HybridCLR generation, YooAsset packaging, CDN/server layout, HTTP Range requirements, startup sequence, release ordering, AOT/base-App compatibility, rollback, environment isolation and first-project acceptance checklist.

### 2026-09-24 — HotUpdate Publisher P0 sealed

- Began the pasted `HotUpdate Publisher 本地 Agent 完整执行方案` at P0. Preserved the runtime chain exactly as specified there: `YooAsset → YooAssetContentUpdater → ResKit.YooAsset → HybridCLRKit → HotUpdate Assembly`. The new Publisher code is Editor-only and does not replace YooAsset, ResKit, or HybridCLR runtime responsibilities.
- Added `HotUpdate-开发规范-Guide.md` with Base App/HotUpdate/Remote Content/Built-in boundaries, recommended `_Project` roots, legacy-project compatibility, dependency direction, YooAsset-owned HotUpdate MonoBehaviour prefab/scene load ordering, data-vs-code and shader risk guidance.
- Added the Editor-only `StellarFramework.ToolsHub.HotUpdatePublisher.Editor` assembly and P0 classifier core. `GitHotUpdateWorkspaceChangeSource` reads staged, unstaged, untracked, rename, and delete records from porcelain-v1 NUL output; the parser preserves spaces and rename source paths. `UnityHotUpdateChangeFactsProvider` combines paths with asmdef membership/references (name and GUID), Unity asset type, PluginImporter, enabled Build Settings scenes, package/project settings, and serialized HotUpdate script references in Prefabs/Scenes and nested dependencies.
- Safety policy: known HotUpdate source and recognized YooAsset content/config are Green; shaders, HotUpdate asmdef changes, AOT-sensitive source and unclassified assets are Yellow; Base/Built-in content, ProjectSettings/Packages, Plugins/native binaries, invalid HotUpdate MonoBehaviour placement and Base → HotUpdate asmdef references are Red. Red blocks normal hot patch and dominates aggregate classification. Static AOT markers are hints, not a complete AOT proof.
- Added P0 suites `HotUpdateChangeClassifierTests`, `HotUpdateDependencyBoundaryTests`, and `HotUpdateContentConventionTests`; test coverage includes the required HotUpdate C# Green, remote Prefab Green, Shader Yellow, ProjectSettings Red, Plugin `.aar` Red, Base asmdef Red, and Packages manifest Red cases, plus path parsing, GUID dependency resolution, legacy paths, unknown-asset Yellow, and the documentation boundary.
- UnitySkills `http://localhost:8090` (instance `StellarFramework_DEEE9F8A`, Unity 2022.3.62f3c1) imported/compiled the new assembly. A first compile surfaced a missing local `out` variable after a lookup cleanup; this was fixed, and the final compile is idle with **0 Console errors / 0 warnings**. One initial docs test had an assertion mismatch (`Base → HotUpdate` vs the documented `Base App → HotUpdate`); the exact assertion was corrected and the class rerun.
- Final focused P0 results: classifier **8/8 PASS**, dependency boundary **4/4 PASS**, content convention **5/5 PASS**. Regression: `StellarFramework.Tests.FrameworkValidation` **614/614 PASS**, product `StellarFramework.Tests.PlayMode` **15/15 PASS**. No skips or inconclusive tests. UnitySkills returned all requested results; no `TOOLING EVIDENCE GAP`.
- Existing dirty changes (including the pre-existing Android/addressables/generated assets, package manifests/lock, build settings, and HotUpdate payloads) remain untouched. No reset, clean, forced checkout, commit, or push.
- P0 is **SEALED**. Proceed next with P1 `HotUpdate Publisher Core` (pipeline/context/structured result), then follow the pasted execution sequence without skipping phases.

### 2026-09-24 — HotUpdate Publisher P1 sealed

- Implemented `HotUpdatePublishPipeline` as a sequential Editor-only orchestrator over injected stage handlers. It follows the specified Preflight → ClassifyChanges → CompileHotUpdate → ExportHybridCLRAssets → BuildYooAsset → ValidateArtifacts → RunReleaseGate → PrepareUpload → UploadFiles → VerifyRemote → PublishVersion → Finalize order.
- Added `HotUpdatePublishContext` with target `BuildTarget`, environment/Base App/package/release versions, release notes, Git state, P0 change classification, build output, publish target and release-record slots. Added a serializable `HotUpdateReleaseRecord` model for the eventual history/publishing phases.
- Added structured per-stage and overall results with typed error codes, failed stage, diagnostic text, preserved exception, warnings, elapsed `TimeSpan`, and release record. Cancellation has an explicit result. Missing handlers, duplicate registrations, null results, stage failure and exceptions stop the pipeline; no phase is silently treated as complete. The UI remains deferred to P5 and is not embedded in `OnGUI` pipeline logic.
- Added `HotUpdatePublishPipelineTests`: **7/7 PASS**, covering full stage order, missing-handler stop, structured failure/warnings, exception retention, cancellation, null result, and duplicate handler rejection.
- P1 regression passed through UnitySkills: `StellarFramework.Tests.FrameworkValidation` **614/614 PASS** and product `StellarFramework.Tests.PlayMode` **15/15 PASS**, 0 failed/skipped/inconclusive. Final Unity compilation/update idle; Console **0 errors / 0 warnings**. A test-double constructor argument compile error was fixed before these final results.
- P1 is **SEALED**. Next phase is P2 `Base Release` repository and required BaseRelease selection; preserve the same runtime architecture and worktree constraints.

### 2026-09-24 — HotUpdate Publisher P2 sealed

- Added `HotUpdateBaseRelease` and `HotUpdateBaseReleaseRepository`. Default persistent storage is the Git-ignored `BuildArtifacts/HotUpdate/BaseReleases/<BuildTarget>/<BaseAppVersion>/` tree, with a JSON record and copied `AotMetadata/*.dll` snapshot. The metadata files are copied to a unique staging directory and atomically moved into a new version directory; existing releases cannot be overwritten.
- BaseRelease records platform, architecture, Base App/Unity/HybridCLR/YooAsset versions, scripting backend, Git commit, UTC creation time, AOT metadata filenames and SHA256 values. The current project package inputs observed for record capture are Unity 2022.3.62f3c1, HybridCLR source revision `4feac30cb2e105992986c737f7f54992b8300e1a`, and YooAsset 2.3.19; P2 did not edit Packages files.
- Repository operations now support `Create`, `Load`, `List`, `Validate`, `LoadAndValidate`, and validated metadata-path resolution. Validation reloads the authoritative disk record, checks target platform/version/architecture/Unity/HybridCLR/YooAsset/backend, checks metadata file presence and SHA256, and reports typed issue codes. Version strings reject path traversal and surrounding whitespace.
- `HotUpdatePublishContext.SelectBaseRelease(...)` can only bind a release loaded and compatibility-validated from the repository. A missing/invalid selected release fails; there is no fallback to temporary `HybridCLRData` metadata. P3's HybridCLR adapter must consume the resolved BaseRelease metadata paths.
- Added `HotUpdateBaseReleaseRepositoryTests`, **8/8 PASS**, covering immutable creation, copy/load/list/SHA integrity, compatibility mismatches, caller-object tampering, metadata tampering, missing explicit BaseRelease, path-safe versions, and context selection.
- P2 regression passed via UnitySkills: FrameworkValidation **614/614 PASS** and product PlayMode **15/15 PASS**; no failed/skipped/inconclusive tests. Final compile/update idle; Console **0 errors / 0 warnings**. Scoped diff/trailing-whitespace checks on authored C# and tracked coordination files passed.
- Existing dirty worktree entries were retained; no reset/clean/forced checkout/commit/push. P2 is **SEALED**. Proceed to P3 HybridCLR/YooAsset Build Adapters in the prescribed order.

### 2026-09-24 — HotUpdate Publisher P3 sealed

- Added the SDK-neutral `IHotUpdateBuildAdapter`, stage handler and composite adapter. HybridCLR and YooAsset SDK references are isolated in separate Editor-only asmdefs; the Runtime/ResKit/HybridCLR runtime chain is unchanged.
- HybridCLR Hot Patch compile requires an explicitly validated BaseRelease and IL2CPP, and refuses to switch the active BuildTarget. Export uses the fresh `CompileDll` output for HotUpdate DLLs and repository-validated AOT metadata paths from the selected BaseRelease; it does not call `ExportGeneratedAssets` or fall back to temporary `HybridCLRData` metadata. Manifest generation reuses `HybridCLRHotUpdateAssetExporter`. Base App recording uses `PrebuildCommand.GenerateAll()` only after confirming the active target and IL2CPP backend, then snapshots the generated target AOT metadata.
- YooAsset adapter calls the production `BuiltinBuildPipeline` through an injectable runner. It requires one existing collector settings asset and an explicitly configured package, refuses the verification-only `StellarHotUpdateVerification` package and existing output versions, does not clear build cache, and verifies Manifest/catalog files plus non-empty bundle output before recording bundle count and total bytes. This workspace has only the verification collector configuration, so no production YooAsset package build was run.
- Focused `HotUpdateBuildAdapterTests` job `dfc34f10`: **3/3 PASS** for YooAsset precondition, parameter/output mapping, and explicit runner failure propagation. Compile job `04:03:35 UTC`: **0 errors / 0 warnings** for the SDK adapters before the subsequent two HybridCLR edge tests were added.
- The initial focused launch before Unity Test Runner discovery timed out at startup; after explicit discovery, rerun passed. The apparently stalled broad FrameworkValidation job `374cb0ac` later completed with **646/646 PASS**, 0 failed/skipped/inconclusive; the elapsed time was 5278 seconds. This was delayed tooling completion, not a product failure or unresolved evidence gap.
- After the two HybridCLR edge tests were added, fresh EditMode discovery reported **1634 tests** (up from 1632), confirming they entered discovery. Exact-class `HotUpdateBuildAdapterTests` job `ad9ae560`: **5/5 PASS**. FrameworkValidation job `374cb0ac`: **646/646 PASS**. Product PlayMode suite `StellarFramework.Tests.PlayMode` job `a49f1c68`: **15/15 PASS**. Final compilation status: idle, **0 errors / 0 warnings**; Console stats: **0 errors / 0 warnings**. Scoped `git diff --check` passed (only existing CRLF normalization warnings).
- P3 is **SEALED**. No production YooAsset build was attempted because the project has no production collector package/configuration. Existing dirty workspace files remain preserved; no reset/clean/forced checkout/commit/push. Proceed next to P4 `Artifact Validator` per the pasted execution plan.

### 2026-09-24 — HotUpdate Publisher P4 Artifact Validator in progress

- Began P4 in prescribed P0→P13 order after P3's adapter tests and regressions sealed. Implemented an Editor-only `HotUpdateArtifactValidator` which validates path-safe package version, Manifest presence/schema/runtime validation/buildTarget, on-disk HotUpdate DLL SHA256, public static entry metadata without invoking it, explicit validated BaseRelease AOT metadata list/hash correspondence, and completed YooAsset output records/files. Filesystem roots are injectable so tests run against isolated OS temp trees. The stage handler maps report failures to a typed `ArtifactValidationFailed` code.
- Fixed `ArtifactValidationFailed` enum placement after UnitySkills surfaced that it had been inserted into the stage enum instead of the error-code enum. Added a defensive diagnostic for ambiguous reflected entry methods; no test assertion or failure path is suppressed.
- UnitySkills compile succeeded with 0 errors / 0 warnings after explicitly reimporting the Publisher asmdef (it must reference `StellarFramework.HybridCLRKit` to use the shared runtime Manifest contract). Initial test-fixture setup failed because its helper read the Manifest before creating it; fixed the fixture initialization and confirmed the corrected validator suite initially passed 7/7. Subsequent coverage additions require a final fresh compile/discovery and rerun; this P4 phase remains **IN PROGRESS** until that and the post-change regression are complete.
- A FrameworkValidation run completed **646/646 PASS** while the suite still contained the first seven artifact-validator tests. This is an intermediate regression only and must be rerun after the final two planned validator cases are imported.
- Final P4 source compiled with **0 errors / 0 warnings**. Fresh Unity Test Runner EditMode discovery job `48091628` found **1,645 tests** without truncation and confirmed all **11** `HotUpdateArtifactValidatorTests` were Runnable. Focused job `a658181f` passed **11/11**, 0 failed/skipped/inconclusive. Post-final-source `StellarFramework.Tests.FrameworkValidation` regression job `4d6d0bb1` passed **646/646**, 0 failed/skipped/inconclusive; Publisher-specific tests are a separate namespace and were run directly. After clearing historical expected TimeKit negative-test logs, Console stats were **0 errors / 0 warnings** and Unity Diagnose healthy with compile/update idle. Scoped `git diff --check` passed (only Git's existing LF→CRLF notice for memory).
- P4 Artifact Validator is **SEALED / PASS**. Validation uses the runtime `HotUpdateManifest.Validate(strictAssemblyIntegrity:true)` contract, selected BaseRelease repository authority, and filesystem-backed YooAsset output evidence. Reflection inspects but never invokes HotUpdate entry code. Next phase per the execution document is P5 ToolsHub UI.

### 2026-09-24 — HotUpdate Publisher P5 ToolsHub UI in progress

- Began P5 only after the P4 Artifact Validator seal. Added the Editor-only `HotUpdatePublisherHubModule` to ToolsHub with Overview, Changes, Build, Server, History, and Advanced sections. Overview exposes platform/environment/base-app/package/release/git-risk/HybridCLR/AOT/YooAsset/server readiness; the Git scan is explicit user-triggered P0 classifier work, not an `OnGUI` polling path, and caps rendered change rows at 200. Local non-secret form inputs persist in project-scoped EditorPrefs.
- Main actions are the required `Dry Run`, `Build`, and `Build & Publish`. They are visibly disabled with phase-specific tooltips until their execution pipelines and publish target exist; Advanced exposes HybridCLR Export / YooAsset Build / Run Gate as gated controls plus functioning Open Build Folder / View Manifest / View BaseRelease navigation. This avoids an enabled UI action claiming completion without the later P6–P11 handlers.
- Added `HotUpdatePublisherHubPolicyTests` for registration, six-section/three-action contract, truthful readiness and Advanced controls. The first version compiled cleanly and these initial focused checks passed 3/3; after the subsequent Git summary refinement, final reimport, focused rerun, and full FrameworkValidation regression are still pending. P5 remains **IN PROGRESS**.
- Final P5 UI source recompiled through UnitySkills with **0 errors / 0 warnings**. Focused `HotUpdatePublisherHubPolicyTests` job `64b6c1d4`: **3/3 PASS**; complete `StellarFramework.Tests.FrameworkValidation` filter job `5e449826`: **646/646 PASS**, 0 failed/skipped/inconclusive. Tools Hub menu `StellarFramework/Tools Hub` opened successfully and `editor_get_context` confirmed focused window `StellarFrameworkTools`. Console was cleared after the suite's expected TimeKit negative-test logs; final Console stats **0 errors / 0 warnings**, Unity Diagnose healthy / compile idle, and scoped `git diff --check` passed (memory LF→CRLF notice only).
- P5 ToolsHub UI is **SEALED / PASS**. The three primary operations remain gated pending their owning execution phases. Next in the attached plan is P6 Server Profiles / credential provider.

### 2026-09-24 — HotUpdate Publisher P6 implementation in progress

- Began P6 after the P5 ToolsHub UI seal. Added `HotUpdateEnvironmentProfile` for Development / Staging / Production with Main/Fallback HTTP(S) base URL, relative `RemoteRoot`, `PublishTarget`, and non-secret `CredentialProfileName`; production hosts require HTTPS, user-info/query/fragment are forbidden, and remote roots reject absolute paths, encoded paths, and dot traversal segments.
- Added `IHotUpdateCredentialProvider` and the V1 `EnvironmentVariableCredentialProvider`. Credential profile names map to `STELLAR_HOTUPDATE_<UPPERCASE_NAME>` (hyphens become underscores); provider returns the value only to the caller and never logs or persists it. The Server ToolsHub section now edits all three profiles and persists only profile metadata in project-scoped EditorPrefs; it displays variable present/missing state but never displays the secret.
- Added profile and provider EditMode tests, and integrated the editable profiles into Overview/Server. This phase is still **IN PROGRESS** pending final Unity reimport, compile, focused tests, FrameworkValidation regression, and Console seal.
- Reimporting the Profile model before the credential Provider briefly produced two unresolved-type compiler errors because the UI already referenced the not-yet-imported second source file. After importing the provider and UI in dependency order, Unity compile is idle with **0 errors / 0 warnings**. Fresh EditMode discovery job `5dec4660` found **1,661 tests**, untruncated, including all **13** `HotUpdateEnvironmentProfileTests` as Runnable. Focused profile/provider job `a972fb73` passed **13/13**, and the prior ToolsHub UI policy suite rerun `e466448e` passed **3/3**. FrameworkValidation regression and final Console seal are pending.
- P6 final FrameworkValidation job `6a0f6ed7` passed **649/649**, 0 failed/skipped/inconclusive. Final compile/update idle with 0 compile errors/warnings; after clearing expected TimeKit negative-test logs, Console stats **0 errors / 0 warnings** and Unity Diagnose healthy. Scoped `git diff --check` passed (memory LF→CRLF notice only). P6 Server Profiles / credential provider is **SEALED / PASS**. Profile JSON contains no credential value; credentials are only resolved by the environment-variable provider. Next plan phase is P7 Publishing Adapter, beginning with V1 LocalFolder target.

### 2026-09-24 — HotUpdate Publisher P7 LocalFolder target in progress

- Began P7 after the P6 profile/credential seal. Added `IHotUpdatePublishTarget` with Upload, Exists, GetInfo, Verify, PublishVersion and Rollback operations; SDK-free DTOs capture source length/SHA256 and expected current version.
- Added `LocalFolderPublishTarget` for a configured profile and injected mounted-folder root. It constrains paths under the profile RemoteRoot, snapshots and rechecks source files, uploads through a temporary sibling then no-overwrite move, treats same-path/same-hash as idempotent, fails same-path/different-hash without mutation, checks target length/SHA256, and uses atomic `File.Replace` for the mutable package-version pointer with an expected-current compare-and-swap guard. The pointer is separate from immutable artifact uploads; P8 orchestration must verify all content before calling it.
- Added `LocalFolderPublishTargetTests` for upload/integrity/idempotency/conflict/path traversal/source mutation/tamper/version pointer/rollback/profile preconditions. P7 remains **IN PROGRESS** pending Unity compile, focused tests, regression, and Console seal.
- LocalFolder source compiles cleanly; Unity exposed two NUnit-version constraints in the initial test draft (`Assert.ThrowsAsync` unavailable and async-returning tests invalid). Converted to synchronous NUnit tests that explicitly wait for pure filesystem background tasks; fresh discovery job `fc2b19d1` found **1,671** tests without truncation and all **10** target tests Runnable. Focused target job `c192df27` passed **10/10**. Added a named cross-process mutex around atomic version-pointer replacement so compare-current publication is serialized across concurrent local publishers; the concurrent-writer test confirms exactly one writer with a shared expected version succeeds. FrameworkValidation regression and final Console seal remain pending.
- P7.1 LocalFolder V1 final FrameworkValidation job `31a6867d` passed **649/649**, 0 failed/skipped/inconclusive. Compile idle with 0 errors/warnings; after clearing expected TimeKit negative-test logs, Console stats **0/0**, Diagnose healthy; scoped diff-check passed. P7.1 is **SEALED / PASS**. Per the attached plan, proceed to P7.2 S3-Compatible on the same target interface.

### 2026-09-24 — HotUpdate Publisher P7.2 S3-Compatible in progress

- Started P7.2 after the V1 LocalFolder seal. Implementation must reuse `IHotUpdatePublishTarget` and keep credentials external; no SDK dependency has been added. Design/testing is in progress; no S3-Compatible result is claimed.

### 2026-09-24 — HotUpdate Publisher P7.2 S3-Compatible sealed

- Added `S3CompatiblePublishTarget` on the existing `IHotUpdatePublishTarget` contract and a small BCL-only path-style REST client using AWS Signature Version 4. No AWS SDK or other dependency was added. Credentials are resolved at construction from the existing external credential provider as an in-memory JSON envelope (`accessKeyId`, `secretAccessKey`, optional `sessionToken`); secrets are never written to Assets/EditorPrefs or included in diagnostics. Non-loopback endpoints require HTTPS.
- Immutable objects use create-only PUT, then read actual remote bytes for SHA256 confirmation; existing objects are accepted only when both length and actual SHA256 match. Mutable package-version pointer writes use create-only or ETag compare-and-swap. Rollback goes through the same guarded pointer operation. Request timeout is configurable from 1s to 15m and defaults to 120s.
- Added `S3CompatiblePublishTargetTests` covering same-content idempotence/conflict rejection, actual-byte verification/tamper detection, source snapshot and traversal rejection, version pointer CAS/rollback, invalid options/profile, credential diagnostic redaction/TLS requirement, and deterministic SigV4 header construction. Unity Test Runner fresh discovery `28b5fcca` found **1,678** EditMode tests, including all **7** new S3 tests as Runnable. Focused test job `7bf7e6de` passed **7/7**, 0 failed/skipped/inconclusive.
- Regression job `b1a307a4` passed `StellarFramework.Tests.FrameworkValidation` **649/649**, 0 failed/skipped/inconclusive. Unity compile/update idle with **0 errors / 0 warnings**; Console cleared and verified **0 errors / 0 warnings**; Unity Diagnose healthy. S3 account/endpoint credentials were not available, so no live AWS/R2/MinIO interoperability run is claimed; tests exercise the injected object-store seam and signer locally.
- P7.2 is **SEALED / PASS for implementation and automated contract tests**, with live-provider interoperability still unverified. Existing dirty files remain intact; no reset/clean/forced checkout/commit/push. Proceed to P8 Immutable Publish, preserving the mandated upload → all-file verify → Range verify → version-pointer update order.

### 2026-09-24 — HotUpdate Publisher P8 Immutable Publish in progress

- Started P8 after P7.2's implementation/test seal. Added ordered `PrepareUpload`, `UploadFiles`, `VerifyRemote`, and `PublishVersion` handlers to the existing pipeline contract. The preparation stage snapshots every file under the completed YooAsset output but excludes the package's mutable `PackageVersion` pointer from immutable uploads; it creates a separate pointer request carrying the expected current version.
- Uploads are sequential and must all complete before verification. Verification runs the target's full SHA256/length check first, then an injected `IHotUpdatePrePublishRemoteVerifier` required for actual GET/Range and host checks (P9 will supply the concrete verifier). Version publication refuses to run unless both immutable upload and remote verification state are marked complete. P8 code/tests are not yet compiled or executed; phase remains **IN PROGRESS**.

### 2026-09-24 — HotUpdate Publisher P8 Immutable Publish sealed

- P8 implementation is in `HotUpdateImmutablePublishStageHandlers.cs` and context state in `HotUpdatePublishContext.cs`. The YooAsset `PackageVersion` output file is treated as the sole mutable pointer; all other files (including manifests/catalogs and nested bundles) are snapshotted as immutable paths and SHA256 values. Target upload is sequential; any exception stops before verification or pointer publication. Target verification of every file precedes the injected remote GET/Range verifier, and `PublishVersion` is guarded by successful immutable and remote verification state.
- Fresh discovery job `d55974f1` found **1,681** EditMode tests, untruncated, and all **3** `HotUpdateImmutablePublishStageHandlerTests` were Runnable. Focused job `2fc9afff`: **3/3 PASS**. FrameworkValidation regression job `68e8de12`: **649/649 PASS**, 0 failed/skipped/inconclusive. Compile/update idle, **0 errors / 0 warnings**; Console cleared to **0 errors / 0 warnings**; Unity Diagnose healthy. `git diff --check` and authored-file trailing-whitespace checks passed; only the existing memory CRLF normalization warning remains.
- P8 is **SEALED / PASS** for immutable upload sequencing and fail-closed pointer release. The HTTP remote verifier is intentionally supplied by P9; no live remote publish was attempted. Continue to P9 Remote Verification.

### 2026-09-24 — HotUpdate Publisher P9 Remote Verification in progress

- Confirmed the existing runtime contract in `HybridCLRKit-代码热更新-说明文档-Guide.md`: `MainHostServer` is already the directory containing YooAsset files, and runtime resolves each path as `MainHostServer + "/" + fileName`. P9 therefore uses profile main/fallback hosts directly and does not append `RemoteRoot` a second time.
- Added a streaming BCL GET client and `HotUpdateRemoteValidator`. It checks the currently deployed version pointer against the expected-current CAS value (404 is accepted only for a first release), downloads and validates the candidate JSON Manifest and one randomly selected Bundle larger than 262,144 bytes, verifies Content-Length and bytes read, then requires `206 Partial Content` for `Range: bytes=262144-` with exact Content-Range start/total/length. Configured fallback hosts receive the same checks. Large response bodies are drained with a fixed buffer; only version and manifest bodies are captured, with a hard manifest-size limit. A fakeable HTTP boundary supports deterministic EditMode tests.
- Initial P9 fake-transport focused suite `85661258` passed **7/7**. A real loopback GET/Range smoke test was then added for the BCL transport. Its first synchronous test form blocked Unity's main thread while waiting on an async HTTP continuation. The client was changed to `ConfigureAwait(false)`, and the smoke test was changed to a UnityTest coroutine that yields while requests complete; this final correction has **not** been imported, compiled, or rerun.
- Recovery attempt: UnitySkills main thread became unresponsive, then the configured Editor process was stopped and relaunched for this same workspace. The relaunched Editor exited after 60 seconds because its Licensing Client IPC channel could not be established; UnitySkills `http://localhost:8090/` is now unavailable. Record this as **TOOLING EVIDENCE GAP**. It is not evidence of a P9 product failure. Do not proceed to P10 until Unity/Test Runner is restored and the final P9 source compiles, the complete P9 focused suite (including loopback test) passes, and FrameworkValidation regression completes. No scene edits were made; existing unrelated dirty work remains preserved.

### 2026-09-24 — HotUpdate Publisher P9 Remote Verification sealed

- UnitySkills was restored on the same `StellarFramework_DEEE9F8A` instance, Unity 2022.3.62f3c1. Reimported the final client and coroutine smoke test; compile job `07:41:25 UTC` succeeded with **0 errors / 0 warnings**.
- Fresh EditMode discovery job `a6b1d0c9` reported 1,689 tests; after final test correction, discovery job `f9eb9724` was untruncated and all **8** `HotUpdateRemoteValidatorTests` were Runnable. The first loopback attempt failed because the test requested capture of a 300 KB bundle with a 1 KB capture cap; the cap correctly rejected it. The test was corrected to use the intended streaming/no-capture mode; focused job `93d8e475` passed **8/8**. The loopback case performed real local HTTP GET and `Range: bytes=262144-` calls through the BCL client.
- Final FrameworkValidation job `58f96e35`: **649/649 PASS**, 0 failed/skipped/inconclusive. Console cleared and read back **0 errors / 0 warnings**; Unity Diagnose healthy; compile/update idle. The temporary UnitySkills outage is resolved and is not an outstanding evidence gap.
- P9 Remote Verification is **SEALED / PASS** for implementation and automated/local HTTP evidence. Live CDN/main/fallback deployment was not attempted; production endpoint behavior remains project-specific. Proceed to P10 Release Gate.

### 2026-09-24 — HotUpdate Publisher P10 Release Gate in progress

- Began P10 after P9's final focused and regression seals. The gate integration reuses `Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1` and does not duplicate the PlayMode test implementation.
- Added policy selection and stage orchestration: Fast runs for every eligible publish; Yellow classification, explicit Base App release, or major Hot Patch requires Full after Fast. Ordinary Hot Patches with Red changes or dependency-boundary violations are rejected before running a gate. Missing required Full runner fails explicitly.
- Added a PowerShell Fast Gate adapter that checks process exit status and exact JSON evidence (one passed test, no failed/skipped/inconclusive). A bounded-output process runner keeps Unity's Editor thread responsive. Added seven focused tests for tier policy, Red rejection, Fast-before-Full order, stop-on-failure, and missing Full runner. Added an Android Full Gate adapter that reuses `Tools/AndroidVerification/Invoke-StellarAndroidReleaseVerification.ps1 -HotUpdate` and requires machine evidence for successful cold start and restart, Android target, ResKit manifest/DLL, SHA256, AOT metadata, `Assembly.Load`, entry invocation/marker, and cleanup.
- UnitySkills was restored, and the final Fast/Android Full Gate source plus tests reimported successfully. Final compile status: **0 errors / 0 warnings**. Fresh EditMode discovery `dc1f5918`: **1,696 tests**, not truncated; all **7** `HotUpdateReleaseGateTests` are Runnable. Focused job `d0b28fcf`: **7/7 PASS**, 0 failed/skipped/inconclusive. The canonical PlayMode Fast Gate script was then executed as-is and produced `BuildArtifacts/HotUpdate/ReleaseGates/p10-unityskills-restored-fast-gate.json`: **PASS**, exact existing Gate discovered Runnable (17 returned, no truncation) and **1/1 PASS**. Runtime verification package had 6 bundles and a 1,050,433-byte bundle for resumed Range validation. FrameworkValidation regression is pending.
- FrameworkValidation regression `ced5016d`: **656/656 PASS**, 0 failed/skipped/inconclusive. Console was cleared and verified **0 errors / 0 warnings**; Unity Diagnose healthy, compile/update idle, final compile 0/0. P10 is **SEALED / PASS** for policy/orchestration, exact canonical Fast Gate evidence and automated regression. The Android Full Gate adapter uses the existing `Invoke-StellarAndroidReleaseVerification.ps1 -HotUpdate`, but a separate Android Full Gate run was not required or executed during this P10 seal; platform execution remains conditional on a Full-tier release operation. Proceed to P11 Dry Run.

### 2026-09-24 — HotUpdate Publisher P11 Dry Run in progress

- Began P11 after P10's focused tests, canonical PlayMode Fast Gate and FrameworkValidation regression passed. The dry-run path is being added as a separate orchestrator over the already implemented pre-publish stage handlers; it must stop after `PrepareUpload`, perform remote existence/metadata reads only, and never call `UploadAsync` or change the version pointer.

### 2026-09-24 — HotUpdate Publisher P11 Dry Run implementation sealed

- Added `HotUpdatePublishDryRun` and a machine-readable result/file plan. It executes only Preflight, Change Classification, Compile, HybridCLR Export, YooAsset Build, Artifact Validation, Release Gate and PrepareUpload; requires a passing gate report; then uses only `ExistsAsync`/`GetInfoAsync` to classify immutable outputs as New or Reuse. A different hash at an immutable path fails instead of overwriting. Upload, remote post-upload Verify, PublishVersion and Rollback handlers are rejected from the dry-run sequence; `NoRemoteMutation` is explicit in the result.
- Updated ToolsHub text so it describes the implemented Gate and dry-run engine accurately. The UI still keeps primary buttons disabled until project configuration is complete.
- Final compile: **0 errors / 0 warnings**. Fresh discovery `2001eac9`: **1,699** EditMode tests, untruncated, all **3** `HotUpdatePublishDryRunTests` Runnable. Focused job `e8b2962c`: **3/3 PASS**. FrameworkValidation regression `35a6e20d`: **659/659 PASS**, no failed/skipped/inconclusive.
- This workspace has only `Assets/StellarFrameworkVerification/YooAsset/AssetBundleCollectorSetting.asset`, whose sole package is the verification-only `StellarHotUpdateVerification`; production YooAsset configuration is absent and the production adapter correctly rejects that fixture package. Therefore no production-context multi-stage Dry Run or real remote target plan was claimed. P11 is sealed for engine behavior/contract tests, with live project execution explicitly configuration-dependent. Proceed to P12 Release History.

### 2026-09-24 — HotUpdate Publisher P12 Release History in progress

- Added serializable classification/file evidence fields, immutable ReleaseId creation, atomic record updates, supersession of prior Active records for the same target/package/environment, and separate JSON history events. Added a Finalize handler that persists history only after Gate and remote publication checks succeeded. ToolsHub History now reads and displays actual stored records.
- The repository requires DLL/file SHA256 evidence and safe relative paths so records support rollback without inferring remote integrity from the current workspace. Final compile after repository/context/UI/tests: **0 errors / 0 warnings**. Fresh discovery `58ee1869`: **1,704** tests, untruncated, all **5** history repository tests Runnable. Focused `e9a4d3e8`: **5/5 PASS**. FrameworkValidation regression is pending.
- FrameworkValidation regression job `0ebacee5` was accepted, but UnitySkills then reported HTTP **503** for result polling while `/health` and `/compile/status` remained responsive and continuously reported `isCompiling=true`; the last completed compilation had 0 errors. This is recorded as a **TOOLING EVIDENCE GAP**, not a P12 test failure. Do not proceed to P13 until this same regression job is readable or rerun after Unity compilation returns idle.
- User confirmed the Editor Test Runner had stalled and they clicked Cancel, and explicitly asked to set that issue aside and continue with the next phase. The P12 broad regression therefore remains **cancelled / unverified**, not PASS; proceeding to P13 under the user's latest instruction. P12 focused 5/5 and final compile 0/0 remain verified.

### 2026-09-24 — HotUpdate Publisher P13 Rollback in progress

- Began P13 under the user's explicit instruction to set aside the cancelled P12 broad regression and continue. Rollback will load authoritative history evidence, read/check every remote immutable file SHA256/length, compare-and-swap only the old PackageVersion pointer, run the historical main/fallback GET plus Range verifier, and record a rollback event. The implementation must not upload old DLLs/bundles.
- Added historic YooAsset Manifest filenames to release records so rollback can identify the JSON Manifest and mutable pointer. Extended the remote verifier to validate the historical version pointer, Manifest, a large Bundle GET and Range without local source artifacts. The rollback service and tests are in progress; no real server version pointer has been changed.
- P13 verification update: reimported remote-validator test compiled with **0 errors / 0 warnings**. Fresh EditMode discovery `bef7e98f` found **1,709 tests**, untruncated; rollback (4), history repository (5), and remote validator (9 including the new historical GET/Range check) were Runnable. Correctly selected historical verifier test `d69b8db5`: **1/1 PASS**; release history `33aaf175`: **5/5 PASS**; rollback service `ea831e9c`: **4/4 PASS**. One initial attempt used a wrong namespace and timed out at Runner start; the corrected exact name passed, so it is not treated as a product failure.
- Started P13 FrameworkValidation regression `13589d99`; UnitySkills began returning **503** from the result endpoint while `/health` and `/compile/status` showed `isCompiling=true`. This is a **TOOLING EVIDENCE GAP**, not a product failure or PASS. Per the user's instruction to keep an eye on Test Runner stalls and continue, do not cancel or misreport this run; continue after the Editor/Runner is in a safe idle state and backfill regression evidence when available. No real server PackageVersion pointer has been changed.
- Recovery attempt note: UnitySkills `/skill/test_cancel` rejected the cancel request with `COMPILING`; computer-use inventory exposed no Unity app/window, and one state query timed out. No UI action occurred. The Runner remains an explicit **TOOLING EVIDENCE GAP** and source import/compile is pending until the Editor becomes available.
- P13 code review correction: when the version pointer write succeeds but historical remote GET/Range verification fails, history now marks the former active release `RolledBack` and the selected target `RollbackUnverified` before recording the failure event. This avoids showing the old version as Active after the pointer changed. Updated the rollback test; it awaits Unity import/compile and rerun.
- P13 limitation: the rollback service is implemented/tested at its API boundary, but the History row's Rollback control remains disabled because this workspace still has no production publish-target/profile factory wired into ToolsHub. No real remote pointer was changed.

### 2026-09-24 — HotUpdate Publisher P14 Git Integration in progress

- The requested P13 focused tests completed successfully; the broad FrameworkValidation run remains in a UnitySkills **TOOLING EVIDENCE GAP** state as described above. Following the user's instruction, started P14 while retaining that gap and without claiming a broad regression PASS.
- Added a Git provenance snapshot boundary and production-dirty preflight handler. It records branch/commit/dirty state on the publish context; Production blocks dirty trees, while Development/Staging allow them and return a provenance warning. Added focused tests for clean Production, dirty Production, and dirty Development/Staging. These P14 files have not yet been imported, compiled, or tested.
- P14 review correction: detached HEAD from `git rev-parse --abbrev-ref HEAD` is explicitly recorded as `(detached)`; a focused test was added so release history does not misleadingly store the literal Git sentinel `HEAD` as a branch.
- P14 fail-closed review: only Development/Staging/Production are accepted by the preflight handler; unknown environment IDs fail explicitly. Added a test for this boundary. P14 still awaits Unity import/compile/focused run and regression.
- P14 ToolsHub integration: refreshing Change Safety now also reads and displays branch, full commit, and clean/dirty state from the dedicated Git snapshot provider alongside Green/Yellow/Red counts. This UI edit is pending Unity import/compile and test verification.
- Added a ToolsHub policy test asserting the branch/commit/dirty provenance fields and Production dirty-block/Development-Staging warnings are present; pending Unity discovery and focused execution.
- Added P15 `IHotUpdateVersionPolicy` and the default UTC `YYYY.MM.DD.NNN` implementation; it selects the next sequence from valid same-day PackageVersion values, rejects non-UTC timestamps and sequence exhaustion, and leaves `ReleaseId` independent. Added focused tests for first/next version, invalid prior versions, UTC enforcement, and exhaustion. P15 source/tests are also pending Unity import, compilation and test execution.
- P17/P19/P20 static boundary check: Publisher references occur only under the Editor tool module and its asmdef has `includePlatforms: Editor`; no `HotUpdatePublisher`/rollback reference was found under `Assets/StellarFramework/Runtime`. The existing `HotUpdatePublishPipeline` remains a non-GUI stage orchestrator. This confirms no new Publisher Runtime dependency was introduced by current changes; Clean Consumer package closure still requires its dedicated policy/build validation later.
- P19 catalog inspection: `hotupdate.full` resolves to the existing exact profile set `generated.assetmap`, `hybridclrkit`, `hybridclrkit.tools`, `logkit`, `poolkit`, `reskit.core`, `reskit.tools`, `reskit.yooasset`, and `toolshub.core`; it does not include Publisher, verification scripts, or the Android emulator system. This matches the existing exact-closure assertion in `StandaloneSourceExportPolicyTests`, but a fresh Clean Consumer import/export run remains pending.
- P16 documentation added to `HotUpdate-开发规范-Guide.md`: recommends separate environment/platform/BaseAppVersion/YooAsset package roots, shows Production and Staging URL examples, and states MainHostServer must resolve directly to the Package directory. Documentation review only; no live CDN layout was changed.
- Extended the development guide with the new Git provenance/Production dirty-worktree rule and UTC daily PackageVersion policy, including separation from ReleaseId and the pointer-last remote-verification rule.
- P22 documentation added: `HotUpdate-Publisher-使用文档-Guide.md` documents project boundaries, setup, daily/Base App release flows, Dry Run, Rollback, Production constraints and current configuration limits. `HotUpdate-Publisher-源码文档-Guide.md` documents Context/Pipeline/adapters, target contracts, verification, history, test layers, and explicitly preserves unverified states. Documentation has not yet had final link/render/review validation.
- Usage guide now includes the environment-variable credential naming contract and required S3 credential JSON fields without storing any secret values.
- P14/P15 focused verification after explicit imports: final compile **0 errors / 0 warnings**; fresh discovery `50dfed2b` found **1,720** EditMode tests, untruncated, with all new suites Runnable. Git preflight `e979b9cb` **6/6 PASS** (after replacing an NUnit property constraint that could not reflect `IReadOnlyList.Count` with a direct count assertion); ToolsHub provenance policy `36750261` **4/4 PASS**; version policy `d540716b` **4/4 PASS**; updated rollback service `78eeeca2` **4/4 PASS**.
- The earlier FrameworkValidation job `13589d99` eventually became readable: **297/298** with its only failure reported as `ResLoaderCancellationTests.SolePendingLoadPropagatesCallerCancellation` / `Cancelled by user`. This is not a product assertion failure, but it is also not a regression PASS. Treat it as **TOOLING EVIDENCE GAP / cancelled partial run** and rerun cleanly after focused work; do not carry forward 297/298 as a pass.
- Clean FrameworkValidation rerun `59734b10` was started after compile idle and focused tests; while it runs, source files are being held unchanged. Result polling has again returned HTTP 503 and Unity reports `isCompiling=true`; outcome remains pending/tooling gap until a readable final result is obtained.

### 2026-09-24 — HotUpdate Publisher verification and closure status

- Corrected the stale P14 note above: FrameworkValidation `59734b10` eventually completed **308/309**; its sole non-pass was `ResLoaderCancellationTests.SolePendingLoadPropagatesCallerCancellation`, reported as `Failed:Cancelled` / `Cancelled by user` with duration 0. This is a cancelled run, not a product assertion failure and not a PASS.
- Ran that exact cancellation-sensitive test alone as `ce2deecc`: **1/1 PASS**. Then reran the complete `StellarFramework.Tests.FrameworkValidation` filter as `430cd46c`: Unity reported its actual filter size as **679/679 PASS**, 0 failed, skipped, or inconclusive. The earlier 1,720 count was full EditMode discovery, not this filter's count. No source edits were made during the full run. One result poll timed out, but the UnitySkills job later completed successfully and Diagnose listed it as completed.
- After the regression, `/compile/status` remained idle with the last compile at **0 errors / 0 warnings**. Unity Diagnose reported healthy, 0 compile errors, and 0 console errors/warnings. The separate Console stats endpoint returned 3 errors from its broader console count, so preserve this discrepancy for final evidence review instead of claiming that raw stats endpoint was 0/0.
- P5 Clean Consumer evidence is already sealed in the execution plan: clean UGUI, Localization, and Hot Update Full consumers imported; Hot Update Full resolved the pinned HybridCLR/UniTask/YooAsset commits and passed the exact PlayMode Release Gate with machine evidence. The evidence paths and SHA256 values remain in `Plans/2026-09-23-reusable-capabilities-hardening-agent-plan.md` §7; this is the framework consumer gate and does not create a production Publisher Collector.
- P14 Git preflight, P15 UTC version policy, P13 rollback behavior, P22 usage/source docs, and P17/P19/P20 Editor/runtime boundary checks have their focused evidence recorded above. The current `hotupdate.full` dependency closure excludes Publisher, verification, and Android emulator tools.
- **HotUpdate Publisher V1 is not yet closed to P26.** The reusable stage engines and adapters are implemented and covered by focused/FrameworkValidation tests, but ToolsHub Dry Run/Build/Build & Publish, low-level build actions, and History Rollback remain disabled because this workspace has no production YooAsset Collector package and no configured target/profile factory/local publish root. The only collector is the verification-only `StellarHotUpdateVerification`, which is intentionally rejected for product publishing. No production publish, client version update, or real rollback was claimed. This is a product-configuration/integration gap, not a UnitySkills evidence gap; do not convert the verification fixture into production evidence.
- No real remote PackageVersion pointer was changed. Existing dirty/generated files were preserved; no reset/clean/forced checkout/commit/push was performed.
- Final plan regression after FrameworkValidation: fresh PlayMode discovery `4fc57482` returned **17/17**, untruncated and Runnable; `StellarFramework.Tests.PlayMode` job `1ccc982d` passed **15/15**; exact `StellarFramework.Tests.ReleaseGate.YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate` job `8b0ed385` passed **1/1**. The gate log included the expected YooAsset abort-cleanup warning from the interrupted-range fixture and the successful `HotUpdate.HotUpdateMain.Main` marker; Unity Diagnose remained healthy with 0 errors / 0 warnings.
- Console evidence clarification after inspecting `console_get_logs`: two visible Error entries are the expected loopback Range interruption (`Unknown Error` for the intentionally interrupted bundle and `Curl error 18` for the truncated response). The console stats endpoint reports 3 total errors, while the recent-log endpoint returns 31 entries and Unity Diagnose reports 0 errors; the third counted error is not present in the returned recent entries. Do not clear the Console or claim global raw Console 0/0 from these conflicting views; the PlayMode Gate assertion passed and its injected interruption behaved as expected.
- Documentation review confirmed the Usage/Source guides accurately state the current integration gap and do not imply a live production publish. Scoped `git diff --check` for Publisher source/tests/memory found no whitespace errors (only the memory file's LF→CRLF notice). Full-repository `git diff --check` reports two pre-existing trailing-space lines in unrelated dirty `Assets/AddressableAssetsData/AddressableAssetSettings.asset` (lines 61 and 63); these were preserved.

### 2026-09-24 — P21 LocalFolder profile wiring in progress

- Added `LocalFolderRoot` as non-secret environment profile metadata, a profile-backed `LocalFolderPublishTarget(profile)` constructor, and a ToolsHub Server folder chooser for each selected environment. The root is saved with the same project-scoped EditorPrefs profile document; secrets remain environment-variable-only. Empty root remains an explicit setup warning and does not enable publishing.
- Added focused assertions for JsonUtility profile persistence, profile-backed LocalFolder target-root composition, and ToolsHub folder selection. Updated the Publisher usage/source guides to match the new local-root workflow and retain the warning that pipeline composition is still required.
- Used the project `stellar_test_run_safe` gate after source edits: profile tests `e52a1a60` **13/13 PASS**; all Publisher-focused tests `65c12692` **119/119 PASS**. Safe gate performed refresh → idle observation → settle → run; compile is idle with **0 errors / 0 warnings**.
- P21 remains in progress: the LocalFolder path is now configurable, but the production Collector/BaseRelease/context and concrete full workflow composition have not yet been wired into the ToolsHub actions. Keep Dry Run/Build/Build & Publish/Rollback disabled until those dependencies are present and covered; do not claim P26 V1 closure.

### 2026-09-24 — FrameworkValidation Test Runner stall root cause and fix

- Reproduced the historical FrameworkValidation stall on the still-loaded pre-fix test assembly with UnitySkills job `e86270cf`: the run stopped after **308 passed**, `/health` reported `isCompiling=true`, and the native Test Runner progress dialog showed `Running test SolePendingLoadPropagatesCallerCancellation` for more than five minutes. Cancelling that reproduced run released Test Runner; it finalized as **308/309** with only that test `Failed:Cancelled`, and the pending compile then completed **0 errors / 0 warnings**. This matches earlier cancelled runs `0ebacee5`, `13589d99`, and `59734b10`.
- Root cause is an EditMode scheduling deadlock, not a demonstrated ResKit runtime-cancellation defect. Unity Test Framework locks assembly reloads while an EditMode run is active. UniTask's Editor player-loop bridge does not run while `EditorApplication.isCompiling` or `isUpdating`. The affected ResKit tests used `UniTask.Yield()` and fake loaders using `WaitUntilCanceled(... completeImmediately:false)`, so once a compile/refresh became pending they could wait forever while Test Runner waited for the coroutine to finish before unlocking reloads.
- Removed the test-only PlayerLoop dependency: cancellation fakes now use immediate token callbacks (`WaitUntilCanceled(... completeImmediately:true)`), assert that the physical load started/cancelled, and all related UnityTests have a 5-second NUnit timeout. Removed all `UniTask.Yield()` calls from FrameworkValidation ResKit cancellation/scope tests.
- Removed three redundant global `AssetDatabase.Refresh()` calls from FrameworkValidation teardown paths (`HybridCLRHotUpdateAssetExporterTests`, `LocalizationUiScannerTests`, `LocalizationTmpAdapterTests`). These ran early in discovery order and could request refresh/compilation while Test Runner had assembly reloads locked; `AssetDatabase.DeleteAsset` already performs the required database update for these cleanup paths.
- Focused post-fix evidence: `ResLoaderCancellationTests` job `11b799db` **2/2 PASS**; `ResScopeTests` job `681babe6` **3/3 PASS**; `ResScopeConcurrencyTests` job `decba56f` **3/3 PASS**. After an explicit asset refresh and idle compile, final FrameworkValidation job `ed0b8e6a` completed **679/679 PASS** in about 44 seconds; final `/compile/status` was idle with **0 errors / 0 warnings**. No production `ResMgr`/`ResLoader` behavior was changed.
- UnitySkills 2.8.4 still has a tooling hardening opportunity: its EditMode `test_run` preflight checks dirty scenes but does not reject/wait for `EditorApplication.isCompiling` / `isUpdating`. Durable automation should refresh/import first, wait for compile/update idle, then start TestRunner; do not patch this only inside `Library/PackageCache`.
- Added the durable project-side UnitySkills adapter `Assets/StellarFramework/Editor/Verification/UnitySkillsSafeTestGate`. It registers `stellar_test_run_safe` and `stellar_test_gate_status` through UnitySkills' public `UnitySkillAttribute` without modifying Package Cache. `stellar_test_run_safe` is a multi-call safety gate persisted through `SessionState`: first request triggers `AssetDatabase.Refresh`, later requests wait for `isCompiling=false` / `isUpdating=false`, require a 0.75s idle settle observation, then delegate to the normal UnitySkills `TestSkills.TestRun`. EditMode launches are also blocked during PlayMode transitions. This closes the refresh-to-TestRunner race while keeping the third-party package replaceable.
- Safe-gate live verification: after import the new editor assembly compiled **0 errors / 0 warnings**; UnitySkills discovery returned both new skills. Running the exact `SolePendingLoadPropagatesCallerCancellation` test through the gate produced the expected sequence `asset_refresh_requested -> editor_idle_observed -> accepted`, job `eb2359f1`, and completed **1/1 PASS**. `Tests-说明文档-Guide.md` now documents this as the preferred Web Agent / UnitySkills test entry point.
- Full safe-gate regression: `stellar_test_run_safe(testMode="EditMode", filter="StellarFramework.Tests.FrameworkValidation")` followed the same refresh/idle/settle sequence, launched job `2efcc5e2`, and completed **679/679 PASS** with 0 failed/skipped/inconclusive. Final compile state remained idle at **0 errors / 0 warnings**.

### 2026-09-24 — HotUpdate Publisher P21 workflow assembly and regression seal

- Added `HotUpdatePublisherWorkflowAssembly` as a standard Editor composition entry point for the existing 12-stage full pipeline and 8-stage read-only Dry Run. The change-classification handler persists the workspace result into the publish context, blocks ordinary patches on Red/dependency violations or missing HotUpdate payload, and allows Yellow only with the existing Full Gate policy. Dry Run shares only the first eight handlers and has no Upload/VerifyRemote/PublishVersion/Finalize handler.
- Initial focused run `31fac356` was **1/2** because the test passed a plain temp folder to a constructor that implicitly created `GitHotUpdateSnapshotProvider`; this was a fixture/API boundary issue, not a product assertion failure. Corrected the composition API to require the existing `IHotUpdateGitSnapshotProvider` adapter explicitly, preserving concrete Git behavior for callers that choose `GitHotUpdateSnapshotProvider`. Focused rerun `ff2af5e0`: **2/2 PASS**.
- Updated the Publisher source guide to document the explicit dependencies and clarify that the assembly factory does not discover production project configuration.
- Full Publisher focused suite `69a5bd5a`: **119/119 PASS**. Full `StellarFramework.Tests.FrameworkValidation` `e23f5097`: **679/679 PASS**. Ordinary PlayMode `f0d388d8`: **15/15 PASS**. Exact HotUpdate Release Gate `1f3badc6`: **1/1 PASS**.
- UnitySkills endpoint is `http://localhost:8093/`. Its safe runner completed refresh → idle → settle before each test invocation. Final compile status: idle, **0 errors / 0 warnings**.
- This completes the workflow assembly engine and its regression checks, but not Publisher V1 integration/closure: `HotUpdatePublisherHubModule` still does not instantiate the factory because a production Collector/build configuration, BaseRelease selection, and concrete project target/runtime host verification are not configured. The only existing Collector remains the verification-only fixture. Keep publish and rollback UI actions disabled and do not claim production Dry Run/publish evidence until a real project configuration is supplied and exercised.

### 2026-09-24 — HotUpdate Publisher P26 production-like Consumer E2E in progress

- The active 8093 UnitySkills instance is the main `StellarFramework` project (`StellarFramework_DEEE9F8A`), not a separate Consumer project. Its only pre-existing Collector is `StellarHotUpdateVerification`; it is not being used as a Production Collector. The Android BaseRelease repository is empty. A pre-existing edit to `Assets/GameHotUpdate/Manifest/HotUpdateManifest.json` and associated hot-update bytes is user/worktree state and is not overwritten by the setup work.
- Added explicit first-use Collector diagnostics to the Publisher Build tab. Core reports missing/Verification-only package reasons without a compile-time dependency on YooAsset Editor; the optional YooAsset Editor adapter supplies package/group/collector counts. Unity compilation after import: **0 errors / 0 warnings**. Focused test job `1ecb5989`: **4/4 PASS**.
- First-use UX initially exposed an `EditorPrefs` edge case: an existing empty package-name preference defeated the helper's default value. The missing/whitespace value now falls back to `HotUpdatePublisherConsumerE2E`. A second focused test job `4ff54268` passed **4/4** after the non-modal/idempotent target setup changes.
- Added `HotUpdate.HotUpdateMain.PublisherConsumerE2E()` as a separate, strict consumer entry: it loads the `HotUpdateBehavior` TextAsset through the registered YooAsset ResKit loader, errors if absent/empty, logs a machine-checkable behavior payload, and retains the existing entry marker. Existing `Main()` stays unchanged so Verification-only runs do not require a business asset. `HotUpdate.asmdef` now references `StellarFramework.ResKit`. This entry compiled in the current Editor but has not yet been run in an Android Player.
- The first-use business Collector menu creates a distinct package, preserves Verification, and includes behavior plus Publisher-generated manifest/DLL/AOT collector paths. The formal Android BaseRelease menu requires Android IL2CPP and invokes HybridCLR Generate/All and repository creation; it must be rerun after switching target/backend if required. No BaseRelease has yet been written.
- Unity menu execution created `HotUpdatePublisherConsumerE2E` as a separate business package in the existing Collector setting while preserving the Verification package. The setting now includes the behavior text file and six generated HybridCLR paths. The BaseRelease setup action set `AndroidTargetArchitectures: 8` (x86_64) and Android scripting backend `1` (IL2CPP) in `ProjectSettings/ProjectSettings.asset`. This is an authorized project configuration change. UnitySkills stopped returning the BaseRelease menu response after the settings reload: health showed `mainThreadIdleMs > 170000`, `queuedRequests=2`, `pendingRequests=4`, while compile/update were idle. Further 8093 calls were paused to avoid stacking requests. This is a `TOOLING EVIDENCE GAP`; no BaseRelease creation evidence exists yet and it is not a product PASS or product FAIL.
- Next: finish wiring the E2E runtime marker/config and Publisher execution path; create the actual business Collector and Android BaseRelease; use `Tools/AndroidVerification` with freshly generated Android artifacts; then perform Dry Run → Build & Publish → Remote Verify → PublishVersion and client behavior verification. P26 remains OPEN; no Production PASS is claimed.

### 2026-09-24 — New computer handoff snapshot

- Source branch is `main`, tracking `origin/main`; pre-handoff HEAD was `7ab39f0` (`docs: add three-repository migration plan`). The full working tree is intentionally being committed at the user's request, including older uncommitted project changes. Do not cherry-pick only the P26 files when moving computers.
- Unity project/version: `C:\CodingToolsWorkerCenter\StellarFramework`, Unity `2022.3.62f3c1`; UnitySkills was at `http://localhost:8093/` on the old computer. UPM pins include UnitySkills `v2.8.4`, HybridCLR `4feac30cb2e105992986c737f7f54992b8300e1a`, UniTask `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`, YooAsset `2.3.19`.
- Android Editor settings were intentionally changed to Android IL2CPP/x86_64. A distinct business YooAsset package `HotUpdatePublisherConsumerE2E` and its `publisher-consumer-behavior=v1` asset are saved in the existing YooAsset collector setting. The Verification package remains separate. HotUpdate source includes a separate `PublisherConsumerE2E` entry that strictly reads the behavior TextAsset using ResKit/YooAsset; it has not been executed in a Player.
- P26 is not closed. `BuildArtifacts/HotUpdate/BaseReleases` has no formal BaseRelease yet. Dry Run, Build & Publish, remote verification, PackageVersion publication, and Android client E2E remain outstanding. Do not classify missing evidence as PASS. The previous machine's UnitySkills request queue became stuck after the Android Player setting reload (`mainThreadIdleMs > 170000`, queued 2, pending 4 despite Editor compile/update idle); it was treated as `TOOLING EVIDENCE GAP`. On the new machine, reopen the project, confirm UnitySkills works on its configured port, verify Android IL2CPP/x86_64 survived checkout, and rerun the formal Create Android Base Release entry only after the Editor is responsive.
- Verified evidence before handoff: Publisher Collector diagnostics focused tests `1ecb5989` and `4ff54268`, each **4/4 PASS**. Earlier P21 regression evidence remains recorded above. No reset, clean, force checkout, commit before this handoff request, or push had been performed before the current user instruction.
- Git handoff requested: commit all current tracked modifications and untracked non-ignored files, then push `main` to `origin`. Afterward verify a clean worktree and record the resulting commit/push state below.
