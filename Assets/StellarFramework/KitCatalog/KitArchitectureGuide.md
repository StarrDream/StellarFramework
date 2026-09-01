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

`sample`、`tooling`、`shared-runtime`、`single-file` 和 `generated-support` 不填写 tier/category。Catalog 的 schema v2 会在导出时校验 Runtime Kit Profile 的元数据，并拒绝 Foundation 直接依赖 Extension。

## 当前分类

| 层级 | Kit / Profile |
| --- | --- |
| Foundation | LogKit、EventKit、PoolKit、SingletonKit、FSMKit、ActionKit、BindableKit、ConfigKit.Core、HttpKit、ResKit.Core、SettingsKit.Core、TimeKit、SaveKit.Core、GridKit |
| Extension | AudioKit.Core、UIKit.Core、HotUpdate.Core |
| Adapter | ConfigKit.NewtonsoftJson、SettingsKit.UnityAdapters、SettingsKit.AudioKitAdapter、AudioKit.ResKitAdapter、ResKit.AssetBundle、ResKit.Addressables、UIKit.ResKitAdapter、HotUpdate.AddressablesAdapter、HotUpdate.HybridCLR、SaveKit.NewtonsoftJson |

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

## TimeKit 的定位

TimeKit 是 `foundation / simulation`：世界 Tick、日历视图、时间倍率、暂停和未来事件调度。Tick 是唯一真值，日历只是视图。

TimeKit 只依赖 LogKit，不依赖 ActionKit、EventKit、PoolKit、UniTask、Addressables、HybridCLR 或任何业务 Kit。`ActionKit.Delay` 用于流程等待；`TimeKit.ScheduleAfter` / `ScheduleEvery` 用于世界时间事件，两者不互相替代。

存档保存业务数据、世界 Tick 和业务目标 Tick；读档后由业务重新注册必要的 Timer。不要序列化 TimeScheduler 的 delegate、receiver、Handle 或 Heap。

## GridKit 的定位

GridKit 是 `foundation / world`：负坐标整数几何、半开 Bounds、稳定坐标↔index、连续 DenseGrid、不可变 Footprint 和整数 Occupancy。它不依赖 UnityEngine 或任何其他 Kit，因此可以单独导出；寻路、Chunk、Tilemap、3D、Placement 和存档由上层或后续 Kit 负责。

## 分发原则

- 所有 Kit 继续按需导出；Foundation 不等于默认全量安装。
- Exporter 与 ToolsHub 以 Kit Catalog 为唯一分发事实来源。
- Exporter 的 Foundation / Extension / Adapter 分组只改善选择界面，不改变多选、搜索、依赖去重、UPM 安装或导出闭包。
- 新 Kit 最低交付应包含 Runtime 源码、asmdef、使用/源码文档、测试、Catalog Profile、验收矩阵、README 登记和干净工程导入验证。

## 新 Kit 的 Validation Contract

新 Kit 必须在设计文档和验收记录中明确自己的 Validation Contract：

- Behavior Tests：公开 API、边界输入、失败原子性和 Regression。
- Performance：是否需要、目标规模、操作次数和可复现证据。
- PlayMode：只有真实 Unity Runtime/Lifecycle/Resource 需要时才要求，并写明原因。
- Sample：是否需要以及最小教学目标和导出边界。
- Policy：asmdef、依赖、Catalog、Sample closure 和禁止依赖。
- Integration：是否扩展维护者 Verification，使用 Fake-only 语义。
- Release：export、clean import、Player、IL2CPP、Addressables、HotUpdate。

不要求每个 Kit 都有 1M Benchmark、PlayMode、ToolsHub 或 Integration Scene；由真实能力决定。验证层级、目录和证据状态以 [ValidationArchitecture.md](../../StellarFrameworkVerification/ValidationArchitecture.md) 为准。

## 后续新增顺序

下一阶段优先验证 Foundation：SpatialKit、SimulationKit、PathKit。WorldKit、PlacementKit、InventoryKit、WorldGenKit 属于后续 Extension；ProductionKit、LogisticsKit 必须在真实项目中验证领域抽象后再升格。
