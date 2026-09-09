# FlowKit 4 人消防演练完整案例

本文对应：

- `FireDrillWorkflow4P.flow.json`：运行时 Graph Source of Truth
- `FireDrillWorkflow4P.flow.editor.json`：Editor 布局元数据
- `FlowKit_Playable.unity`：可直接 Play 的 Sample 场景
- `FireDrillFlowSample.cs`：模拟外部 Gameplay，并向 FlowKit 回报事实
- `FireDrillFlowSampleAdapter.cs`：Operation Adapter，负责 FlowKit -> Gameplay 命令适配
- `FireDrillSampleIds.cs`：稳定业务契约 ID

## 1. 这个案例验证什么

消防演练不是“从头到尾只有成功线”的演示，而是用来验证真正的工作流闭环：

1. FlowKit 下发当前阶段命令。
2. Gameplay 收到命令后开启对应玩法。
3. 玩家实际完成操作。
4. Gameplay 把事实回报给 FlowKit。
5. FlowKit 根据成功、失败、取消、超时和并行汇合决定下一节点。
6. 下一节点再次通过 Operation 通知 Gameplay 开启新阶段。
## 2. 架构边界

FlowKit 只负责流程编排，不负责玩法实现。

```text
FlowKit Graph
    |
    | Operation Command
    v
IFlowOperationAdapter
    |
    v
Gameplay / UI / Interaction / Network / Scene Service
    |
    | Signal / State / OperationResult
    v
FlowKit Runtime
    |
    v
Next Node
```

固定原则：

- `Operation` 是命令：现在该做什么。
- `Signal` 是一次性事件：刚刚发生了什么。
- `State` 是当前事实：现在是什么状态。
- `OperationResult` 是命令执行结果：成功、失败或取消。
- FlowKit 不直接调用灭火器、PICO 手柄、Collider、动画、网络 RPC 或具体 UI。
## 3. 四人角色

| 玩家 | 角色 | FlowKit 开启命令 | Gameplay 完成事实 |
| :--- | :--- | :--- | :--- |
| 1号 | 指挥员 | `fire_drill.commander.start` | `fire_drill.commander.reported = true` |
| 2号 | 灭火员 A | `fire_drill.extinguisher_a.start` | `fire_drill.extinguisher_a.completed = true` |
| 3号 | 灭火员 B | `fire_drill.extinguisher_b.start` | `fire_drill.extinguisher_b.completed = true` |
| 4号 | 疏散员 | `fire_drill.evacuator.start` | `fire_drill.evacuation.completed = true` |

角色开始前还有 4 个 Host Signal：

- `fire_drill.player1.ready`
- `fire_drill.player2.ready`
- `fire_drill.player3.ready`
- `fire_drill.player4.ready`

它们只表示“这一次玩家确认就绪”，所以用 Signal，而不是 State。

## 4. 正常流程

```text
开始
 -> 演练说明
 -> 分配角色
 -> Parallel：4 人分别收到角色提示
 -> 4 个 WaitSignal：等待 Ready
 -> Join：四人全部就绪
 -> 启动消防警报
 -> Race：任务完成组 vs 180 秒超时
```

任务阶段继续：

```text
Race
├─ Parallel：1/2/3/4 号任务并行
│  ├─ Operation.start -> WaitState 完成事实
│  └─ failed/cancelled -> 对应失败处理 -> flow.fail
└─ Delay 180s -> flow.fail（超时）

四人全部完成 -> Join -> 安全复核 Operation
-> WaitState safety.passed == true
-> 演练总结 -> Delay 5s -> flow.complete
```

这里的关键点不是“灭火怎么做”，而是 FlowKit 能够同时管理四条工作流分支，并在事实满足后安全汇合。

## 5. 失败、取消与超时

`flow.operation` 有三个业务输出：`succeeded`、`failed`、`cancelled`。正式 Graph 应明确决定失败和取消如何处理；未连接建议处理的失败端口时 Compiler 会给出 Warning。

本案例的关键任务失败会进入对应失败处理 Operation，随后进入 `flow.fail`。`flow.fail` 会让 Run 以 `FlowRunStatus.Failed` + `BusinessFailure` 结束，而不是伪装成正常 Complete。

180 秒超时由 `Race` 实现：任务分支先完成，则超时分支被取消；超时先到，则流程失败。不要在 Gameplay 里再维护第二套重复的流程计时状态。

## 6. 双向通信代码怎么对应

FlowKit -> Gameplay：`FireDrillFlowSampleAdapter` 实现 `IFlowHostConfigurator`，在 Host 初始化前显式注册所有 OperationId；运行时通过 `IFlowOperationAdapter.Start` 接收命令并转交给 Gameplay。

Gameplay -> FlowKit：

```csharp
host.Services.Signals.Publish(
    new FlowSignalId("fire_drill.player2.ready"),
    FlowSignalScope.Host);

host.Services.States.Set(
    new FlowStateKey("fire_drill.extinguisher_a.completed"),
    FlowValue.FromBool(true),
    FlowStateLifetime.External);
```

Operation 自身执行失败时，则由 Adapter 回调：

```csharp
complete(FlowOperationResult.Failure("reason"));
```

命令和事实必须分开：Operation 不等于“玩家已经完成”，Signal/State 也不负责命令 Gameplay 开始做事。

## 7. 在真实项目里替换 Sample

Sample 中的 `FireDrillFlowSample` 只是教学用 Gameplay 模拟器。真实项目保持 Graph 和 Adapter 边界，替换成自己的 Service：

- `fire_drill.extinguisher_a.start` -> 灭火任务 Service / UI / 交互系统。
- 玩家完成后 -> Service 向 FlowKit 设置 State 或发布 Signal。
- 多人网络项目中，服务端/房间系统先确认事实，再由客户端 Adapter 把经过确认的事实写入本地 FlowKit；FlowKit Core 不承担网络权威同步。
- PICO/VR 交互、碰撞、抓取、灭火器模拟都属于 Gameplay，不进入 FlowKit Core。

建议所有业务 ID 集中维护，禁止散落魔法字符串；本 Sample 的 `FireDrillSampleIds.cs` 就是这种契约层示例。

## 8. 如何运行与验收

1. 打开 `Assets/StellarFramework/Samples/KitSamples/Scenes/FlowKit_Playable.unity`。
2. 进入 Play Mode，观察左上角 Sample 面板和双向事件日志。
3. 依次让 4 人 Ready，再完成 4 个角色任务，最后通过安全复核；Run 应进入 `Completed`。
4. 重新运行并勾选任一 `Operation.failed` 模拟项；对应任务应进入失败分支，Run 应进入 `Failed / BusinessFailure`。
5. 在 FlowKit Editor 中打开 `FireDrillWorkflow4P.flow.json`，确认成功、失败、取消和超时线路与运行结果一致。

自动化测试还会直接读取这份真实 JSON，验证正常闭环和 Operation 失败闭环，避免 Sample Graph 与测试手写 Graph 漂移。
