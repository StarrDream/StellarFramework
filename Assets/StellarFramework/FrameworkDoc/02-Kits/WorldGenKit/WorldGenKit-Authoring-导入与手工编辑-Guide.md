# WorldGenKit.Authoring — 导入与手工编辑

`WorldGenKit.Authoring` 是 `WorldGenKit.Core + WorldGenKit.Builtins` 之上的纯 C# Authoring 扩展，用于证明 WorldGenKit 并不是 procedural-only 世界生成器。

它负责：

- 从 caller-owned buffer 导入 typed world data；
- Stable-ID Biome / Surface 导入；
- 保留 Base 数据不变的 Sparse Authoring Override；
- Height Raise / Lower / SetHeight / Flatten / Smooth；
- Biome / Surface / Mask paint；
- Dirty bounds；
- Builtins 派生数据的 region-only recompute。

它不负责 Unity `Texture2D`、`TerrainData`、Tilemap、Mesh、Editor Window 或文件格式解析。这些必须通过后续 Unity-facing Adapter / ToolsHub 完成。

## Assembly Boundary

```text
StellarFramework.WorldGenKit.Authoring
        ↓
WorldGenKit.Builtins
        ↓
WorldGenKit.Core
```

Authoring：

- `noEngineReferences=true`；
- 只引用 Core + Builtins；
- 不引用 WorldKit / GridKit / PathKit / SaveKit / PlacementKit；
- 不使用运行时 reflection scan；
- 不使用 `Dictionary<string, object>` per-cell 模型；
- 不把 Unity 类型写进 Runtime Contract。

## 1. Typed Import

最基础的 Height / Mask 等数据直接从调用方 buffer 导入：

```csharp
float[] importedHeight = ...;
DenseChannelStorage<float> height = new DenseChannelStorage<float>(importedHeight.Length);

WorldDenseChannelImport.CopyExact(
    importedHeight.AsSpan(),
    height);
```

长度必须完全一致。长度不匹配会在写入前失败，不会产生半导入状态。

因此 Unity Adapter 可以自己负责：

```text
Texture2D / TerrainData / Binary / JSON / Remote DTO
                    ↓
             caller-owned buffer
                    ↓
        WorldGenKit.Authoring typed import
```

Authoring Runtime 不需要知道数据最初来自哪里。

## 2. Stable-ID Semantic Import

Biome / Surface 导入接受 Stable ID，而不是要求外部系统知道 Runtime catalog index：

```csharp
WorldSemanticChannelImport.TryImportBiomeIds(
    biomeIds,
    biomeCatalog,
    biomeStorage,
    out int errorIndex,
    out WorldSemanticImportError error);
```

导入前会先验证所有 ID。

如果其中一个 ID 不存在：

```text
UnknownStableId
```

整个导入失败，而且 destination 不会被部分修改。

## 3. Base + AuthoringOverride

Authoring 的核心原则：

```text
Generated / Imported Base
          +
Sparse Authoring Override
          =
Final Data
```

Base 不被编辑工具直接修改。

```csharp
var overrides = new WorldDenseOverrideLayer<float>(layout);

overrides.Set(10, 20, 35f);

overrides.Compose(
    baseHeight,
    finalHeight);
```

这样可以明确区分：

- 原始生成结果；
- 原始导入结果；
- 开发者手工编辑；
- 未来 SavePatch / RuntimeDelta。

避免“一次手工编辑后再也不知道原始世界是什么”的不可逆数据污染。

## 4. Sparse Override

`WorldDenseOverrideLayer<T>` 逻辑上覆盖 Dense Base，但只保存被修改的 sample：

```text
Base:       262,144 samples
Overrides:   4,096 edited samples
```

没有修改的 sample 不额外复制一份 Authoring 数据。

常用 API：

```csharp
Set(x, y, value)
SetByIndex(index, value)
Remove(x, y)
Clear()
GetComposedValue(...)
Compose(...)
TryConsumeDirtyBounds(...)
```

## 5. Height Editing

当前支持：

```text
Raise
Lower
SetHeight
Flatten
Smooth
```

例如：

```csharp
WorldSampleRect area = new WorldSampleRect(20, 20, 16, 16);

WorldHeightAuthoringOperations.Raise(
    baseHeight,
    heightOverrides,
    in area,
    2f);
```

`Raise` / `Lower` 是对当前 composed value 操作，所以多次连续编辑会叠加在此前 Override 上，而不是每次回到 Base。

### Smooth

Smooth 使用调用方提供 scratch buffer：

```csharp
Span<float> scratch = ...;

WorldHeightAuthoringOperations.Smooth(
    baseHeight,
    heightOverrides,
    in area,
    factor: 0.5f,
    scratch);
```

这样 Runtime 操作本身不需要偷偷创建大临时数组。

当前 Smooth 使用当前 sample + 上下左右 cardinal neighbor 的 snapshot average，再按 `factor` 混合。

## 6. Paint

通用数据可以直接：

```csharp
WorldAuthoringPaint.Fill(maskOverrides, in area, (byte)1);
```

Biome / Surface 推荐使用 Stable-ID helper：

```csharp
WorldSemanticAuthoringPaint.PaintBiome(
    biomeOverrides,
    in area,
    WorldBiomeId.From("biome.sakura_forest"),
    biomeCatalog);
```

```csharp
WorldSemanticAuthoringPaint.PaintSurface(
    surfaceOverrides,
    in area,
    WorldSurfaceId.From("surface.mud"),
    surfaceCatalog);
```

未知 ID 会在任何 Override / Dirty mutation 之前失败。

## 7. Dirty Bounds

每个 Override Layer 会合并本轮受影响矩形：

```text
Edit A (1,1,2x2)
Edit B (10,8,3x4)
        ↓
Union Dirty Bounds
```

调用方可以：

```csharp
if (overrides.TryConsumeDirtyBounds(out WorldSampleRect dirty))
{
    ...
}
```

消费后 Dirty Bounds 清空，但 Override 数据本身仍保留。

## 8. Derived Dirty Propagation

Height 编辑不是所有派生 Channel 都需要整图重算。

当前 Builtins dependency：

```text
Height Dirty
  ├─ WaterDepth: same rect
  └─ Slope: expand 1 sample ring
         ↓
       Biome
         ↓
       Surface
         ↓
      Buildable
```

Slope 会扩一圈，是因为中央差分读取相邻 Height。

Moisture Edit：

```text
Moisture
   ↓
Biome
   ↓
Surface
   ↓
Buildable
```

不会错误地把 WaterDepth / Slope 标脏。

Biome Paint：

```text
Biome Paint
   ↓
Surface
   ↓
Buildable
```

Surface Paint 当前没有 Builtins 下游依赖。

## 9. Regional Recompute

Authoring 为 Builtins 派生 Stage 增加了公开 region execution：

```csharp
waterStage.ExecuteRegion(data, in waterRegion);
slopeStage.ExecuteRegion(data, in slopeRegion);
biomeStage.ExecuteRegion(data, in biomeRegion);
surfaceStage.ExecuteRegion(data, in surfaceRegion);
buildableStage.ExecuteRegion(data, in buildableRegion);
```

正常 WorldGen Pipeline 的 `Execute(...)` 行为没有改变，它仍然执行 whole region。

Regional API 直接接受公开 `WorldGenerationDataSet`，业务/Authoring 代码不需要构造 Core-internal `WorldGenerationContext`。

Regression 已验证：只编辑 5×5 地图中心一个 Height sample 时，dirty cascade 外的 Water / Slope / Biome / Surface / Buildable 一个值都不会被重写。

## 10. 典型 Authoring 流程

```text
Procedural / Imported Base Height
            ↓
Height Authoring Override
            ↓
Compose Final Height
            ↓
Consume Height Dirty Bounds
            ↓
Propagate Derived Dirty Regions
            ↓
Regional Water / Slope / Biome / Surface / Buildable Recompute
            ↓
Presentation Adapter rebuild affected sections
```

后续 ToolsHub 可以把这个流程包装成 Brush / Inspector / Preview，但 Runtime Contract 不依赖 Editor UI。

## 11. Current Benchmark

Unity `2022.3.62f3c1` Editor Test Runner：

```text
Map            512×512 = 262,144 samples
Height edit     64×64  =   4,096 samples
Dirty cascade              4,356 samples

Observed two-run range:
Sparse Lower edit       0.209–1.123 ms
Base+Override Compose   0.247–0.543 ms
Derived recompute       0.161–0.396 ms
Heap delta                         0  // both runs, coarse trend only
```

这是单线程 Editor 的两次真实观测范围，不是目标平台性能承诺，也不应把单次 Editor Test Runner 数字当作稳定硬指标。

## 12. Scope Boundary

Runtime Authoring 不包括：

- Unity Terrain import adapter；
- Texture/heightmap file decoder；
- Tilemap importer；
- Brush Editor UI；
- Undo/Redo Editor integration；
- SaveKit persistence adapter；
- Runtime player terraforming domain rules；
- Resource Scatter / Feature / POI；
- PlacementKit。

这些功能应在后续 Adapter / ToolsHub / 专用 Kit 中组合，而不是继续膨胀 Authoring Runtime Core。
