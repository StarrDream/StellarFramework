# WorldGenKit.StreamingAdapter

这个 Adapter 连接 `WorldKit.Streaming` 与冻结的 `WorldGenKit`，自身无 UnityEngine。

## 核心契约

- `WorldChunkCoord + WorldPlanarSampleLayout -> WorldGenerationRunKey`
- `WorldRegionCoord + WorldRegionLayout + Chunk Layout -> Region absolute sample-origin RunKey`
- Chunk/Region 的绝对 sample origin 使用 checked Int64 运算，溢出显式失败。
- `ExecuteChunk` 直接复用既有 `WorldGenerationPlan`；生成确定性继续由 `worldSeed + runKey + Stable Stage ID` 保证。

因此 Chunk A→B 或 B→A 的探索顺序不会改变任一 Chunk 的结果；相邻 Chunk 也与对应绝对坐标的一次性生成保持一致。

未修改 Chunk 不需要保存完整生成数据：卸载后重新用相同 Seed、Layout、ChunkCoord 生成即可。业务修改应通过 WorldKit Delta 叠加。
