# PathKit 路径搜索说明

PathKit 是一个 Graph-first 的同步最短路径 Foundation Kit。Core 只认识节点、出边、边成本和启发式，不认识网格、GameObject、NPC、场景或移动系统。内置 `AStarPathfinder` 与 `DijkstraPathfinder`，GridKit 支持放在可选的 `PathKit.GridKitAdapter` 包中。

## Quick Start

```csharp
IPathfinder pathfinder = new AStarPathfinder(64);
var request = new PathSearchRequest(start, goal, maxExpandedNodes: 4096);
PathNodeId[] buffer = new PathNodeId[128];
PathSearchResult result = pathfinder.FindPath(graph, request, buffer.AsSpan());

if (result.Success)
{
    // buffer[0..result.WrittenCount] 是 Start -> Goal（包含两端）的路径。
}
else if (result.Status == PathSearchStatus.OutputBufferTooSmall)
{
    // result.RequiredNodeCount 是完整路径长度；本次不会写入半条路径。
}
```

`PathNodeId` 由业务或 Graph 分配。零表示无效，正数表示有效；负数构造会抛出异常。`PathNeighbor` 的成本是大于零的 `long`，不能用负数表达不可通行边。

## Graph 契约

实现 `IPathGraph` 的类型按稳定顺序返回 outgoing neighbors，因此既可以表达有向图，也可以表达无向图。一次 `FindPath` 期间 Graph 的拓扑与成本必须保持稳定；PathKit 不做快照、锁、事件监听或全局缓存。`GetNeighborCount` 不能为负，邻居节点必须存在于 Graph，成本必须为正。A* 的 `EstimateCost` 必须非负，并且为了保证最优成本必须是 admissible（不能高估剩余成本）。

## A* 与 Dijkstra

- A* 使用 `F = G + H`，平局依次比较 `H`、`PathNodeId.Value`。
- Dijkstra 使用 `G`，平局比较 `PathNodeId.Value`，绝不会调用 `EstimateCost`。
- A* 允许 admissible 但不一致的 heuristic；Closed 节点发现更小的 `G` 时会 reopen。
- 所有 `G + edge`、`G + H` 运算都检查 `long` 溢出，溢出返回 `CostOverflow`，不发生 wrap。

## Result 与边界

`Success` 返回 Start -> Goal，`WrittenCount == RequiredNodeCount`，并包含完整成本。Start 与 Goal 是同一存在节点时直接返回一个节点、成本为零、展开数为零；输出 buffer 为空则返回 `OutputBufferTooSmall`。不可达返回 `NoPath`，不返回 fallback 或部分路径。`MaxExpandedNodes` 是一次同步搜索中真正枚举 outgoing neighbors 的节点数上限，超过后返回 `ExpansionLimitReached`；Goal 从堆中弹出时不会再次计为 expanded。

Pathfinder 会复用内部 workspace、record 数组、字典和二叉堆。预热并提供足够初始容量后，caller-owned 输出 buffer 的重复搜索不产生临时 List/Array；容量不足时允许扩容。一个 Pathfinder 不可并发或重入使用，应为每个 worker 持有独立实例。

## GridKit 适配器

需要网格时导入 `StellarFramework-PathKit-GridKitAdapter.unitypackage`。`GridPathGraph` 通过 `IGridPathTraversalPolicy` 读取 walkability、方向边界和成本，支持 FourWay/EightWay 与 NoCornerCut/AllowCornerCut。适配器使用 GridRect 的负坐标和 row-major 本地索引映射，`PathNodeId.Value = localIndex + 1`。占用、地形、门和动态障碍均由 policy 组合；下一次搜索会读取最新状态。

Core 与适配器均为纯 C#，没有 UnityEngine、Addressables、HybridCLR 或其他 UPM 依赖。同步寻路不负责移动对象、路径缓存、跨帧续算、Jobs/Burst、NavMesh、JPS、FlowField 或保存 workspace。

详见 [源码文档](PathKit-路径搜索-源码文档-Guide.md) 与 [GridKit 适配器说明](Adapters/GridKit/PathKit-GridKit适配器-Guide.md)。
