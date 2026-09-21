# FlowKit 业务编程规范 / Coding Contract

## 1. 目标

本规范定义业务项目如何围绕 FlowKit 编写生产级代码。

FlowKit Core 只负责工作流编排。项目业务仍然遵循 StellarFramework 的 MSV：

- Model：业务状态唯一真值。
- Service：业务规则、状态变更和外部系统协作。
- View：表现与输入，不持有玩法规则。
- FlowKit：阶段、等待、条件、并行、竞速、超时、失败和流程结束。

FlowKit 不取代 MSV，也不能成为第二套 Model、第二套 Service Locator 或第二套 Scene Manager。

## 2. 标准数据流

生产项目统一采用：

    Flow Graph
        |
        | Operation / External Capability Call
        v
    Operation Adapter
        |
        v
    Domain Service
        |
        v
    Model ----------------------> View
        |
        | Domain Event / observable fact
        v
    Flow Facts Bridge
        |
        | Signal / State
        v
    Flow Runtime

辅助边界：

    FlowAuthoringCatalog  -> ID / type / capability contract
    FlowBinding           -> stable scene-object boundary
    IFlowHostConfigurator -> composition root
    Blackboard            -> Flow-local context only
    OperationResult       -> one external call result

## 3. 六个基础交互原语

FlowKit 与外部世界保持少量正交原语，不因为新业务随意增加 Message / Event / Query 等平行概念。

| 原语 | 本质 | 回答的问题 |
| --- | --- | --- |
| Operation | 外部能力调用 | 请外部系统做或查询一件事 |
| OperationResult | 本次调用结果 | 刚才这次调用结果如何 |
| Signal | 瞬时事实 | 刚刚发生了什么 |
| State | 持续事实投影 | 当前是什么状态 |
| Blackboard | Flow 私有上下文 | 当前流程自己需要记住什么 |
| Binding | 稳定对象定位 | 这个稳定 ID 当前对应哪个运行对象 |

复杂流程能力来自 Graph 自身的 Condition、Delay、Wait、Parallel、Race、Join、Fail、Cancel、Operation 等节点，不需要为每种业务再创造一个通信原语。

## 4. Operation 的正式定义

Operation 定义为：FlowKit 发起的一次 External Capability Call。

它不只等于有副作用命令。Authoring Contract 可用 FlowExternalCallKind 标注：

- Command
- Query
- AsyncRequest
- Presentation
- Resource
- Network
- Other

底层仍统一使用 IFlowOperationAdapter，避免出现 IFlowCommandAdapter、IFlowQueryAdapter 等重复基础设施。

## 5. Operation Adapter Contract

### 5.1 Adapter 负责

- 把 FlowOperationRequest 翻译成一个明确的 Service 或 Platform Adapter 调用。
- 把外部调用的成功、失败、取消映射成 FlowOperationResult。
- 如果启动了异步外部任务，保存并在 Cancel 中取消自己启动的句柄。
- SDK 回调在工作线程时，先切回宿主调度线程再完成 Flow Operation。
- 使用 OwnerToken / IdempotencyKey 处理需要的所有权与幂等。

### 5.2 Adapter 禁止

- 承载 Gameplay 规则。
- 直接绕过 Service 修改 Model。
- 维护第二套长期业务状态。
- 使用巨大 switch(operationId) 作为生产级万能路由器。
- GameObject.Find、全场景扫描、运行时程序集扫描。
- 直接耦合多个无关 SDK 或系统。
- 吞异常、伪造 Success、无脑 fallback。

推荐一类明确能力一个 Adapter，例如：

- ShowOverviewOperationAdapter
- PlayVoiceOperationAdapter
- StartNavigationOperationAdapter
- LoadSceneOperationAdapter

教学 Sample 可以为了紧凑展示使用 Router，但必须明确标注它不是生产范式。

## 6. Configurator Contract

IFlowHostConfigurator 是 Composition Root，只负责：

- 注册 Operation Adapter。
- 注册 Capability。
- 注册扩展 Node。
- 设置 AssetResolver / Trace。

Configurator 不负责：

- 执行业务流程。
- 修改 Model。
- 打开 UI、播音频、切场景。
- 等待玩家事件。
- 保存业务状态。

## 7. Flow Facts Bridge Contract

Facts Bridge 是 Domain -> FlowKit 的 Adapter。

它订阅 Service、Model 或 Domain Event，只投影 Flow 真正需要的事实。例如：

    真实 Model:
      RoomModel.Players
      RoomModel.ReadyPlayers
      RoomModel.Connection

    Flow Projection:
      school.assembly.all_ready = true

禁止把完整 Model 镜像到 Flow State。

### Signal

Signal 表示一次发生的事件：

- 玩家刚进入区域。
- 视频刚播放完成。
- 某次按钮刚确认。
- 某连接刚断开。

Signal 不回放历史；需要当前仍成立时应使用 State。

### State

State 表示流程关心的当前事实：

- 所有人当前已就绪。
- 视频当前已完成。
- 船当前已到达。
- 当前连接可用。

State 是 Workflow Projection，不是业务数据库。

## 8. Blackboard Contract

Blackboard 只保存 Flow-local Context，例如：

- selected_route
- retry_count
- current_role
- branch_result
- temporary_target_id

禁止保存：

- PlayerModel / InventoryModel / WorldModel。
- GameObject / Component / ScriptableObject。
- SDK Handle / Task / Delegate。
- 大型业务集合。
- 世界持久状态。

原则：Model = 世界事实；Blackboard = 当前 Flow 的局部上下文。

## 9. Binding Contract

Graph 只保存稳定 Binding ID，不保存 Unity Instance ID 或对象引用。

禁止：

- GameObject.Find
- FindObjectOfType
- Resources.FindObjectsOfTypeAll
- 用静态字段缓存跨场景对象

Binding ID 应在 FlowAuthoringCatalog 登记。需要类型约束时填写 ExpectedBindingType；项目校验会确认该类型在 Editor 中可解析。FlowBinding 可显式指定 Target，Operation Adapter 解析 Binding 时直接得到该 Target；未指定 Target 时仍绑定 FlowBinding 自身以保持兼容。Runtime Core 不做程序集扫描。

## 10. View Contract

View 原则上不直接调用 FlowHost.Services、FlowRunner 或直接发布流程事实。

推荐：

    View
      -> Service / Presentation Controller
      -> Model / Domain Event
      -> Flow Facts Bridge
      -> FlowKit

简单纯表现交互也应通过明确 Adapter / Controller 边界，不允许 FlowKit API 扩散到全部 View。

## 11. Stable ID Contract

所有 ID 集中维护，禁止业务代码散落魔法字符串。

推荐格式：

    <domain>.<feature>.<meaning>

示例：

- school.overview.show
- school.navigation.start
- school.assembly.player_entered
- school.assembly.all_ready
- rainforest.observation.video_completed

默认规则：

- Operation / Signal / State / Blackboard：至少 3 个 dot segment。
- Binding：至少 2 个 segment。
- segment 使用 lower_snake_case。
- ID 一旦进入存档、远程配置或已发布 Graph，不随 CLR 类型名重命名。

## 12. Typed Authoring Contract

FlowAuthoringCatalog 是项目流程协议清单。

Operation 可描述：

- ID
- ExternalCallKind
- Arguments：key / FlowValueKind / required
- ResultKind
- RequiredCapability
- 是否允许额外参数

Signal / State / Blackboard 可描述：

- ID
- ValueKind

Binding 可描述：

- ID
- ExpectedBindingType

项目校验会在存在 Catalog 时检查：

- 已登记 Contract 的参数/值类型始终校验。
- Catalog 默认允许“局部模块登记”：未登记的外部 ID 不因为另一个模块存在 Catalog 就自动报错。
- 需要完整封闭协议时启用 `Strict Unknown References`。
- `Strict FlowIds` 可把严格模式限制到指定的精确 FlowId；列表为空表示严格校验所有 Flow。
- 严格作用域中的 Graph 如果引用未知 Operation / Signal / State / Blackboard / Binding，会直接报错。
- Operation required argument 是否缺失。
- argument / Signal payload / State expected / Blackboard value 类型是否匹配。
- 未声明 Operation 参数是否越界。
- Contract ID / argument key 是否符合稳定命名规则。
- 多个 Catalog 是否重复声明同类 ID。

因此可以按模块维护 `SchoolFlowCatalog`、`RainforestFlowCatalog` 等局部 Catalog，而不要求某一个 Catalog 声明全项目协议；当某个 Flow 已进入稳定/发布阶段，再对该 FlowId 开启严格模式作为 Release Gate。相同类别的稳定 ID 在项目中仍要求唯一所有权，避免两个模块同时声明同一协议但类型定义不同。

Runtime FlowRunner 不依赖 Catalog；这是 Authoring / Build-time Contract。

## 13. Error / Failure / Cancellation / Timeout

可预期业务失败使用 OperationResult.Failure，然后由 Graph 的 failed 端口进入 retry、compensation 或 flow.fail。

Adapter 启动外部异步操作后必须支持取消。Graph 必须明确决定 cancelled 路由；不能默认把 Cancelled 当 Success。

Timeout 优先在 Graph 表达，例如：

    Race
      |- Operation / Wait
      |- Delay -> Timeout

不要让每个业务 Adapter 再维护一套重复计时状态。

不变量破坏、缺失必须依赖、错误类型等编程问题应明确抛错或记录 Error，不应该转成假成功。

## 14. Retry / Idempotency

Retry 必须结合 FlowRetryPolicy 与 FlowEffectSemantics。

- Pure / Idempotent 可以按策略重试。
- ReplaySensitive 不允许 FlowKit 隐式重复副作用。
- 对网络、支付、存档提交、场景切换等副作用，Adapter 应使用 IdempotencyKey 或外部事务能力。

Exactly-once 不能只靠 FlowKit 内存状态保证。

## 15. MSV 组合示例

    Graph: school.guideline.start
        |
        v
    StartGuidelineOperationAdapter
        |
        v
    NavigationService.StartGuideline()
        |
        +--> NavigationModel.State
        |       |
        |       +--> NavigationView refresh
        |
        +--> Domain fact: guideline ready
                |
                v
          SchoolFlowFactsBridge
                |
                v
      school.navigation.ready = true
                |
                v
           FlowKit next

Operation Adapter 不是 Service；Facts Bridge 不是 Model；Flow Graph 不是 View Controller。

## 16. 推荐目录

    Flow/
    └─ SchoolFlow/
       ├─ Contracts/
       │  └─ SchoolFlowContracts.cs
       ├─ Bootstrap/
       │  └─ SchoolFlowConfigurator.cs
       ├─ Operations/
       │  ├─ ShowOverviewOperationAdapter.cs
       │  └─ StartGuidelineOperationAdapter.cs
       ├─ Facts/
       │  └─ SchoolFlowFactsBridge.cs
       ├─ Bindings/
       ├─ Graphs/
       │  ├─ School.flow.json
       │  └─ School.flow.editor.json
       └─ Tests/

Tools Hub 的 FlowKit 编辑器提供“业务骨架”按钮，可根据当前 FlowId 生成上述基础边界。

### FlowKit 与业务架构的边界

FlowKit Core / UnityIntegration 仍要求可独立导出，因此 FlowKit 不应为了演示 MSV 而反向依赖整个 `StellarFramework.Runtime`。真正使用 StellarFramework `Architecture<T> / Model / Service / View` 的跨 Kit MSV 闭环应放在项目业务层，由 Composition Root 完成组装。

因此“业务骨架”生成器只生成 FlowKit 边界和 Composition Root，不替业务项目生成或复制 Domain Model / Service。项目应把生成的 Operation Adapter 接到已有 Service，把 Facts Bridge 接到已有 Model/Domain Event。这样既保持 FlowKit 可独立导出，也保持 MSV 的状态与业务所有权清晰。

## 17. Performance / GC

- 高频连续数据不要直接灌入 FlowKit；XR Pose、Transform、Physics、网络快照由各自系统处理。
- 只把已经提炼的业务事实写入 Signal / State。
- 不在每帧创建新 Adapter、Catalog、Binding。
- 高频路径避免 LINQ、闭包和 Dictionary<string, object>。
- 大量实体状态留在 Model、SpatialKit、SimulationKit 等专用系统中。
- Blackboard / State 应保持小而稳定。
- Operation Adapter 的异步回调必须单次完成。

## 18. Testing Contract

生产 Flow 至少覆盖：

1. Graph Compile + Contract Validation。
2. Happy Path 完整闭环。
3. Operation Failure。
4. Operation Cancellation。
5. Timeout。
6. 缺失 / 错误 Binding。
7. Signal / State Facts Projection。
8. 修复过的流程 Bug Regression。

测试不能重新手写一份与正式 Graph 不同的 Graph 来假装验证正式流程；优先直接读取真实 .flow.json。

## 19. 明确反模式

禁止：

- Giant Flow Manager。
- Giant operationId switch adapter。
- Flow Graph 直接实现玩法算法。
- Flow State 镜像完整业务 Model。
- Blackboard 保存 Unity / SDK 对象。
- View 到处直接操作 FlowHost。
- Runtime reflection / assembly scan 自动发现业务 Adapter。
- GameObject.Find 作为 Binding。
- catch-all 后返回 Success。
- failed / cancelled 端口全部忽略。
- Sample 当 Release Validation。

## 20. 关于未来 SubFlow

六个交互原语目前足够作为 FlowKit 与外部世界的协议。

未来大型流程真正可能需要的是 Flow Composition：

- SubFlow / ChildFlow
- Reusable Flow
- typed Input / Output
- parameter scope
- cancellation propagation
- error propagation
- snapshot / PlanHash semantics

这些属于流程结构能力，不应通过新增更多 Event / Message 原语解决。

SubFlow 在单独设计完成前不改变 FlowKit V1 Core Semantics。

## 21. 上线检查

- [ ] 业务状态仍在 Model。
- [ ] 业务规则仍在 Service。
- [ ] View 没有承载流程业务。
- [ ] Operation Adapter 只做翻译 / 生命周期适配。
- [ ] Facts Bridge 只做 Workflow Projection。
- [ ] 所有稳定 ID 集中维护并登记 Catalog。
- [ ] Graph Contract Validation 无 Error。
- [ ] State 没有复制整个 Model。
- [ ] Blackboard 只含 Flow-local 值。
- [ ] Binding 不依赖 Find / 扫描。
- [ ] Failure / Cancel / Timeout 有明确路线。
- [ ] 真正执行了对应 Behavior / Regression Test。

