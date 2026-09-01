# PathKit.GridKitAdapter

## 目的

这个适配器把 GridKit 的 `GridCoord/GridRect` 和应用自己的 traversal policy 接到通用 `IPathGraph`。Core 保持 Graph-first，因此不需要依赖 GridKit，也不会把地形、占用、门或 NPC 语义写进寻路算法。

## 映射与负坐标

`GridRect` 采用 Min inclusive、Max exclusive。适配器先把坐标减去 `Bounds.Min` 得到 local X/Y，再用 row-major `localY * width + localX` 得到 index，最后使用 `PathNodeId(index + 1)`；0 永远保留给 Invalid。构造时限制 area 不超过 `Int32.MaxValue - 1`。`TryGetNodeId` 和 `TryGetCoord` 可用于双向转换，负坐标不需要额外偏移。

## Traversal Policy

`IGridPathTraversalPolicy` 提供 `IsWalkable(coord)`、有向的 `CanTraverse(from,to)` 和 `GetTraversalCost(from,to)`。源 cell 不可走时没有邻居；目标必须在 bounds、walkable 且通过有向边策略。policy 可以组合多个 Grid、占用表、道路、门和动态模型，适配器不会默认把 `GridOccupancy` 视为 blocked。

`MinimumOrthogonalCost` 与 `MinimumDiagonalCost` 必须大于零。实际 orthogonal/diagonal edge 必须为正，并分别不低于声明的下界；违反时抛出 `InvalidOperationException`，不静默修正。

## 邻居与转角

FourWay 使用 N/E/S/W，EightWay 使用 N/NE/E/SE/S/SW/W/NW。默认 `NoCornerCut`：斜线目标之外，两个 side cell 必须在 bounds 内且 walkable；`AllowCornerCut` 不检查 side cell，只检查目标与 `CanTraverse`。两种模式的邻居顺序固定，便于回放和测试。

## Heuristic

FourWay 使用曼哈顿距离乘最小 orthogonal cost。EightWay 使用 diagonal/straight 分解，斜向最低成本取 `min(MinimumDiagonalCost, 2 * MinimumOrthogonalCost)`，所有计算都以 long checked 方式进行。极端值发生溢出时 A* 返回 `CostOverflow`，不会 wrap。

## 使用与动态状态

```csharp
var policy = new MyTraversalPolicy();
var graph = new GridPathGraph(
    new GridRect(new GridCoord(-6, -4), new GridSize(12, 8)),
    policy,
    GridPathNeighborMode.EightWay,
    GridPathDiagonalPolicy.NoCornerCut);
var pathfinder = new AStarPathfinder(128);
```

policy 的动态状态需要在一次同步 `FindPath` 期间保持稳定；下一次搜索会自然读取更新后的状态。旧路径不会自动失效，是否重寻由业务 Service 决定。

## 导出与样例

Core 包为 `StellarFramework-PathKit.unitypackage`，不包含 GridKit 或本适配器。需要网格时导入 `StellarFramework-PathKit-GridKitAdapter.unitypackage`，导出器会把 PathKit.Core 与 GridKit 的依赖闭包一起打入 Bootstrap。`Example_PathKit_GridKitAdapter` 展示负坐标、阻挡、加权路线、四/八方向和转角策略。

## 非目标

适配器不提供 PathManager、NPC 移动、路径缓存、跨帧搜索、NavMesh、JPS、FlowField、HPA*、Jobs/Burst、Addressables、HybridCLR 或保存系统。
