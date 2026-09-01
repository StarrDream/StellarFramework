# SimulationKit 批量模拟调度

SimulationKit 是 Foundation 层的纯 C# 批量调度器。它把“哪些 ID 在当前 tick 到期”与业务对象、Unity 生命周期和执行逻辑分开，适合农场作物、生产线、冷却、AI 心跳等大量低频模拟。

## 适用场景

业务为每个对象分配一个正数 `SimulationId`，登记一个正的 `intervalTicks`，然后在自己的世界时钟推进后调用 `CollectDue`。调度器只返回 ID，业务决定如何读取和更新对象状态。

100000 个对象不需要 100000 个 `Update`。业务可以为每次 `CollectDue` 提供固定长度的缓冲区，分多帧完成工作。SimulationKit Core 不知道 Unity Frame、`Update`、`FixedUpdate` 或 PlayerLoop；`destination.Length` 只是**单次 `CollectDue()` 调用的 Count Budget**，不是 Core 自动识别的每帧预算。

## 与 TimeKit 的边界

TimeKit 管理世界时间、倍率、暂停和通用 Timer；SimulationKit 只消费调用方传入的 `long` tick 和模拟对象 ID。SimulationKit 不引用 TimeKit，也不自动读取 `Time.time`、`Time.deltaTime` 或 Unity PlayerLoop。可以用 TimeKit 驱动它，也可以使用服务器 tick、回合数或测试时钟。

## 基本用法

```csharp
var scheduler = new SimulationScheduler(100000);
var id = new SimulationId(42);

// 第一次在 now + interval 到期
scheduler.TryRegister(id, nowTick: 0, intervalTicks: 10);

// 需要分散首帧压力时指定首次延迟，0 表示当前 tick 立即到期
scheduler.TryRegister(new SimulationId(43), 0, 10, firstDelayTicks: 3);

SimulationId[] buffer = new SimulationId[512];
SimulationCollectResult batch = scheduler.CollectDue(nowTick, buffer);
for (int i = 0; i < batch.WrittenCount; i++)
{
    SimulateOne(buffer[i]);
}
// batch.HasBacklog 表示仍有已到期但尚未派发的 Entry。
// 实时主循环通常留到下一帧，不要在这里 while-drain。
```

注册成功后，普通注册的 `NextDueTick = nowTick + intervalTicks`。显式 `firstDelayTicks` 只影响第一次派发；每次实际派发后，下一次都从本次传入的 `nowTick + intervalTicks` 计算。

## Count Budget 与实时分帧

`destination.Length` 是一次调用的硬预算。若游戏希望“每帧最多处理 500 个对象”，调用方应在该帧只调用一次 `CollectDue(..., buffer500)`，处理返回的 ID，然后结束本帧 Simulation 工作。`HasBacklog == true` 是状态信号，默认含义是下一帧继续，不是“本帧必须清空”。没有到期项时只检查堆根，不扫描全部对象；`Span.Empty` 不写出、不改变调度状态，但仍返回当前 `HasBacklog`。

推荐的实时 Frame-Spreading 用法如下。`FrameStep` 可以是业务 `Update` 中的一次调用，也可以是样例里的教学按钮；它不属于 Core API。

```csharp
void Update()
{
    long nowTick = GetCurrentGameTick();
    SimulationCollectResult result = cropScheduler.CollectDue(nowTick, cropBuffer500);

    for (int i = 0; i < result.WrittenCount; i++)
    {
        cropService.Simulate(cropBuffer500[i], nowTick);
    }

    // 本帧到这里结束。HasBacklog 留给下一帧继续。
}
```

例如 100000 株植物同时在同一 tick 到期、`buffer500` 长度为 500 时，实时循环每帧只调用一次：第 1 帧处理 500，第 2 帧处理下一批，约 200 帧（60 FPS 下约 3.33 秒）消化完 backlog。

不要把下面的写法当作实时 `Update` 的默认范式：

```csharp
SimulationCollectResult result;
do
{
    result = scheduler.CollectDue(nowTick, buffer);
    Simulate(result);
}
while (result.HasBacklog);
```

它会在同一帧连续调用 `CollectDue`，例如 100000 个到期对象和 `buffer.Length == 500` 会在一帧执行约 200 次调用，从而绕过“每帧最多 500”的业务预算。

## HasBacklog 与显式 Flush

正式定义：**`HasBacklog` 表示当前 `nowTick` 下，Scheduler 中仍存在至少一个已到期但尚未被本次调用派发的 Entry。**

同 tick 连续 `CollectDue(nowTick, buffer)` 是合法能力，适合 Benchmark、Unit Test、Editor Tool、Loading、Pause/Offline Flush 和维护脚本。需要主动一次性清空时，可以明确写成 Explicit Flush：

```csharp
while (true)
{
    SimulationCollectResult result = scheduler.CollectDue(nowTick, buffer);
    Simulate(result);
    if (!result.HasBacklog) break;
}
```

Explicit Flush 会在当前调用线程立即连续消化 backlog，不提供 frame spreading；它不是实时游戏主循环的默认策略。保留同 tick 重复调用能力不会改变 Core 的时间语义：Core 只要求 `nowTick` 非递减，同 tick 合法，刚刚派发的 Entry 已被重排到未来。

## 预算与过期合并

如果业务很久没有推进，某项可能已经过期很多个 interval。一次 `CollectDue` 只返回一次，并把下一次设置为“当前派发 tick + interval”，不会追赶式连续返回历史周期。这能把恢复负载限制在调用方预算内。

当同一 tick 的到期项超过预算时，先按 `NextDueTick`、再按 `SimulationId.Value` 稳定排序；实时模式每帧取一批，Explicit Flush 才在同一 tick 连续取到 `HasBacklog=false`。相同 tick 再调用不会重复返回刚刚重排到未来的项。

大规模模拟有两层防线：`firstDelay` 用于尽量错开首次到期、降低峰值；Count Budget 用于峰值仍出现时限制单次处理量。两者都不替业务保存状态：实际处理可能从理论 due tick 延后，但业务应根据 `CurrentGameTick - LastSimulationTick` 一次推进完整的逻辑时间，不丢失离线期间的模拟时间。

## 变更、休眠与读档

- `TrySetInterval` 从传入的当前 tick 重新计算下一次到期，并自动修复堆序。
- `TryUnregister` 适合对象休眠或销毁；唤醒时用业务保存的 ID 重新登记。
- 存档只保存业务数据、业务 ID 和 `LastSimulationTick`。不要序列化 `SimulationScheduler` 的堆或字典；读档后按业务状态重建登记。
- 一个调度器是一条时间线；需要独立时间线时创建多个实例。

## 时间和失败规则

同一个调度器的 `nowTick` 必须单调不减；回退会抛出 `InvalidOperationException`。同 tick 重复调用合法。即使某次注册/SetInterval 因参数失败，传入的更晚 tick 仍被观察，之后不能回到更早时间。

写操作用 `SimulationMutationResult` 报告 `InvalidId`、`DuplicateId`、`NotFound`、`InvalidInterval`、`InvalidDelay` 或 `TickOverflow`。参数检查和加法检查在修改前完成，失败不会半改状态。Collect 在当前 root 的下一次 tick 溢出时抛出 `OverflowException`，不会写出或修改这个 root；之前已经成功派发的项保持已提交状态。

## 不包含的能力

V1 不包含回调、委托、`ISimulatable`、GameObject/MonoBehaviour、Transform 跟踪、每实体 Update、线程安全、Jobs/Burst、NativeArray、存档、事件、网络、分组优先级、CPU 毫秒预算或 ToolsHub 集成。需要这些能力时，在业务项目或独立 Adapter 中组合，不要把依赖倒灌到 Core。

## 导出边界

`StellarFramework-SimulationKit.unitypackage` 只包含 SimulationKit Runtime 和 asmdef；不包含 UnityEngine 引用、其他 Kit、UPM、样例、Tests 或 Verification。`StellarFramework-Sample-SimulationKit.unitypackage` 通过依赖闭包带入 SimulationKit，并额外包含 Common、样例脚本和 `SimulationKit_Playable.unity`。
