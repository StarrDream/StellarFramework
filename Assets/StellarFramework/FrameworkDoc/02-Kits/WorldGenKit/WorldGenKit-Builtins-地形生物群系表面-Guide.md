# WorldGenKit.Builtins — 地形 / Biome / Surface MVP

`WorldGenKit.Builtins` 是建立在 `WorldGenKit.Core` 之上的可选世界生成扩展。

它提供第一个可直接使用的地图数据闭环：

```text
Height
  ├─ Moisture (optional)
  ├─ WaterDepth
  └─ Slope
        ↓
      Biome
        ↓
      Surface
        ↓
    Buildable
```

Builtins 不是 Core 的硬编码字段集合，也不要求项目使用全部 Stage。项目可以只用 Height、导入自己的 Height 后继续派生、完全不注册 Moisture、自定义 Biome/Surface，或用自己的 Stage 替换任意内置 Stage。

## Assembly Boundary

```text
StellarFramework.WorldGenKit.Builtins
        ↓
StellarFramework.WorldGenKit.Core
```

Builtins：

- `noEngineReferences=true`；
- 不依赖 UnityEngine / UnityEditor；
- 不依赖 WorldKit / GridKit / PathKit / SaveKit / SimulationKit；
- 不负责 Terrain、Mesh、Tilemap 的显示；
- 不负责 Streaming / Save / Placement / Resource / Feature。

## Planar Sample Layout

`WorldPlanarSampleLayout` 描述 row-major Dense sample tile。`WorldGenerationRunKey.X/Y` 是 tile 的绝对逻辑采样原点。

```csharp
var layout = new WorldPlanarSampleLayout(256, 256, sampleStep: 1);
```

例如相邻区域：

```text
Tile A origin = (0, 0),   size = 256×256
Tile B origin = (256, 0), size = 256×256
```

两者采样同一个全局 deterministic field，不会在每个 Tile 内从 local `(0,0)` 重启 Noise。Seam regression 已验证“一整块生成”和“拆成两个相邻 Tile 分别生成”的 Height 逐点一致。

## Height

```csharp
var noise = new WorldFractalNoiseSettings(
    WorldRuleId.From("noise.height"),
    basePeriod: 128,
    octaves: 5,
    lacunarity: 2,
    persistence: 0.5);

pipeline.AddStage(new WorldHeightStage(
    heightHandle,
    layout,
    noise,
    minHeight: -40f,
    maxHeight: 60f));
```

Height Stage 使用绝对逻辑坐标和 World Seed，结果与 Chunk/Tile 生成顺序无关，输出 Dense `float` Channel，不创建 Unity Terrain。

## Compiled Noise Key

`WorldFractalNoiseSettings` 构造时会把 Stable `WorldRuleId` 编译为 `WorldNoiseKey`。

Core 原始 API 仍保留：

```csharp
WorldNoiseRule.Sample01(seed, x, y, ruleId, localKey);
```

Builtins 热路径使用 compiled key overload：

```csharp
WorldNoiseRule.Sample01(seed, x, y, noiseKey, localKey);
```

这避免每个 octave / lattice corner 重复处理 Stable ID 字符串。旧 `WorldRuleId` overload 和既有固定 seed regression vector 没有改变。

## Moisture

Moisture 完全可选。不需要的项目不要注册 `terrain.moisture`，也不要添加 `WorldMoistureStage`。

Biome Definition 如果声明 Moisture 条件，而执行时没有绑定 Moisture Storage，则该 Definition 不匹配；不依赖 Moisture 的 Definition 仍可正常参与选择。

## WaterDepth

当前 MVP：

```text
waterDepth = max(0, seaLevel - height)
```

它是派生 Channel，不修改 Height。高级河流、湖泊、水文网络应作为后续 Stage/Extension 增加。

## Slope

`WorldSlopeStage` 从 Height 派生坡度幅值：内部使用中央差分，边缘使用单边差分，`sampleStep` 会参与导数计算。结果仍然只是逻辑数据，不绑定 Unity Terrain slope API。

## Biome Stable ID

Biome 不使用固定 enum，而使用 Stable ID：

```text
biome.grassland
biome.wetland
biome.water
biome.sakura_forest
game.magic_swamp
```

定义示例：

```csharp
new WorldBiomeDefinition(
    WorldBiomeId.From("biome.wetland"),
    WorldSurfaceId.From("surface.mud"),
    new WorldBiomeCriteria(
        moisture: new WorldRangeRule(0.62, 1.0)),
    priority: 20);
```

## Biome Criteria

当前 MVP 可选条件：Height、Moisture、WaterDepth、Slope。Definition 没声明某项，就不会依赖该项。

选择顺序：

1. Criteria 必须匹配；
2. Priority 高者优先；
3. Priority 相同按 Stable Biome ID 做 deterministic tie-break；
4. 无 Definition 命中时使用 unconditional fallback Biome。

因此 Catalog 添加顺序不会改变相同优先级冲突的结果。

## Runtime Biome Representation

每个 Sample 不保存字符串。Biome Stage 输出 `int biomeIndex`，Surface Stage 同样输出 `int surfaceIndex`。

Stable string ID 只用于 Authoring、Catalog、配置、Debug/Tools 和未来版本身份映射；热路径按 catalog index 工作。

## Surface

Surface 与 Biome 分离：

```text
Biome   = 生态 / 语义区域
Surface = 地表逻辑 / 表现材质身份
```

例如：

```text
biome.grassland -> surface.grass
biome.wetland   -> surface.mud
biome.water     -> surface.water
```

未来 Unity Adapter 可以把 Surface ID 映射到 Tile、RuleTile、TerrainLayer、Material、Mesh material index 或项目自定义表现定义；Builtins 不知道这些 Unity 类型。

## Buildable Mask

Buildable 是独立 `byte` Channel：

```text
0 = blocked
1 = buildable
```

当前规则组合：MaxSlope、MaxWaterDepth、Blocked Biome IDs。

```csharp
var settings = new WorldBuildableSettings(
    maxSlope: 8f,
    maxWaterDepth: 0f,
    blockedBiomes: new[]
    {
        WorldBiomeId.From("biome.water")
    });
```

它只是 WorldGen 基础 mask，不等于 PlacementKit 的最终建筑合法性。PlacementKit 未来仍可叠加 footprint、collision、zone、道路连接、建筑间距等业务规则。

## Imported Height Workflow

Builtins 不要求 Height 必须由 `WorldHeightStage` 产生：

```text
Imported Height
     ↓
ProvidedInput terrain.height
     ↓
WaterDepth + Slope
     ↓
Biome
     ↓
Surface / Buildable
```

现有 tests 已真实验证该路径。

## DAG Usage

Stage 可以故意乱序注册：

```csharp
pipeline.AddStage(buildableStage);
pipeline.AddStage(surfaceStage);
pipeline.AddStage(biomeStage);
pipeline.AddStage(slopeStage);
pipeline.AddStage(waterStage);
pipeline.AddStage(moistureStage);
pipeline.AddStage(heightStage);
```

WorldGenKit.Core Compiler 会根据 Channel dependency 编译成正确执行顺序。

## Validation

当前验证覆盖：

- negative absolute coordinates；
- deterministic fractal noise；
- monolithic map vs adjacent tile exact Height equality；
- Imported Height -> Water/Slope；
- optional Moisture；
- Biome catalog/fallback；
- deterministic priority/stable-ID tie；
- Surface mapping；
- Buildable constraints；
- seven-stage repeat determinism；
- engine-free Builtins boundary；
- 512×512 full-pipeline benchmark。

## Current Benchmark

Unity `2022.3.62f3c1` Editor Test Runner：

```text
Samples      262,144 (512×512)
Stages       7
Compile      1.185 ms
Run        366.175 ms
Buildable  203,295
Water       58,849
Heap delta       0  // GC.GetTotalMemory(false), coarse trend only
```

这是纯 C# 单线程 Editor 基线，不是目标平台固定性能承诺。第一次基准为 931.154 ms；引入 `WorldNoiseKey` compiled hot path 后下降到 366.175 ms，约降低 60.7%。

后续性能扩展应通过可选层完成，例如 Jobs/Burst Adapter、SIMD noise、macro field cache、chunk scheduling、async orchestration；不要把 Unity Jobs/Burst 依赖倒灌进 engine-free Builtins。

## Scope Boundary

Builtins 到这里负责基础地形语义数据闭环。以下仍属于后续扩展：

- Heightmap / Mask import tooling；
- manual Raise / Lower / Flatten / Smooth；
- Authoring Override Layer；
- Resource Scatter / Occupancy；
- Feature / POI；
- PlacementKit；
- Unity Terrain / Mesh / Tilemap output adapter；
- WorldKit streaming adapter。
