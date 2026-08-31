# TimeKit / 游戏世界时间与定时调度

`TimeKit` 是游戏世界的统一时间轴和未来事件调度器。它适合建筑生产、作物成熟、Buff 失效、商店刷新、离线结算等“某个世界时刻应该发生什么”的业务。

世界时间的唯一真值是 `long Tick`：`1 Tick = 1 游戏毫秒`。日期、月份和星期只是由 Tick 按游戏日历换算出来的视图。

## 先判断该用哪个 Kit

| 需求 | 使用 |
| --- | --- |
| 等待动画、UI 转场或当前流程的几秒钟 | `ActionKit.Delay` |
| 在游戏世界两小时后完成一项生产 | `TimeKit.ScheduleAfter` |
| 每个游戏日结算一次收益 | `TimeKit.ScheduleEvery` |
| 读取或保存游戏世界当前时间 | `TimeKit.Tick` / `TimeKit.Now` |

不要用 TimeKit 替代协程或 ActionKit 的流程等待。TimeKit 的回调不保证和某个 MonoBehaviour 一起销毁；它描述的是独立于页面和流程的世界规则。

## 导入与启动

从框架开发工程导出 `TimeKit` 时，导出器会同时带上它唯一的运行时依赖 `LogKit`。不依赖 UniTask、ActionKit、EventKit、PoolKit、Addressables 或 HybridCLR。

运行时第一次访问 `TimeKit` 后会自动创建一个 `DontDestroyOnLoad` 驱动器，并在每帧使用 `Time.unscaledDeltaTime` 推进。通常不需要创建 GameObject，也不需要手动调用 `Update`。

默认世界从 `Tick = 0` 开始，以 1 倍速推进，采用 7 天/周、30 天/月、12 月/年的游戏日历。

## 最小示例

```csharp
using StellarFramework;

public sealed class Workshop
{
    private TimerHandle _finishHandle;

    public void StartProduction()
    {
        _finishHandle = TimeKit.ScheduleAfter(
            GameDuration.Hours(2),
            CompleteProduction);
    }

    public void CancelProduction()
    {
        if (TimeKit.Cancel(_finishHandle))
        {
            _finishHandle = TimerHandle.Invalid;
        }
    }

    private void CompleteProduction()
    {
        // 发放产物、刷新数据。
    }
}
```

`ScheduleAfter` 返回的 `TimerHandle` 是取消凭据。任务触发完成、取消、`Reset` 或 `ClearAllTimers` 后，旧 Handle 都会失效；不要保存它并期望跨存档继续有效。

## 时间控制

```csharp
TimeKit.Pause();              // 显式暂停世界时间
TimeKit.Resume();             // 恢复推进
TimeKit.TimeScale = 3d;       // 三倍游戏速度
TimeKit.AddTime(GameDuration.Hours(8)); // 立即向未来结算八个游戏小时
```

TimeKit 默认使用 `unscaledDeltaTime`，所以 Unity 的 `Time.timeScale = 0` 不会暂停世界时间。这使暂停菜单、广告和加载 UI 不会意外冻结生产队列；需要冻结世界时请调用 `TimeKit.Pause()`。

`AddTime` 会立即处理到期任务，但每次最多执行 `MaxCallbacksPerUpdate` 个回调。剩余到期任务会留到后续帧执行，避免一次离线结算卡死主线程。

## 配置日历与预算

在注册任何 Timer 之前配置。配置会清空 Timer 并把世界时间恢复到 0，因此不应在游戏进行中随意调用。

```csharp
TimeKit.Configure(new TimeKitSettings
{
    InitialTimerCapacity = 2048,
    MaxCallbacksPerUpdate = 512,
    DefaultTimeScale = 1d,
    Calendar = new GameCalendarSettings(
        daysPerWeek: 7,
        daysPerMonth: 28,
        monthsPerYear: 4)
});
```

`InitialTimerCapacity` 用于减少首次大批量注册时的扩容；也可以在进入大地图前调用 `TimeKit.Reserve(2048)`。`MaxCallbacksPerUpdate` 是帧预算，数值越大，离线结算越快，但单帧尖峰也越高。

日期换算使用当前配置的日历：

```csharp
GameDateTime now = TimeKit.Now;
long tick = TimeKit.ToTick(new GameDateTime(2, 1, 1, 8));
TimerHandle handle = TimeKit.ScheduleAt(tick, OnShopRefresh);
```

`GameDateTime` 的公共构造函数按默认日历计算 `WeekOfYear` 与 `DayOfWeek`。当项目使用自定义日历时，以 `TimeKit.Now` 得到的日期视图为准；传给 `ToTick` / `ScheduleAt` 的年月日时分秒仍会按当前 TimeKit 日历验证和转换。

## 一次性与周期任务

```csharp
// 在绝对世界时刻触发。过去或当前时刻不会在这一行同步回调，
// 而是在下一次 TimeKit 更新或 ProcessDueNow 时处理。
TimeKit.ScheduleAt(8 * 60 * 60 * 1000L, OnShopRefresh);

// 每个游戏小时结算一次；-1 表示无限循环。
TimeKit.ScheduleEvery(
    GameDuration.Hours(1),
    OnHourlySettlement,
    TimerCatchUpPolicy.Latest);

// 只执行三次。
TimeKit.ScheduleEvery(GameDuration.Minutes(10), OnPulse,
    TimerCatchUpPolicy.Once, repeatCount: 3);
```

周期任务在大幅跳时会出现“错过多个周期”的情况。选择业务需要的追赶策略：

| 策略 | 回调次数 | 下次触发 | 典型用途 |
| --- | --- | --- | --- |
| `All` | 每个遗漏周期各执行一次 | 保持原始周期网格 | 必须逐次结算的离散步骤 |
| `Once` | 只执行一次 | 从当前时刻开始新的周期 | UI 刷新、可合并轮询 |
| `Latest` | 只执行一次，`ElapsedCount` 表示合并次数 | 保持原始周期网格 | 资源产出、状态同步 |
| `Skip` | 不执行遗漏回调 | 保持原始周期网格 | 只关心未来周期的提示 |

`All` 也受到每帧预算限制，因此一次大跳不会无限占用同一帧。需要汇总遗漏次数时使用带上下文的回调：

```csharp
TimeKit.ScheduleEvery(
    GameDuration.Hours(1),
    context => AddIncome(context.ElapsedCount),
    TimerCatchUpPolicy.Latest);
```

`TimeTriggerContext` 包含计划触发 Tick、实际处理 Tick、`ElapsedCount` 和 `IsCatchUp`。只有 `Latest` 才应把 `ElapsedCount` 当作合并后的结算数量。

## 高规模业务：避免捕获 Lambda

少量 Timer 可直接用 lambda。成千上万个 Timer 时，优先让稳定对象实现 `ITimeEventReceiver`，避免每次注册创建捕获闭包：

```csharp
using StellarFramework;

public sealed class BuildingTimeEvents : ITimeEventReceiver
{
    public void OnTimeEvent(int eventId, in TimeTriggerContext context)
    {
        if (eventId == 1)
        {
            // 建筑完成。
        }
    }
}

TimeKit.ScheduleAfter(GameDuration.Hours(4), buildingEvents, eventId: 1);
```

Receiver 在 Timer 生命周期内被强引用。若对象不再有效，主动 `Cancel` 对应 Handle，或在切场景/读档时调用 `ClearAllTimers`。

## 回调内允许什么

回调中可以取消自身或其他任务、注册新任务、调用 `ClearAllTimers`。调度器会先把当前任务从 Heap 取出，再执行回调，因此这些操作是安全的。

不要在回调中调用 `ProcessDueNow`；这是嵌套调度，会被拒绝并记录错误。也不要在回调内无上限地注册零延迟任务，虽然帧预算会阻止死循环，但会制造不必要的 backlog。

## 存档、读档和离线结算

TimeKit 不序列化 delegate、receiver 或 `TimerHandle`。存档应保存业务数据和世界 Tick，而不是保存内部 Timer：

```csharp
// 存档：保存 TimeKit.Tick，以及建筑的完成目标 Tick。
long finishTick = TimeKit.Tick + GameDuration.Hours(2).Ticks;

// 读档：先 Reset 世界时间，再根据业务数据重新注册任务。
TimeKit.Reset(TimeKit.ToDateTime(savedWorldTick));
TimeKit.ScheduleAt(savedFinishTick, CompleteProduction);
```

如果有离线时长，先计算出目标 Tick，再用 `AddTime` 或 `SetTime` 结算。对可合并收益一般选 `Latest`；对不可丢失的离散事件选择 `All` 并合理设置帧预算。

## 诊断与排查

```csharp
TimeKitDiagnosticsSnapshot snapshot = TimeKit.GetDiagnostics();
Debug.Log($"Active={snapshot.ActiveTimerCount}, Heap={snapshot.HeapCount}, " +
          $"Backlog={snapshot.DueBacklogCount}");
```

`DueBacklogCount > 0` 表示预算用尽时至少还有一个到期任务，并非精确总数。`TimeKitBenchmark.Run100k()` 仅在 Editor 或 Development Build 编译，用于比较注册、取消、空闲帧和到期结算耗时；不要把它放入正式业务逻辑。

常见问题：

- Timer 没触发：确认没有 `Pause()`，并检查目标 Tick 是否在未来。
- 暂停菜单中生产仍在走：这是默认行为，调用 `TimeKit.Pause()`。
- 读档后旧任务消失：这是设计行为，读档时必须由业务重新注册。
- 大跳后回调没有全部在同帧执行：检查 `MaxCallbacksPerUpdate` 与诊断 backlog。
- 取消失败：Handle 可能已触发、已取消、已被 `Reset` 清空，或来自另一轮运行。

## 上线前检查

- 在项目入口配置日历、容量和回调预算。
- 给每种周期业务明确选择追赶策略。
- 存档保存目标 Tick，不保存 delegate 或 Handle。
- 进入大规模场景前调用 `Reserve`，并在 Development Build 观察诊断数据。
- 用目标平台和真实离线结算数据做一次压力测试，确认预算不会造成可见卡顿。
