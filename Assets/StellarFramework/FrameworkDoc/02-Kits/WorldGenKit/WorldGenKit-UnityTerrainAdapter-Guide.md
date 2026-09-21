# WorldGenKit.UnityTerrainAdapter

`WorldGenKit.UnityTerrainAdapter` 将 Dense Height Channel 投影到调用方已有的 `TerrainData`。它是表现 Adapter，不是 Terrain authoring system。

## 依赖与边界

- 只依赖 `WorldGenKit.Core + WorldGenKit.Builtins`。
- 不引用 UnityEditor，也不要求 WorldKit/GridKit/SaveKit/PlacementKit。
- Adapter 不创建 Terrain、不改变 `TerrainData.size`，也不会隐式 resize heightmap。

## Height Projection

`WorldTerrainHeightProjectionSettings` 明确声明 source min/max height、可选 Y flip 与是否允许 clamp。

调用方必须准备：

- square `WorldPlanarSampleLayout`
- `heightmapResolution` 与 layout Width/Height 完全一致的 `TerrainData`
- 与 layout 尺寸一致且可复用的 `float[,]` buffer
- Dense `float` Height Channel

默认 `ClampOutOfRange=false`。任何 NaN/Infinity 或越出声明 source range 的高度都会在 `TerrainData.SetHeights` 前失败，避免静默压平异常数据。只有项目明确选择 clamp 时才允许截断到 0..1。

## 设计说明

Terrain resolution、world size、TerrainLayer、detail/tree、邻接 Terrain 和 streaming 均属于更上层 presentation/runtime orchestration，不应倒灌到 WorldGen Core。
