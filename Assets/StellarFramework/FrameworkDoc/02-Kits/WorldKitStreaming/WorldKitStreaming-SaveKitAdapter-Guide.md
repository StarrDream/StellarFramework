# WorldKit.Streaming.SaveKitAdapter

这个 Adapter 使用 SaveKit Section 持久化 WorldKit Runtime Delta，不直接序列化 `IWorldDelta` 多态对象。

## 显式 Codec

每种 Delta TypeId 必须显式注册 `IWorldDeltaCodec`：

- `TryEncode(IWorldDelta -> string payload)`
- `TryDecode(Target + Version + payload -> IWorldDelta)`

Snapshot 保存 Stable TypeId、Version、World/Region/Chunk Target 和 payload。没有反射扫描、CLR `$type` 元数据或运行时类型名依赖。

## 原子 Restore

`WorldDeltaPersistenceState` 先把完整 Snapshot 解码到候选 `WorldDeltaSet`。任一 entry 的 TypeId、Version、Target、codec 或 payload 非法时全部失败，旧 DeltaSet 保持不变；只有完整验证后才替换当前状态。

真实 SaveKit `Save -> Clear -> Load` 已纳入 EditMode 验证。
