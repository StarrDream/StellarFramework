# WorldGenKit.TilemapAdapter

`WorldGenKit.TilemapAdapter` 把 Dense `int` semantic/index Channel 投影到 Unity `Tilemap`，是独立的 2D presentation boundary。

## 依赖与边界

- 只依赖 `WorldGenKit.Core + WorldGenKit.Builtins` 和 Unity 内置 Tilemap module。
- 不要求 WorldKit/GridKit；WorldGenKit 的 planar sample 并不等同于 GridKit occupancy。
- Adapter 不拥有 Tile asset、Grid size、Transform、sorting 或场景生命周期。

## Update 契约

调用方提供：

- 目标 `Tilemap`
- Dense `ChannelHandle<int>`
- `TileBase[] tilePalette`
- 可复用且长度等于 sample count 的 `TileBase[] tileBuffer`
- `Vector3Int cellOrigin`
- 可选 `FlipY`

一个 logical sample 映射一个 Tilemap cell。`SampleStep` 是 WorldGen 逻辑采样间距，不会被偷偷写成 Unity Grid cell size；若项目需要物理尺寸映射，应显式配置 Grid/Transform。

Adapter 在 `SetTilesBlock` 前验证全部 palette index，因此非法语义 index 不会留下部分 Tilemap mutation。
