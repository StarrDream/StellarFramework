# WorldKit.Streaming — 无限世界流送指南

`WorldKit.Streaming` 是纯 C# `extension / world`。它建立在冻结的 `WorldKit.Core` 之上，但**不修改**既有 `WorldChunkState` 语义。

## 负责什么

- `WorldRegionLayout`：Chunk → generation/macro Region 的确定性映射；负坐标使用数学 floor division。
- `WorldStreamingPolicy`：以 Chunk focus 为中心的 Metadata/Data/Simulation/Presentation 分级需求半径。
- `WorldStreamingPlanner`：caller-buffer、row-major、确定性的 demand collection。
- `WorldChunkStreamingRegistry`：独立于冻结 `WorldChunkState` 的四级 residency 状态。
- `WorldStreamingReconciler`：只生成**一层相邻 Tier** 的 transition wave；先 downgrade，后 upgrade；自身不直接修改 Registry。

## 为什么不改 WorldKit.Core

`Unloaded -> Metadata -> DataReady -> Active` 已冻结。Streaming 需要把 Simulation 与 Presentation 分离，因此新增 `WorldStreamingTier`，而不是扩展旧 enum。这样旧项目、已有存档与既有行为测试都不改变。

## 无限世界与负坐标

`WorldChunkCoord` / `WorldRegionCoord` 使用 `long`。Demand 和 Region mapping 支持正负坐标；如果 `focus +/- radius` 或 Region→Chunk 范围超出 Int64，会显式失败，不做 wrap-around。

## 使用建议

每帧或固定 streaming tick：

1. 用 `WorldStreamingPlanner` 计算目标 demand。
2. 用 `WorldStreamingReconciler` 生成一波 transition。
3. 应用层按 transition 执行异步/同步工作；成功后再调用 Registry `TryTransition` 提交状态。
4. 加载到 Data 时可调用 WorldGen Adapter；进入 Simulation 时接业务模拟；进入 Presentation 时创建 Unity 表现。
5. 降载顺序反向执行，未修改 Chunk 可直接丢弃数据，修改内容由 Delta 保存。

这种“计划与提交分离”的方式避免异步加载失败时把 Registry 提前推进到错误状态。
