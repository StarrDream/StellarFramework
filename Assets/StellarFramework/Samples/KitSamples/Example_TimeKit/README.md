# TimeKit Sample

## 场景

`Assets/StellarFramework/Samples/KitSamples/Scenes/TimeKit_Playable.unity`

运行场景后，左侧面板会直接显示世界 Tick、日历视图、Timer、Catch-Up 和诊断信息。示例只依赖 `StellarFramework.TimeKit`，不依赖 SaveKit、ResKit、Addressables 或 HybridCLR。

## 演示内容

- World Clock：Tick 是世界时间唯一真值，Calendar 是 Tick 的视图。
- Time Scale：1x、10x、60x 只改变 TimeKit 世界时间推进速度。
- Pause / Resume：暂停的是 TimeKit，不是 Unity 的 `Time.timeScale`。
- Workshop：使用真正的 `ScheduleAfter(GameDuration.Hours(2), ...)` 创建一次性任务，可取消并显示目标 Tick。
- Periodic：使用 `ScheduleEvery` 创建每 1 小时任务，显示真实 `ElapsedCount`。
- Catch-Up：切换 All、Once、Latest、Skip 后点击 `+5 Hours`，观察遗漏周期的不同处理。
- Unity 对比：设置 Unity `Time.timeScale=0` 后，TimeKit 仍会使用 unscaled 时间推进；点击 `Pause TimeKit` 才会停止。
- Diagnostics：显示 Active Timer、Heap、Backlog 和最近一次回调数量。

## 手动验收

1. 运行场景，确认 Tick 和时钟持续变化。
2. 点击 `60x`，确认时钟明显加速；点击 `1x` 恢复。
3. 点击 `Start 2-hour Work`，再点击两次 `+1 Hour`，确认 Workshop 变为 `Completed`。
4. 再次启动 Workshop，点击 `Cancel`，推进时间后确认不会增加 Completed Count。
5. 选择 `Latest`，点击 `+5 Hours`，确认一次回调的 `ElapsedCount` 反映遗漏周期；切换其他策略重复观察。
6. 点击 `Unity timeScale = 0`，确认 Tick 仍推进；再点击 `Pause TimeKit`，确认 Tick 停止；最后恢复两个按钮。
7. 停止并重新运行场景，确认 Timer 不残留、Unity `Time.timeScale` 恢复为 1。

## 不在本示例中的内容

Timer 大规模 Benchmark、复杂日历编辑器、农场/作物业务、SaveKit 存档和跨 Kit 组合场景属于其他测试或业务项目，不放进这个最小示例。

完整 API 与设计说明：

- `Assets/StellarFramework/Runtime/Kits/TimeKit/TimeKit-时间系统-说明文档-Guide.md`
- `Assets/StellarFramework/Runtime/Kits/TimeKit/TimeKit-时间系统-源码文档-Guide.md`
