# WorldKit 世界组织系统说明

WorldKit 是 `foundation / world` 的纯 C# 世界组织基础 Kit。它负责“世界由哪些 World / Region / Chunk 组成、Chunk 当前处于什么生命周期、有哪些强类型数据层、哪些 Chunk 变脏、运行时发生了哪些语义 Delta”，但**不负责生成地形、寻路、存档文件、Unity GameObject、Tilemap 或 Terrain**。

WorldKit.Core 没有其他 Kit 依赖，`noEngineReferences=true`，可以单独导出使用。

## 核心边界

WorldKit 负责：

- 稳定 `WorldId`；
- 有限 / 无限平面世界范围；
- signed 64-bit `WorldChunkCoord` / `WorldRegionCoord`；
- double 精度逻辑 `WorldPoint2D`；
- Chunk 生命周期与按需 Registry；
- 强类型 World / Region / Chunk Data Layer；
- Dirty Chunk tracking；
- 有序 runtime Delta contract。

WorldKit 不负责：

- `GridKit` 的 Cell / Hex / Occupancy；
- `PathKit` 的 A* / Dijkstra；
- `SpatialKit` 的动态实体空间查询；
- `WorldGenKit` 的 Procedural Generation；
- `SaveKit` 的文件、Serializer、Migration；
- Unity `Transform` / `Terrain` / `Mesh` / `Tilemap`。

## 最小有限世界

```csharp
var worldId = WorldId.From("world.campaign");
var bounds = new WorldChunkBounds(
    new WorldChunkCoord(-8, -8),
    new WorldChunkCoord(8, 8));

var chunks = new WorldChunkRegistry(
    worldId,
    WorldExtent.Finite(bounds));

var coord = new WorldChunkCoord(0, 0);
chunks.TryRegister(coord);
chunks.TryTransition(coord, WorldChunkState.Metadata);
chunks.TryTransition(coord, WorldChunkState.DataReady);
chunks.TryTransition(coord, WorldChunkState.Active);
```

`WorldChunkBounds` 永久采用 `Min inclusive / MaxExclusive`。有限世界不会用“一个非常大的矩形”冒充无限世界。

## 无限世界

```csharp
var chunks = new WorldChunkRegistry(
    WorldId.From("world.factory"),
    WorldExtent.Infinite);

chunks.TryRegister(new WorldChunkCoord(-5000000000L, 7000000000L));
```

无限世界只按需登记实际需要的 Chunk，不预分配整张世界。

逻辑坐标与 Unity Transform 坐标必须分离。`WorldPoint2D` 使用 `double`；以后 Unity Adapter 可以用 Floating Origin 将附近逻辑位置映射到局部 `Vector3`。

## Chunk 生命周期

V1 状态：

```text
Unloaded
   ↕
Metadata
   ↕
DataReady
   ↕
Active
```

只允许相邻显式转换，禁止 `Unloaded -> Active` 跳级。失败返回 `WorldChunkTransitionResult`，不会静默修正状态。

`Active` 只表示 WorldKit 层的激活状态，不等于：

- 必须生成 GameObject；
- 必须渲染；
- 必须运行完整 Simulation。

这些由 Adapter / Domain 决定。

## Data Layer Registry

WorldKit 不硬编码：

```text
HeightMap
BiomeMap
WaterMap
ResourceIndex
BuildingIndex
```

项目显式注册自己的 Layer：

```csharp
var builder = new WorldDataLayerRegistryBuilder();

WorldDataLayerHandle<MyChunkPage> terrain = builder.Register<MyChunkPage>(
    WorldDataLayerId.From("terrain.page"),
    WorldDataLayerScope.Chunk);

WorldDataLayerHandle<MyWorldMetadata> metadata = builder.Register<MyWorldMetadata>(
    WorldDataLayerId.From("world.metadata"),
    WorldDataLayerScope.World);

WorldDataLayerRegistry registry = builder.Build();
```

稳定字符串只用于注册/解析边界。运行时高频路径持有 `WorldDataLayerHandle<T>`：

- `Index`
- `RegistryGeneration`
- generic `T`

Handle 不能跨 Registry 串用。

## Typed Layer Store

一个 Layer 对应一个强类型 Store：

```csharp
var terrainStore = new WorldDataLayerStore<MyChunkPage>(registry, terrain);

terrainStore.SetChunk(new WorldChunkCoord(0, 0), page);
terrainStore.TryGetChunk(new WorldChunkCoord(0, 0), out MyChunkPage loaded);
```

Chunk payload 可以是：

- `DenseGrid<T>`（通过业务/Adapter 组合 GridKit）；
- 自定义 Sparse page；
- graph page；
- POI list；
- server DTO；
- 任意项目自己的强类型对象。

WorldKit 不使用 `Dictionary<string, object>` 保存每格数据，也不要求所有世界数据都装进一个巨大 `WorldCell`。

## Dirty Chunk

```csharp
var dirty = new WorldDirtyChunkTracker();
dirty.MarkDirty(chunkCoord);

Span<WorldChunkCoord> buffer = stackalloc WorldChunkCoord[dirty.Count];
int count = dirty.WriteDirty(buffer);
```

Dirty Tracker：

- 同一个 Chunk 重复 Mark 是幂等的；
- 输出保持当前 active dirty 项的标记顺序；
- caller-owned buffer；
- buffer 不足直接失败，不写 partial result。

## Runtime Delta

Domain 定义自己的 Delta：

```csharp
public sealed class ResourceRemovedDelta : IWorldDelta
{
    public WorldDeltaTypeId TypeId => WorldDeltaTypeId.From("resource.removed");
    public WorldDeltaVersion Version => new WorldDeltaVersion(1);
    public WorldDeltaTarget Target { get; }
}
```

WorldKit 只负责：

- Stable Type ID；
- Version；
- World / Region / Chunk Target；
- Append 顺序；
- metadata snapshot。

`WorldDeltaSet` **不序列化** Delta，也不调用 JSON/Newtonsoft/FileSystem。未来 `WorldKit.SaveKitAdapter` 负责持久化和 Migration。

典型最终世界状态：

```text
Base Data
  + Authoring Override
  + Runtime Delta
  + Persistent Patch
  = Final World State
```

## 与其他 Kit 组合

- **GridKit**：Chunk payload 可由业务持有 `DenseGrid<T>`；WorldKit.Core 不引用 GridKit。
- **SpatialKit**：可按 Chunk 持有动态空间索引 page；通过 Adapter/业务组合。
- **PathKit**：路径图可以作为 Chunk/Region 数据；WorldKit 不提供寻路算法。
- **SimulationKit**：Simulation 是否随 Chunk Active/休眠由 WorldKit.SimulationKitAdapter 或业务决定。
- **SaveKit**：只通过未来 Adapter 持久化 World metadata / Delta / dirty state；Core 不依赖 SaveKit。
- **WorldGenKit**：WorldGen 生成结果可通过 Adapter 写入 WorldKit typed layer；WorldKit 不依赖生成器。

## 生产约束

1. 稳定 ID 使用 lower-case dot-separated 格式，例如 `world.main`、`terrain.page`。
2. Runtime Handle 不得持久化到存档；持久化 Stable ID / Profile Version。
3. 不把 Unity `Vector3` 当作无限世界逻辑坐标真值。
4. 不把 Cell-level 大数据塞进 WorldKit Registry Dictionary；应放在 Layer payload 自己的紧凑存储中。
5. Chunk Registry 只登记当前需要追踪的 Chunk；无限世界不能把“探索过的所有 Chunk”永久驻留内存。
6. Delta Domain 类型应保持明确版本，不要把所有修改塞进一个 string/object blob。

## 当前 V1 边界

当前正式支持：

- planar finite world；
- planar infinite world；
- World / Region / Chunk data scope；
- logical double position；
- generic typed payload layer；
- chunk lifecycle / dirty / delta infrastructure。

不在当前 Core 边界：

- spherical planets；
- full 3D voxel chunk coordinates；
- terrain generation；
- resource scatter；
- feature/POI generation；
- placement；
- Unity presentation。

这些由后续 Extension/Adapter 继续实现，而不是把 WorldKit.Core 做成万能大世界系统。
