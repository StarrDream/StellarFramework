# FlowKit 使用说明

FlowKit 是本地单进程的声明式工作流运行时。Graph 以 JSON 保存，启动前经过迁移（如有）、校验和编译，运行时只执行不可变的 `FlowPlan`。它适合任务编排、剧情流程、UI 流程、资源准备流程和工具链，不承担实时物理、网络权威同步或 XR 姿态采样。

## 导出组合

- `flowkit.core`：纯 C# Core，不引用 Unity，不需要 UniTask、Addressables 或 HybridCLR。
- `flowkit.unity`：`FlowHost`、`FlowBinding` 和 JSON 入口，依赖 `flowkit.core`，只引用 UnityEngine。
- `flowkit.tools`：框架开发工程中的 Graph Validator 和独立 FlowKit 窗口，依赖 ToolsHub；业务项目不需要导入。

导出目录以 `Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json` 为准。热更新是可选投递方式，不是 FlowKit 的运行时前置条件。

## 最小运行示例

```csharp
var registry = FlowBuiltInNodes.CreateRegistry();
FlowCompileResult result = FlowCompiler.Compile(graph, registry);
if (!result.Succeeded) throw new InvalidOperationException(result.Issues[0].ToString());

var services = new FlowRuntimeServices();
var runner = new FlowRunner(services);
FlowRun run = runner.Start(result.Plan);
runner.Tick(new FlowTimeSnapshot(scaledSeconds, unscaledSeconds, flowSeconds));
```

Unity 场景中可挂载 `FlowHost`，由 Host 在 `Update` 中推进三个时间域。需要场景对象时，在 Host 下放置 `FlowBinding`，为其填写稳定的 `bindingId`；运行时不会调用 `Find` 或依赖 Unity 实例 ID。

复用同一组 `FlowRuntimeServices` 时，Domain Reload、Host 重建或全局重置后应调用 `RuntimeEpoch.Advance()`；旧 Run、Timer、Operation 完成回调会被隔离。

## Graph 约束

- `FlowId`、Node `Id`、TypeId 和 PortId 必须稳定且唯一；JSON 不写 CLR 类型名。参数值使用 Core 的显式 `FlowValueKind`（含 Enum/AssetReference），不会把业务对象直接塞进 JSON。
- Node 的 `DefinitionVersion` 必须与显式 Registry 中的版本一致。
- Edge 只能连接已声明的输入/输出端口；重复 Edge、未知端口、缺少必需参数都会阻止编译。
- 完成节点必须有可达输出，或由 Descriptor 声明 `CompletesFlow`（内置终止节点为 `flow.complete`）；无路由的完成会让 Run 失败，不会静默挂起。
- 迁移器必须显式注册版本链，不会自动升级到“最新版本”。
- Immediate 节点之间的循环会被拒绝；循环流程应插入 Delay、WaitSignal、WaitState 或 Operation 等完成边界。

内置 TypeId：`flow.entry`、`flow.pass`、`flow.complete`、`flow.branch.bool`、`flow.delay`、`flow.wait.signal`、`flow.wait.state`、`flow.stable.for`、`flow.wait.blackboard`、`flow.set.blackboard`、`flow.increment.blackboard`、`flow.emit.signal`、`flow.operation`、`flow.parallel`、`flow.race`、`flow.join`。

## 外部适配器

通过 `IFlowOperationAdapter` 注册资源加载、网络、UI、音频、场景和平台调用。Core 只接收 OperationId、BindingHandle、Capability 和完成结果，不引用第三方 SDK。资源参数可使用 JSON 可序列化的 `FlowValueKind.Asset`/`FlowAssetReference`，再由 `IFlowAssetResolver` 决定 Resources、Addressables 或 AssetBundle 的加载方式。需要的 Capability 必须在启动前显式注册；缺少能力时 Run 状态为 `Rejected`。Operation 适配器应在宿主调度线程（通常是 Unity 主线程）回调完成；如果底层 SDK 在工作线程回调，先由适配器自行投递到宿主线程，不要从后台线程直接改写 Runner/Signal/State/Blackboard。副作用适配器可使用 `FlowOperationContext.OwnerToken` 做所有权仲裁，并使用 `IdempotencyKey` 去重；Exactly-Once 仍由外部系统的幂等/事务能力决定。

Signal 是排队的一次性广播，不回放历史事件；`RunLocal` Signal 必须显式携带有效 `RunId`，Host Signal 不绑定 Run，订阅可用稳定 `SourceKey` 过滤来源。State 是带 Revision 的当前事实；内置 `flow.wait.state` 默认 `CurrentOrFuture`，也可选择 `FutureChange`（下一次真实变化）或 `FutureMatch`（只等待未来匹配值）。Blackboard 属于 Flow 实例，支持批量提交和 Persistent/Reconstructable 标记。需要读取外部变化时，通过 `IFlowPollingSource` 显式注册到 PollingScheduler，并设置预算，不扫描整张 Graph。所有完成回调都通过 `FlowNodeHandle` 单次仲裁，旧回调会被判定为 stale。

取消使用 `FlowCancellationToken` 的父子树传播；带副作用的 Operation 默认应由适配器依据 `FlowRetryPolicy` 和 `EffectSemantics` 决定是否重试，ReplaySensitive 操作不会被隐式重放。

## 生产注意事项

宿主应为激活、完成、Timer、Signal 和 Polling 设置合理预算，并为单个 Run 设置 `MaxTotalActivationsPerRun`，防止穿过 Completion 边界的逻辑循环无界运行；同时监控 `FlowTraceRingBuffer`。快照只在无活动执行和无待处理队列时捕获，恢复时必须校验 FlowId、PlanHash 和 PlanVersion；V1 快照只保存流程身份、终态、Persistent Blackboard 和 Persistent State，不包含计时器、订阅、Operation 或中途继续执行所需的 continuation，因此不能把它当作任意运行点的存档。跨 PlanHash 恢复必须显式注册 `FlowSnapshotMigrationPipeline`。业务保存系统（例如 SaveKit）负责外部世界数据，FlowKit 只保存自身运行态。
