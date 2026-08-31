# TimeKit / 源码设计

运行链路：`TimeKitDriver(Update/unscaledDeltaTime) -> TimeClock(long Tick) -> TimeScheduler -> IndexedMinHeap<int> -> TimerSlotPool`。

- `TimeClock` 只推进 Tick 和保存小数余量。
- `GameCalendarConverter` 只负责 Tick 与游戏日期转换。
- `TimeScheduler` 只处理 Tick，不依赖日历、ActionKit、EventKit、UniTask 或 Coroutine。
- Heap 存 Slot Id；节点保存 HeapIndex，使取消为 `O(log N)`。
- Slot 回收会清空 callback / receiver 并递增 Version，旧 Handle 无法影响复用节点。

关键状态：`Scheduled` 节点恰好在 Heap 中；`Executing` 节点已移出 Heap；`CancelRequested` 在 callback 返回后释放；`Free` 节点不持有业务引用。

Timer callback 可以取消自身、取消其他 Timer、注册新 Timer 或调用 `ClearAllTimers`。调度器先从 Heap 移除节点再回调，并以每帧预算阻止即时任务无限重入。
