# WorldKit 源码文档

> 状态：WorldKit.Core Stable

## Assembly Boundary

程序集：

```text
StellarFramework.WorldKit.Core
```

约束：

- `references = []`
- `noEngineReferences = true`
- `allowUnsafeCode = false`
- Namespace：`StellarFramework.WorldKit`

禁止 Core 直接依赖：

- GridKit
- SpatialKit
- PathKit
- SaveKit
- SimulationKit
- WorldGenKit
- PlacementKit
- UnityEngine / UnityEditor

## 目录

```text
WorldKit/
├─ Identity/
├─ Geometry/
├─ Lifecycle/
├─ Data/
├─ Dirty/
├─ Delta/
└─ Internal/
```

## Identity

`WorldId`、`WorldDataLayerId`、`WorldDeltaTypeId` 都使用同一 canonical stable-ID 规则：

```text
lower-case
dot-separated segments
[a-z0-9_]
max length = 128
```

ID 的 `GetHashCode()` 只用于当前进程 Dictionary，不作为存档/网络协议 identity。

## Logical Coordinates

### WorldChunkCoord / WorldRegionCoord

- `(long X, long Y)`；
- 负坐标合法；
- `(0,0)` 合法；
- 不使用 Unity `Vector2Int`。

### WorldPoint2D

- `double X/Y`；
- constructor 拒绝 NaN/Infinity；
- 逻辑位置与 Unity presentation position 解耦。

### WorldChunkBounds

- `Min inclusive / MaxExclusive`；
- Width/Height 使用 unsigned difference，可表达跨越 long 正负范围的宽度；
- Area 通过 `TryGetArea` 显式报告 `ulong` overflow。

## WorldExtent

`default(WorldExtent)` = invalid `None`，不会默认为一个假合法有限世界。

正式状态：

- `Finite`
- `Infinite`

`Infinite` 不暴露 fake finite bounds。

## Chunk Lifecycle

`WorldChunkLifecycle` 是单 Chunk state machine；`WorldChunkRegistry` 是按需 world-level Chunk state registry。

允许边：

```text
Unloaded <-> Metadata <-> DataReady <-> Active
```

禁止跳级。失败通过 typed result/error 返回。

Registry 删除 Chunk 前要求 state=`Unloaded`，防止“数据还处于 loaded/active 就直接从 registry 消失”。

Registry 不生成 Chunk 数据、不持有 GameObject、不负责线程 IO。

## Data Layer Runtime Type Safety

`WorldDataLayerRegistryBuilder.Register<T>` 在注册阶段产生：

```text
Stable Layer ID
Scope
Runtime Type Token
Index
RegistryGeneration
```

Runtime type token 通过 generic static token 生成，不进行 assembly scan / reflection discovery。

`WorldDataLayerHandle<T>` 只有：

- numeric Index；
- RegistryGeneration；
- compile-time generic T。

default handle invalid。

### Cross-registry 防护

两个 Registry 即使都把 `terrain.page` 放在 Index=0，也拥有不同 `RegistryGeneration`。

因此：

```text
Plan/Registry A: index 0 @ generation 12
Plan/Registry B: index 0 @ generation 13
```

不能错误互用。

## WorldDataLayerStore<T>

Store 是“一层一份 typed storage”，不是统一 object bag。

Scope 行为：

| Scope | 物理模型 |
| --- | --- |
| World | 单个 `T` + presence flag |
| Region | `Dictionary<WorldRegionCoord,T>` |
| Chunk | `Dictionary<WorldChunkCoord,T>` |

Dictionary 位于 coarse world organization 层，不是 per-cell 存储。

真正大规模 Cell 数据应该由 `T` 自己承载，例如：

```text
WorldDataLayerStore<MyChunkGridPage>
WorldDataLayerStore<MySparseResourcePage>
WorldDataLayerStore<MyRoadGraphPage>
```

这样 WorldKit 不复制整个世界为全局二维数组，也不会出现 global + per-chunk authoritative double storage。

## Dirty Tracking

`WorldDirtyChunkTracker` 使用：

- active coord -> index Dictionary；
- insertion-order Entry List；
- inactive tombstone；
- threshold-based in-place compact。

`WriteDirty(Span<WorldChunkCoord>)` 在任何写入前验证容量，因此 buffer 不足保持 destination 原值。

Clear 后重新 Mark 的 Chunk 排到新的 active insertion order 尾部。

## Delta

`IWorldDelta` 只要求：

```text
TypeId
Version
Target
```

Target：

- World
- Region
- Chunk

`WorldDeltaSet`：

- 验证 payload metadata；
- 拒绝 wrong-world delta；
- append 时生成单调 sequence；
- `WorldDeltaRecord` snapshot metadata；
- caller-owned Span 导出 records；
- Clear 不回收 sequence number。

Core 不提供 `Apply()`，因为“如何应用 ResourceRemoved / BuildingPlaced / TerrainChanged”属于 Domain/Adapter，不应该让 WorldKit.Core 反向知道业务数据模型。

## Allocation / Hot-path Policy

允许 coarse-grained、生命周期明确的容器分配：

- Chunk Registry Dictionary；
- one Layer Store Dictionary；
- Delta List；
- Dirty List/Dictionary。

禁止：

- per-cell `Dictionary<string, object>`；
- per-query stable string resolve；
- runtime reflection scan；
- LINQ/yield hot paths；
- Unity object reference 进入 Core。

注册与 Build 阶段允许创建 schema Dictionary/array；高频 Layer access 持有 typed handle/store。

## Threading

Core containers 默认**非线程安全**。并发读写由上层同步。

Core API 不要求 Unity Main Thread，因此未来可由 Server、Jobs Adapter 或后台数据管线使用，但容器本身不会偷偷加锁。

## Failure Semantics

### Programmer/config invariant

抛异常，例如：

- invalid constructor arguments；
- wrong Layer Scope API；
- cross-registry handle 构造 Store；
- Build builder twice。

### Expected runtime failure

typed result/error，例如：

- Chunk out of extent；
- duplicate register；
- invalid transition；
- wrong-world Delta；
- Layer resolve type mismatch。

不 catch-all，不把异常变成 success。

## Validation Contract

Behavior：

- stable ID；
- finite/infinite coordinates；
- half-open bounds；
- lifecycle；
- Registry/typed handles；
- Layer Store；
- Chunk Registry；
- Dirty tracking；
- Delta order/metadata/error。

Policy：

- zero asmdef references；
- engine-free；
- no existing Foundation dependency；
- no WorldGen/Placement dependency；
- standalone source export closure。

Performance：

- Chunk registry operations；
- typed Chunk Layer set/get；
- dirty mark/write；
- benchmark records trend, no machine-independent fixed ms gate。

PlayMode：

Core 是纯 C# 数据基础，不需要 Unity lifecycle，因此不为“凑测试类型”强制增加 PlayMode。

Sample：

Core 不强制可视化 Sample；Presentation Adapter 提供更有教学价值的 World sample。纯 Core 用法由本说明文档和 EditMode tests 覆盖。
