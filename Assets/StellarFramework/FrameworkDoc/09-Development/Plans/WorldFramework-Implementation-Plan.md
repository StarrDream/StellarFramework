# StellarFramework World Framework — 0→1 实施计划

> 状态：设计冻结前实施计划  
> 目标：建立一套面向有限/无限、2D/3D、规则网格/连续空间、程序化/手工/导入式地图的高自由度世界基础设施。

---

## 1. 总目标

这不是一个单独的“随机地形生成器”，而是一组可以独立使用、按需组合的世界类 Kit。

核心定位：

- **WorldKit**：世界如何存在、分区、分块、加载、查询、修改和持久化。
- **WorldGenKit**：世界数据如何生成、导入、编辑、组合、验证和输出。
- **PlacementKit**：对象/建筑/区域如何合法放置，不承担具体玩法。
- **GridKit**：离散网格、拓扑、Cell/Edge/Vertex、占位与几何。
- **SpatialKit**：连续空间索引与查询。
- **PathKit**：路径搜索。
- **SaveKit**：持久化、版本、迁移与存储。
- **SimulationKit**：大量运行实体/系统的调度。
- **TimeKit**：世界时间。

必须保持：

1. Core Kit 可单独导出。
2. Kit 之间通过 Adapter 组合，不形成强制“全家桶”。
3. Runtime 不依赖 ToolsHub。
4. Core 尽量 Pure C#；Unity Terrain / Tilemap / Mesh / ScriptableObject / MonoBehaviour 放到 Unity Integration / Adapter。
5. Authoring 可以灵活，Runtime 必须强类型、低 GC、可预测。
6. 新内容通过注册/Definition/Rule/Stage 扩展，而不是修改 Core。

---

## 2. 目标游戏覆盖

第一阶段架构必须自然覆盖：

- 星露谷物语式：手工 Tilemap + 运行时变化。
- RimWorld / 边缘世界式：方格 + 多 Layer + 大量模拟。
- 文明式：六边形 + Cell/Edge 数据。
- 魔兽争霸 / RTS：有限地图 + 地形/资源/POI/路径。
- 放逐之城 / 部落幸存者：3D 地形 + 建造 + 资源 + 居民模拟。
- 都市天际线式：连续 3D 地形 + Region/Chunk + 后续道路/交通 Domain。
- Factorio / 异星工厂式：无限平面 Chunk + 确定性资源生成。
- Terraria 式：有限 2D Block World，通过专用存储 Adapter。

兼容性预留但不作为 V1 全量实现：

- Dyson Sphere 式球形 Planet World。
- Minecraft 式 3D Voxel World。
- 非规则 Province / Voronoi / Graph World。

原则：V1 不实现所有世界形态，但 Core API 不得堵死这些扩展。

---

## 3. 正式 Kit / Package 拆分

### 3.0 独立使用是硬约束

任何 World Framework 实现都必须保留“小项目只拿一块能力”的路径：

- 仅自定义 Graph 寻路：`PathKit.Core`
- 仅逻辑网格：`GridKit.Core`
- 格子寻路：`GridKit.Core + PathKit.Core + PathKit.GridKitAdapter`
- 已有 Terrain/Mesh 映射网格：`GridKit.Core + GridKit.UnityProjectionAdapter`
- Terrain/Mesh 网格寻路：再可选加入 `PathKit.Core + PathKit.GridKitAdapter`
- 仅世界组织：`WorldKit.Core`
- 仅离线/自定义数据生成：`WorldGenKit.Core`
- 仅放置判定：`PlacementKit.Core`

禁止为了简单寻路、网格或 Terrain→Grid 烘焙，强制引入 WorldKit / WorldGenKit。

已有 Terrain/Mesh 的正式工作流：

```text
Existing Terrain / Mesh / Scene
        ↓
Grid Projection / Bake Adapter
        ↓
Auto Bake Base
height / slope / walkable / movement cost
        ↓
Manual Override Layer
force walkable / force blocked / cost override
        ↓
Final Grid State
        ↓
optional PathKit.GridKitAdapter
```

重新 Bake 只能替换 Auto Bake Base，不得静默清除 Manual Override。

### 3.1 WorldKit.Core

独立 Foundation Kit，无 GridKit / WorldGenKit 强依赖。

负责：

- WorldId / WorldVersion
- WorldExtent：Finite / Infinite
- WorldBounds（有限世界）
- Region / Chunk Identity
- Chunk Lifecycle
- Chunk State / Version
- World-local logical coordinate
- Chunk-local coordinate
- World Data Layer registry/access
- Runtime Delta contract
- Chunk streaming state
- Dirty region / dirty chunk tracking
- World query boundary

不负责：

- Noise / HeightMap / Biome
- Grid topology
- Pathfinding
- 建筑放置规则
- JSON / 文件 IO
- Unity Terrain / Mesh / Tilemap
- Economy / Citizens / Combat / Traffic

### 3.2 WorldGenKit.Core

独立 Extension Kit；原则上不强依赖 WorldKit。

负责：

- Typed WorldDataChannel<T>
- Channel metadata / Stable ID
- Dense / Sparse / Chunked / Constant / Computed / External storage contract
- GenerationStage contract
- Pipeline dependency graph
- Rule contract
- Deterministic seed derivation
- Generation context/result/report
- Layer contract
- Biome / Surface semantic definition
- Import / override source contract
- Dirty dependency recomputation
- Validation contract

### 3.3 WorldGenKit.Resource

可以先作为 WorldGenKit 内独立 asmdef/module，后续视分发需要拆独立 profile。

负责：

- ResourceDefinition
- Density / Coverage / Cluster / Richness
- Player-adjustable generation settings
- SpawnCandidate
- SpawnRecord
- Category/global budgets
- deterministic scatter

### 3.4 WorldGenKit.Feature

World Feature / POI。

负责：

- Landmark
- Area Feature
- Compound Feature
- FeatureDefinition
- Candidate / score
- Reservation
- uniqueness / quotas / spacing
- terrain adaptation request
- deterministic Feature seed

不负责具体村庄 Gameplay、经济、NPC。

### 3.5 PlacementKit.Core

独立 Foundation/Extension Kit，不强依赖 GridKit/WorldKit。

负责：

- Footprint
- PlacementRequest
- PlacementRule
- PlacementContext
- PlacementResult
- PlacementFailureReason
- occupancy conflict contract
- connection requirement contract

Adapter：

- PlacementKit.GridKitAdapter
- PlacementKit.WorldKitAdapter
- PlacementKit.WorldGenKitAdapter（可选）

### 3.6 Adapter / Integration Profiles

预计至少：

- WorldKit.SaveKitAdapter
- WorldKit.SpatialKitAdapter
- WorldKit.GridKitAdapter
- WorldGenKit.WorldKitAdapter
- WorldGenKit.GridKitAdapter
- WorldGenKit.TilemapAdapter
- WorldGenKit.MeshAdapter
- WorldGenKit.UnityTerrainAdapter
- WorldGenKit.HeightmapImportAdapter
- PlacementKit.GridKitAdapter
- PlacementKit.WorldKitAdapter

后续 Extension：

- WorldKit.Planet
- WorldKit.Voxel
- WorldGenKit.Voxel
- WorldGenKit.Planet

---

## 4. 六个必须冻结的 Contract

### 4.1 Channel Contract

世界属性不写死。

示例：

    terrain.height
    terrain.moisture
    terrain.temperature
    soil.fertility
    environment.pollution
    game.magic_density

要求：

- Stable ID
- 强类型
- 可声明默认值
- 可声明存储策略
- 编译后使用 typed/indexed handle
- 高频 Runtime 禁止 string/object/dictionary per-cell lookup

### 4.2 Stage Contract

每个 Stage 明确：

- Requires
- Optional Inputs
- Produces
- Mutates
- Deterministic Seed Scope

编译时检测：

- missing producer
- duplicate producer
- circular dependency
- invalid type
- dead/unused output

### 4.3 Rule Contract

内置最小规则：

- Range
- Threshold
- Curve
- Inverse
- Distance
- Noise
- Tag
- Weighted Sum
- Multiply
- Min / Max
- AND / OR

高级项目允许自定义 Rule。

### 4.4 Layer Contract

支持逻辑多层世界：

- Ground
- Surface
- Vegetation
- Mineral
- Underground
- Decoration
- Building
- Road
- Water
- Custom

Layer 是语义数据边界，不等同于 Unity Layer。

### 4.5 Occupancy Contract

统一解决：

- Tree + ore 可共存
- building + tree 冲突
- underground + surface 可共存
- road / building / decoration 的互斥或叠加

禁止各系统自行堆 if/else 冲突逻辑。

### 4.6 Feature Contract

统一表达：

- 高塔
- 水田
- 村庄
- 沉船
- 神庙
- 遗迹
- 地牢入口

而不是每种 POI 写一套生成器框架。

---

## 5. World Space / Topology 原则

### 5.1 WorldKit 与 GridKit 分离

Chunk != Grid。

一个 Chunk 可以包含：

- Grid cells
- Road graph
- continuous objects
- POI
- spatial index page

### 5.2 V1 正式支持

- Planar finite world
- Planar infinite world
- 2D logical / 3D surface presentation

### 5.3 GridKit 扩展

需要补齐/验证：

- Orthogonal 4-way
- Orthogonal 8-way
- Hex axial/cube coordinate
- Cell / Edge / Vertex topology
- topology neighbor/distance/ring/range
- Isometric coordinate mapping（表现映射，不等同新 topology）

### 5.4 Future-compatible

- Layered world
- Spherical world
- 3D voxel world
- irregular region graph

避免在 Core 到处写死 Vector2Int/Mathf/Unity Vector 类型。

---

## 6. 世界状态分层

统一采用：

    BaseGeneratedLayer
          +
    AuthoringOverrideLayer
          +
    RuntimeDeltaLayer
          +
    PersistentPatchLayer
          =
    FinalWorldState

含义：

- BaseGeneratedLayer：程序生成/导入的基础世界。
- AuthoringOverrideLayer：开发者手工笔刷/编辑器修改。
- RuntimeDeltaLayer：游戏运行中玩家/系统造成的改变。
- PersistentPatchLayer：SaveKit 恢复的版本化修改。

禁止直接把运行时修改烘回基础生成数据。

---

## 7. 无限世界原则

无限世界必须从第一版的数据模型支持：

- 无固定 width/height。
- Chunk-first。
- GenerateChunk(seed, chunkCoord) 与生成顺序无关。
- Global sample coordinate 保证 Chunk 边界连续。
- 未修改 Chunk 可通过 Seed + Profile 重建。
- SaveKit 只保存世界元数据 + 修改 Delta。
- 逻辑位置使用 ChunkCoord + LocalPosition，避免依赖远距离 Unity float Transform。
- Floating Origin 属于 Unity Adapter。

Feature / river / road 等跨 Chunk 内容通过 Macro Region / Generation Region 解决。

---

## 8. Resource 系统

### 8.1 资源参数

至少区分：

- Occurrence / Density
- Coverage
- Cluster / Vein Size
- Richness / Amount
- Spacing
- MaxPerCell / MaxPerArea
- Regeneration（Gameplay integration，可选）

### 8.2 玩家可调

每个 ResourceDefinition 声明：

- 是否允许玩家修改
- Min / Max / Step
- World-generation-only / runtime-adjustable

有效规则：

    Definition
      * ProjectProfile
      * WorldPreset
      * PlayerGenerationSettings
      * ScenarioModifier
      =
    EffectiveResourceRule

### 8.3 已生成地图

默认：

- 只影响尚未生成 Chunk。

显式策略：

- NewChunksOnly
- UnvisitedChunks
- UnmodifiedChunks
- ExplicitRegion
- FullRegenerate（危险操作）

不得静默破坏已有建筑/资源/玩家修改。

---

## 9. Feature / POI 系统

生成顺序：

    Base Terrain
        ->
    Feature Candidate
        ->
    Reservation
        ->
    Terrain Adaptation
        ->
    Resource Scatter
        ->
    Validation

Feature 支持：

- fixed count
- probability
- density
- per-region quota
- unique-per-world
- unique-per-region
- biome/tag restriction
- channel-based score
- slope / height / water/coast/road distance
- footprint
- influence/reservation radius
- terrain flatten/carve/fill/stamp
- template + procedural sub-generator

Compound Feature（村庄/城堡/营地）：

- WorldGenKit 决定“在哪里”
- Feature sub-generator 决定内部布局
- PlacementKit 决定具体合法放置

---

## 10. Authoring / Import / Presentation

### 10.1 输入 Source

支持统一 Source Adapter：

- Procedural
- Manual
- Heightmap
- Mask/Biome image
- Unity Terrain import
- Tilemap import
- JSON/binary/custom source

### 10.2 输出 Adapter

- Tilemap
- Mesh
- Unity Terrain
- Debug Texture
- WorldKit Chunk
- Custom project data

Core 不知道最终如何显示。

---

## 11. ToolsHub 规划

### WorldKit

偏诊断：

- World Inspector
- Region/Chunk Viewer
- Streaming Debug
- Loaded/Loading/Unloaded visualization
- Data Layer Inspector
- Delta Inspector
- Memory Statistics

### WorldGenKit

完整 Authoring：

- World Profile
- Pipeline
- Data Channels
- Rules
- Biome
- Surface
- Resource Scatter
- Feature / POI
- Import
- Manual Authoring
- Preview
- Heatmap
- Validation
- Export

### PlacementKit

- Footprint Editor
- Rule Editor
- Occupancy Preview
- Placement Probe
- Failure Reason diagnostics

ToolsHub 必须：

- Editor-only
- 不成为 Runtime 依赖
- Basic / Advanced / Diagnostics 分级，避免过度复杂。

---

## 12. 实施阶段

## P0 — Architecture Freeze & Tiny Foundation Integration

目标：不写世界生成算法，先冻结边界。

任务：

- 对照 GridKit / SpatialKit / PathKit / SaveKit / SimulationKit。
- 对照 StellarGridMap。
- 审计并冻结独立 Kit 最小依赖矩阵。
- 冻结 Existing Terrain/Mesh -> Grid Projection/Bake -> Manual Override 的 Adapter 边界。
- 完成 WorldKit / WorldGenKit / PlacementKit ADR/Contract 文档。
- 定义依赖矩阵与 asmdef 边界。
- 定义 Distribution Catalog profiles。
- 做 Tiny Foundation Integration，验证现有 Kit 组合无隐藏耦合。

验收：

- 依赖图无环。
- Foundation 不依赖 Extension。
- 所有 Core 可独立编译。
- 现有 Kit 测试全部保持通过。

## P1 — GridKit Topology Foundation

目标：补齐世界系统需要的拓扑基础，但不把 World 概念塞进 GridKit。

任务：

- Cell / Edge / Vertex identity。
- Orthogonal 4/8。
- Hex axial/cube。
- Neighbor / Distance / Ring / Range。
- topology contract。
- 性能测试。

验收：

- Square/Hex 同一算法可通过 topology 操作。
- 100万级 neighbor/range 操作无异常 GC 尖峰。
- GridKit 仍可独立导出。

## P2 — WorldKit Core

目标：完成有限/无限平面世界的数据与 Chunk 生命周期。

任务：

- World/Region/Chunk identity。
- Finite/Infinite extent。
- Chunk state machine。
- logical position。
- data layer registry。
- dirty tracking。
- runtime delta abstraction。
- streaming state model。

验收：

- 不依赖 UnityEngine。
- 可创建有限世界。
- 可访问正/负 ChunkCoord 的无限世界。
- Chunk load/unload 状态机有完整测试。
- 无 WorldGenKit 也可使用。

## P3 — WorldGenKit Core

目标：完成可扩展生成引擎。

任务：

- Channel Registry。
- typed handles。
- Dense/Sparse/Chunked/Constant/Computed storage。
- Stage contract。
- Pipeline compiler。
- deterministic random/seed。
- Rule system。
- GenerationReport。

验收：

- 自定义 Channel 不改 Core。
- 删除 Temperature Stage 后系统仍正常。
- 自定义 Magic Channel 可参与 Rule。
- pipeline 能检测依赖错误。
- 相同 seed/profile/chunk 始终相同。

## P4 — Terrain / Biome / Surface MVP

目标：形成第一个真正可用的生成闭环。

内置示范能力：

- Height
- Moisture（可选）
- Water
- Slope
- Biome
- Surface
- Buildable mask

重点：它们是 Sample/Builtin Stage，不是 Core 硬编码字段。

验收：

- 2D finite map generation。
- 3D heightfield logical result。
- 可禁用温度/湿度等 Stage。
- Chunk 边界连续。

## P5 — Import & Manual Authoring

目标：证明 WorldGenKit 不是 procedural-only。

任务：

- Heightmap import。
- Mask/Biome import。
- AuthoringOverrideLayer。
- Raise/Lower/Flatten/Smooth。
- Paint Biome/Surface/Mask。
- Dirty region recomputation。

验收：

- 导入现有地图后继续运行 pipeline。
- 手工修改后只重算受影响依赖。
- 基础生成数据不被破坏。

## P6 — Resource + Occupancy

目标：完成模拟经营/RTS/生存常用资源生成。

任务：

- ResourceDefinition。
- Density/Coverage/Cluster/Richness。
- SpawnCandidate / SpawnRecord。
- Occupancy Resolver。
- budget/quota。
- player generation settings。

验收：

- Tree + underground ore 可共存。
- building-reserved area 不生成 tree。
- iron x2 / copper x0.5 / forest coverage 50% 可重复确定生成。
- 设置持久化后未来 Chunk 保持一致。

## P7 — Feature / POI + PlacementKit

目标：支持高塔/水田/村庄/沉船/遗迹等。

任务：

- FeatureDefinition。
- Landmark / Area / Compound。
- Candidate/Reservation。
- Terrain adaptation。
- Unique-per-world tracking。
- PlacementKit Core。

验收：

- 高塔单体。
- 水田区域 Feature。
- 村庄 Compound Feature。
- 沉船水域规则。
- Feature reservation 与 Resource Scatter 正确协同。

## P8 — Unity Presentation Adapters

目标：验证 Core 真正与表现解耦。

至少完成：

- 2D Tilemap Adapter。
- 3D Mesh 或 Unity Terrain Adapter（首个 3D）。
- Debug Texture Adapter。

第二个 3D Adapter 在首个稳定后补齐。

验收：

- 同一逻辑 WorldData 可输出 2D/3D。
- Core 不引用 Tilemap/Terrain/Mesh。

## P9 — Infinite World / Streaming

目标：Factorio-style 无限平面世界。

任务：

- generation region / macro region。
- demand generation。
- streaming policy。
- metadata/data/simulation/presentation 分级状态。
- logical position + floating-origin adapter。
- SaveKit delta integration。

验收：

- 正负坐标任意探索。
- 生成顺序不影响结果。
- 未修改 Chunk 可卸载后重建。
- 修改 Chunk 可通过 Delta 恢复。
- 长距离 Unity 表现通过 floating origin 保持稳定。

## P10 — ToolsHub Production Authoring

目标：让非框架开发者可实际使用。

完成：

- WorldKit diagnostics。
- WorldGen Profile/Pipeline/Channel/Rule/Biome/Resource/Feature editors。
- candidate heatmap。
- accepted/rejected diagnostics。
- memory report。
- validator。

验收：

- 常见地图无需改 Core 代码即可配置。
- 高级扩展仍可代码注册。
- runtime assembly 不依赖 Editor。

## P11 — Integration Samples

必须至少有：

1. **Farm2D**：固定方格 / Tilemap / 手工 + 程序混合。
2. **HexStrategy**：文明式 Hex / Cell + Edge / Resource / Feature。
3. **Survival3D**：Heightfield / Biome / Resource / Village / Placement。
4. **InfiniteFactory**：Factorio-style infinite Chunk / resource settings / SaveDelta。
5. **StellarGridMap Migration Sample**：将 StellarGridMap 思想按新 Kit 边界重新组合。
6. **TerrainGridNavigation**：已有 Terrain/Mesh -> Grid Bake -> Manual Walkability Override -> PathKit。

这些 Sample 是压力测试，不是只展示 API。

## P12 — Performance / Release Seal

必须建立独立 Performance Tests：

- channel dense read/write
- sparse lookup
- chunk generation
- pipeline compile
- neighbor/topology
- scatter candidate resolution
- feature candidate resolution
- streaming churn
- save delta size
- allocation/GC

硬性要求：

- 禁止每 Cell string dictionary/object boxing。
- 禁止 Runtime reflection/assembly scan。
- 高频路径不得产生持续 GC。
- 大数组/Chunk 有明确内存预算。
- Jobs/Burst 只在数据布局稳定后引入，不提前污染 API。

最终：

- Distribution tests。
- Standalone export tests。
- asmdef dependency tests。
- Unity compile 0 error。
- tests 0 failed / 0 skipped（封版集合）。
- docs / samples / catalog 同步。

P12 统一性能与 Release 证据矩阵：

`Assets/StellarFramework/FrameworkDoc/06-WorldFramework/WorldFramework-Performance-Release-Matrix.md`

## P13 — Example Productization / Localization / Final Clean Seal

目标：在 P12 Runtime/Performance seal 之后，对所有 Examples / Integration Samples / Playable Scenes 做最终产品化整理，并新增独立 LocalizationKit。

必须完成：

- LocalizationKit.Core：engine-free locale/key/table/catalog/service/fallback contract。
- LocalizationKit.UnityUGUIAdapter：UGUI localized view + ScriptableObject table authoring。
- LocalizationKit.SettingsAdapter：适配现有 SettingsKit.ILanguageSettingsAdapter，Kit 之间不反向耦合。
- Localization editor validator：zh-CN / en-US coverage、duplicate/missing key、fallback 检查。
- 所有 Sample UI 提供中文/英文两套文案和运行时语言切换。
- GUI/UGUI 只负责 UI；非 UI Kit 的主要验证必须由真实 2D/3D 场景对象、路线、动画、生成/销毁、网格、Terrain、AudioSource 等表现。
- 缺失的 Example 美术素材由统一 Editor-only ExampleAssetFactory 程序化生成。
- 22 个 Kit Example 全量整理；P11 六个 World Framework Integration Sample、ArchitectureDemo、FlowKitMsvIntegration 同步统一。
- FlowKitMsvIntegration 补独立 Playable Scene。
- Sample 目录、Scene、Builder、Generated/Authored assets、README、Catalog sourcePaths 全部规整。
- 全部新 Builder 验证通过后，删除旧 SampleTemplates、重复资源、废弃 Scene/README 与 IMGUI 主验证逻辑。

验收：

- LocalizationKit profiles 可独立导出，Runtime Core 不依赖 Unity/Settings/UI。
- zh-CN / en-US key coverage 100%。
- 所有 active sample scene 无 Missing Script / broken asset reference。
- 非 UI Kit 不允许仅靠 GUI/Console 作为主要验证。
- 所有 Sample PlayMode smoke 0 error。
- P0-P12 frozen regression、Metadata、Standalone、asmdef boundary、Catalog closure 全绿。
- Unity compile 0 error，最终 Console 0 error / 0 warning。
- repository-wide git diff --check PASS。

详细执行计划现归档于：Assets/StellarFramework/FrameworkDoc/09-Development/Plans/WorldFramework-P13-Example-Localization-Cleanup-Plan.md。

---

## 13. 版本路线

### V1 — Broad Planar World

必须完成：

- finite/infinite planar world
- square/hex topology
- typed channels
- generation pipeline
- import/manual override
- biome/surface
- resource/occupancy
- feature/POI
- placement
- Tilemap + 3D surface adapter
- ToolsHub
- SaveKit delta integration

### V1.x

- stronger streaming
- more import/export
- additional rules
- better Burst/Jobs adapters
- 2D block storage adapter（Terraria-like）

### V2 / Extensions

- Spherical / Planet World
- Voxel World
- Layered underground/interior world
- irregular Region/Province topology
- optional RoadKit / TrafficKit / LogisticsKit integrations

---

## 14. 防止框架失控的规则

任何新需求进入 Core 前必须回答：

1. 这是新的通用 Contract，还是某游戏 Domain？
2. 能否通过 Channel/Rule/Stage/Adapter/Feature 扩展解决？
3. 是否会让 WorldKit 知道 Building/Tree/Player/Traffic 等业务概念？
4. 是否让一个独立 Kit 被迫依赖另一个可选 Kit？
5. 是否引入每 Cell object/string/dictionary 高频开销？
6. 是否破坏 finite/infinite、2D/3D、square/hex 中至少一种已有场景？

如果 2~4 中出现不合理答案，默认不进入 Core。

---

## 15. 最终成功标准

不是“支持多少游戏名称”，而是：

- 新增温度/魔力/污染等属性，不改 Core。
- 新增 Biome/Surface，不改 Core。
- 新增铁矿/铜矿/树木，不改 Core。
- 新增高塔/村庄/沉船，不改 Core。
- Square 切换 Hex，不改 WorldKit。
- 2D Tilemap 切换 3D Terrain/Mesh，不改 WorldGen Core。
- 有限地图切换无限 Chunk，不推翻数据模型。
- 手工地图和程序地图可以共用同一套数据与运行时。
- 大部分开发者通过 ToolsHub + Definition 完成工作；高级开发者通过 Stage/Rule/Adapter 扩展。
- 每个 Core Kit 可以独立导出并通过自己的测试。

达到以上条件，才认为 World Framework 真正从 0 到 1 完成。

