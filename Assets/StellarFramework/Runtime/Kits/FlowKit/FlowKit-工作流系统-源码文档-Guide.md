# FlowKit 源码结构

## Core

- `FlowPrimitives.cs`：稳定定义 ID、可写入 JSON 的 AssetReference/EnumReference、运行态身份（含 RuntimeEpoch）、值容器、时间域、效果语义和幂等键。
- `Graph/FlowGraphData.cs`：可序列化 Graph 定义、参数包和节点/端口描述。
- `UnityIntegration/FlowGraphJson.cs`：Unity JsonUtility 入口；导出 JSON 时复制并按 NodeId、Edge 端点和参数 key 排序，避免列表重排造成无意义 Git diff。
- `Compile/FlowCompiler.cs`：显式 Registry 校验、稳定索引（输出路由同时保留目标输入端口）、Immediate 循环检测和 FNV-64 PlanHash。
- `Signal/FlowSignalStateBlackboard.cs`：SignalRouter、StateStore、Blackboard。Signal 入队后按预算派发并暴露 Pending/OldestAge 背压指标，State 只有真实变更才递增 Revision，Blackboard 支持 Batch。
- `Scheduling/FlowTimerScheduler.cs`：`IFlowTimeScheduler` 与每个时间域独立的 due-time 最小堆、Slot+Generation 句柄，不做每帧全量扫描；Scaled、Unscaled、FlowTime 互不阻塞。
- `Scheduling/FlowPollingScheduler.cs`：只轮询显式注册的外部状态源，支持 EveryFrame/30Hz/10Hz/5Hz/1Hz 频率，并受回调预算限制。
- `Execution/FlowRunner.cs`：按“Signal/Polling → Timer → Run 完成仲裁/激活”的显式阶段推进，提供每帧与单 Run 总激活预算、单 Execution `TryCancel`、失败/取消清理和执行组 Join。
- `Execution/FlowCompletionArbiter.cs`：同一执行句柄的优先级/序号仲裁；`FlowNodeHandle` 负责 stale 与单次关闭。
- `Execution/FlowRuntimeDiagnostics.cs`：`FlowRunner.CaptureDiagnostics()` 提供 Signal backlog age、激活/完成队列、Timer、Poller、Operation、State 和 Binding 摘要，供监控/Debugger 采样。
- `Execution/FlowBuiltInNodes.cs`：内置节点的显式注册与 Handler；第三方功能通过 Adapter 注入。
- `Execution/FlowOperations.cs`：Operation、Capability、Binding、Trace 接口与运行表，并向 Adapter 传递 RuntimeEpoch/PersistentRunId/OwnerToken/IdempotencyKey。
- `Execution/FlowAssets.cs`：`IFlowAssetResolver`/`IFlowAssetLease` 资源边界；Resources、Addressables、AssetBundle 由业务项目适配。
- `Execution/FlowCancellation.cs`：Run/Execution 取消树（子作用域在关闭时从父节点摘除，避免长流程累积）和显式 RetryPolicy 约束。
- `Execution/FlowRuntimeEpoch.cs`：宿主 RuntimeEpoch；Domain Reload/重建后旧 Run 与回调会被隔离。
- `Execution/FlowComposition.cs`：条件 AST、Parallel/Race/Join 执行组和 SubFlow 依赖循环检测。
- `Persistence/FlowSnapshot.cs`：quiescent 快照、PlanHash 校验和 Blackboard/持久 State 恢复；V1 不序列化活动节点、Timer、订阅或 Operation continuation。
- `Persistence/FlowSnapshotMigration.cs`：独立于 Graph Migration 的 PlanHash → PlanHash 快照迁移链，缺失或返回错误版本时拒绝恢复。

Core asmdef 设置 `noEngineReferences=true`，可以单独导出为纯 C# Kit。

## UnityIntegration

`FlowHost` 只负责 Unity 生命周期和时间推进；`FlowBinding` 负责稳定槽注册；`FlowGraphJson` 使用 Unity JsonUtility 读写 Graph 文本。这里不放 UI、ResKit、Addressables、HybridCLR 或业务节点实现。

## Editor / Tests

ToolsHub 模块和 `FlowGraphWindow` 仅在框架开发工程编译，验证 JSON 并显示诊断。EditMode 测试覆盖编译稳定性、Immediate 循环、Runner、Signal、State、Timer 与 Blackboard。导出业务包时不包含 Editor 和 Tests。

## 扩展约定

新增节点时：

1. 选择稳定 TypeId 和 DefinitionVersion。
2. 创建不可变 Descriptor，声明端口、参数、Capability 和 EffectSemantics。
3. 实现 `IFlowNodeHandler`，异步回调必须持有 `FlowNodeHandle`，并在 Handle 上 Track 所有订阅/定时器/操作。
4. 在生成的 Registry 或启动代码中显式 `Register`，不要在 Runtime 扫描程序集或使用反射。
5. 为错误参数、取消、stale 回调、重复完成、宿主线程回调约束和无输出终止补充测试。

节点若要成为终点，Descriptor 必须显式设置 `CompletesFlow`；普通节点的完成回调必须连到已声明的输出端口，否则 Runner 会以 `UnroutedCompletion` 失败。


## v1 Editor / Authoring / Failure Contract

- `FlowHostBuilder` + `IFlowHostConfigurator`：Unity Host 的显式、确定性扩展入口；业务 Operation Adapter 在 Host 初始化前注册。
- `FlowAuthoringCatalog`：可选的稳定 Operation/Signal/State/Blackboard/Binding ID 作者目录，只服务 Editor Authoring，不替代 Runtime 注册。
- `flow.branch.condition` 使用可序列化 Condition AST；`FlowGraphJson` 通过显式 DTO 保存递归条件和所有可 Author 的 `FlowValueKind`。
- `flow.operation` 明确区分 `succeeded / failed / cancelled`；Compiler 会对建议处理但未连接的失败端口给 Warning。
- `flow.fail` 是业务失败终点，Run 进入 `Failed / BusinessFailure`；框架配置/运行时错误仍使用结构化 Runtime Error，不与业务失败混淆。
- `.flow.json` 是 Runtime Source of Truth，`.flow.editor.json` 只保存位置/布局等编辑器元数据。
