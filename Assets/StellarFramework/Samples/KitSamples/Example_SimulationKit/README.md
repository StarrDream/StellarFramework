# SimulationKit Sample

`Example_SimulationKit` 展示一个不依赖 TimeKit、ResKit、Addressables 或 HybridCLR 的纯 C# 调度器样例。样例把逻辑 `Game Tick` 与 CPU/主循环处理批次 `Frame Step` 分开，按钮驱动只用于教学，不绑定真实帧率。

- `Reset Burst`：20 个 ID 的 interval=5，在 tick=5 同时到期。点击 `Advance +5`，选择 Budget 4，再连续点击 `Frame Step (Collect once)`：Frame 1 返回 1~4，Frame 2 返回 5~8，直到第 5 个 Frame Step 后 `HasBacklog=false`。Game Tick 在这些 Frame Step 之间保持不变。
- `Reset Staggered`：20 个 ID 的 `firstDelay` 按 ID 分散，手动推进 Game Tick 后用 Frame Step 观察错峰效果。
- `HasBacklog=true` 只表示当前 tick 还有已到期但未派发的 Entry，实时玩法通常留到下一帧，不要在同一个 Update 里 `while` 清空。
- `Manual Drain (same tick)` 是显式 Flush/Debug 操作：同一 Game Tick 主动再取一批，连续点击可清空 backlog，但不具备 frame spreading。
- `SetInterval` 会从当前 tick 重新计算下一次到期；过期项不会追赶式重复派发。

场景：`Assets/StellarFramework/Samples/KitSamples/Scenes/SimulationKit_Playable.unity`。
