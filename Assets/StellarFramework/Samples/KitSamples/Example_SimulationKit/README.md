# SimulationKit Sample

`Example_SimulationKit` 展示一个不依赖 TimeKit、ResKit、Addressables 或 HybridCLR 的纯 C# 调度器样例。

- `Reset Burst`：20 个 ID 的 interval=5，在 tick=5 同时到期；用 Budget 1/4/16 观察分批 Drain。
- `Reset Staggered`：20 个 ID 的 firstDelay 按 ID 分散，手动推进 tick 后 Drain 当前 tick。
- `SetInterval` 会从当前 tick 重新计算下一次到期；过期项不会追赶式重复派发。

场景：`Assets/StellarFramework/Samples/KitSamples/Scenes/SimulationKit_Playable.unity`。
