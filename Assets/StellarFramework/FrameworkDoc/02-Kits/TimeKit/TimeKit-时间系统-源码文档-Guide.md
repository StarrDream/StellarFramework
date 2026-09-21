# TimeKit / 源码设计与维护说明

本文面向需要扩展或维护 TimeKit 的开发者。使用方式请先看同目录的“时间系统-说明文档”。

## 目标与边界

TimeKit 只解决两件事：维护可控的游戏世界 Tick，以及在未来 Tick 调度业务事件。它不实现现实时间校准、网络时间同步、存档格式、协程等待或跨线程调度。

运行时程序集为 `StellarFramework.TimeKit`，唯一程序集依赖是 `StellarFramework.LogKit`。因此单 Kit 导出时不应引入 UniTask、ActionKit、EventKit、PoolKit、Addressables、HybridCLR 或任何业务程序集。

## 运行链路

```text
Unity Update
  └─ TimeKitDriver (Time.unscaledDeltaTime)
       └─ TimeClock.Advance
            └─ TimeKit.Tick
                 └─ TimeScheduler.ProcessDue
                      ├─ IndexedMinHeap (按 TriggerTick / Sequence 取最早节点)
                      ├─ TimerSlotPool (节点所有权与复用)
                      └─ callback / ITimeEventReceiver
```

`TimeKit` 是静态外观，负责参数校验、初始化、日历转换和公开 API；`TimeClock` 不知道 Timer；`TimeScheduler` 不知道日历和 Unity；`TimeKitDriver` 是唯一引用 UnityEngine 的运行时驱动器。这种单向依赖保证核心调度可以独立测试和独立导出。

## 时间模型

- Tick 是 `long`，单位为游戏毫秒，范围远大于一般项目生命周期。
- `TimeClock` 使用 `double` 仅保存帧 delta 的小数 Tick 余量；已提交的世界时间始终是整数 Tick。
- 默认推进源是 `Time.unscaledDeltaTime`，因此 Unity `Time.timeScale` 与世界时间完全解耦。
- `Pause` 只冻结 TimeClock；任务仍在 Scheduler 中等待。
- `AddTime` 与 `SetTime` 只允许向未来推进。需要回退世界时必须 `Reset`，并按业务数据重建任务。

所有可能溢出的 Tick 加减乘都必须走 `TickMath`。遇到非法浮点数、负值或溢出，公开 API 会失败并通过 LogKit 报错，不得静默回绕。

## 日历模型

`GameCalendarSettings` 是无状态值类型，只定义每周天数、每月天数、每年月数。`GameCalendarConverter` 负责 Tick 与 `GameDateTime` 的纯换算，Scheduler 从不引用日历。

日期是展示与业务输入的辅助层，Tick 才是调度真值。修改日历只允许在没有活动 Timer 时通过 `TimeKit.Configure` 完成；否则同一目标 Tick 的日期语义会改变，容易产生存档和任务错位。

## Timer 节点与 Handle

`TimerSlotPool` 维护连续的 `TimerNode[]`。Slot Id 从 1 开始，0 保留给无效 Handle。每个节点含有：

- `TriggerTick`、`IntervalTicks`、`RemainingExecutions` 和追赶策略；
- 统一排序所需的 `Sequence`；
- callback 或 receiver/eventId；
- `HeapIndex`、状态和 `Version`。

`TimerHandle` 由 Slot Id 与 Version 组成。Slot 回收时会清空 callback / receiver、递增 Version 并加入空闲链表，因此旧 Handle 无法取消复用后的新 Timer，也不会因为引用残留阻止 GC。

节点状态约束如下：

| 状态 | Heap 中 | 业务引用 | 含义 |
| --- | --- | --- | --- |
| `Free` | 否 | 无 | 可复用 Slot |
| `Scheduled` | 恰好一次 | 有 | 等待到期 |
| `Executing` | 否 | 有 | 正在回调 |
| `CancelRequested` | 否 | 有 | 回调返回后释放 |

维护时必须保持这些不变量。`TimeScheduler.ValidateInvariants` 提供给测试验证 Heap、Slot 与状态的关系。

## 排序与复杂度

`IndexedMinHeap` 只保存 Slot Id，比较时读取节点的 `(TriggerTick, Sequence)`。同 Tick 的任务按注册顺序确定性执行。

- 注册：`O(log N)`
- 取消 Scheduled Timer：`O(log N)`
- 读取最近任务：`O(1)`
- 空闲帧：只比较 Heap 顶部，不扫描全部 Timer
- 到期处理：每个执行或跳过的节点按 Heap 操作成本处理

节点保存在 Slot Pool 而不是随 Heap 交换，所以每个节点保存的 `HeapIndex` 始终可用于定位和移除。不要改成 `List.Remove` 或按 callback 查找，这会把大规模取消退化成线性扫描。

## 到期与追赶策略

一次性 Timer 从 Heap 弹出后执行，再立即释放 Slot。周期 Timer 先计算：

```text
elapsedPeriods = (nowTick - triggerTick) / intervalTicks + 1
```

然后依策略处理：

- `All`：每次只消费一个周期，继续把下一周期压回 Heap；帧预算会自然限制追赶量。
- `Once`：回调一次，然后把下一次设为 `nowTick + interval`。
- `Latest`：回调一次，`ElapsedCount` 表示合并周期数量；下一次仍对齐原始周期网格。
- `Skip`：不回调，直接前进到 `nowTick` 之后的下一周期。

`RemainingExecutions` 是实际应消费的周期数。无限循环使用 `-1`，不允许 `0`。新增策略时必须同时更新参数校验、消费规则、下一触发 Tick 的计算和测试。

## 回调重入规则

执行 callback 前，节点已经从 Heap 移除并进入 `Executing`。因此 callback 内以下操作是允许的：

- `Cancel` 自身或其他 Handle；
- 注册新的 Timer；
- `ClearAllTimers`。

取消正在执行的节点只改成 `CancelRequested`，当前 callback 可以正常返回，但周期任务不会被再次压回 Heap。`ClearAll` 会释放全部已调度节点，并请求取消当前执行节点。

不允许 callback 中调用 `ProcessDueNow`。Scheduler 会以 `_isProcessing` 防止嵌套调度，避免回调顺序、预算和 Slot 状态被重入打乱。新增公开 API 时不得绕过这个保护。

## 生命周期与 Unity 驱动

`TimeKitDriver` 在 `BeforeSceneLoad` 确保创建，使用 `DontDestroyOnLoad` 保持跨场景唯一。`SubsystemRegistration` 会在无域重载进入 Play Mode 时重置静态状态，防止上一轮运行遗留 Timer。

不要让业务主动销毁名为 `[StellarFramework.TimeKit]` 的对象；若场景或测试环境销毁了它，下次加载前应重新确保驱动器存在。TimeKit 不支持从工作线程调用，所有公开调用都应在 Unity 主线程进行。

## 诊断与基准

`TimeKitDiagnostics` 累计活跃数、峰值、注册/执行/取消/错误次数；`GetDiagnostics` 返回快照，适合 Development Build 的调试面板。

`DueBacklogCount` 为刻意的低成本信号：预算耗尽时为 1，含义是“至少仍有一个到期任务”，不是精确待处理数量。精确计数需要扫描或复制 Heap，不应放在普通帧路径。

`TimeKitBenchmark.Run100k()` 仅在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 中编译。它覆盖 10 万次注册、空闲帧、间隔取消与批量到期，帮助观察时间与内存趋势；基准自身会分配测试用 Handle 数组，不能把测得的 managed delta 误判为核心调度器每帧分配。

## 测试与修改清单

`Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Kits/TimeKit/TimeKitTests.cs` 覆盖日历往返、分数 Tick、暂停和倍率、同 Tick 顺序、失效 Handle、追赶策略、预算 backlog、回调重入与错误输入。

`Assets/StellarFramework/Tests/PlayMode/TimeKitPlayModeTests.cs` 覆盖实际 Unity 帧驱动、`Time.timeScale = 0` 与显式暂停行为。

修改 TimeKit 后至少执行：

1. TimeKit EditMode 测试；
2. TimeKit PlayMode 测试；
3. 全量 EditMode 验证；
4. Unity Console 的 Error/Warning 检查；
5. 变更涉及导出目录或依赖时，在干净业务工程导入导出的 `.unitypackage`，确认只有 LogKit 被带入且能编译。

## 扩展边界

业务层可以在 TimeKit 之上封装“每日刷新”“建筑队列”“离线结算”或存档重建服务，但不要把业务状态写进 Scheduler。若未来需要真实世界时钟或服务端权威时间，应在外层把可信时间转换为目标 Tick，再调用 `SetTime` / `AddTime`；不要让 Scheduler 直接请求网络或读取系统时钟。
