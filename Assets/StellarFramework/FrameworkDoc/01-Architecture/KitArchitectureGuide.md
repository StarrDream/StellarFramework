# Kit 架构分层与依赖规则

本文件定义 StellarFramework 原始框架工程中 Kit 的架构职责和分发分类。它不要求业务项目导入全部 Kit，也不改变 `Assets/StellarFramework/Runtime/Kits` 的物理目录。

## 四类职责

```text
Runtime/Core
  └─ Architecture：项目组织、Model / Service / View 生命周期

Foundation Kit：稳定、低层、通用能力
Extension Kit：基于 Foundation 组合出的高层能力
Adapter Profile：可选的 Kit 间、Unity 或第三方技术栈连接层
```

`Runtime/Core` 只承载 Architecture。TimeKit、ResKit 等即使属于 Foundation，也继续放在 `Runtime/Kits`。

## Catalog 元数据

`KitDistributionCatalog.json` 的 `kind` 描述分发形式，不能表示架构层级。Runtime Kit Profile 使用独立字段：

```json
"tier": "foundation",
"category": "simulation"
```

支持的 tier：

- `foundation`
- `extension`
- `adapter`

支持的 category：`diagnostics`、`infrastructure`、`flow`、`data`、`network`、`resource`、`simulation`、`presentation`、`world`、`gameplay`、`runtime-delivery`。

`sample`、`tooling`、`shared-runtime`、`single-file` 和 `generated-support` 不填写 tier/category。Catalog 的 schema v4 会在导出时校验 Runtime Kit Profile 的架构元数据、成熟度、Foundation 依赖方向，并校验 Recommended Profile 引用的原子 Profile 是否真实存在且可用。

Catalog 中的 `profiles` 与 `recommendedProfiles` 是两层不同概念：

- `profiles`：原子分发单元，描述 Kit、Adapter、Tooling、Generated Support 等真实源码与依赖闭包。
- `recommendedProfiles`：面向常见项目目标的推荐导出配置，只组合已有原子 Profile，不生成新的 Runtime assembly，也不能隐藏或改写底层依赖。

## Profile 成熟度

Catalog schema v4 将“能不能安装”和“推荐在什么阶段使用”拆成两个维度：

```text
availability = available / unavailable
maturity     = stable / rc / experimental
```

`availability=available` 只说明 Package Publisher 可以解析并导出该 Profile，不代表它已经完成所有生产发布 Gate。

- `stable`：公开契约已稳定，自动回归与分发边界证据充分；涉及 Unity Runtime 的能力应具备相应 PlayMode、clean import 或真实项目证据。
- `rc`：功能与 API 已基本可用，但仍缺部分目标平台、第三方插件、远端服务或发布环境证据。可以用于项目验证，生产采用前必须补齐对应 Gate。
- `experimental`：能力方向可用，但关键发布链、平台集成或契约仍允许调整。默认不应作为无人复核的生产基础设施。

Recommended Profile 不单独手写成熟度；Publisher 根据完整依赖闭包取“最低成熟度”：

```text
Stable + Stable       -> Stable
Stable + RC           -> RC
Stable + Experimental -> Experimental
```

因此组合包不能通过自己的描述把底层尚未完成发布验证的 Profile“包装成 Stable”。成熟度允许根据全 Catalog Audit 的新证据升降级，但必须有可追溯原因。

当前推荐交付配置：

- `localization.complete`：完整本地化生产配置，包含 Core、UnityUGUI + TextMeshPro 运行时绑定、UGUI/TMP Scanner、稳定 Binding Registry、Translation Workspace、JSON/CSV 交换、Validator 与 ToolsHub；不强制引入 SettingsKit、UIKit 或资源系统。
- `reskit.complete`：完整 ResKit 开发配置，以 `reskit.tools` 为入口，包含 ResKit.Core、PoolKit、LogKit、Generated.AssetMap、ToolsHub 与资源审计/生成工具；具体 AssetBundle/Addressables/YooAsset 后端继续按项目选择。
- `uiadaptation.complete`：独立 UI 适配生产配置，包含 UIAdaptationKit Runtime + ToolsHub Preview/Validator；只依赖 UGUI，不依赖 UIKit。
- `uikit.complete`：完整 UIKit 生产配置，包含 UIKit Runtime/Tools、UIKit.ResKitAdapter、ResKit Complete，并组合独立 UIAdaptationKit；第三方资源后端仍按项目需要扩展。
- `hotupdate.full`：完整热更新扩展，以 `reskit.yooasset + reskit.tools + hybridclrkit.tools` 为入口，自动得到 ResKit、YooAsset、HybridCLR、PoolKit、LogKit、ToolsHub、AssetsMap 与对应编辑器工具。

Export 的用户导航与架构 tier 是两个独立维度：

- `01 基础功能`：可按需单独选择的原子 Runtime Kit，只补齐真实硬依赖。
- `02 完整功能`：经过验证的组合交付，例如 Localization / ResKit / UIKit Complete。
- `03 扩展功能`：Adapter、Editor Tooling 等原子扩展，以及 Hot Update Full 这类组合扩展。

`foundation / extension / adapter` 继续作为内部架构依赖规则，不再直接承担用户导出分类。

## 当前分类

| 层级 | Kit / Profile |
| --- | --- |
| Foundation | LogKit、EventKit、PoolKit、SingletonKit、FSMKit、ActionKit、BindableKit、ConfigKit.Core、HttpKit、ResKit.Core、SettingsKit.Core、TimeKit、SaveKit.Core、GridKit、WorldKit.Core、SpatialKit、SimulationKit、PathKit、FlowKit.Core、PlacementKit.Core |
| Extension | AudioKit.Core、RuntimeTools.Core、UIKit.Core、UIAdaptationKit.Core、HybridCLRKit、WorldGenKit.Core、WorldGenKit.Builtins、WorldGenKit.Authoring、WorldGenKit.Resources、WorldGenKit.Feature、WorldKit.Streaming |
| Adapter | ConfigKit.NewtonsoftJson、SettingsKit.UnityAdapters、SettingsKit.AudioKitAdapter、AudioKit.ResKitAdapter、ResKit.AssetBundle、ResKit.Addressables、ResKit.YooAsset、UIKit.ResKitAdapter、LocalizationKit.SettingsAdapter、LocalizationKit.UnityUGUIAdapter、LocalizationKit.TMPAdapter、SaveKit.NewtonsoftJson、PathKit.GridKitAdapter、FlowKit.UnityIntegration、Feature.ResourcesAdapter、Feature.PlacementAdapter、Feature.AuthoringAdapter、Feature.WorldKitAdapter、Feature.SaveKitAdapter、WorldGenKit.DebugTextureAdapter、WorldGenKit.MeshAdapter、WorldGenKit.TilemapAdapter、WorldGenKit.UnityTerrainAdapter、WorldGenKit.StreamingAdapter、WorldKit.Streaming.SaveKitAdapter、WorldKit.Streaming.UnityAdapter |

这只是展示和依赖约束元数据，不会让 Foundation 自动安装。选择某个 Kit 时，导出器仍只按 `requiredProfileIds` 计算实际依赖闭包。

## 依赖规则

```text
Architecture
    ↑
Foundation
    ↑
Extension

Adapter 横向连接可选能力
```

- Foundation 不能依赖 Extension。
- Extension 可以依赖 Foundation；Extension 间是否依赖必须由真实稳定的领域边界决定。
- Adapter 用于可选集成，避免把 Addressables、HybridCLR、ResKit 等选择变成 Core Kit 的硬依赖。
- 不因“方便”把业务系统写入 Foundation。Crop、NPC、Quest、Farm 等先留在业务项目，经过真实项目验证后再决定是否升格为 Extension。

## RuntimeTools 的定位

RuntimeTools.Core 是 `extension / infrastructure`，用于承载**小而完整、跨项目高复用、低依赖**的运行时工具。它不是第二套 Kit 系统，也不是通用杂物箱。

工具进入 RuntimeTools 至少满足一项：高频重复、容易手写出错、可以统一性能/GC 行为，或能形成稳定而独立的 MonoBehaviour / 静态 API。仅仅“能少写两行代码”不足以成为框架工具。

RuntimeTools.Core 当前保持零 StellarFramework Kit 依赖；Pool、时间调度、UI 页面、资源生命周期、流程编排等完整能力域继续由 PoolKit、TimeKit、UIKit、ResKit、FlowKit 负责，不在 RuntimeTools 中复制实现。UGUI、URP 等技术栈专属能力后续若引入，必须通过独立 Adapter/Profile 隔离。

`RuntimeTools.Tools` 是独立 Editor-only Profile，只负责 ToolsHub 快速挂载、Transform Snapshot、Bounds Diagnostics 和配置风险检查；Runtime assembly 禁止反向引用 ToolsHub/UnityEditor。

## TimeKit 的定位

TimeKit 是 `foundation / simulation`：世界 Tick、日历视图、时间倍率、暂停和未来事件调度。Tick 是唯一真值，日历只是视图。

TimeKit 只依赖 LogKit，不依赖 ActionKit、EventKit、PoolKit、UniTask、Addressables、HybridCLR 或任何业务 Kit。`ActionKit.Delay` 用于流程等待；`TimeKit.ScheduleAfter` / `ScheduleEvery` 用于世界时间事件，两者不互相替代。

存档保存业务数据、世界 Tick 和业务目标 Tick；读档后由业务重新注册必要的 Timer。不要序列化 TimeScheduler 的 delegate、receiver、Handle 或 Heap。

## GridKit 的定位

GridKit 是 `foundation / world`：负坐标整数几何、半开 Bounds、稳定坐标↔index、连续 DenseGrid、不可变 Footprint、整数 Occupancy，以及 additive 的 Square/Hex Topology。Topology 扩展提供 `IGridTopology<TCoord>`、Orthogonal4/8、Axial Hex、Ring/Range 与 Hex Edge/Vertex identity，但不把 Hex 强塞进 Square DenseGrid/Occupancy。GridKit 不依赖 UnityEngine 或任何其他 Kit，因此可以单独导出；寻路、Chunk、Tilemap、3D、Placement 和存档由上层或后续 Kit/Adapter 负责。

## WorldKit 的定位

WorldKit.Core 是 `foundation / world`：负责有限/无限平面 World、Region/Chunk identity、Chunk 生命周期、强类型 World/Region/Chunk Data Layer、Dirty Chunk tracking 与有序 Runtime Delta。它不生成地形、不寻路、不写存档文件，也不引用 GridKit、SpatialKit、PathKit、SaveKit、SimulationKit、WorldGenKit、PlacementKit 或 UnityEngine。Chunk payload 由项目通过 typed Layer Store 自己定义，因此一个 Chunk 可以承载 Grid page、road graph、server DTO 或其他数据，而 WorldKit.Core 不复制这些领域系统。

`WorldKit.Streaming` 是 `extension / world`，只依赖冻结的 WorldKit.Core。它新增 generation/macro Region 布局、Demand Policy、Metadata/Data/Simulation/Presentation 四级 residency 与 deterministic reconciliation，但不修改既有 `WorldChunkState` 契约。WorldGen 生成、SaveKit Delta 和 Unity floating origin 分别通过三个独立 Adapter 接入，因此纯服务器、2D 或 3D 项目都可以只安装需要的边界。

## WorldGenKit 的定位

WorldGenKit.Core 是 `extension / world`：负责强类型 Channel/Storage、Stage Descriptor、DAG Pipeline Compiler、稳定 Seed、Rule 原语与 GenerationReport。它被归类为 Extension 是因为它解决的是更高层“世界数据生成/Authoring Pipeline”问题，而不是因为它必须依赖 Foundation；当前 Core 仍保持 `references=[]`、`noEngineReferences=true`，可以完全独立导出，也不依赖 WorldKit.Core。WorldKit 与 WorldGenKit 通过未来 `WorldGenKit.WorldKitAdapter` 组合，而不是形成强制依赖链。

`WorldGenKit.Builtins` 是独立 `extension / world` Profile，只依赖 `WorldGenKit.Core`，提供 Height/Moisture/WaterDepth/Slope/Biome/Surface/Buildable 的第一个可用数据生成闭环。Biome/Surface 使用 Stable ID + Catalog，sample 热路径输出整数 catalog index；Moisture 可完全不注册。Builtins 不依赖 UnityEngine、WorldKit 或其他 Kit，Unity Terrain/Mesh/Tilemap 输出和 WorldKit runtime 接入必须由后续 Adapter 完成。

`WorldGenKit.Authoring` 同样属于 `extension / world`，只依赖 WorldGenKit.Core + Builtins。它负责 typed buffer import、Stable-ID 语义导入、`Base + Sparse AuthoringOverride -> Final`、Height 编辑、Biome/Surface/Mask paint、Dirty Region propagation 与 Builtins regional recompute；不依赖 UnityEngine/UnityEditor，不负责 Texture/Terrain/Tilemap 文件/对象读取，也不负责 SaveKit persistence。Unity-facing Importer、Brush/Undo ToolsHub 与 Save adapter 必须继续放在边界层。

`WorldGenKit.Resources` 属于 `extension / world`，**只依赖 WorldGenKit.Core**。它拥有独立 `WorldResourcePlanarDomain`，不因为 planar sample 结构而强制依赖 Builtins；负责 Stable-ID Resource/Category/Occupancy、Density/Coverage/Cluster/Richness candidate generation、Budget、MinSpacing、Occupancy Resolver、Player Generation Settings 与 Exposure Policy。Biome/Height/Temperature 等具体语义必须先由项目规则转换成通用 Eligibility/Suitability 输入。Prefab、Save、WorldKit lifecycle、Feature/POI 与 Placement 均留在 Adapter/后续 Kit。

`WorldGenKit.Feature` 属于 `extension / world`，只依赖 `WorldGenKit.Core`。它负责 Stable-ID Feature、Landmark/Area/Compound、确定性 Candidate/Resolver、World/Region quota、连续 Reservation、Terrain Adaptation intent 与 Compound semantic layout；不实例化 Prefab，不保存文件，也不直接依赖 Resources、Placement、WorldKit 或 Authoring。

五个 Feature Adapter 都是横向可选组合：ResourcesAdapter 负责 Feature Reservation → Resource Occupancy；PlacementAdapter 负责 Feature Candidate → PlacementRequest；AuthoringAdapter 负责 Flatten/Carve/Fill/Stamp 到 Authoring；WorldKitAdapter 负责世界/Region Feature usage tracking；SaveKitAdapter 负责 Stable-ID usage snapshot 持久化。选择其中一个不会强制安装其他 Adapter。

四个 Unity Presentation Adapter 同样保持横向可选：DebugTextureAdapter、MeshAdapter、TilemapAdapter、UnityTerrainAdapter 都只消费 `WorldGenerationDataSet + WorldPlanarSampleLayout + typed ChannelHandle`，只依赖 Core + Builtins。Texture/Tilemap/Mesh/TerrainData 是输出目标而不是世界真值；所有场景对象、材质、Grid/Terrain 尺寸、LOD/Collider/streaming 生命周期继续由应用层负责。WorldGenKit.Core/Builtins 不引用这些 Unity 类型，因此同一逻辑数据可以切换 2D/3D 输出而无需修改生成核心。

`PlacementKit.Core` 是 `foundation / world`，零依赖、无 UnityEngine。它只定义 PlacementType/Rule/Failure Stable ID、Rectangle/Circle footprint、PlacementRequest、规则评估、显式 failure IDs 与通用 Slope/Water/Zone/Conflict/Connection facts。Terrain/Grid/World 采样、资源扣除、实例化、网络与保存均由 Adapter/业务层负责。

## WorldFramework.ToolsHub

WorldFramework.ToolsHub 是 Editor-only tooling，不是 Runtime Kit。

- 常见 WorldGen 配置通过 WorldGenerationAuthoringProfile + 显式编译器映射到现有 Runtime contract。
- ToolsHub 可以依赖 WorldKit / WorldGen / Placement 的公开 API；这些 Runtime assembly 禁止反向依赖 ToolsHub / UnityEditor。
- Runtime World 不要求 ToolsHub scene singleton。项目通过 Editor-only IWorldFrameworkDiagnosticsSource / IWorldFrameworkDetailDiagnosticsSource 显式提供诊断快照。
- Candidate Heatmap、accepted/rejected、Placement failure、Memory Report 与 Validator 均调用或检查现有 Runtime 规则，不复制一套 Editor 专用业务语义。
- ScriptableObject 是 Unity Authoring/persistence 选项，不是 WorldGenKit Core 的唯一 Profile 表示。
- 通用配置走 ToolsHub；高级扩展继续通过 IWorldGenerationStage、PipelineBuilder、项目 Rule/Adapter 和显式 diagnostics bridge 代码注册。

因此架构硬边界继续成立：Runtime 不依赖 ToolsHub，Foundation 不能依赖 Extension，所有 Kit 继续按需导出。

## GridKit.UnityProjectionAdapter

GridKit.UnityProjectionAdapter 是独立的 adapter / world profile，用来把现有 Unity Terrain / MeshCollider / Physics scene geometry 投影为 GridKit 的基础逻辑格数据。

- 物理源码目录独立位于 Runtime/Kits/GridKitUnityProjection，不放进 GridKit.Core 的 source closure。
- 只依赖 GridKit.Core + UnityEngine；不依赖 WorldKit、WorldGenKit、PlacementKit、PathKit、SaveKit。
- TerrainGridProjectionSource 直接采样 TerrainData；PhysicsGridProjectionSource 使用显式 LayerMask Raycast，可覆盖 MeshCollider / scene geometry。
- GridProjectionBaker 写 caller-owned DenseGrid<GridBakeCell>，使用 caller-owned scratch，并在采样源失败时保持 destination 不变。
- AutoBakeBase 与 DenseGrid<GridTraversalOverrideCell> ManualOverride 分离；Rebake 只替换自动层，不清除设计师 ForceWalkable / ForceBlocked / Cost Override。
- PathKit 接入继续由项目层通过 IGridPathTraversalPolicy 完成，Adapter 本身不反向依赖 PathKit。

该 Adapter 实现已冻结的 Terrain/Mesh -> Grid Bake -> Manual Override 边界，同时保持 GridKit Foundation 的 engine-free 语义不变。

## SpatialKit 的定位

SpatialKit 是 `foundation / world`：连续二维点的动态均匀空间哈希。它只保存外部提供的 `SpatialId`、`SpatialPoint` 和内部桶链表，提供点的插入、移除、移动、半开矩形查询、闭圆查询和有限半径最近邻。它与 GridKit 的整数格子/Occupancy 分工明确，不负责 3D、体积实体、Transform 跟踪、寻路、模拟、放置规则、对象引用或生命周期驱动，因此可以单独导出且不需要 UnityEngine、UPM、Addressables、HybridCLR 或其他 Kit。

## PathKit 的定位

PathKit 是 `foundation / world`：只依赖通用 Graph 的节点、outgoing neighbor、正 `long` 成本和可选 admissible heuristic，提供同步 A* 与 Dijkstra。Core 不知道 GridCoord、NPC、Transform、移动、路径缓存或世界事件，因此可以作为完全独立的纯 C# 包导出。A* 对不一致 heuristic 允许 Closed reopen，Dijkstra 永不调用 heuristic；所有成本运算都防止 long 溢出。

`PathKit.GridKitAdapter` 是独立的 `adapter / world` profile，使用 `GridPathGraph` 和应用提供的 `IGridPathTraversalPolicy` 接入负坐标 GridRect、FourWay/EightWay、NoCornerCut/AllowCornerCut 与动态 walkability。适配器不把 DenseGrid 或 Occupancy 写死为唯一数据源，Core 导出不会带入该目录。

PathKit V1 Core Semantics 已冻结：Graph-first Core、A*/Dijkstra、admissible heuristic、Closed Reopen、正 `long` cost、Start→Goal 输出、BufferTooSmall 零 partial write、MaxExpandedNodes、deterministic tie、`PathSearchStatus.None` 默认结果语义，以及 GridKit 通过独立 Adapter 接入。后续只接受不改变这些语义的内部优化或文档澄清。

### PathKit.GridKitAdapter Profile

`PathKit.GridKitAdapter` 将 GridKit 的离散正交 Grid 映射为 PathKit Graph，提供 FourWay / EightWay、Corner Policy 与基于真实 minimum traversal cost 的 admissible heuristic；不改变 `PathKit.Core` 的独立性。

## FlowKit 的定位

FlowKit.Core 是 `foundation / flow`：Graph JSON 经显式迁移、验证和编译后形成不可变 FlowPlan，由预算化 Runner、Timer、Signal、State、Blackboard、Operation 和执行组调度。Core 不引用 Unity、UniTask、Addressables、HybridCLR、UI、资源或业务领域对象，因此可以单独导出。

`FlowKit.UnityIntegration` 是 `adapter / flow`，只提供 FlowHost、稳定 Binding 和 JSON 文本入口。FlowKit 的可视化编辑、项目校验和运行时诊断统一收容在 ToolsHub 的 FlowKit 模块中，属于独立 tooling profile，不进入玩家 Runtime。Parallel/Race/Join、条件 AST、PlanHash 快照和 Operation/Capability 边界均通过显式 API 接入，不使用运行时反射或全图扫描。

## LocalizationKit 的定位

LocalizationKit.Core 是 `foundation / data`：只负责稳定 `LocaleId`、`LocalizationKey`、不可变 Table/Catalog、显式 Fallback、Lookup 结果、语言切换事件和命名参数格式化。Core 为零依赖、engine-free，不引用 SettingsKit、UIKit、SaveKit、UnityEngine/UnityEditor，也不使用 Runtime reflection / assembly scan。

`LocalizationKit.SettingsAdapter` 是 `adapter / data`：只实现 SettingsKit 既有 `ILanguageSettingsAdapter`，把语言选项/当前语言/ApplyLanguage 桥接到 `LocalizationService`。SettingsKit 不知道 LocalizationTable，LocalizationKit.Core 也不反向引用 SettingsKit。

`LocalizationKit.UnityUGUIAdapter` 是 `adapter / presentation`：提供 `LocalizationCatalogAsset` / `LocalizationTableAsset` ScriptableObject authoring、`LocalizationContext`、`LocalizedTextView` 和 `LocalizedButtonLabel`。UGUI View 只负责表现，切语言时只更新已绑定 View；业务 Model 不被修改。

`LocalizationKit.TMPAdapter` 是独立 `adapter / presentation`：基于 Core 的 `ILocalizationContext` 契约为 TextMeshPro 提供 `LocalizedTMPTextView`，不要求 Core 或 TMP 反向依赖 UGUI。对应 `LocalizationKit.TMP.Editor` 负责 TMP Prefab Scanner，`LocalizationKit.TMP.Tools` 只提供 ToolsHub 入口；三者均保持为可选 Profile。完整 `localization.complete` 会同时组合 UGUI 与 TMP，以覆盖正常 Unity UI 生产链；只需要其中一种时仍可单独导出。

`LocalizationKit.Editor` 是 Editor-only 生产与验证边界：除 Catalog coverage / duplicate / missing / fallback validator 外，还拥有稳定 Binding Registry、UGUI Scanner、Translation Workspace 与 JSON/CSV 外部翻译交换；`LocalizationKit.Tools` 仅负责将这些能力接入 ToolsHub。扫描采用 Preview → Apply；机器身份使用持久化 BindingId，Hierarchy/Sibling 顺序只作为当前位置元数据，不参与长期 Key 身份。源文本变化保留 Key/BindingId 并通过 sourceHash 标记译文需复核。

## UIAdaptationKit 的定位

`UIAdaptationKit.Core` 是独立 `extension / presentation`：只依赖 Unity UGUI，不依赖 UIKit、ResKit、SingletonKit 或其他 StellarFramework Runtime Kit。它提供 CanvasScaler 配置、Safe Area、Screen Cutouts、精确危险区避让、Automatic Fallback、屏幕形状 Breakpoint、横竖屏与 Layout Variant。

Breakpoint 的 aspect 统一使用“长边 / 短边”得到 >= 1 的 Shape Aspect；Orientation 单独判断，因此同一 20:9 设备横竖屏使用同一个形状区间，同时仍可按 Portrait/Landscape 做显式约束。

UI 作者只声明 `None / SafeArea / PreciseCutout`。`PreciseCutout + Automatic` 在系统无法提供可靠 Cutout 时降到 SafeArea，再无法获得有效 SafeArea 时降到 Reference Edge Padding。业务不维护 Android/iOS/HarmonyOS/品牌/型号分支。

`UIAdaptationKit.Tools` 是 Editor-only ToolsHub 能力：一键建立独立 `Canvas + FullScreenRoot + SafeAreaRoot + Controller + 推荐 Profile`，并提供设备比例预览、SafeArea/Cutout preview、Breakpoint 校验、Anchor 风险提示、Mode/Fallback/Effective 诊断与 Layout Variant Capture。

UIKit 只是可选消费者：标准 UIRoot 的 Static/Dynamic Canvas 提供 `FullScreenRoot / SafeAreaRoot`，Panel 继续通过 `UIPanelBase.PanelLayoutRegion` 选择区域。`UIKit.Core` 自身不依赖 UIAdaptationKit；完整 `uikit.complete` 默认组合 `uiadaptation.tools`。旧 `uikit.adaptation*` Catalog ID 仅作为兼容导出别名保留。

## SimulationKit 的定位

SimulationKit 是 `foundation / simulation`：只管理业务提供的 `SimulationId`、正间隔、首次延迟和下一次到期 tick。`SimulationScheduler` 使用索引最小堆和 ID 索引，按 `NextDueTick` 再按 ID 稳定排序，把到期 ID 写入调用方提供的 `Span`，由预算和 `HasBacklog` 控制批量派发。

SimulationKit 不依赖 UnityEngine、TimeKit、GridKit、SpatialKit、ResKit、UPM 或热更插件，不保存对象引用、不执行回调、不追赶式重复派发，也不负责线程、Jobs/Burst、存档或生命周期。业务保存自己的 ID/状态和最后模拟 tick，读档后重新注册；需要休眠时直接注销。它可以单独导出，Catalog 不再维护额外 `samples.*` Profile。

## 分发原则

- 所有 Kit 继续按需导出；Foundation 不等于默认全量安装。
- Exporter 与 ToolsHub 以 Kit Catalog 为唯一分发事实来源。
- Runtime Profile 禁止依赖 `toolshub.core`，也禁止直接把 `Assets/StellarFramework/Editor/StellarToolsHub/...` 作为自身源码；可视化、审计和便捷编辑器能力必须进入独立 `tooling` Profile。
- Editor 代码是否独立拆分取决于职责，而不是目录名：构建正确性所必需的生成器可以随 Core 交付，例如 SingletonKit 的 `SingletonGenerator`；ToolsHub 面板、审计器、CodeGen UX 等可选开发体验则独立为 tooling Profile。
- Recommended Profile 只是推荐组合，不是新的 Kit。它必须复用原子 Profile 的真实依赖闭包，不能为了“一键导入”重新制造大一统模块。
- Exporter 面向用户只显示“基础功能 / 完整功能 / 扩展功能”交付模型；Foundation / Extension / Adapter 继续只承担内部依赖约束，不改变多选、搜索、依赖去重、UPM 安装或导出闭包。
- 新 Kit 最低交付应包含 Runtime 源码、asmdef、使用/源码文档、测试、Catalog Profile、验收矩阵、README 登记和干净工程导入验证。Demo / Sample 不属于强制项；只有无法通过 Guide、自动测试或现有 ArchitectureDemo 清晰表达的交互/跨 Kit 行为才新增。

## 新 Kit 的 Validation Contract

新 Kit 必须在设计文档和验收记录中明确自己的 Validation Contract：

- Behavior Tests：公开 API、边界输入、失败原子性和 Regression。
- Performance：是否需要、目标规模、操作次数和可复现证据。
- PlayMode：只有真实 Unity Runtime/Lifecycle/Resource 需要时才要求，并写明原因。
- Demo：只有确实无法通过文档、自动测试或现有 ArchitectureDemo 表达时才新增，并明确最小教学目标。
- Policy：asmdef、依赖、Catalog、导出闭包和禁止依赖。
- Integration：是否扩展维护者 Verification，使用 Fake-only 语义。
- Release：export、clean import、Player、IL2CPP、Addressables、HotUpdate。

不要求每个 Kit 都有 1M Benchmark、PlayMode、ToolsHub 或 Integration Scene；由真实能力决定。验证层级、目录和证据状态以 [ValidationArchitecture.md](../../StellarFrameworkVerification/ValidationArchitecture.md) 为准。

## 人类可读性也是交付契约

StellarFramework 的源码与文档首先必须让人类开发者独立读懂，而不是只保证 AI 能解析或自动测试能通过。公开 API、复杂内部实现、Sample 与 Guide 的具体要求统一见 [CodeReadabilityAndDocumentationStandard.md](CodeReadabilityAndDocumentationStandard.md)。

新 Kit 的最低交付除了 Runtime、asmdef、测试、Catalog 与 Guide 外，还必须通过 Human Readability Review：公开契约有语义化 XML 文档，复杂算法/生命周期/性能取舍解释 why，Guide 能作为真实教学入口；若确有必要提供 Sample，则 Sample 必须可运行并承担明确教学目标。不得通过批量生成无意义注释来满足形式指标。

## SpatialKit V1 冻结状态

SpatialKit V1 Core Semantics 已冻结：公开 ID、连续点、半开矩形、闭圆、有限半径最近邻、写入原子性和调用方缓冲区契约保持稳定；后续只接受不改变契约的内部优化或文档澄清。真实项目出现稳定需求后，再评估 SpatialKit.UnityAdapter（Transform/MonoBehaviour 同步）、3D/体积专用 Kit、Jobs/Burst Adapter 或过滤索引。任何候选都不能把对象引用、线程调度、业务分类或 Unity 生命周期倒灌进 Core。

## 后续新增顺序

TimeKit、SaveKit、GridKit 的既有 Square/Core 语义，以及 SpatialKit、SimulationKit、PathKit、FlowKit 的 V1 Core Semantics 保持冻结。`Tiny Foundation Integration` 作为早期 Foundation 基线审计/回归验证术语仅保留在历史资料中。World Framework 当前性能与 Release 证据见 `Assets/StellarFramework/FrameworkDoc/06-WorldFramework/WorldFramework-Performance-Release-Matrix.md`。ProductionKit、LogisticsKit 仍必须在真实项目中验证领域抽象后再升格。

LocalizationKit family 同样遵循 Foundation 不能依赖 Extension、Adapter 单向组合、所有 Kit 按需独立导出的既有规则。
