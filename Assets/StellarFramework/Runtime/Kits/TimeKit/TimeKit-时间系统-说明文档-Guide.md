# TimeKit / 游戏世界时间与调度系统

`TimeKit` 管理游戏世界的绝对 Tick 和未来事件调度；它不是 ActionKit 的流程等待替代品。

- `ActionKit.Delay`：当前表现流程等待。
- `TimeKit.ScheduleAt / ScheduleAfter / ScheduleEvery`：世界时间到达某个时刻时执行业务逻辑。

TimeKit 使用 `long Tick`（1 Tick = 1 游戏毫秒）、数组 Slot Pool 和 Indexed MinHeap。空闲帧只检查最近到期节点，不遍历全部 Timer。它默认由 `Time.unscaledDeltaTime` 驱动，因此 Unity `Time.timeScale = 0` 不会自动暂停世界时间；请显式调用 `TimeKit.Pause()`。

```csharp
TimerHandle handle = TimeKit.ScheduleAfter(GameDuration.Hours(2), OnProductionFinished);
TimeKit.ScheduleEvery(GameDuration.Hours(1), OnProduce, TimerCatchUpPolicy.Latest);
```

高规模业务避免捕获 Lambda，优先使用 `ITimeEventReceiver`：

```csharp
TimeKit.ScheduleAfter(GameDuration.Hours(4), receiver, eventId: 1);
```

`Latest` 会将遗漏周期压缩为一次回调，并在 `TimeTriggerContext.ElapsedCount` 中给出代表次数；`All` 逐次执行但受 `MaxCallbacksPerUpdate` 预算限制；`Once` 从当前时刻重新开始；`Skip` 不补发历史周期。

TimeKit 不序列化 delegate。存档保存世界 Tick 与业务的目标 Tick，读档后重置 TimeKit 并由业务重新注册仍需主动通知的未来事件。
