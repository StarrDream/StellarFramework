# WorldGenKit.Resources — Resource Scatter / Occupancy Guide

`WorldGenKit.Resources` 是建立在 `WorldGenKit.Core` 之上的**独立资源生成扩展**。

它解决：

```text
Resource Definition
      ↓
Generation Settings
      ↓
Spawn Candidate
      ↓
Budget / Spacing / Occupancy Resolver
      ↓
Spawn Record
```

它不负责实例化 Unity Prefab，也不要求项目使用 `WorldGenKit.Builtins`。

因此以下项目都可以只使用 Resources：

- 自己已有地图数据结构；
- 自己已有 Terrain / Tilemap / Mesh；
- 服务端纯逻辑世界；
- 只想做矿物、植被、掉落点、装饰点生成；
- 使用自定义 Channel，而不是 Builtins 的 Height/Biome。

---

## 1. Assembly Boundary

```text
StellarFramework.WorldGenKit.Resources
        ↓
StellarFramework.WorldGenKit.Core
```

Resources：

- `noEngineReferences=true`；
- 只依赖 WorldGenKit.Core；
- 不依赖 Builtins；
- 不依赖 Authoring；
- 不依赖 WorldKit / GridKit / SpatialKit / PathKit / SaveKit / PlacementKit；
- 不使用 UnityEngine / UnityEditor；
- 不使用运行时反射扫描；
- 不使用 `Dictionary<string, object>` per-cell 数据模型。

Builtins、WorldKit、SaveKit 等通过调用方或 Adapter 组合，而不是形成强制依赖链。

---

## 2. Stable Resource Identity

资源使用 Stable ID：

```text
resource.oak_tree
resource.iron_ore
resource.copper_ore
resource.flower.red
resource.magic_crystal
```

分类同样使用 Stable ID：

```text
resource_category.vegetation
resource_category.mineral
resource_category.decoration
```

Authoring / 配置层使用 Stable ID。

热路径使用 Catalog 编译后的：

```text
resourceIndex
categoryIndex
```

避免在每个 sample 上做字符串查找。

---

## 3. ResourceDefinition

一个 Resource Definition 负责描述：

- Stable Resource ID；
- Category ID；
- Distribution；
- Occupancy；
- Exclusion；
- Priority。

示例：

```csharp
var tree = new WorldResourceDefinition(
    WorldResourceId.From("resource.oak_tree"),
    WorldResourceCategoryId.From("resource_category.vegetation"),
    new WorldResourceDistributionDefinition(
        WorldResourceDistributionMode.Density,
        occurrence: 0.08,
        clusterSize: 3,
        richness: 1.0,
        minSpacing: 2.0),
    vegetationMask,
    buildingAndRoadMask,
    priority: 10);
```

Resources Core 不知道 `Prefab`、`GameObject`、`TileBase` 或 `TerrainLayer`。

最终表现由外部 Adapter 根据 `ResourceId` 决定。

---

## 4. Distribution

当前支持两种基础模式：

```text
Density
Coverage
```

### Density

Density 表示 eligible sample 成为一个生成 seed 的概率。

例如：

```text
Occurrence = 0.10
```

表示每个 eligible sample 使用 absolute-coordinate deterministic noise 判断是否成为 seed。

### Coverage

Coverage 表示**eligible area 的目标比例**。

例如：

```text
Eligible = 80 samples
Coverage = 50%
Target = 40 samples
```

不是 `random < 0.5` 的近似结果，而是确定选择 40 个 eligible samples。

这使 “森林覆盖率 50%” 有稳定、可解释的含义。

---

## 5. Cluster / Vein Size

Density seed 可以扩展成 cluster：

```text
Seed
 ├─ member 0
 ├─ member 1
 ├─ member 2
 └─ ...
```

Cluster 使用确定性的 spiral offsets，并根据 seed-derived key 做旋转/镜像。

因此：

```text
WorldSeed + ResourceId + Absolute Coordinate
```

不变时，cluster 结果不变。

矿脉、树林、小型资源簇都可复用同一套基础机制。

---

## 6. Richness

`Richness` 与出现概率分离。

例如：

```text
Occurrence = 0.10
Richness   = 100
```

玩家可以只增加矿点数量，或只增加单个矿点储量。

这两种行为不会被混成一个参数。

Resources 当前只把最终 Richness 写入 `WorldSpawnRecord`。

实际资源库存、采集、再生属于游戏 Domain，不属于 WorldGenKit.Resources。

---

## 7. Generic Eligibility / Suitability

Resources 不硬编码：

- Height；
- Temperature；
- Moisture；
- Biome；
- Radiation；
- Fertility；
- Magic Density。

Candidate Generator 接受调用方准备好的：

```text
Eligibility Mask   byte[] / Span<byte>
Suitability Score  float[] / Span<float>
```

所以任何项目 Channel 都可以先被自己的 Rule/Stage 转换成这两个通用输入。

例如：

```text
Height + Slope + Biome
        ↓
Project Rule
        ↓
Eligibility / Suitability
        ↓
WorldGenKit.Resources
```

或者：

```text
Radiation + Corruption + DistanceToRoad
        ↓
Eligibility / Suitability
```

Resources Core 完全不需要知道这些 Channel 的含义。

---

## 8. WorldResourcePlanarDomain

Resources 自己拥有最小 planar generation domain：

```csharp
var domain = new WorldResourcePlanarDomain(
    width: 256,
    height: 256,
    sampleStep: 1,
    originX: -1024,
    originY: 2048);
```

它只表达：

- Width / Height；
- SampleStep；
- Absolute logical origin。

这样 Resources 不需要依赖 Builtins 的 `WorldPlanarSampleLayout`。

调用方若使用 Builtins，只需要从相同地图参数构造 Resource Domain 即可。

---

## 9. Deterministic Candidate Generation

Candidate Generation 使用：

```text
WorldGenerationSeed
Resource Stable ID
Absolute Logical Coordinate
Local deterministic key
```

不会使用全局 sequential RNG。

因此：

```text
Generate Tile A
Generate Tile B
```

和：

```text
Generate Tile B
Generate Tile A
```

每个 Tile 的候选结果一致。

Regression 已锁定这一点。

---

## 10. SpawnCandidate / SpawnRecord

Candidate 是尚未通过冲突解析的提案：

```text
ResourceIndex
SampleIndex
Absolute X/Y
Score
DeterministicKey
Richness
```

Resolver 接受后才转换为最终：

```text
WorldSpawnRecord
```

因此资源生成不是：

```text
Cell.Resource = tree
```

而是：

```text
Candidates
   ↓
Deterministic Resolution
   ↓
Spawn Records
```

---

## 11. Occupancy Registry

Occupancy 类型同样使用 Stable ID 注册：

```text
occupancy.ground
occupancy.surface_solid
occupancy.vegetation
occupancy.mineral
occupancy.underground
occupancy.decoration
occupancy.building
occupancy.road
occupancy.water
```

以及项目自定义类型。

Registry 编译后，热路径使用一个 64-bit mask。

当前每个 Registry 最多 64 个 occupancy semantic types。

这是运行时性能边界，不是固定 enum；开发者仍然通过 Stable ID 注册自己的类型。

---

## 12. Occupied + Excluded

每个 logical sample 的 Occupancy State 同时维护：

```text
Occupied
Excluded
```

这使冲突可以双向表达。

例如：

```text
Tree
  occupies Vegetation
  excludes Building + Road
```

Building Reservation：

```text
occupies Building
excludes Vegetation
```

即使 Tree 后生成，也会被 Building reservation 拒绝。

---

## 13. Coexistence

Tree 和 Underground Ore 可以定义为：

```text
Tree -> Vegetation
Ore  -> Mineral + Underground
```

只要双方 exclusion 没有冲突，就可以出现在同一个 logical sample。

现有 test 已验证：

```text
Tree + Underground Ore = allowed
Building + Tree        = rejected
```

---

## 14. Reservation

Feature / Building / Road 等更早阶段可以先写 reservation：

```csharp
WorldOccupancyReservations.Apply(...);
```

然后 Resource Resolver 再执行。

这正好支持未来：

```text
Terrain
→ Feature Reservation
→ Resource Scatter
```

而不是资源先生成后再到处删除。

---

## 15. Deterministic Resolver Order

Resolver 不依赖输入 candidate 数组顺序。

排序规则：

```text
Priority DESC
→ Score DESC
→ DeterministicKey ASC
→ Stable Resource ID Rank ASC
→ X
→ Y
→ SampleIndex
```

因此调用方即使改变候选遍历顺序，最终选择仍然一致。

---

## 16. MinSpacing

Resource Definition 可设置：

```text
MinSpacing
```

完整 Resolver 使用 per-sample spatial buckets，只检查附近 bucket 内的**同 Resource**记录。

不是对所有已接受资源做 O(n²) 扫描。

边界规则：

```text
distance < MinSpacing  => reject
distance == MinSpacing => allow
```

不同资源不会仅因为各自 MinSpacing 自动互斥；跨资源冲突应通过 Occupancy/Exclusion 表达。

轻量 Resolver 如果发现 Definition 的 `MinSpacing > 0` 会明确抛错，避免静默忽略规则。

---

## 17. Budget / Quota

Budget 支持三层上限：

```text
Global
Category
Resource
```

例如：

```text
Global max       = 10,000
Mineral max      = 4,000
Iron max         = 1,500
Copper max       = 1,000
```

Budget 使用 Stable ID 配置，然后 `Compile(catalog)` 成 index array。

Resolver 热路径不做 Stable ID 字符串查找。

当所有资源倍率都被玩家调高时，Budget 仍然提供确定性的最终上限。

---

## 18. Player Generation Settings

Generation multiplier 分三层：

```text
Global
× Category
× Per Resource
```

当前可调：

```text
Occurrence
Cluster Size
Richness
```

示例：

```text
Iron occurrence x2
Copper occurrence x0.5
Mineral cluster size x2
Global richness x1.5
```

Resolved settings 在生成前一次计算完成。

---

## 19. Developer Exposure Profile

不是所有倍率都必须暴露给玩家。

开发者可以分别控制：

```text
Occurrence
ClusterSize
Richness
```

每项包括：

```text
IsExposed
MinMultiplier
MaxMultiplier
DefaultMultiplier
```

Exposure 支持：

```text
Global
Category
Resource
```

例如：

```text
Iron Occurrence: exposed 0.5x ~ 3x
Iron Richness:   exposed 0.5x ~ 5x
Iron Cluster:    locked
```

`WorldResourceGenerationExposureProfile.Validate(...)` 会在生成前拒绝：

- 未暴露的 override；
- 超范围倍率。

---

## 20. Existing World Application Policy

Generation Settings 保存：

```text
NewChunksOnly
UnvisitedChunks
NonModifiedChunks
ExplicitRegion
FullRegenerate
```

注意：Resources 本身不知道 Chunk 是否 visited / modified。

因此这个 Policy 是**持久化的生成意图**，真正执行由 WorldKit / SaveKit / Game Domain orchestration 决定。

Resources 不会偷偷重刷已经存在的区域。

推荐默认：

```text
NewChunksOnly
```

---

## 21. SaveKit Integration

`WorldResourceGenerationSettings` 同时保留：

- Global multiplier；
- Category Stable-ID modifier entries；
- Resource Stable-ID modifier entries；
- ApplicationPolicy。

所以 SaveKit Adapter 可以直接序列化这些稳定配置。

未来 Chunk 使用相同：

```text
WorldSeed
Resource Catalog/Profile Version
Generation Settings
```

即可得到相同候选。

现有 test 已验证“重建 settings 后未来 tile candidate 完全一致”。

---

## 22. Persistence Boundary

Resources 不直接依赖 SaveKit。

正确结构：

```text
WorldGenKit.Resources
        ↓ DTO / stable data
Save Adapter
        ↓
SaveKit
```

这样 Resources 也能用于：

- 无存档项目；
- 自研存档系统；
- 服务端数据库；
- Remote API。

---

## 23. Performance Model

热路径原则：

- Stable ID 只在 Catalog/Profile/Compile 层；
- candidate 使用 integer resource index；
- occupancy 使用 64-bit mask；
- Budget 使用 compiled arrays；
- Resolver 使用 caller-owned heap scratch；
- Coverage 使用 caller-owned ranking scratch；
- MinSpacing 使用 caller-owned spatial bucket scratch；
- 不使用 LINQ/yield；
- 不使用运行时反射扫描。

---

## 24. Current Benchmark

Unity `2022.3.62f3c1` EditMode：

```text
Domain               512 × 512 = 262,144 samples
Generated Candidates 20,763
Accepted              9,527
Spacing Rejected     11,236

Method               1 warmup + 5 measured iterations
Generate min/median  10.438 / 10.714 ms
Resolver min/median   9.402 /  9.605 ms
Heap delta            4,096 bytes // GC.GetTotalMemory(false), coarse trend only
```

单次 Editor timing 曾出现约 10–32 ms 的明显波动，因此最终基准不再使用单次结果作为主要证据。上述 min/median 仅作为本机 Editor 趋势，不作为目标设备性能承诺，也不把 heap delta 描述为严格零分配证明。

---

## 25. Validation Coverage

当前行为验证覆盖：

- Stable Occupancy registration；
- 64-bit compiled masks；
- failed occupancy atomicity；
- Tree + underground Ore coexistence；
- Building reservation blocks Tree；
- resolver input-order independence；
- invalid candidate prevalidation；
- global/category/resource generation modifiers；
- Iron x2 / Copper x0.5；
- exact Density determinism；
- exact eligible Coverage target；
- suitability priority；
- persisted settings reconstruction；
- adjacent tile generation-order independence；
- Global/Category/Resource Budget；
- MinSpacing；
- budget/spacing pre-occupancy rejection；
- player exposure metadata/range validation。

---

## 26. Scope Boundary

Resources 模块不负责：

- Unity prefab spawning；
- Runtime harvesting / mining；
- resource regeneration gameplay；
- Inventory；
- economy；
- logistics；
- Feature / POI；
- Settlement generation；
- building placement；
- pathfinding；
- simulation LOD。

这些必须由后续独立 Kit / Domain / Adapter 组合。

下一阶段 Feature / POI 会先做 reservation，再让 Resources 解析剩余空间。
