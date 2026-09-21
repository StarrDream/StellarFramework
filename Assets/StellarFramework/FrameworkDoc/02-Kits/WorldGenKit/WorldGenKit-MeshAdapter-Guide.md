# WorldGenKit.MeshAdapter

`WorldGenKit.MeshAdapter` 将 Dense Height Channel 投影为 Unity heightfield `Mesh`，用于验证同一逻辑 WorldData 可以产生 3D 表现而无需修改 Core。

## 依赖与边界

- 只依赖 `WorldGenKit.Core + WorldGenKit.Builtins`。
- 不依赖 WorldKit、GridKit、SaveKit、PlacementKit、Resources、Feature 或 Authoring。
- 输入为 `WorldGenerationDataSet + ChannelHandle<float> + WorldPlanarSampleLayout`。
- Mesh、材质、GameObject、Collider、LOD 与场景生命周期由项目层负责。

## Build 契约

调用方提供并可复用：

- `Vector3[] vertices`
- `Vector2[] uv`
- `int[] triangleIndices`
- 目标 `Mesh`

`WorldHeightMeshSettings` 显式声明 horizontal/vertical scale 与是否重算 normals。SampleStep 会参与水平间距；Adapter 不修改逻辑坐标或 Height Channel。

所有 buffer 长度、Dense storage、非有限高度和 Unity float 可表示范围都会在 Mesh mutation 前检查。超过 65,535 vertex 时自动使用 `IndexFormat.UInt32`。

## 性能使用

- 对重复 Chunk rebuild 复用三个 managed scratch buffer 与 Mesh。
- 高频场景可关闭 `RecalculateNormals`，由专用 normal 数据/Job/Burst Adapter 后续处理。
- 本 Adapter 不引入 Jobs/Burst，以免在数据布局稳定前污染公共 API。
