# WorldGenKit.DebugTextureAdapter

`WorldGenKit.DebugTextureAdapter` 是可选 Unity 表现 Adapter，用于把 WorldGenKit 的 Dense Channel 投影到调用方提供的 `Texture2D`。它不是生成数据的来源，也不拥有 World 生命周期。

## 依赖与边界

- 只依赖 `WorldGenKit.Core + WorldGenKit.Builtins`。
- 允许引用 UnityEngine；不引用 UnityEditor、WorldKit、GridKit、SaveKit、PlacementKit、Resources、Feature 或 Authoring。
- Core/Builtins 不反向依赖本 Adapter。
- 输入始终是 `WorldGenerationDataSet + ChannelHandle<T> + WorldPlanarSampleLayout`。

## Scalar 模式

`WorldDebugTextureAdapter.UpdateScalarTexture` 读取 Dense `float` Channel。调用方明确提供 `MinValue/MaxValue`、低/高颜色、可选 `FlipY` 与长度严格等于 sample count 的 `Color32[]` buffer。

Adapter 会先验证 Texture 尺寸/可写状态、Dense storage、buffer 和全部 source scalar；存在 NaN/Infinity 时在修改 Texture 前失败。验证完成后一次 `SetPixels32 + Apply`。

## Indexed 模式

`UpdateIndexedTexture` 读取 Dense `int` Channel，索引到调用方提供的 `Color32[] palette`。Surface/Biome 等业务语义仍由 Stable-ID Catalog 管理；Adapter 只消费编译后的整数 index，不把颜色写回 WorldGen Core。

所有 index 会在 Texture mutation 前预检，因此错误 palette/index 不产生半写入。

## 性能使用

- 重复刷新时复用 `Color32[]`。
- 不要每帧创建 Texture、palette 或 buffer。
- 大地图应按 Chunk/Region 或调试窗口需要刷新，而不是默认全图每帧重绘。
