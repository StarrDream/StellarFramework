# SimulationKit 批量模拟调度

SimulationKit 是 Foundation 层的纯 C# 批量调度器。它把“哪些 ID 在当前 tick 到期”与业务对象、Unity 生命周期和执行逻辑分开，适合农场作物、生产线、冷却、AI 心跳等大量低频模拟。

## 适用场景

业务为每个对象分配一个正数 `SimulationId`，登记一个正的 `intervalTicks`，然后在自己的世界时钟推进后调用 `CollectDue`。调度器只返回 ID，业务决定如何读取和更新对象状态。

100000 个对象不需要 100000 个 `Update`。业务可以每个世界 tick 只取固定数量，例如 512 个 ID，分多帧完成工作；`HasBacklog` 为 `true` 时继续在同一个 tick Drain。

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
// batch.HasBacklog 为 true 时，用同一个 nowTick 再次 CollectDue。
```

注册成功后，普通注册的 `NextDueTick = nowTick + intervalTicks`。显式 `firstDelayTicks` 只影响第一次派发；每次实际派发后，下一次都从本次传入的 `nowTick + intervalTicks` 计算。

## 预算与过期合并

`destination.Length` 是一次调用的硬预算。没有到期项时只检查堆根，不扫描全部对象。`Span.Empty` 不写出、不改变调度状态，但仍返回当前 `HasBacklog`。

如果业务很久没有推进，某项可能已经过期很多个 interval。一次 `CollectDue` 只返回一次，并把下一次设置为“当前派发 tick + interval”，不会追赶式连续返回历史周期。这能把恢复负载限制在调用方预算内。

当同一 tick 的到期项超过预算时，先按 `NextDueTick`、再按 `SimulationId.Value` 稳定排序；每次 Drain 使用同一个 now tick，直到 `HasBacklog=false`。相同 tick 再调用不会重复返回刚刚重排到未来的项。

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
